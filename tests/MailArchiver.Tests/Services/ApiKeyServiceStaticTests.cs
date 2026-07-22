using MailArchiver.Models;
using MailArchiver.Services;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for the static, pure helpers of <see cref="ApiKeyService"/>.
/// </summary>
public class ApiKeyServiceStaticTests
{
    [Fact]
    public void GenerateKey_HasCorrectFormat()
    {
        var key = ApiKeyService.GenerateKey();
        Assert.StartsWith(ApiKeyService.KeyPrefixMarker, key);
        Assert.Equal(ApiKeyService.KeyPrefixMarker.Length + 43, key.Length);
    }

    [Fact]
    public void GenerateKey_ContainsUrlSafeCharacters()
    {
        for (int i = 0; i < 20; i++)
        {
            var key = ApiKeyService.GenerateKey();
            var body = key.Substring(ApiKeyService.KeyPrefixMarker.Length);
            Assert.DoesNotContain('+', body);
            Assert.DoesNotContain('/', body);
            Assert.DoesNotContain('=', body);
        }
    }

    [Fact]
    public void GenerateKey_ProducesUniqueKeys()
    {
        var keys = new HashSet<string>();
        for (int i = 0; i < 100; i++)
            keys.Add(ApiKeyService.GenerateKey());
        Assert.Equal(100, keys.Count);
    }

    [Fact]
    public void ComputeHash_IsDeterministicAndHexLower()
    {
        var key = "ma_testkey";
        var h1 = ApiKeyService.ComputeHash(key);
        var h2 = ApiKeyService.ComputeHash(key);
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
        Assert.Matches("^[0-9a-f]{64}$", h1);
    }

    [Fact]
    public void ComputeHash_DifferentKeys_ProduceDifferentHashes()
        => Assert.NotEqual(ApiKeyService.ComputeHash("key1"), ApiKeyService.ComputeHash("key2"));

    [Fact]
    public void ComputeHash_DifferentStrings_DifferentFromKey()
        => Assert.NotEqual(ApiKeyService.ComputeHash("ma_a"), ApiKeyService.ComputeHash("ma_b"));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void IsUsable_CombinationsOfActiveStates(bool keyActive, bool userActive, bool expected)
    {
        var now = DateTime.UtcNow;
        var key = new ApiKey
        {
            RevokedAt = keyActive ? null : now.AddMinutes(-1),
            ExpiresAt = keyActive ? now.AddHours(1) : null
        };
        var user = new User { IsActive = userActive };
        Assert.Equal(expected, ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ExpiredKey_ReturnsFalse()
    {
        var key = new ApiKey { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        var user = new User { IsActive = true };
        Assert.False(ApiKeyService.IsUsable(key, user));
    }

    [Fact]
    public void IsUsable_ActiveKeyNoExpiry_ReturnsTrue()
    {
        var key = new ApiKey { RevokedAt = null, ExpiresAt = null };
        var user = new User { IsActive = true };
        Assert.True(ApiKeyService.IsUsable(key, user));
    }
}