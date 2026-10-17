using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, applying auto-triage (priority, SLA due date, and agent assignment).
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    ITicketTriageService triageService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        // BR-1: Priority determination
        var priority = triageService.DeterminePriority(category, request.RequestedPriority, out var priorityReason);

        // BR-2, BR-3, BR-6: SLA window & due date
        var (dueAtUtc, _, slaReason) = triageService.CalculateDueAt(now, priority, customer.Tier);

        // BR-4, BR-5: Agent assignment
        var agents = await agentQueries.GetAllAsync(ct);
        var (assignedAgent, assignmentReason) = triageService.SelectAgent(category, agents);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now,
            dueAtUtc,
            assignedAgent?.Id);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}. Priority: {Priority}, Agent: {AgentId}, Due: {DueAtUtc}",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId, ticket.DueAtUtc);

        var detail = await getTicket.HandleAsync(ticket.Id, ct);
        detail.Triage = new TriageResultDto(priorityReason, slaReason, assignmentReason);
        return detail;
    }
}
