using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    ITicketEscalationRepository escalations,
    ITicketTriageService triageService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<TicketEscalationResultDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is already at Critical priority and cannot be escalated.");
        }

        if (!ticket.IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        }

        var nextPriority = Ticket.NextPriority(ticket.Priority)
            ?? throw new BusinessRuleViolationException("Ticket is at maximum priority and cannot be escalated.");

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        // BR-7: Recomputes due date from the moment of escalation (BR-2, BR-3)
        var (newDueAtUtc, _, _) = triageService.CalculateDueAt(now, nextPriority, customer.Tier);

        // BR-7: Re-evaluates owner: keep the current agent if still eligible, else reassign per BR-5
        var agents = await agentQueries.GetAllAsync(ct);
        var (reEvaluatedAgent, _) = triageService.EvaluateAssigneeForEscalation(ticket, category, agents);

        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        var escalation = new TicketEscalation(
            ticket.Id,
            fromPriority,
            nextPriority,
            fromAgentId,
            reEvaluatedAgent?.Id,
            fromDueAtUtc,
            newDueAtUtc,
            request.Reason,
            request.EscalatedBy ?? string.Empty,
            now);

        ticket.Escalate(nextPriority, newDueAtUtc, reEvaluatedAgent?.Id, now);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}. Due: {DueAtUtc}, Agent: {AgentId}",
            ticket.Reference, fromPriority, nextPriority, request.EscalatedBy, newDueAtUtc, reEvaluatedAgent?.Id);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);

        var fromAgent = agents.FirstOrDefault(a => a.Id == fromAgentId);
        var toAgent = reEvaluatedAgent ?? (ticket.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == ticket.AssignedAgentId.Value) : null);

        var escalationDto = new TicketEscalationDto(
            escalation.Id,
            escalation.TicketId,
            fromPriority,
            nextPriority,
            fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
            toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
            fromDueAtUtc,
            newDueAtUtc,
            escalation.Reason,
            escalation.EscalatedBy,
            escalation.EscalatedAtUtc);

        return new TicketEscalationResultDto(updatedTicket, escalationDto);
    }
}
