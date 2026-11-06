using MailArchiver.Auth.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Auth.Handlers
{
    /// <summary>
    /// Turns a failure inside the OIDC pipeline into a redirect to the login page with a
    /// readable message. Without this handler the exception escapes the authentication
    /// middleware and the user is served an HTTP 500.
    /// </summary>
    internal static class OidcRemoteFailureHandler
    {
        internal const string LoginErrorPath = "/Auth/Login";

        public static Task HandleAsync(RemoteFailureContext context)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

            string message;
            if (context.Failure is OidcAuthenticationException oidcFailure)
            {
                logger.LogError(oidcFailure, "OIDC sign-in failed: {Reason}", oidcFailure.ResourceKey);
                message = localizer[oidcFailure.ResourceKey, oidcFailure.Args].Value;
            }
            else
            {
                logger.LogError(context.Failure, "OIDC sign-in failed");
                message = localizer["OidcLoginFailed"].Value;
            }

            context.HandleResponse();
            context.Response.Redirect($"{LoginErrorPath}?error={Uri.EscapeDataString(message)}");
            return Task.CompletedTask;
        }
    }
}
