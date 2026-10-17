using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Services;

public class TicketTriageServiceTests
{
    private static readonly DateTime StartUtc = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly TicketTriageService _sut;

    public TicketTriageServiceTests()
    {
        var options = Options.Create(new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Critical"] = 4,
                ["High"] = 8,
                ["Medium"] = 24,
                ["Low"] = 72
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0,
            AtRiskThresholdPercent = 25.0
        });

        _sut = new TicketTriageService(options);
    }

    [Fact]
    public void DeterminePriority_WhenCategoryForcesCritical_AlwaysReturnsCritical()
    {
        var category = new Category("Security Incident", requiresSpecialist: true, forcesCriticalPriority: true);

        var priority = _sut.DeterminePriority(category, TicketPriority.Low, out var reason);

        Assert.Equal(TicketPriority.Critical, priority);
        Assert.Contains("forces critical priority", reason);
    }

    [Fact]
    public void DeterminePriority_WhenCategoryDoesNotForceCritical_ReturnsRequestedPriority()
    {
        var category = new Category("General Inquiry", requiresSpecialist: false, forcesCriticalPriority: false);

        var priority = _sut.DeterminePriority(category, TicketPriority.High, out var reason);

        Assert.Equal(TicketPriority.High, priority);
        Assert.Contains("Requested priority 'High' applied", reason);
    }

    [Fact]
    public void DeterminePriority_WhenRequestedPriorityIsNull_DefaultsToMedium()
    {
        var category = new Category("General Inquiry", requiresSpecialist: false, forcesCriticalPriority: false);

        var priority = _sut.DeterminePriority(category, null, out var reason);

        Assert.Equal(TicketPriority.Medium, priority);
        Assert.Contains("Default priority 'Medium' applied", reason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void CalculateDueAt_StandardCustomer_UsesBaseWindow(TicketPriority priority, double expectedHours)
    {
        var (dueAt, window, reason) = _sut.CalculateDueAt(StartUtc, priority, CustomerTier.Standard);

        Assert.Equal(StartUtc.AddHours(expectedHours), dueAt);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
        Assert.Contains("Standard customer", reason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)] // 4 * 0.5 = 2h
    [InlineData(TicketPriority.High, 4)]     // 8 * 0.5 = 4h
    [InlineData(TicketPriority.Medium, 12)]  // 24 * 0.5 = 12h
    [InlineData(TicketPriority.Low, 36)]    // 72 * 0.5 = 36h
    public void CalculateDueAt_PremiumCustomer_HalvesWindow(TicketPriority priority, double expectedHours)
    {
        var (dueAt, window, reason) = _sut.CalculateDueAt(StartUtc, priority, CustomerTier.Premium);

        Assert.Equal(StartUtc.AddHours(expectedHours), dueAt);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
        Assert.Contains("Premium customer", reason);
    }

    [Fact]
    public void CalculateDueAt_EnforcesMinimumOneHourFloor()
    {
        var options = Options.Create(new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double> { ["Critical"] = 1.0 },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0
        });

        var service = new TicketTriageService(options);
        var (dueAt, window, reason) = service.CalculateDueAt(StartUtc, TicketPriority.Critical, CustomerTier.Premium);

        // 1.0 * 0.5 = 0.5h -> clamped to 1.0h floor
        Assert.Equal(StartUtc.AddHours(1), dueAt);
        Assert.Equal(TimeSpan.FromHours(1), window);
        Assert.Contains("min 1h floor", reason);
    }

    [Fact]
    public void SelectAgent_ChoosesAgentWithFewestOpenTickets()
    {
        var category = new Category("Support", requiresSpecialist: false, forcesCriticalPriority: false);
        var agent1 = new AgentDto(1, "Agent One", "one@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 3, Array.Empty<CategorySummaryDto>());
        var agent2 = new AgentDto(2, "Agent Two", "two@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 1, Array.Empty<CategorySummaryDto>());

        var (selected, _) = _sut.SelectAgent(category, new[] { agent1, agent2 });

        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
    }

    [Fact]
    public void SelectAgent_BreaksTiesDeterministicallyById()
    {
        var category = new Category("Support", requiresSpecialist: false, forcesCriticalPriority: false);
        var agent2 = new AgentDto(2, "Agent Two", "two@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, Array.Empty<CategorySummaryDto>());
        var agent1 = new AgentDto(1, "Agent One", "one@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, Array.Empty<CategorySummaryDto>());

        var (selected, _) = _sut.SelectAgent(category, new[] { agent2, agent1 });

        Assert.NotNull(selected);
        Assert.Equal(1, selected.Id);
    }

    [Fact]
    public void SelectAgent_RequiresSpecialist_OnlySelectsSpecialist()
    {
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var categoryDto = new CategorySummaryDto(category.Id, category.Name);

        var generalist = new AgentDto(1, "Generalist", "gen@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 0, Array.Empty<CategorySummaryDto>());
        var specialist = new AgentDto(2, "Specialist", "spec@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, new[] { categoryDto });

        var (selected, _) = _sut.SelectAgent(category, new[] { generalist, specialist });

        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
    }

    [Fact]
    public void SelectAgent_ExcludesInactiveAgents()
    {
        var category = new Category("Support", requiresSpecialist: false, forcesCriticalPriority: false);
        var inactive = new AgentDto(1, "Inactive Agent", "in@example.com", IsActive: false, MaxOpenTickets: 5, OpenTicketCount: 0, Array.Empty<CategorySummaryDto>());
        var active = new AgentDto(2, "Active Agent", "act@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, Array.Empty<CategorySummaryDto>());

        var (selected, _) = _sut.SelectAgent(category, new[] { inactive, active });

        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
    }

    [Fact]
    public void SelectAgent_ExcludesAgentsAtOrOverMaxCapacity()
    {
        var category = new Category("Support", requiresSpecialist: false, forcesCriticalPriority: false);
        var busy = new AgentDto(1, "Busy Agent", "busy@example.com", IsActive: true, MaxOpenTickets: 3, OpenTicketCount: 3, Array.Empty<CategorySummaryDto>());
        var available = new AgentDto(2, "Available Agent", "avail@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 4, Array.Empty<CategorySummaryDto>());

        var (selected, _) = _sut.SelectAgent(category, new[] { busy, available });

        Assert.NotNull(selected);
        Assert.Equal(2, selected.Id);
    }

    [Fact]
    public void SelectAgent_WhenNobodyEligible_ReturnsNullAndReason()
    {
        var category = new Category("Support", requiresSpecialist: false, forcesCriticalPriority: false);
        var busy = new AgentDto(1, "Busy Agent", "busy@example.com", IsActive: true, MaxOpenTickets: 2, OpenTicketCount: 2, Array.Empty<CategorySummaryDto>());

        var (selected, reason) = _sut.SelectAgent(category, new[] { busy });

        Assert.Null(selected);
        Assert.Contains("capacity limit", reason);
    }
}
