using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IAgentRepository agents)
{
    public async Task<AuthenticatedAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct)
            ?? throw new UnauthorizedException("User session is invalid or user no longer exists.");

        if (!agent.IsActive)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        return new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email, agent.Role);
    }
}
