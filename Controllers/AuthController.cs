using MailArchiver.Auth.Handlers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers
{
    public class AuthController : Controller
    {
        private readonly AuthenticationHandler _authenticationHandler;
        private readonly MailArchiver.Services.IAuthenticationService _authService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IAccessLogService _accessLogService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IOptions<OAuthOptions> _oAuthOptions;

        public AuthController(
            MailArchiver.Services.IAuthenticationService authService
            , AuthenticationHandler authenticationHandler
            , IUserService userService
            , ILogger<AuthController> logger
            , IStringLocalizer<SharedResource> localizer
            , IAccessLogService accessLogService
            , IServiceScopeFactory serviceScopeFactory
            , IOptions<OAuthOptions> oAuthOptions)
        {
            _authService = authService;
            _authenticationHandler = authenticationHandler;
            _userService = userService;
            _logger = logger;
            _localizer = localizer;
            _accessLogService = accessLogService;
            _serviceScopeFactory = serviceScopeFactory;
            _oAuthOptions = oAuthOptions;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            // If already authenticated, redirect to return URL or home
            if (_authService.IsAuthenticated(HttpContext))
            {
                return RedirectToLocal(returnUrl);
            }

            ViewBag.OAuthEnabled = _oAuthOptions.Value.Enabled;
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("LoginAttempts")]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                if (_authService.ValidateCredentials(model.Username, model.Password))
                {
                    // Check if 2FA is enabled for the user
                    var user = await _userService.GetUserByUsernameAsync(model.Username);
                    if (user != null && user.IsTwoFactorEnabled)
                    {
                        // Store username in session for 2FA verification
                        HttpContext.Session.SetString("TwoFactorUsername", model.Username);
                        HttpContext.Session.SetString("TwoFactorRememberMe", model.RememberMe.ToString());
                        return RedirectToAction("Verify", "TwoFactor");
                    }

                    await _authenticationHandler.HandleUserAuthenticated(
                        CookieAuthenticationDefaults.AuthenticationScheme
                        , model.Username
                        , model.RememberMe);
                    
                    // Check if this is initial setup (no mail accounts + default credentials)
                    var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    var defaultUsername = configuration["Authentication:Username"];
                    var defaultPassword = configuration["Authentication:Password"];
                    var dbContext = HttpContext.RequestServices.GetRequiredService<MailArchiverDbContext>();
                    var mailAccountCount = await dbContext.MailAccounts.CountAsync();
                    
                    if (mailAccountCount == 0 && 
                        model.Username == defaultUsername && 
                        model.Password == defaultPassword)
                    {
                        // Force password change for initial setup
                        HttpContext.Session.SetString("MustChangePassword", "true");
                        _logger.LogWarning("User {Username} logged in with default credentials on initial setup - forcing password change", model.Username);
                    }
                    
                    // Log the successful login using a separate task to avoid DbContext concurrency issues
                    var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                            await accessLogService.LogAccessAsync(model.Username, AccessLogType.Login, searchParameters: $"IP: {sourceIp}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error logging login action for user {Username}", model.Username);
                        }
                    });
                    
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("", _localizer["InvalidUserPassword"]);
                    _logger.LogWarning("Failed login attempt for username: {Username} from IP: {IP}", 
                        model.Username, HttpContext.Connection.RemoteIpAddress);
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task LoginWithOAuth(OAuthLoginViewModel oAuthLoginViewModel) {
            var properties = new AuthenticationProperties();

            if(!string.IsNullOrWhiteSpace(oAuthLoginViewModel.ReturnUrl))
                properties.Items["returnUrl"] = oAuthLoginViewModel.ReturnUrl;


            // trigger the  OIDC login flow
            await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(false);
        }

        [HttpGet("[Controller]/LoginWithOAuth")]
        public async Task<IActionResult> OidcCallback()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = _authService.GetCurrentUserDisplayName(HttpContext);
            _authService.SignOut(HttpContext);
            
            // Log the logout if we have a username using a separate task to avoid DbContext concurrency issues
            if (!string.IsNullOrEmpty(username))
            {
                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                        await accessLogService.LogAccessAsync(username, AccessLogType.Logout, searchParameters: $"IP: {sourceIp}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error logging logout action for user {Username}", username);
                    }
                });
            }
            
            return RedirectToAction("Login");
        }
        
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        
        [HttpGet]
        public IActionResult Blocked()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}
