using MailArchiver.Auth.Helpers;
using System.Text.Json;

namespace MailArchiver.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="OidcClaimExtractor"/>. The fallback chains are what
/// decide whether a provider can sign users in at all, so every order and every
/// edge case of the chain is pinned down here.
/// </summary>
public class OidcClaimExtractorTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    [Fact]
    public void ExtractName_PrefersNameOverEverythingElse()
    {
        var userInfo = Json("""
            { "name": "Ada Lovelace", "preferred_username": "ada", "given_name": "Ada", "username": "ada" }
            """);

        Assert.Equal("Ada Lovelace", OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_FallsBackToPreferredUsername()
    {
        var userInfo = Json("""
            { "preferred_username": "ada", "given_name": "Ada", "username": "ada" }
            """);

        Assert.Equal("ada", OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_FallsBackToGivenName()
    {
        var userInfo = Json("""{ "given_name": "Ada", "username": "ada" }""");

        Assert.Equal("Ada", OidcClaimExtractor.ExtractName(userInfo));
    }

    /// <summary>
    /// Synology SSO Server returns only <c>username</c>; see issue #598.
    /// </summary>
    [Fact]
    public void ExtractName_FallsBackToUsername()
    {
        var userInfo = Json("""{ "sub": "1", "username": "ada", "email": "ada@example.com" }""");

        Assert.Equal("ada", OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_SkipsEmptyAndWhitespaceClaims()
    {
        var userInfo = Json("""{ "name": "   ", "preferred_username": "", "given_name": "Ada" }""");

        Assert.Equal("Ada", OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_SkipsClaimsThatAreNotStrings()
    {
        var userInfo = Json("""{ "name": 42, "preferred_username": "ada" }""");

        Assert.Equal("ada", OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_ReturnsNullWhenNoCandidateIsPresent()
    {
        var userInfo = Json("""{ "sub": "1", "email": "ada@example.com" }""");

        Assert.Null(OidcClaimExtractor.ExtractName(userInfo));
    }

    [Fact]
    public void ExtractName_ReturnsNullForNonObjectPayload()
    {
        Assert.Null(OidcClaimExtractor.ExtractName(Json("\"not-an-object\"")));
    }

    [Fact]
    public void ExtractEmail_PrefersEmailOverMailAndUpn()
    {
        var userInfo = Json("""{ "email": "ada@example.com", "mail": "other@example.com", "upn": "upn@example.com" }""");

        Assert.Equal("ada@example.com", OidcClaimExtractor.ExtractEmail(userInfo));
    }

    [Fact]
    public void ExtractEmail_FallsBackToMailThenUpn()
    {
        Assert.Equal("mail@example.com", OidcClaimExtractor.ExtractEmail(Json("""{ "mail": "mail@example.com", "upn": "upn@example.com" }""")));
        Assert.Equal("upn@example.com", OidcClaimExtractor.ExtractEmail(Json("""{ "upn": "upn@example.com" }""")));
    }

    [Fact]
    public void ExtractSubject_ReadsSub()
    {
        Assert.Equal("abc", OidcClaimExtractor.ExtractSubject(Json("""{ "sub": "abc" }""")));
    }

    [Fact]
    public void ExtractSubject_ReturnsNullWhenSubIsNotAString()
    {
        Assert.Null(OidcClaimExtractor.ExtractSubject(Json("""{ "sub": 12345 }""")));
    }
}
