namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Human-readable triage rationale determined upon ticket creation.
/// </summary>
public sealed record TriageResultDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
