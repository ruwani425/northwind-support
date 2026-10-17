using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

public interface ITicketTriageService
{
    TicketPriority DeterminePriority(Category category, TicketPriority? requestedPriority, out string reason);

    (DateTime DueAtUtc, TimeSpan Window, string Reason) CalculateDueAt(DateTime startUtc, TicketPriority priority, CustomerTier customerTier);

    (AgentDto? SelectedAgent, string Reason) SelectAgent(Category category, IReadOnlyList<AgentDto> agents);

    (AgentDto? AssignedAgent, string Reason) EvaluateAssigneeForEscalation(Ticket ticket, Category category, IReadOnlyList<AgentDto> agents);
}

public sealed class TicketTriageService(IOptions<SlaOptions> options) : ITicketTriageService
{
    private readonly SlaOptions _options = options.Value;

    public TicketPriority DeterminePriority(Category category, TicketPriority? requestedPriority, out string reason)
    {
        if (category.ForcesCriticalPriority)
        {
            reason = $"Category '{category.Name}' forces critical priority.";
            return TicketPriority.Critical;
        }

        if (requestedPriority.HasValue)
        {
            reason = $"Requested priority '{requestedPriority.Value}' applied.";
            return requestedPriority.Value;
        }

        reason = "Default priority 'Medium' applied.";
        return TicketPriority.Medium;
    }

    public (DateTime DueAtUtc, TimeSpan Window, string Reason) CalculateDueAt(
        DateTime startUtc,
        TicketPriority priority,
        CustomerTier customerTier)
    {
        var priorityKey = priority.ToString();
        if (!_options.BaseWindowHours.TryGetValue(priorityKey, out var baseHours))
        {
            baseHours = priority switch
            {
                TicketPriority.Critical => 4,
                TicketPriority.High => 8,
                TicketPriority.Medium => 24,
                TicketPriority.Low => 72,
                _ => 24
            };
        }

        double effectiveHours;
        string reason;

        if (customerTier == CustomerTier.Premium)
        {
            effectiveHours = Math.Max(baseHours * _options.PremiumCustomerMultiplier, _options.MinimumWindowHours);
            reason = $"SLA window is {effectiveHours:0.##}h ({baseHours}h base window halved for Premium customer, min {_options.MinimumWindowHours}h floor).";
        }
        else
        {
            effectiveHours = baseHours;
            reason = $"SLA window is {effectiveHours:0.##}h base window for Standard customer.";
        }

        var dueAtUtc = startUtc.AddHours(effectiveHours);
        return (dueAtUtc, TimeSpan.FromHours(effectiveHours), reason);
    }

    public (AgentDto? SelectedAgent, string Reason) SelectAgent(Category category, IReadOnlyList<AgentDto> agents)
    {
        var activeAgents = agents.Where(a => a.IsActive).ToList();
        if (activeAgents.Count == 0)
        {
            return (null, "No active agents available.");
        }

        var eligibleSpecialists = category.RequiresSpecialist
            ? activeAgents.Where(a => a.Specializations.Any(s => s.Id == category.Id)).ToList()
            : activeAgents;

        if (eligibleSpecialists.Count == 0)
        {
            return (null, $"No active specialist agent available for category '{category.Name}'.");
        }

        var belowCapacity = eligibleSpecialists.Where(a => a.OpenTicketCount < a.MaxOpenTickets).ToList();
        if (belowCapacity.Count == 0)
        {
            return (null, $"All eligible agents for category '{category.Name}' have reached their ticket capacity limit.");
        }

        var selected = belowCapacity
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .First();

        return (selected, $"Assigned to {selected.FullName} ({selected.OpenTicketCount}/{selected.MaxOpenTickets} open tickets).");
    }

    public (AgentDto? AssignedAgent, string Reason) EvaluateAssigneeForEscalation(
        Ticket ticket,
        Category category,
        IReadOnlyList<AgentDto> agents)
    {
        if (ticket.AssignedAgentId.HasValue)
        {
            var currentAgent = agents.FirstOrDefault(a => a.Id == ticket.AssignedAgentId.Value);
            if (currentAgent != null && currentAgent.IsActive)
            {
                var hasSpecialization = !category.RequiresSpecialist || currentAgent.Specializations.Any(s => s.Id == category.Id);
                var withinCapacity = currentAgent.OpenTicketCount <= currentAgent.MaxOpenTickets;

                if (hasSpecialization && withinCapacity)
                {
                    return (currentAgent, $"Current agent {currentAgent.FullName} remains eligible and is retained.");
                }
            }
        }

        return SelectAgent(category, agents);
    }
}
