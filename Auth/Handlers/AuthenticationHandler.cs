using System.Security.Claims;
using System.Security.Principal;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MailArchiver.Auth.Handlers
{
    public class AuthenticationHandler
    {
        private readonly ILogger<AuthenticationHandler> _logger;
        private readonly MailArchiver.Services.IAuthenticationService _authenticationService;
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAccessLogService _accessLogService;

        public AuthenticationHandler(
            ILogger<AuthenticationHandler> logger
            , MailArchiver.Services.IAuthenticationService authenticationService
            , IUserService userService
            , IHttpContextAccessor httpContextAccessor
            , IAccessLogService accessLogService)
        {
            _logger = logger;
            _authenticationService = authenticationService;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
            _accessLogService = accessLogService;
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

            // start a user session
            await _authenticationService.StartUserSessionAsync(
                localUser
                , persistAuthentication);

            // log access
            var sourceIp = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            await _accessLogService.LogAccessAsync(localUser.Username, AccessLogType.Login, searchParameters: $"IP: {sourceIp}");

            // set last login time
            localUser.LastLoginAt = DateTime.UtcNow;
            await _userService.UpdateUserAsync(localUser);
        }
    }
}