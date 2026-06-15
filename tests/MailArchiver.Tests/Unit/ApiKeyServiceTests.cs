using MailArchiver.Models;
using MailArchiver.Services;

namespace MailArchiver.Tests.Unit;

public class ApiKeyServiceTests
{
    [Fact]
    public void GenerateKey_ReturnsExpectedFormatAndEntropy()
    {
        var firstKey = ApiKeyService.GenerateKey();
        var secondKey = ApiKeyService.GenerateKey();

        Assert.StartsWith(ApiKeyService.KeyPrefixMarker, firstKey);
        Assert.Equal(46, firstKey.Length);
        Assert.All(firstKey[ApiKeyService.KeyPrefixMarker.Length..], c =>
            Assert.True(IsBase64UrlCharacter(c), $"Unexpected character '{c}'."));
        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void ComputeHash_IsDeterministicLowercaseSha256Hex()
    {
        var firstHash = ApiKeyService.ComputeHash("ma_test-key");
        var secondHash = ApiKeyService.ComputeHash("ma_test-key");
        var differentHash = ApiKeyService.ComputeHash("ma_other-key");

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
        Assert.All(firstHash, c => Assert.True(IsLowercaseHexCharacter(c), $"Unexpected character '{c}'."));
        Assert.NotEqual(firstHash, differentHash);
    }

    [Fact]
    public void IsUsable_ReturnsTrueForActiveKeyAndActiveUser()
    {
        var key = CreateKey();
        var user = CreateUser();

        Assert.True(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ReturnsFalseWhenKeyRevoked()
    {
        var key = CreateKey(revokedAt: DateTime.UtcNow);
        var user = CreateUser();

        Assert.False(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ReturnsFalseWhenKeyExpired()
    {
        var key = CreateKey(expiresAt: DateTime.UtcNow.AddHours(-1));
        var user = CreateUser();

        Assert.False(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ReturnsTrueWhenKeyExpiresInFuture()
    {
        var key = CreateKey(expiresAt: DateTime.UtcNow.AddHours(1));
        var user = CreateUser();

        Assert.True(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ReturnsTrueWhenKeyDoesNotExpire()
    {
        var key = CreateKey(expiresAt: null);
        var user = CreateUser();

        Assert.True(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ReturnsFalseWhenUserInactive()
    {
        var key = CreateKey();
        var user = CreateUser(isActive: false);

        Assert.False(ApiKeyService.IsUsable(key, user));
    }

    private static bool IsBase64UrlCharacter(char c) =>
        c is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-'
            or '_';

    private static bool IsLowercaseHexCharacter(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static ApiKey CreateKey(DateTime? expiresAt = null, DateTime? revokedAt = null) =>
        new()
        {
            UserId = 1,
            Name = "test key",
            KeyPrefix = "ma_testkey",
            KeyHash = ApiKeyService.ComputeHash("ma_test-key"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };

    private static User CreateUser(bool isActive = true) =>
        new()
        {
            Id = 1,
            Username = "test-user",
            Email = "test@example.com",
            IsActive = isActive
        };
}
