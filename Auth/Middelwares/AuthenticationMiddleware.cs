using MailArchiver.Auth.Handlers;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MailArchiver.Auth.Middlewares
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, MailArchiver.Services.IAuthenticationService authService,
            IOptions<ApiOptions> apiOptions)
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // Read-only REST API branch: API keys only, never a login redirect.
            if (path.StartsWith("/api/"))
            {
                await HandleApiRequestAsync(context, apiOptions.Value);
                return;
            }

            // Skip authentication for certain paths
            var skipPaths = new[] { "/auth/login", "/auth/logout", "/auth/blocked", "/oidc-signin-completed", "/twofactor/", "/css/", "/js/", "/images/", "/favicon" };

            var shouldSkip = skipPaths.Any(skipPath => path.StartsWith(skipPath));

            if (!shouldSkip)
            {
                // Check if user is authenticated through framework
                var isAuthenticated = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Also check our custom service for 2FA state
                if (!authService.IsAuthenticated(context))
                {
                    // Store the original URL for redirect after login
                    var returnUrl = context.Request.Path + context.Request.QueryString;
                    context.Response.Redirect($"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                    return;
                }
            }

            await _next(context);
        }

        private async Task HandleApiRequestAsync(HttpContext context, ApiOptions apiOptions)
        {
            // API disabled => behave as if the routes do not exist (do not leak existence).
            if (!apiOptions.Enabled)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var result = await context.AuthenticateAsync(ApiKeyAuthenticationHandler.SchemeName);
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await WriteProblemAsync(context, StatusCodes.Status401Unauthorized,
                    "Unauthorized", "A valid API key is required. Send it as 'Authorization: Bearer ma_...'.");
                return;
            }

            // Establish the principal so existing claim-based logic works unchanged.
            context.User = result.Principal!;
            await _next(context);
        }

        private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
        {
            var problemDetailsService = context.RequestServices.GetService<IProblemDetailsService>();
            context.Response.ContentType = "application/problem+json";

            if (problemDetailsService != null)
            {
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails =
                    {
                        Status = status,
                        Title = title,
                        Detail = detail,
                        Instance = context.Request.Path
                    }
                });
            }
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status,
                    title,
                    detail,
                    instance = context.Request.Path.Value
                });
                await context.Response.WriteAsync(json);
            }
        }
    }
}
