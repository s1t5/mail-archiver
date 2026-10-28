using MailArchiver.Models;
using Xunit;

namespace MailArchiver.Tests.Api;

/// <summary>
/// Unit tests for <see cref="ApiKeyScopes"/> — the storage format and the
/// read/write split that keeps existing keys read-only.
/// </summary>
public class ApiKeyScopesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_MissingValue_FallsBackToRead(string? raw)
    {
        var scopes = ApiKeyScopes.Parse(raw);

        Assert.Equal(new[] { ApiKeyScopes.Read }, scopes);
    }

    [Fact]
    public void Parse_ReadWrite_ReturnsBothScopes()
    {
        var scopes = ApiKeyScopes.Parse("read,write");

        Assert.Contains(ApiKeyScopes.Read, scopes);
        Assert.Contains(ApiKeyScopes.Write, scopes);
        Assert.Equal(2, scopes.Count);
    }

    [Fact]
    public void Parse_IsCaseAndWhitespaceInsensitive()
    {
        var scopes = ApiKeyScopes.Parse(" READ , Write ");

        Assert.Contains(ApiKeyScopes.Read, scopes);
        Assert.Contains(ApiKeyScopes.Write, scopes);
    }

    [Fact]
    public void Parse_UnknownScopeOnly_FallsBackToRead()
    {
        var scopes = ApiKeyScopes.Parse("admin");

        Assert.Equal(new[] { ApiKeyScopes.Read }, scopes);
    }

    [Fact]
    public void Parse_IgnoresUnknownScopesNextToKnownOnes()
    {
        var scopes = ApiKeyScopes.Parse("read,future-scope");

        Assert.Equal(new[] { ApiKeyScopes.Read }, scopes);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("read", false)]
    [InlineData("read,write", true)]
    [InlineData("write", true)]
    public void HasWrite_ReflectsStoredScopes(string? raw, bool expected)
        => Assert.Equal(expected, ApiKeyScopes.HasWrite(raw));

    [Fact]
    public void TryNormalize_DefaultsToCanonicalOrder()
    {
        var ok = ApiKeyScopes.TryNormalize(new[] { "write", "read" }, out var normalized, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("read,write", normalized);
    }

    [Fact]
    public void TryNormalize_SingleWriteScope_IsAllowed()
    {
        var ok = ApiKeyScopes.TryNormalize(new[] { "write" }, out var normalized, out _);

        Assert.True(ok);
        Assert.Equal("write", normalized);
    }

    [Fact]
    public void TryNormalize_EmptyList_IsRejected()
    {
        var ok = ApiKeyScopes.TryNormalize(Array.Empty<string>(), out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.Contains("At least one scope", error);
    }

    [Fact]
    public void TryNormalize_UnknownScope_IsRejected()
    {
        var ok = ApiKeyScopes.TryNormalize(new[] { "read", "root" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("root", error);
    }

    [Fact]
    public void Default_IsReadOnly()
    {
        Assert.Equal(ApiKeyScopes.Read, ApiKeyScopes.Default);
        Assert.False(ApiKeyScopes.HasWrite(new ApiKey().Scopes));
    }
}
