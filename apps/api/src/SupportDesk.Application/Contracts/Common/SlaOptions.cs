namespace SupportDesk.Application.Contracts.Common;

/// <summary>
/// Configuration for SLA window calculations and at-risk monitoring.
/// Bound from the "Sla" configuration section.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, double> BaseWindowHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 72,
        ["Medium"] = 24,
        ["High"] = 8,
        ["Critical"] = 4
    };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1.0;

    public double AtRiskThresholdPercent { get; set; } = 25.0;
}
