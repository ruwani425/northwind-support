using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<LoginCommandHandler> logger)
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponseDto> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (agent is null)
        {
            logger.LogWarning("Login failed: agent with email '{Email}' was not found.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!agent.IsActive)
        {
            logger.LogWarning("Login failed: agent '{Email}' is inactive.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!passwordHasher.VerifyPassword(request.Password, agent.PasswordHash))
        {
            logger.LogWarning("Login failed: password verification failed for agent '{Email}'.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var (token, expiresAtUtc) = jwtTokenService.GenerateToken(agent);

        logger.LogInformation("Agent '{Email}' (Id: {AgentId}) logged in successfully.", agent.Email, agent.Id);

        return new LoginResponseDto(
            token,
            expiresAtUtc,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email, agent.Role));
    }
}
