using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TicketEscalationQueries(SupportDbContext db) : ITicketEscalationQueries
{
    public async Task<IReadOnlyList<TicketEscalationDto>> GetForTicketAsync(int ticketId, CancellationToken ct) =>
        await (
            from e in db.Set<TicketEscalation>().AsNoTracking()
            where e.TicketId == ticketId
            orderby e.EscalatedAtUtc descending, e.Id descending
            from fromAgent in db.Set<Agent>().Where(a => a.Id == e.FromAgentId).DefaultIfEmpty()
            from toAgent in db.Set<Agent>().Where(a => a.Id == e.ToAgentId).DefaultIfEmpty()
            select new TicketEscalationDto(
                e.Id,
                e.TicketId,
                e.FromPriority,
                e.ToPriority,
                fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                e.FromDueAtUtc,
                e.ToDueAtUtc,
                e.Reason,
                e.EscalatedBy,
                e.EscalatedAtUtc)
        ).ToListAsync(ct);
}
