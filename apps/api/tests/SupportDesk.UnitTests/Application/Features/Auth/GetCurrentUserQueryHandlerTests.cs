using Moq;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepository = new();
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(_agentRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_GivenExistingActiveAgent_ReturnsAuthenticatedAgentDto()
    {
        var agent = new Agent("Agent Smith", "agent@northwind-support.example", 12, DateTime.UtcNow);
        _agentRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await _handler.HandleAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.Id);
        Assert.Equal("Agent Smith", result.FullName);
        Assert.Equal("agent@northwind-support.example", result.Email);
        Assert.Equal(agent.Role, result.Role);
    }

    [Fact]
    public async Task HandleAsync_GivenNonExistentAgentId_ThrowsUnauthorizedException()
    {
        _agentRepository
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_GivenInactiveAgent_ThrowsUnauthorizedException()
    {
        var agent = new Agent("Inactive Agent", "inactive@northwind-support.example", 5, DateTime.UtcNow);
        agent.Deactivate();

        _agentRepository
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(2, CancellationToken.None));
    }
}
