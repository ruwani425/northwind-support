using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_OpenWithLessThanQuarterWindowRemaining_IsAtRisk()
    {
        // 4 hour total window: created at Now - 3.5h, due at Now + 0.5h (0.5h / 4h = 12.5% remaining <= 25%)
        var createdAt = Now.AddHours(-3.5);
        var dueAt = Now.AddHours(0.5);
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(dueAt, null, Now, createdAt));
    }

    [Fact]
    public void Evaluate_OpenWithExactlyQuarterWindowRemaining_IsAtRisk()
    {
        // 4 hour total window: created at Now - 3h, due at Now + 1h (1h / 4h = 25% remaining)
        var createdAt = Now.AddHours(-3);
        var dueAt = Now.AddHours(1);
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(dueAt, null, Now, createdAt));
    }

    [Fact]
    public void Evaluate_OpenWithMoreThanQuarterWindowRemaining_IsWithinSla()
    {
        // 4 hour total window: created at Now - 1h, due at Now + 3h (3h / 4h = 75% remaining > 25%)
        var createdAt = Now.AddHours(-1);
        var dueAt = Now.AddHours(3);
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(dueAt, null, Now, createdAt));
    }
}
