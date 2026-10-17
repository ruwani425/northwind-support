using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly FixedClock _clock = new();
    private readonly TicketQueries _sut;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        SeedTestData();

        _sut = new TicketQueries(_db, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersExactMatchPriority()
    {
        var query = new TicketQuery { Priority = TicketPriority.High };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersCombinedCriteriaWithAnd()
    {
        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = 1,
            Status = TicketStatus.InProgress
        };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
        Assert.Equal(TicketPriority.High, item.Priority);
        Assert.Equal(1, item.Customer.Id);
        Assert.Equal(TicketStatus.InProgress, item.Status);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersSearchByCustomerName()
    {
        var query = new TicketQuery { Search = "Fabrikam" };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("Fabrikam Inc", item.Customer.Name));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersSearchByReferenceAndTitle()
    {
        var queryByRef = new TicketQuery { Search = " TCK-0002 " };
        var resultRef = await _sut.GetPagedAsync(queryByRef, CancellationToken.None);

        var singleByRef = Assert.Single(resultRef.Items);
        Assert.Equal("TCK-0002", singleByRef.Reference);

        var queryByTitle = new TicketQuery { Search = "Login issue" };
        var resultTitle = await _sut.GetPagedAsync(queryByTitle, CancellationToken.None);

        var singleByTitle = Assert.Single(resultTitle.Items);
        Assert.Equal("TCK-0001", singleByTitle.Reference);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersUnassignedOnly()
    {
        var query = new TicketQuery { UnassignedOnly = true };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0002", item.Reference);
        Assert.Null(item.AssignedAgent);
    }

    private void SeedTestData()
    {
        var customer1 = new Customer("Contoso Ltd", "contact@contoso.com", null, CustomerTier.Premium, _clock.UtcNow);
        var customer2 = new Customer("Fabrikam Inc", "contact@fabrikam.com", null, CustomerTier.Standard, _clock.UtcNow);
        _db.AddRange(customer1, customer2);

        var category1 = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var category2 = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _db.AddRange(category1, category2);

        var agent1 = new Agent("Alex Turner", "alex@northwind.test", 10, _clock.UtcNow);
        var agent2 = new Agent("Ben Osei", "ben@northwind.test", 8, _clock.UtcNow);
        _db.AddRange(agent1, agent2);

        _db.SaveChanges();

        var ticket1 = Ticket.Raise("TCK-0001", "Login issue", "Cannot login to portal", customer1.Id, category1.Id, TicketPriority.High, _clock.UtcNow);
        ticket1.AssignTo(agent1.Id, _clock.UtcNow);
        ticket1.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow);

        var ticket2 = Ticket.Raise("TCK-0002", "Invoice query", "Need help with invoice", customer2.Id, category2.Id, TicketPriority.Low, _clock.UtcNow);

        var ticket3 = Ticket.Raise("TCK-0003", "Database slow", "Queries are timing out", customer2.Id, category1.Id, TicketPriority.High, _clock.UtcNow);
        ticket3.AssignTo(agent2.Id, _clock.UtcNow);
        ticket3.ChangeStatus(TicketStatus.Open, _clock.UtcNow);

        _db.AddRange(ticket1, ticket2, ticket3);
        _db.SaveChanges();
    }
}
