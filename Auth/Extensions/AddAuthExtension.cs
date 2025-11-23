using MailArchiver.Auth.Exceptions;
using MailArchiver.Auth.Handlers;
using MailArchiver.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

namespace MailArchiver.Auth.Extensions
{
    public static class AddAuthExtension
    {
        public static WebApplicationBuilder AddAuth(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<AuthenticationHandler>();
            builder.Services.AddHttpContextAccessor();
            var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);
            authBuilder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.Cookie.Name = "MailArchiverAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.SlidingExpiration = true;
            });

            // conditional OAuth setup
            var oauthOptions = builder.Configuration.GetSection(OAuthOptions.OAuth).Get<OAuthOptions>();
            if (oauthOptions?.Enabled ?? false)
            {
                authBuilder.AddCookie("OidcCookie"); // temporary storage for OIDC result
                authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, (o) => {
                    o.ClientId = oauthOptions.ClientId;
                    o.ClientSecret = oauthOptions.ClientSecret;
                    o.CallbackPath = "/oidc-signin-completed";
                    o.Authority = oauthOptions.Authority;
                    o.ResponseType = OpenIdConnectResponseType.Code;
                    o.GetClaimsFromUserInfoEndpoint = true;
                    o.SignInScheme = "OidcCookie";
                    o.TokenValidationParameters.NameClaimType = "name";
                    if (oauthOptions.ClientScopes != null)
                    {
                        o.Scope.Clear();
                        foreach (var scope in oauthOptions.ClientScopes)
                        {
                            o.Scope.Add(scope);
                        }
                    }
                    o.Events.OnUserInformationReceived = async (UserInformationReceivedContext ctx) => {
                        var handler = ctx.Request.HttpContext.RequestServices.GetRequiredService<AuthenticationHandler>();

                        var id = ctx.User.RootElement.GetProperty("sub").GetString();
                        var name = ctx.User.RootElement.GetProperty("name").GetString();
                        var email = ctx.User.RootElement.GetProperty("email").GetString();

                        if(string.IsNullOrWhiteSpace(id))
                            throw new MissingClaimException("sub");
                        if(string.IsNullOrWhiteSpace(name))
                            throw new MissingClaimException("name");
                        if(string.IsNullOrWhiteSpace(email))
                            throw new MissingClaimException("email");


                        var identity = ctx.Principal.Identity as ClaimsIdentity;
                        identity.AddClaim(new Claim(ClaimTypes.Email, email));
                        identity.AddClaim(new Claim(ClaimTypes.Name, name));

                        await handler.HandleUserAuthenticated(
                            OpenIdConnectDefaults.AuthenticationScheme
                            , id
                            , persistAuthentication: false
                            , remoteIdentity: ctx.Principal.Identity);
                    };
                });
            }
            return builder;
        }
    }
}
