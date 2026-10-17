using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationQueries queries)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        if (ticket == null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        return await queries.GetForTicketAsync(ticketId, ct);
    }
}
