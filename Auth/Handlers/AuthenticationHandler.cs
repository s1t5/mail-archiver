using System.Security.Claims;
using System.Security.Principal;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MailArchiver.Auth.Handlers
{
    public class AuthenticationHandler
    {
        private readonly ILogger<AuthenticationHandler> _logger;
        private readonly IAuthenticationService _authenticationService;
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationHandler(
            ILogger<AuthenticationHandler> logger
            , IAuthenticationService authenticationService
            , IUserService userService
            , IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _authenticationService = authenticationService;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task HandleUserAuthenticated(
            string authenticationSchema
            , string userIdentifier
            , bool persistAuthentication = false
            , IIdentity? remoteIdentity = null)
        {
            User localUser;
            if(authenticationSchema != CookieAuthenticationDefaults.AuthenticationScheme)
            {
                localUser = await _userService.GetOrCreateUserFromRemoteIdentity(remoteIdentity as ClaimsIdentity);
            } else
            {
                localUser = await _userService.GetUserByUsernameAsync(userIdentifier);
            }

            await _authenticationService.StartUserSessionAsync(
                        _httpContextAccessor.HttpContext!
                        , CookieAuthenticationDefaults.AuthenticationScheme
                        , localUser.Username
                        , persistAuthentication);
        }
    }
}