using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using OtpNet;
using QRCoder;
using System.Security.Cryptography;

namespace MailArchiver.Controllers
{
    public class TwoFactorController : Controller
    {
        private readonly IUserService _userService;
        private readonly MailArchiver.Services.IAuthenticationService _authService;
        private readonly ILogger<TwoFactorController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IAccessLogService _accessLogService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public TwoFactorController(IUserService userService, MailArchiver.Services.IAuthenticationService authService, ILogger<TwoFactorController> logger, IStringLocalizer<SharedResource> localizer, IAccessLogService accessLogService, IServiceScopeFactory serviceScopeFactory)
        {
            _userService = userService;
            _authService = authService;
            _logger = logger;
            _localizer = localizer;
            _accessLogService = accessLogService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        // GET: TwoFactor/Setup
        public async Task<IActionResult> Setup()
        {
            var currentUsername = _authService.GetCurrentUserDisplayName(HttpContext);
            var user = await _userService.GetUserByUsernameAsync(currentUsername);
            
            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction("Index", "Home");
            }

            // Generate a new TOTP secret if not already set
            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                var secretKey = KeyGeneration.GenerateRandomKey(20);
                user.TwoFactorSecret = Base32Encoding.ToString(secretKey);
            }
            
            // Create the TOTP setup URL
            var issuer = "MailArchiver";
            var setupUrl = $"otpauth://totp/{issuer}:{user.Email}?secret={user.TwoFactorSecret}&issuer={issuer}";
            ViewBag.SetupUrl = setupUrl;
            
            // Generate QR Code as Base64 image
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(setupUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);
            ViewBag.QRCodeImage = $"data:image/png;base64,{qrCodeBase64}";
            
            // Temporarily store the secret in session for verification
            HttpContext.Session.SetString("TwoFactorSecret", user.TwoFactorSecret);
            
            return View(user);
        }

        // POST: TwoFactor/Enable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable(string token)
        {
            var currentUsername = _authService.GetCurrentUserDisplayName(HttpContext);
            var user = await _userService.GetUserByUsernameAsync(currentUsername);
            
            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction("Index", "Home");
            }

