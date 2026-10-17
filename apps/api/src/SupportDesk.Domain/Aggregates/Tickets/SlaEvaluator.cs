namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? createdAtUtc = null,
        double atRiskThresholdPercent = 25.0)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (createdAtUtc is not null && dueAtUtc > createdAtUtc)
        {
            var totalWindow = dueAtUtc.Value - createdAtUtc.Value;
            var remaining = dueAtUtc.Value - nowUtc;

            if (remaining.TotalSeconds <= totalWindow.TotalSeconds * (atRiskThresholdPercent / 100.0))
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }
}

