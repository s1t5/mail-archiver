using MailArchiver.Auth.Exceptions;
using MailArchiver.Auth.Handlers;
using MailArchiver.Auth.Helpers;
using MailArchiver.Auth.Options;
using MailArchiver.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace MailArchiver.Auth.Extensions
{
    public static class AddAuthExtension
    {
        // Helper method to parse SameSite mode from string
        private static SameSiteMode ParseSameSiteMode(string? value)
        {
            return value?.ToLowerInvariant() switch
            {
                "strict" => SameSiteMode.Strict,
                "none" => SameSiteMode.None,
                _ => SameSiteMode.Lax // Default to Lax for better cross-site navigation support
            };
        }

        public static WebApplicationBuilder AddAuth(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<AuthenticationHandler>();
            builder.Services.AddHttpContextAccessor();
            
            // Get authentication options for SameSite configuration
            var authOptionsConfig = builder.Configuration.GetSection(AuthenticationOptions.Authentication).Get<AuthenticationOptions>() ?? new AuthenticationOptions();
            var cookieSameSiteMode = ParseSameSiteMode(authOptionsConfig.CookieSameSite);
            
            var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);
            authBuilder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.Cookie.Name = authOptionsConfig.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = cookieSameSiteMode;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(authOptionsConfig.SessionTimeoutMinutes);
                options.SlidingExpiration = true;

                // Cookie authentication is self-contained by default: the ticket stays
                // valid until it expires, no matter what happens to the user row. Validate
                // it against the stored security stamp so a password change, password
                // reset, deactivation, role change or deletion ends existing sessions.
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var dbContext = context.HttpContext.RequestServices
                            .GetRequiredService<MailArchiver.Data.MailArchiverDbContext>();

                        if (await MailArchiver.Auth.Services.SecurityStampValidator
                                .IsSessionValidAsync(context.Principal, dbContext))
                        {
                            return;
                        }

                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("MailArchiver.Auth.SecurityStamp");
                        logger.LogInformation(
                            "Rejected auth cookie for user {Username}: security stamp changed, account inactive or deleted",
                            context.Principal?.Identity?.Name);

                        context.RejectPrincipal();
                        await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions
                            .SignOutAsync(context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

            // Read-only REST API: bearer API-key scheme (validated by the /api/
            // branch of AuthenticationMiddleware). Cookies are never accepted on /api.
            authBuilder.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                MailArchiver.Auth.Handlers.ApiKeyAuthenticationHandler>(
                MailArchiver.Auth.Handlers.ApiKeyAuthenticationHandler.SchemeName, null);

            // conditional OAuth setup
            var oauthOptions = builder.Configuration.GetSection(OAuthOptions.OAuth).Get<OAuthOptions>();
            if (oauthOptions?.Enabled ?? false)
            {
                authBuilder.AddCookie(OAuthOptions.SignInScheme, options =>
                {
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = cookieSameSiteMode;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }); // temporary storage for OIDC result
                authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, (o) => {
                    o.ClientId = oauthOptions.ClientId;
                    o.ClientSecret = oauthOptions.ClientSecret;
                    o.CallbackPath = "/oidc-signin-completed";
                    o.Authority = oauthOptions.Authority;
                    o.ResponseType = OpenIdConnectResponseType.Code;
                    o.GetClaimsFromUserInfoEndpoint = true;
                    o.SignInScheme = OAuthOptions.SignInScheme;
                    o.RequireHttpsMetadata = true; // Require HTTPS for security
                    o.SaveTokens = false; // Don't save tokens in cookies for security
                    o.UsePkce = true; // Use PKCE for additional security

                    // Providers such as the Synology SSO Server do not support the
                    // framework default 'form_post' and do not advertise their supported
                    // response modes in the discovery document. Allow overriding it.
                    if (!string.IsNullOrWhiteSpace(oauthOptions.ResponseMode))
                    {
                        o.ResponseMode = oauthOptions.ResponseMode;
                    }
                    
                    // SECURITY: Proper token validation parameters
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = oauthOptions.Authority,
                        ValidateAudience = true,
                        ValidAudience = oauthOptions.ClientId,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 min clock skew
                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
                    
                    if (oauthOptions.ClientScopes != null)
                    {
                        o.Scope.Clear();
                        foreach (var scope in oauthOptions.ClientScopes)
                        {
                            o.Scope.Add(scope);
                        }
                    }
                    
                    o.Events.OnRemoteSignOut = context =>
                    {
                        // Handle remote sign-out requests from the OIDC provider
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogInformation("Received remote sign-out request from OIDC provider");
                        return Task.CompletedTask;
                    };

                    o.Events.OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        // Customize the redirect to identity provider for sign-out
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogInformation("Redirecting to identity provider for sign-out");
                        
                        // Add post-logout redirect URI to return user to login page after OIDC logout
                        context.ProtocolMessage.PostLogoutRedirectUri = context.Request.Scheme + "://" + context.Request.Host + "/Auth/Login";
                        return Task.CompletedTask;
                    };

                    // Any failure inside the OIDC pipeline (unusable claims, token
                    // validation, user provisioning) ends up here. Without this handler
                    // the exception escapes and the user is served an HTTP 500.
                    o.Events.OnRemoteFailure = OidcRemoteFailureHandler.HandleAsync;

                    o.Events.OnUserInformationReceived = async (UserInformationReceivedContext ctx) => {
                        var handler = ctx.Request.HttpContext.RequestServices.GetRequiredService<AuthenticationHandler>();
                        var logger = ctx.Request.HttpContext.RequestServices.GetRequiredService<ILogger<AuthenticationHandler>>();

                        // SECURITY: Safely extract claims with fallback from UserInfo and JWT
                        var id = OidcClaimExtractor.ExtractSubject(ctx.User.RootElement);
                        var name = OidcClaimExtractor.ExtractName(ctx.User.RootElement);
                        var email = OidcClaimExtractor.ExtractEmail(ctx.User.RootElement);
                        
                        // If email not in UserInfo, try to get from JWT token claims
                        if (string.IsNullOrWhiteSpace(email))
                        {
                            var emailClaim = ctx.Principal?.FindFirst(ClaimTypes.Email) 
                                          ?? ctx.Principal?.FindFirst("email")
                                          ?? ctx.Principal?.FindFirst("mail")
                                          ?? ctx.Principal?.FindFirst("upn");
                            if (emailClaim != null)
                                email = emailClaim.Value;
                        }

                        // SECURITY: Validate all required claims are present
                        if(string.IsNullOrWhiteSpace(id))
                        {
                            logger.LogError("Missing 'sub' claim in OIDC user info");
                            throw new MissingClaimException("sub");
                        }
                        if(string.IsNullOrWhiteSpace(name))
                        {
                            var accepted = string.Join(", ", OidcClaimExtractor.NameClaims);
                            logger.LogError("Missing name claim in OIDC user info (accepted: {Claims})", accepted);
                            throw new MissingClaimException(accepted);
                        }
                        if(string.IsNullOrWhiteSpace(email))
                        {
                            var accepted = string.Join(", ", OidcClaimExtractor.EmailClaims);
                            logger.LogError("Missing email claim in OIDC user info (accepted: {Claims})", accepted);
                            throw new MissingClaimException(accepted);
                        }

                        // SECURITY: Validate email format
                        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
                        if (!emailRegex.IsMatch(email))
                        {
                            logger.LogWarning("Invalid email format from OIDC provider: {Email}", email);
                            throw new OidcAuthenticationException("OidcInvalidEmailFormat");
                        }

                        // SECURITY: Validate claim lengths to prevent abuse
                        if (id.Length > 500 || name.Length > 100 || email.Length > 100)
                        {
                            logger.LogWarning("OIDC claim exceeds maximum length: sub={SubLen}, name={NameLen}, email={EmailLen}", 
                                id.Length, name.Length, email.Length);
                            throw new OidcAuthenticationException("OidcClaimLengthExceeded");
                        }

                        // SECURITY: Sanitize name to prevent XSS (remove HTML tags and dangerous characters)
                        name = Regex.Replace(name, @"<[^>]*>", string.Empty); // Remove HTML tags
                        name = name.Trim();

                        var identity = ctx.Principal.Identity as ClaimsIdentity;
                        identity.AddClaim(new Claim(ClaimTypes.Email, email));
                        identity.AddClaim(new Claim(ClaimTypes.Name, name));

                        try
                        {
                            await handler.HandleUserAuthenticated(
                                OpenIdConnectDefaults.AuthenticationScheme
                                , id
                                , persistAuthentication: false
                                , remoteIdentity: ctx.Principal.Identity);
                        }
                        catch (InvalidOperationException ex)
                        {
                            // Handle account linking errors (e.g., email already exists, pending approval)
                            logger.LogWarning(ex, "OIDC authentication failed: {Message}", ex.Message);
                            ctx.HandleResponse();
                            
                            // Check if this is a pending approval or deactivated account error
                            // These should redirect to the Blocked page to avoid infinite redirect loops
                            // when AutoRedirect is enabled
                            var localizer2 = ctx.Request.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                            var pendingApprovalMsg = localizer2["OidcAccountPendingApproval"].Value;
                            var deactivatedMsg = localizer2["OidcAccountDeactivated"].Value;
                            
                            if (ex.Message == pendingApprovalMsg || ex.Message == deactivatedMsg)
                            {
                                ctx.Response.Redirect($"/Auth/Blocked?message={Uri.EscapeDataString(ex.Message)}");
                            }
                            else
                            {
                                // Other errors (e.g., email already exists) go to login page
                                ctx.Response.Redirect($"/Auth/Login?error={Uri.EscapeDataString(ex.Message)}");
                            }
                        }
                    };
                });
            }
            return builder;
        }
    }
}