            // Get the secret from session (temporary storage)
            var secretFromSession = HttpContext.Session.GetString("TwoFactorSecret");
            if (string.IsNullOrEmpty(secretFromSession))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorSecretMissing"].Value;
                return RedirectToAction(nameof(Setup));
            }

            // Verify the TOTP token
            var totp = new Totp(Base32Encoding.ToBytes(secretFromSession));
            var isValid = totp.VerifyTotp(token, out _, new VerificationWindow(1, 1));
            
            if (!isValid)
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorTokenInvalid"].Value;
                // Recreate the setup URL
                var issuer = "MailArchiver";
                var setupUrl = $"otpauth://totp/{issuer}:{user.Email}?secret={secretFromSession}&issuer={issuer}";
                ViewBag.SetupUrl = setupUrl;
                // Generate QR Code as Base64 image
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(setupUrl, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(20);
                var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);
                ViewBag.QRCodeImage = $"data:image/png;base64,{qrCodeBase64}";
                return View("Setup", user);
            }

            // Save the secret to the database and enable 2FA
            var secretResult = await _userService.SetTwoFactorSecretAsync(user.Id, secretFromSession);
            var enabledResult = await _userService.SetTwoFactorEnabledAsync(user.Id, true);
            
            // Generate and save backup codes
            var backupCodesList = GenerateBackupCodes(10);
            var backupCodesString = string.Join(";", backupCodesList);
            var backupCodesResult = await _userService.SetTwoFactorBackupCodesAsync(user.Id, backupCodesString);
            
            if (secretResult && enabledResult && backupCodesResult)
            {
                TempData["SuccessMessage"] = _localizer["TwoFactorEnabledSuccess"].Value;
                // Remove temporary secret from session
                HttpContext.Session.Remove("TwoFactorSecret");
                // Show backup codes to user (one-time display)
                return View("ShowBackupCodes", backupCodesList);
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorEnableFail"].Value;
                return RedirectToAction(nameof(Setup));
            }
        }

        // GET: TwoFactor/Disable
        public async Task<IActionResult> Disable()
        {
            var currentUsername = _authService.GetCurrentUserDisplayName(HttpContext);
            var user = await _userService.GetUserByUsernameAsync(currentUsername);
            
            if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorNotEnabled"].Value;
                return RedirectToAction("Index", "Home");
            }
            
            return View();
        }

        // POST: TwoFactor/Disable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string password)
        {
            var currentUsername = _authService.GetCurrentUserDisplayName(HttpContext);
            var user = await _userService.GetUserByUsernameAsync(currentUsername);
            
            if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorNotEnabled"].Value;
                return RedirectToAction("Index", "Home");
            }

            // Verify password
            var isPasswordValid = await _userService.AuthenticateUserAsync(user.Username, password);
            if (!isPasswordValid)
            {
                ModelState.AddModelError("password", _localizer["PasswordIncorrect"].Value);
                return View();
            }

            // Disable 2FA
            var result = await _userService.SetTwoFactorEnabledAsync(user.Id, false);
            if (result)
            {
                TempData["SuccessMessage"] = _localizer["TwoFactorDisabledSuccess"].Value;
                return RedirectToAction("Index", "Home");
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorDisableFail"].Value;
                return View();
            }
        }

        // GET: TwoFactor/Verify
        public async Task<IActionResult> Verify()
        {
            // Get username from session
            var username = HttpContext.Session.GetString("TwoFactorUsername");
            if (string.IsNullOrEmpty(username))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorSessionExpired"].Value;
                return RedirectToAction("Login", "Auth");
            }

            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorNotEnabled"].Value;
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        // POST: TwoFactor/Verify
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("TwoFactorVerify")]
        public async Task<IActionResult> Verify(string token)
        {
            // Get username from session
            var username = HttpContext.Session.GetString("TwoFactorUsername");
            if (string.IsNullOrEmpty(username))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorSessionExpired"].Value;
                return RedirectToAction("Login", "Auth");
            }

            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction("Login", "Auth");
            }

            if (!user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                TempData["ErrorMessage"] = _localizer["TwoFactorNotEnabled"].Value;
                return RedirectToAction("Login", "Auth");
            }

            // Check if token is a backup code
            if (token.Length == 16)
            {
                var isBackupCode = await _userService.VerifyTwoFactorBackupCodeAsync(user.Id, token);
                if (isBackupCode)
                {
                    // Log successful backup code usage
                    _logger.LogInformation("2FA backup code used successfully for user {Username} from IP {IP}", 
                        user.Username, HttpContext.Connection.RemoteIpAddress);
                    
                    // Remove the used backup code
                    await _userService.RemoveUsedBackupCodeAsync(user.Id, token);
                    // Get remember me setting from session
                    var rememberMeString = HttpContext.Session.GetString("TwoFactorRememberMe");
                    bool rememberMe = false;
                    if (!string.IsNullOrEmpty(rememberMeString))
                    {
                        bool.TryParse(rememberMeString, out rememberMe);
                    }
                    // Sign in the user
                    await _authService.StartUserSessionAsync(
                        user
                        , rememberMe);
                    
                    // Log the successful login to access log
                    var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                            await accessLogService.LogAccessAsync(user.Username, AccessLogType.Login, searchParameters: $"IP: {sourceIp} (2FA Backup Code)");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error logging 2FA login action for user {Username}", user.Username);
                        }
                    });
                    
                    // Clear 2FA session data
                    HttpContext.Session.Remove("TwoFactorUsername");
                    HttpContext.Session.Remove("TwoFactorRememberMe");
                    // Clear any previous error messages
                    TempData.Clear();
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // Log failed backup code attempt
                    _logger.LogWarning("Invalid 2FA backup code attempt for user {Username} from IP {IP}", 
                        user.Username, HttpContext.Connection.RemoteIpAddress);
                }
            }

            // Verify the TOTP token
            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
            var isValid = totp.VerifyTotp(token, out _, new VerificationWindow(1, 1));
            
            if (isValid)
            {
                // Log successful 2FA verification
                _logger.LogInformation("2FA verification successful for user {Username} from IP {IP}", 
                    user.Username, HttpContext.Connection.RemoteIpAddress);
                
                // Get remember me setting from session
                var rememberMeString = HttpContext.Session.GetString("TwoFactorRememberMe");
                bool rememberMe = false;
                if (!string.IsNullOrEmpty(rememberMeString))
                {
                    bool.TryParse(rememberMeString, out rememberMe);
                }
                // Sign in the user
                await _authService.StartUserSessionAsync(
                    user
                    , rememberMe);
                
                // Log the successful login to access log
                var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                        await accessLogService.LogAccessAsync(user.Username, AccessLogType.Login, searchParameters: $"IP: {sourceIp} (2FA)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error logging 2FA login action for user {Username}", user.Username);
                    }
                });
                
                // Clear 2FA session data
                HttpContext.Session.Remove("TwoFactorUsername");
                HttpContext.Session.Remove("TwoFactorRememberMe");
                // Clear any previous error messages
                TempData.Clear();
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Log failed 2FA attempt with security details
                _logger.LogWarning("2FA verification failed for user {Username} from IP {IP}. Token: {TokenMask}", 
                    user.Username, 
                    HttpContext.Connection.RemoteIpAddress,
                    string.IsNullOrEmpty(token) ? "[empty]" : $"{token[..Math.Min(2, token.Length)]}****");
                
                TempData["ErrorMessage"] = _localizer["TwoFactorTokenInvalid"].Value;
                return View();
            }
        }

        private List<string> GenerateBackupCodes(int count)
        {
            var codes = new List<string>();
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < count; i++)
                {
                    var bytes = new byte[8];
                    rng.GetBytes(bytes);
                    var base64String = Convert.ToBase64String(bytes);
                    // Remove invalid characters and ensure we have exactly 16 characters
                    var cleanedString = base64String.Replace("+", "").Replace("/", "").Replace("=", "");
                    // Pad with 'A' if too short, or truncate if too long
                    if (cleanedString.Length < 16)
                    {
                        cleanedString = cleanedString.PadRight(16, 'A');
                    }
                    else if (cleanedString.Length > 16)
                    {
                        cleanedString = cleanedString.Substring(0, 16);
                    }
                    codes.Add(cleanedString);
                }
            }
            return codes;
        }

        // GET: TwoFactor/BackupCodesViewed
        public IActionResult BackupCodesViewed()
        {
            return RedirectToAction("Index", "Home");
        }
    }
}
