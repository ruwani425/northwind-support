using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _agentRepository.Object,
            _passwordHasher.Object,
            _jwtTokenService.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_GivenValidCredentials_ReturnsTokenAndAgentDto()
    {
        var agent = new Agent("Jane Doe", "jane.doe@northwind-support.example", 10, DateTime.UtcNow, "valid_hashed_pw");

        _agentRepository
            .Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher
            .Setup(h => h.VerifyPassword("CorrectPassword123!", "valid_hashed_pw"))
            .Returns(true);

        _jwtTokenService
            .Setup(s => s.GenerateToken(agent))
            .Returns(("mocked-jwt-token", DateTime.UtcNow.AddHours(8)));

        var response = await _handler.HandleAsync(
            new LoginRequest("jane.doe@northwind-support.example", "CorrectPassword123!"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("mocked-jwt-token", response.Token);
        Assert.Equal(agent.Id, response.Agent.Id);
        Assert.Equal(agent.FullName, response.Agent.FullName);
        Assert.Equal(agent.Email, response.Agent.Email);
        Assert.Equal(agent.Role, response.Agent.Role);
    }

    [Fact]
    public async Task HandleAsync_GivenUnknownEmail_ThrowsUnauthorizedException()
    {
        _agentRepository
            .Setup(r => r.GetByEmailAsync("unknown@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(
                new LoginRequest("unknown@northwind-support.example", "Password123!"),
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_GivenIncorrectPassword_ThrowsUnauthorizedException()
    {
        var agent = new Agent("Jane Doe", "jane.doe@northwind-support.example", 5, DateTime.UtcNow, "valid_hash");

        _agentRepository
            .Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher
            .Setup(h => h.VerifyPassword("WrongPassword", "valid_hash"))
            .Returns(false);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(
                new LoginRequest("jane.doe@northwind-support.example", "WrongPassword"),
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_GivenInactiveAgent_ThrowsUnauthorizedException()
    {
        var agent = new Agent("Inactive Agent", "inactive@northwind-support.example", 5, DateTime.UtcNow, "valid_hash");
        agent.Deactivate();

        _agentRepository
            .Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher
            .Setup(h => h.VerifyPassword("Password123!", "valid_hash"))
            .Returns(true);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(
                new LoginRequest("inactive@northwind-support.example", "Password123!"),
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }
}
