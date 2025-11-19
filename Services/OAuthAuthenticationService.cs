using MailArchiver.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MailArchiver.Services
{
    public class OAuthAuthenticationService
    {
        private readonly ILogger<OAuthAuthenticationService> _logger;
        private readonly IOptions<OAuthOptions> _oAuthOptions;

        public OAuthAuthenticationService(
            ILogger<OAuthAuthenticationService> logger
            , IOptions<OAuthOptions> oAuthOptions)
        {
            _logger = logger;
            _oAuthOptions = oAuthOptions;
        }

        public async Task HandleLoginAsync(
            UserInformationReceivedContext ctx
            , CancellationToken cancellationToken = default) { 
        }
    }
}
