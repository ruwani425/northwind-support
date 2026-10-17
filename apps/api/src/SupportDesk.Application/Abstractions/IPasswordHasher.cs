namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Cryptographic password hashing and verification abstraction.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
