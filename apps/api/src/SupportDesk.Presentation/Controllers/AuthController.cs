using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Authenticates an agent and returns a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponseDto> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>Returns profile information for the currently authenticated agent.</summary>
    [HttpGet("me")]
    public Task<AuthenticatedAgentDto> GetMe(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken ct)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var agentId))
        {
            throw new UnauthorizedException("Invalid authentication credentials.");
        }

        return handler.HandleAsync(agentId, ct);
    }
}
