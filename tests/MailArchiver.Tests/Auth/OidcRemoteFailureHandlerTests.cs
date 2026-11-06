using MailArchiver.Auth.Exceptions;
using MailArchiver.Auth.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="OidcRemoteFailureHandler"/>. This handler is what keeps a
/// failed OIDC sign-in from surfacing as an HTTP 500, so the redirect target and the
/// localized message are pinned down here.
/// </summary>
public class OidcRemoteFailureHandlerTests
{
    /// <summary>
    /// Returns the resource key as the message and appends the format arguments, so the
    /// assertions can check which key was requested and what was passed to it.
    /// </summary>
    private sealed class FakeLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, $"{name}({string.Join("|", arguments)})", resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Array.Empty<LocalizedString>();
    }

    private static RemoteFailureContext CreateContext(Exception failure, out DefaultHttpContext httpContext)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IStringLocalizer<SharedResource>>(new FakeLocalizer());

        httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var scheme = new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler));
        return new RemoteFailureContext(httpContext, scheme, new OpenIdConnectOptions(), failure);
    }

    private static string DecodedError(DefaultHttpContext httpContext)
    {
        var location = httpContext.Response.Headers.Location.ToString();
        var marker = "error=";
        var index = location.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Redirect target does not carry an error parameter: {location}");
        return Uri.UnescapeDataString(location[(index + marker.Length)..]);
    }

    [Fact]
    public async Task MissingClaim_RedirectsToLoginPage()
    {
        var context = CreateContext(new MissingClaimException("name, preferred_username"), out var httpContext);

        await OidcRemoteFailureHandler.HandleAsync(context);

        Assert.NotNull(context.Result);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.StartsWith(OidcRemoteFailureHandler.LoginErrorPath + "?error=", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task MissingClaim_ShowsWhichClaimsWereLookedFor()
    {
        var context = CreateContext(
            new MissingClaimException("name, preferred_username, given_name, username"),
            out var httpContext);

        await OidcRemoteFailureHandler.HandleAsync(context);

        var message = DecodedError(httpContext);
        Assert.Contains("OidcMissingClaim", message);
        Assert.Contains("username", message);
    }

    [Fact]
    public async Task OidcAuthenticationException_UsesItsResourceKey()
    {
        var context = CreateContext(new OidcAuthenticationException("OidcInvalidEmailFormat"), out var httpContext);

        await OidcRemoteFailureHandler.HandleAsync(context);

        Assert.Contains("OidcInvalidEmailFormat", DecodedError(httpContext));
    }

    [Fact]
    public async Task UnexpectedException_FallsBackToGenericMessage()
    {
        var context = CreateContext(new InvalidOperationException("boom"), out var httpContext);

        await OidcRemoteFailureHandler.HandleAsync(context);

        Assert.NotNull(context.Result);
        Assert.Contains("OidcLoginFailed", DecodedError(httpContext));
    }

    [Fact]
    public async Task Message_IsUrlEncoded()
    {
        var context = CreateContext(new OidcAuthenticationException("OidcLoginFailed"), out var httpContext);

        await OidcRemoteFailureHandler.HandleAsync(context);

        Assert.DoesNotContain(" ", httpContext.Response.Headers.Location.ToString());
    }
}
