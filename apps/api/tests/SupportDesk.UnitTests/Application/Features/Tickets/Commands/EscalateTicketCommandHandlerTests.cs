using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(string reason = "Issue impact increased significantly", string escalatedBy = "lead@example.com") =>
        new(reason, escalatedBy);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public async Task HandleAsync_RaisesPriorityByOneLevel(TicketPriority initialPriority, TicketPriority expectedPriority)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.InProgress)
            .WithPriority(initialPriority)
            .Build();

        context.ExistingTicket(ticket);

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(expectedPriority, ticket.Priority);
        Assert.NotNull(result);
        context.Escalations.Verify(e => e.AddAsync(It.Is<TicketEscalation>(esc =>
            esc.FromPriority == initialPriority &&
            esc.ToPriority == expectedPriority &&
            esc.Reason == "Issue impact increased significantly" &&
            esc.EscalatedBy == "lead@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.InProgress)
            .WithPriority(TicketPriority.Critical)
            .Build();

        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsResolved_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .WithPriority(TicketPriority.Medium)
            .Build();

        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Resolved", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsClosed_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Closed)
            .WithPriority(TicketPriority.Medium)
            .Build();

        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Closed", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketNotFound_ThrowsNotFoundException()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
