using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Controllers
{
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<UsersController> _logger;
        private readonly MailArchiver.Services.IAuthenticationService _authService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public UsersController(IUserService userService, MailArchiverDbContext context, ILogger<UsersController> logger, MailArchiver.Services.IAuthenticationService authService, IStringLocalizer<SharedResource> localizer)
        {
            _userService = userService;
            _context = context;
            _logger = logger;
            _authService = authService;
            _localizer = localizer;
        }

        // GET: Users
        [AdminRequired]
        public async Task<IActionResult> Index()
        {
            var users = await _userService.GetAllUsersAsync();
            return View(users);
        }

        // GET: Users/Details/5
        [AdminRequired]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Get user's mail accounts
            var mailAccounts = await _userService.GetUserMailAccountsAsync(id);
            ViewBag.MailAccounts = mailAccounts;

            return View(user);
        }

        // GET: Users/Create
        [AdminRequired]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> Create(CreateUserViewModel model, string password)
        {
            _logger.LogInformation("Create user called with Username: {Username}, Email: {Email}, Password length: {PasswordLength}",
                model?.Username, model?.Email, password?.Length ?? 0);

            // Validate password
            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Password is null or empty");

                ModelState.AddModelError("password", _localizer["PasswordRequired"]);
            }
            else if (password.Length < 6)
            {
                _logger.LogWarning("Password too short: {Length}", password.Length);
                ModelState.AddModelError("password", _localizer["PasswordTooShort"]);
            }

            // Check if username or email already exists only if other validations pass
            if (ModelState.IsValid)
            {
                var existingUser = await _userService.GetUserByUsernameAsync(model.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", _localizer["UsernameExists"]);
                }

                var existingEmailUser = await _userService.GetUserByEmailAsync(model.Email);
                if (existingEmailUser != null)
                {
                    ModelState.AddModelError("Email", _localizer["EmailExists"]);
                }
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid. Errors: {Errors}",
                    string.Join(", ", ModelState.SelectMany(x => x.Value.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))));
                // Pass the password back to the view for form retention
                ViewBag.Password = password;
                return View(model);
            }

            try
            {
                // Create the user
                var newUser = await _userService.CreateUserAsync(
                    model.Username,
                    model.Email,
                    password,
                    model.IsAdmin);

                // Set self-manager flag if specified
                if (model.IsSelfManager)
                {
                    newUser.IsSelfManager = true;
                    await _userService.UpdateUserAsync(newUser);
                }

                TempData["SuccessMessage"] = _localizer["UserCreatedSuccess", newUser.Username].Value;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Message}", ex.Message);
                ModelState.AddModelError("", $"{_localizer["ErrorOccurred"]}: {ex.Message}");
                return View(model);
            }
        }

        // GET: Users/Edit/5
        [AdminRequired]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> Edit(int id, User user, string? newPassword)
        {
            _logger.LogInformation("Edit POST called with id: {Id}, user: {User}, newPassword length: {PasswordLength}",
                id, user?.Username, newPassword?.Length ?? 0);

            if (id != user.Id)
            {
                _logger.LogWarning("ID mismatch: route id {RouteId} != user.Id {UserId}", id, user?.Id);
                return NotFound();
            }

            // Validate new password if provided
            if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 6)
            {
                _logger.LogWarning("New password too short: {Length}", newPassword.Length);
                ModelState.AddModelError("newPassword", _localizer["PasswordTooShort"]);
            }

            _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid. Errors: {Errors}",
                    string.Join(", ", ModelState.SelectMany(x => x.Value.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation("Attempting to get user with id: {Id}", id);
                    var existingUser = await _userService.GetUserByIdAsync(id);
                    if (existingUser == null)
                    {
                        _logger.LogWarning("User not found with id: {Id}", id);
                        return NotFound();
                    }

                    // Check if trying to remove admin rights from the last admin
                    if (existingUser.IsAdmin && !user.IsAdmin)
                    {
                        var adminCount = await _userService.GetAdminCountAsync();
                        if (adminCount <= 1)
                        {
                            _logger.LogWarning("Cannot remove admin rights. At least one admin must exist.");
                            ModelState.AddModelError("IsAdmin", _localizer["CannotRemoveAdmin"]);
                            return View(user);
                        }
                    }

                    // Update user properties
                    _logger.LogInformation("Updating user properties for user: {Username}", existingUser.Username);
                    existingUser.Username = user.Username;
                    existingUser.Email = user.Email;
                    existingUser.IsAdmin = user.IsAdmin;
                    existingUser.IsSelfManager = user.IsSelfManager;
                    existingUser.IsActive = user.IsActive;

                    // Update password if provided
                    if (!string.IsNullOrWhiteSpace(newPassword))
                    {
                        _logger.LogInformation("Updating password for user: {Username}", existingUser.Username);
                        existingUser.PasswordHash = _userService.HashPassword(newPassword);
                    }

                    _logger.LogInformation("Attempting to update user: {Username}", existingUser.Username);
                    var result = await _userService.UpdateUserAsync(existingUser);
                    if (result)
                    {
                        _logger.LogInformation("User '{Username}' updated successfully.", existingUser.Username);
                        TempData["SuccessMessage"] = _localizer["UserUpdated", existingUser.Username].Value;
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update user: {Username}", existingUser.Username);
                        ModelState.AddModelError("", _localizer["UserUpdateFailed"]);
                        return View(user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user: {Message}", ex.Message);
                    ModelState.AddModelError("", $"{_localizer["ErrorOccurred"]}: {ex.Message}");
                    return View(user);
                }
            }

            // If we get here, something went wrong with validation or model binding
            // Let's get the user again to ensure we have all the data for the view
            _logger.LogInformation("Returning view with validation errors");
            var currentUser = await _userService.GetUserByIdAsync(id);
            if (currentUser != null)
            {
                // Preserve the values that the user entered in the form
                currentUser.Username = user.Username;
                currentUser.Email = user.Email;
                currentUser.IsAdmin = user.IsAdmin;
                currentUser.IsSelfManager = user.IsSelfManager;
                currentUser.IsActive = user.IsActive;
                return View(currentUser);
            }

            return View(user);
        }

        // POST: Users/ResetTwoFactor/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> ResetTwoFactor(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                    return RedirectToAction(nameof(Index));
                }

                // Reset 2FA properties
                user.IsTwoFactorEnabled = false;
                user.TwoFactorSecret = null;
                user.TwoFactorBackupCodes = null;

                var result = await _userService.UpdateUserAsync(user);
                if (result)
                {
                    TempData["SuccessMessage"] = _localizer["TwoFactorResetSuccess", user.Username].Value;
                }
                else
                {
                    TempData["ErrorMessage"] = _localizer["TwoFactorResetFail", user.Username].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting 2FA for user: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["ErrorOccurred"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                    return RedirectToAction(nameof(Index));
                }

                // Prevent deleting the last admin user
                if (user.IsAdmin)
                {
                    var adminCount = await _userService.GetAdminCountAsync();
                    if (adminCount <= 1)
                    {
                        TempData["ErrorMessage"] = _localizer["UserDeleteAdmin"].Value;
                        return RedirectToAction(nameof(Index));
                    }
                }

                var result = await _userService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = _localizer["UserDeleteSuccess", user.Username].Value;
                }
                else
                {
                    TempData["ErrorMessage"] = _localizer["UserDeleteFail", user.Username].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["ErrorOccurred"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/AssignAccounts/5
        [AdminRequired]
        public async Task<IActionResult> AssignAccounts(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Get all mail accounts
            var allAccounts = await _context.MailAccounts.ToListAsync();

            // Get currently assigned accounts
            var assignedAccounts = await _userService.GetUserMailAccountsAsync(id);
            var assignedAccountIds = assignedAccounts.Select(a => a.Id).ToList();

            var model = new UserMailAccountViewModel
            {
                User = user,
                AllMailAccounts = allAccounts,
                AssignedAccountIds = assignedAccountIds
            };

            return View(model);
        }

        // POST: Users/AssignAccounts/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> AssignAccounts(int id, int[] selectedAccountIds)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                    return RedirectToAction(nameof(Index));
                }

                // Get currently assigned accounts
                var currentAssignedAccounts = await _userService.GetUserMailAccountsAsync(id);
                var currentAssignedIds = currentAssignedAccounts.Select(a => a.Id).ToList();

                // Remove accounts that are no longer selected
                foreach (var accountId in currentAssignedIds)
                {
                    if (!selectedAccountIds.Contains(accountId))
                    {
                        await _userService.RemoveMailAccountFromUserAsync(id, accountId);
                    }
                }

                // Add newly selected accounts
                foreach (var accountId in selectedAccountIds)
                {
                    if (!currentAssignedIds.Contains(accountId))
                    {
                        await _userService.AssignMailAccountToUserAsync(id, accountId);
                    }
                }

                TempData["SuccessMessage"] = _localizer["AccountAssignmentSuccess", user.Username].Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating mail account assignments: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["ErrorOccurred"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/ChangePassword
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            // Get the current user's information
            var currentUsername = _authService.GetCurrentUser(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction("Index", "Home");
            }

            // For security reasons, we don't want to pass the full user object to the view
            // Instead, we'll create a simple view model with just the username
            ViewBag.Username = currentUser.Username;
            return View();
        }

        // POST: Users/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
        {
            // Get the current user's information
            var currentUsername = _authService.GetCurrentUser(HttpContext);
            var currentUser = await _userService.GetUserByUsernameAsync(currentUsername);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                return RedirectToAction("Index", "Home");
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                ModelState.AddModelError("currentPassword", _localizer["PasswordCurrentRequired"]);
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ModelState.AddModelError("newPassword", _localizer["PasswordNewRequired"]);
            }
            else if (newPassword.Length < 6)
            {
                ModelState.AddModelError("newPassword", _localizer["PasswordTooShort"]);
            }
            else if (newPassword != confirmNewPassword)
            {
                ModelState.AddModelError("confirmNewPassword", _localizer["PasswordNotMatch"]);
            }

            // If validation passes, check current password
            if (ModelState.IsValid)
            {
                // Verify current password
                var isCurrentPasswordValid = await _userService.AuthenticateUserAsync(currentUser.Username, currentPassword);
                if (!isCurrentPasswordValid)
                {
                    ModelState.AddModelError("currentPassword", _localizer["PasswordCurrentIncorrect"]);
                }
                else
                {
                    // Update password
                    try
                    {
                        currentUser.PasswordHash = _userService.HashPassword(newPassword);
                        var result = await _userService.UpdateUserAsync(currentUser);

                        if (result)
                        {
                            TempData["SuccessMessage"] = _localizer["PasswordChangeSuccess"].Value;
                            return RedirectToAction("Index", "Home");
                        }
                        else
                        {
                            ModelState.AddModelError("", _localizer["PasswordChangeFail"]);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating password for user: {Username}", currentUser.Username);
                        ModelState.AddModelError("", $"{_localizer["ErrorOccurred"]}: {ex.Message}");
                    }
                }
            }

            // If we get here, something went wrong
            ViewBag.Username = currentUser.Username;
            return View();
        }

        // POST: Users/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminRequired]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = _localizer["UserNotFound"].Value;
                    return RedirectToAction(nameof(Index));
                }

                // Prevent disabling the last admin user
                if (user.IsAdmin && user.IsActive)
                {
                    var adminCount = await _userService.GetAdminCountAsync();
                    if (adminCount <= 1)
                    {
                        TempData["ErrorMessage"] = _localizer["CannotDisableAdmin"].Value;
                        return RedirectToAction(nameof(Index));
                    }
                }

                var result = await _userService.SetUserActiveStatusAsync(id, !user.IsActive);
                if (result)
                {
                    TempData["SuccessMessage"] = _localizer["UserChangeActiveSuccess", user.Username, (user.IsActive ? _localizer["UserEnabled"] : _localizer["UserDisabled"])].Value;
                }
                else
                {
                    TempData["ErrorMessage"] = _localizer["UserChangeActiveFail", user.Username, (user.IsActive ? _localizer["UserEnabled"] : _localizer["UserDisabled"])].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user active status: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["ErrorOccurred"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
