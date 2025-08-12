using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Controllers
{
    [AdminRequired]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, MailArchiverDbContext context, ILogger<UsersController> logger)
        {
            _userService = userService;
            _context = context;
            _logger = logger;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _userService.GetAllUsersAsync();
            return View(users);
        }

        // GET: Users/Details/5
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
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model, string password)
        {
            _logger.LogInformation("Create user called with Username: {Username}, Email: {Email}, Password length: {PasswordLength}", 
                model?.Username, model?.Email, password?.Length ?? 0);

            // Validate password
            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Password is null or empty");
                ModelState.AddModelError("password", "Password is required.");
            }
            else if (password.Length < 6)
            {
                _logger.LogWarning("Password too short: {Length}", password.Length);
                ModelState.AddModelError("password", "Password must be at least 6 characters long.");
            }

            // Check if username or email already exists only if other validations pass
            if (ModelState.IsValid)
            {
                var existingUser = await _userService.GetUserByUsernameAsync(model.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                }

                var existingEmailUser = await _userService.GetUserByEmailAsync(model.Email);
                if (existingEmailUser != null)
                {
                    ModelState.AddModelError("Email", "Email already exists.");
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

                TempData["SuccessMessage"] = $"User '{newUser.Username}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Message}", ex.Message);
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                return View(model);
            }
        }

        // GET: Users/Edit/5
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
                ModelState.AddModelError("newPassword", "Password must be at least 6 characters long.");
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
                            ModelState.AddModelError("IsAdmin", "Cannot remove admin rights. At least one admin must exist.");
                            return View(user);
                        }
                    }

                    // Update user properties
                    _logger.LogInformation("Updating user properties for user: {Username}", existingUser.Username);
                    existingUser.Username = user.Username;
                    existingUser.Email = user.Email;
                    existingUser.IsAdmin = user.IsAdmin;
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
                        TempData["SuccessMessage"] = $"User '{existingUser.Username}' updated successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update user: {Username}", existingUser.Username);
                        ModelState.AddModelError("", "Failed to update user.");
                        return View(user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user: {Message}", ex.Message);
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
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
                currentUser.IsActive = user.IsActive;
                return View(currentUser);
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Prevent deleting the admin user
                if (user.IsAdmin && user.Username == "admin")
                {
                    TempData["ErrorMessage"] = "Cannot delete the main admin user.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _userService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = $"User '{user.Username}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to delete user '{user.Username}'.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/AssignAccounts/5
        public async Task<IActionResult> AssignAccounts(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
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
        public async Task<IActionResult> AssignAccounts(int id, int[] selectedAccountIds)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
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

                TempData["SuccessMessage"] = $"Mail account assignments for user '{user.Username}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating mail account assignments: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Users/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Prevent disabling the admin user
                if (user.IsAdmin && user.Username == "admin")
                {
                    TempData["ErrorMessage"] = "Cannot disable the main admin user.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _userService.SetUserActiveStatusAsync(id, !user.IsActive);
                if (result)
                {
                    TempData["SuccessMessage"] = $"User '{user.Username}' has been {(user.IsActive ? "disabled" : "enabled")}.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to {(user.IsActive ? "disable" : "enable")} user '{user.Username}'.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user active status: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
