using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MinimumLength(5).WithMessage("Reason must be at least 5 characters.")
            .MaximumLength(500).WithMessage("Reason must be 500 characters or fewer.");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty().WithMessage("EscalatedBy is required.")
            .MaximumLength(100).WithMessage("EscalatedBy must be 100 characters or fewer.");
    }
}
