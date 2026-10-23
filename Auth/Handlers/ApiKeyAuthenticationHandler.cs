using System.Security.Claims;
using System.Text.Encodings.Web;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailArchiver.Auth.Handlers
{
    /// <summary>
    /// Authenticates read-only REST API requests via an <c>Authorization: Bearer ma_...</c>
    /// header. On success it builds a <see cref="ClaimsPrincipal"/> with the exact claim
    /// shape that cookie login issues (see CookieAuthenticationService.StartUserSessionAsync),
    /// so all existing IAuthenticationService / allowedAccountIds logic works unchanged.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";
        private const string BearerPrefix = "Bearer ";

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? authHeader = Request.Headers.Authorization;
            if (string.IsNullOrWhiteSpace(authHeader) ||
                !authHeader.StartsWith(BearerPrefix, StringComparison.Ordinal))
            {
                // No bearer token presented; let the caller decide (results in 401).
                return AuthenticateResult.NoResult();
            }

            var token = authHeader.Substring(BearerPrefix.Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.Fail("Missing API key.");
            }

            // IApiKeyService is scoped; resolve it from the request services.
            var apiKeyService = Context.RequestServices.GetRequiredService<IApiKeyService>();
            var user = await apiKeyService.ValidateAsync(token);
            if (user == null)
            {
                return AuthenticateResult.Fail("Invalid or inactive API key.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("UserId", user.Id.ToString())
            };

            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }
            if (user.IsSelfManager)
            {
                claims.Add(new Claim(ClaimTypes.Role, "SelfManager"));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return AuthenticateResult.Success(ticket);
        }
    }
}
