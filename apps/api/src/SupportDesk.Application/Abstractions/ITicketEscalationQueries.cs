using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Abstractions;

public interface ITicketEscalationQueries
{
    /// <summary>
    /// Loads escalation history for a ticket, newest first.
    /// </summary>
    Task<IReadOnlyList<TicketEscalationDto>> GetForTicketAsync(int ticketId, CancellationToken ct);
}
