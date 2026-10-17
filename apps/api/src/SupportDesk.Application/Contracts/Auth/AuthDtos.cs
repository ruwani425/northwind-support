namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedAgentDto(int Id, string FullName, string Email, string Role);

public sealed record LoginResponseDto(
    string Token,
    DateTime ExpiresAtUtc,
    AuthenticatedAgentDto Agent);
