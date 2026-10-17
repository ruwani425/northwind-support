namespace SupportDesk.Application.Contracts.Common;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SupportDesk";

    public string Audience { get; set; } = "SupportDesk";

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime of issued JWT tokens in hours. Default is 8 hours (standard working shift).
    /// </summary>
    public int LifetimeHours { get; set; } = 8;
}
