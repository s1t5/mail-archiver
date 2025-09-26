using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Controllers
{
    public class AuthController : Controller
    {
        private readonly MailArchiver.Services.IAuthenticationService _authService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IAccessLogService _accessLogService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AuthController(MailArchiver.Services.IAuthenticationService authService, IUserService userService, ILogger<AuthController> logger, IStringLocalizer<SharedResource> localizer, IAccessLogService accessLogService, IServiceScopeFactory serviceScopeFactory)
        {
            _authService = authService;
            _userService = userService;
            _logger = logger;
            _localizer = localizer;
            _accessLogService = accessLogService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            // If authentication is disabled, redirect to home
            if (!_authService.IsAuthenticationRequired())
            {
                return RedirectToAction("Index", "Home");
            }

            // If already authenticated, redirect to return URL or home
            if (_authService.IsAuthenticated(HttpContext))
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!_authService.IsAuthenticationRequired())
            {
                return RedirectToAction("Index", "Home");
            }

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
                    
                    _authService.SignIn(HttpContext, model.Username, model.RememberMe);
                    
                    // Log the successful login using a separate task to avoid DbContext concurrency issues
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                            await accessLogService.LogAccessAsync(model.Username, AccessLogType.Login);
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = _authService.GetCurrentUser(HttpContext);
            _authService.SignOut(HttpContext);
            
            // Log the logout if we have a username using a separate task to avoid DbContext concurrency issues
            if (!string.IsNullOrEmpty(username))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                        await accessLogService.LogAccessAsync(username, AccessLogType.Logout);
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
