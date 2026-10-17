using SupportDesk.Infrastructure.Security;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_GivenPassword_ProducesNonEmptySaltedHash()
    {
        var password = "SecurePassword123!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.NotEmpty(hash1);
        Assert.NotEmpty(hash2);
        // Salted hashing means different salts result in different hashes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_GivenMatchingPassword_ReturnsTrue()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(password, hash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_GivenIncorrectPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("CorrectPassword");

        var result = _hasher.VerifyPassword("WrongPassword", hash);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-format")]
    [InlineData("not:a:valid:hash")]
    public void Verify_GivenMalformedHash_ReturnsFalse(string malformedHash)
    {
        var result = _hasher.VerifyPassword("AnyPassword", malformedHash);

        Assert.False(result);
    }
}
