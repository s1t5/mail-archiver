using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;

using MailArchiver.Attributes;

namespace MailArchiver.Controllers
{
    [SelfManagerRequired]
    public class MailAccountsController : Controller
    {
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<MailAccountsController> _logger;
        private readonly BatchRestoreOptions _batchOptions;
        private readonly ISyncJobService _syncJobService;
        private readonly IMBoxImportService _mboxImportService;
        private readonly UploadOptions _uploadOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MailAccountsController(
            MailArchiverDbContext context,
            IEmailService emailService,
            ILogger<MailAccountsController> logger,
            IOptions<BatchRestoreOptions> batchOptions,
            ISyncJobService syncJobService,
            IMBoxImportService mboxImportService,
            IOptions<UploadOptions> uploadOptions, 
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _syncJobService = syncJobService;
            _mboxImportService = mboxImportService;
            _uploadOptions = uploadOptions.Value;
            _localizer = localizer;
        }

        private async Task<bool> HasAccessToAccountAsync(int accountId)
        {
            // Use the authentication service to get user info properly
            var authService = HttpContext.RequestServices.GetService<IAuthenticationService>();
            var currentUsername = authService.GetCurrentUser(HttpContext);
            var isAdmin = authService.IsCurrentUserAdmin(HttpContext);
            var isSelfManager = authService.IsCurrentUserSelfManager(HttpContext);

            _logger.LogInformation("HasAccessToAccountAsync - Current username: {Username}, IsAdmin: {IsAdmin}, IsSelfManager: {IsSelfManager}", 
                currentUsername, isAdmin, isSelfManager);

            // Admin users have access to all accounts
            if (isAdmin)
            {
                _logger.LogInformation("User is admin, granting access to account {AccountId}", accountId);
                return true;
            }

            // SelfManager users have access only to assigned accounts
            if (isSelfManager)
            {
                var hasAccess = await _context.MailAccounts
                    .AnyAsync(ma => ma.Id == accountId && ma.UserMailAccounts.Any(uma => uma.User.Username == currentUsername));
                _logger.LogInformation("User is SelfManager, access to account {AccountId}: {HasAccess}", accountId, hasAccess);
                return hasAccess;
            }

            // Other users have no access
            _logger.LogInformation("User has no special permissions, denying access to account {AccountId}", accountId);
            return false;
        }

        // GET: MailAccounts
        public async Task<IActionResult> Index()
        {
            // Use the authentication service to get user info properly
            var authService = HttpContext.RequestServices.GetService<IAuthenticationService>();
            var currentUsername = authService.GetCurrentUser(HttpContext);
            var isAdmin = authService.IsCurrentUserAdmin(HttpContext);
            var isSelfManager = authService.IsCurrentUserSelfManager(HttpContext);
            
            _logger.LogInformation("Current username: {Username}, IsAdmin: {IsAdmin}, IsSelfManager: {IsSelfManager}", 
                currentUsername, isAdmin, isSelfManager);

            IQueryable<MailAccount> mailAccountsQuery;

            // Check if user is admin (including legacy admin)
            if (isAdmin)
            {
                _logger.LogInformation("User is admin, showing all accounts");
                mailAccountsQuery = _context.MailAccounts;
            }
            else if (isSelfManager)
            {
                _logger.LogInformation("User is SelfManager, showing only assigned accounts");
                mailAccountsQuery = _context.MailAccounts
                    .Where(ma => ma.UserMailAccounts.Any(uma => uma.User.Username == currentUsername));
            }
            else
            {
                _logger.LogInformation("User has no special permissions, showing no accounts");
                mailAccountsQuery = _context.MailAccounts.Where(ma => false); // Empty query
            }

            var accounts = await mailAccountsQuery
                .Select(a => new MailAccountViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    EmailAddress = a.EmailAddress,
                    ImapServer = a.ImapServer,
                    ImapPort = a.ImapPort,
                    Username = a.Username,
                    UseSSL = a.UseSSL,
                    IsEnabled = a.IsEnabled,
                    LastSync = a.LastSync,
                    DeleteAfterDays = a.DeleteAfterDays,
                    IsImportOnly = a.IsImportOnly
                })
                .ToListAsync();

            _logger.LogInformation("Returning {Count} accounts for user {Username}", accounts.Count, currentUsername);
            return View(accounts);
        }

        // GET: MailAccounts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // E-Mail-Anzahl abrufen
            var emailCount = await _emailService.GetEmailCountByAccountAsync(id);

var model = new MailAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                EmailAddress = account.EmailAddress,
                ImapServer = account.ImapServer,
                ImapPort = account.ImapPort,
                Username = account.Username,
                UseSSL = account.UseSSL,
                LastSync = account.LastSync,
                IsEnabled = account.IsEnabled,
                DeleteAfterDays = account.DeleteAfterDays,
                IsImportOnly = account.IsImportOnly
            };

            ViewBag.EmailCount = emailCount;
            return View(model);
        }

        // GET: MailAccounts/Create
        public IActionResult Create()
        {
            var model = new CreateMailAccountViewModel
            {
                ImapPort = 993, // Standard values
                UseSSL = true
            };
            return View(model);
        }

        // POST: MailAccounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMailAccountViewModel model)
        {
            if (ModelState.IsValid)
            {
var account = new MailAccount
                {
                    Name = model.Name,
                    EmailAddress = model.EmailAddress,
                    ImapServer = model.IsImportOnly ? null : model.ImapServer,
                    ImapPort = model.IsImportOnly ? null : model.ImapPort,
                    Username = model.IsImportOnly ? null : model.Username,
                    Password = model.IsImportOnly ? null : model.Password,
                    UseSSL = model.UseSSL,
                    IsEnabled = model.IsEnabled,
                    IsImportOnly = model.IsImportOnly,
                    ExcludedFolders = string.Empty,
                    DeleteAfterDays = model.DeleteAfterDays,
                    LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                try
                {
                    _logger.LogInformation("Creating new account: {Name}, IsImportOnly: {IsImportOnly}",
                        model.Name, model.IsImportOnly);

                // Test connection before saving (only for non-import-only accounts)
                if (!account.IsImportOnly)
                {
                    _logger.LogInformation("Testing connection for account: {Name}, Server: {Server}:{Port}",
                        model.Name, model.ImapServer, model.ImapPort);
                    var connectionResult = await _emailService.TestConnectionAsync(account);
                    if (!connectionResult)
                    {
                        _logger.LogWarning("Connection test failed for account {Name}", model.Name);
                        ModelState.AddModelError("", _localizer["EmailAccountError"]);
                        return View(model);
                    }
                }

                    _logger.LogInformation("Saving account to database");
                    _context.MailAccounts.Add(account);
                    await _context.SaveChangesAsync();

                    // Auto-assign the account to the current user if they are a SelfManager (not Admin)
                    var currentUsername = HttpContext.User.Identity?.Name;
                    var currentUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Username == currentUsername);
                    
                    if (currentUser != null && !currentUser.IsAdmin && currentUser.IsSelfManager)
                    {
                        var userMailAccount = new UserMailAccount
                        {
                            UserId = currentUser.Id,
                            MailAccountId = account.Id
                        };
                        _context.UserMailAccounts.Add(userMailAccount);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Auto-assigned account {AccountName} to SelfManager user {Username}", 
                            account.Name, currentUser.Username);
                    }

                    TempData["SuccessMessage"] = _localizer["EmailAccountCreateSuccess"].Value;
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating email account: {Message}", ex.Message);
                    ModelState.AddModelError("", $"{_localizer["ErrorOccurred"]}: {ex.Message}");
                    return View(model);
                }
            }

            // Wenn ModelState ungültig ist, zurück zur Ansicht mit Fehlern
            return View(model);
        }

        // GET: MailAccounts/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

var model = new MailAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                EmailAddress = account.EmailAddress,
                ImapServer = account.ImapServer,
                ImapPort = account.ImapPort,
                Username = account.Username,
                UseSSL = account.UseSSL,
                IsEnabled = account.IsEnabled,
                LastSync = account.LastSync,
                ExcludedFolders = account.ExcludedFolders,
                DeleteAfterDays = account.DeleteAfterDays,
                IsImportOnly = account.IsImportOnly
            };

            // Set ViewBag properties
            ViewBag.IsImportOnly = account.IsImportOnly;
            
            // Load available folders for exclusion selection (only for non-import-only accounts)
            if (!account.IsImportOnly)
            {
                try
                {
                    var folders = await _emailService.GetMailFoldersAsync(id);
                    ViewBag.AvailableFolders = folders;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load folders for account {AccountId}", id);
                    ViewBag.AvailableFolders = new List<string>();
                }
            }

            return View(model);
        }

        // POST: MailAccounts/ToggleEnabled/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEnabled(int id)
        {
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // Toggle the enabled status
            account.IsEnabled = !account.IsEnabled;
            await _context.SaveChangesAsync();

            // Correct message based on the NEW status (after toggling)
            TempData["SuccessMessage"] = account.IsEnabled
                ? _localizer["EmailAccountEnabled", account.Name].Value
                : _localizer["EmailAccountDisabled", account.Name].Value;

            return RedirectToAction(nameof(Index));
        }

        // POST: MailAccounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MailAccountViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            // Remove password validation if left blank
            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.Remove("Password");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var account = await _context.MailAccounts.FindAsync(id);
                    if (account == null)
                    {
                        return NotFound();
                    }

                    account.Name = model.Name;
                    account.EmailAddress = model.EmailAddress;
                    account.ImapServer = model.ImapServer;
                    account.ImapPort = model.ImapPort;
                    account.Username = model.Username;
                    account.IsEnabled = model.IsEnabled;

                    // Only update password if provided
                    if (!string.IsNullOrEmpty(model.Password))
                    {
                        account.Password = model.Password;
                    }

                    account.UseSSL = model.UseSSL;
                    account.ExcludedFolders = model.ExcludedFolders ?? string.Empty;
                    account.DeleteAfterDays = model.DeleteAfterDays;

                    // Test connection before saving (only for non-import-only accounts)
                    if (!string.IsNullOrEmpty(model.Password) && !account.IsImportOnly)
                    {
                        var connectionResult = await _emailService.TestConnectionAsync(account);
                        if (!connectionResult)
                        {
                            ModelState.AddModelError("", _localizer["EmailAccountError"]);
                            return View(model);
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = _localizer["EmailAccountUpdateSuccess"].Value;
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.MailAccounts.AnyAsync(e => e.Id == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(model);
        }

        // GET: MailAccounts/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // E-Mail-Anzahl abrufen (das war der fehlende Teil!)
            var emailCount = await _emailService.GetEmailCountByAccountAsync(id);

            var model = new MailAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                EmailAddress = account.EmailAddress
            };

            // ViewBag für die E-Mail-Anzahl setzen
            ViewBag.EmailCount = emailCount;

            return View(model);
        }

        // POST: MailAccounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            // Determine number of emails to delete
            var emailCount = await _context.ArchivedEmails.CountAsync(e => e.MailAccountId == id);

            var account = await _context.MailAccounts.FindAsync(id);
            if (account != null)
            {
                // First delete attachments
                var emailIds = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == id)
                    .Select(e => e.Id)
                    .ToListAsync();

                var attachments = await _context.EmailAttachments
                    .Where(a => emailIds.Contains(a.ArchivedEmailId))
                    .ToListAsync();

                _context.EmailAttachments.RemoveRange(attachments);

                // Then delete emails
                var emails = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == id)
                    .ToListAsync();

                _context.ArchivedEmails.RemoveRange(emails);

                // Finally delete the account
                _context.MailAccounts.Remove(account);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = _localizer["EmailAccountDeleteSuccess", emailCount].Value;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: MailAccounts/Sync/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // Prevent sync for import-only accounts
            if (account.IsImportOnly)
            {
                TempData["ErrorMessage"] = _localizer["ImportOnlyAccountNoSync"].Value;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Sync-Job starten
                var jobId = _syncJobService.StartSync(account.Id, account.Name, account.LastSync);

                // Sync ausführen
                await _emailService.SyncMailAccountAsync(account, jobId);

                TempData["SuccessMessage"] = _localizer["SyncSuccess", account.Name].Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing account {AccountName}: {Message}", account.Name, ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["SyncFailed"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: MailAccounts/Resync/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resync(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // Prevent resync for import-only accounts
            if (account.IsImportOnly)
            {
                TempData["ErrorMessage"] = _localizer["ImportOnlyAccountNoSync"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var success = await _emailService.ResyncAccountAsync(id);
                if (success)
                {
                    TempData["SuccessMessage"] = _localizer["FullSyncStarted", account.Name].Value;
                }
                else
                {
                    TempData["ErrorMessage"] = _localizer["FullSyncFailed", account.Name].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting resync for account {AccountName}: {Message}", account.Name, ex.Message);
                TempData["ErrorMessage"] = $"{_localizer["FullSyncError"]}: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: MailAccounts/MoveAllEmails/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveAllEmails(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null) return NotFound();

            var emailIds = await _context.ArchivedEmails
                .Where(e => e.MailAccountId == id)
                .Select(e => e.Id)
                .ToListAsync();

            if (!emailIds.Any())
            {
                TempData["ErrorMessage"] = _localizer["MoveEmailsNotFound"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            _logger.LogInformation("Account {AccountId} has {Count} emails. Thresholds: Async={AsyncThreshold}, MaxAsync={MaxAsync}",
                id, emailIds.Count, _batchOptions.AsyncThreshold, _batchOptions.MaxAsyncEmails);

            // Prüfe absolute Limits
            if (emailIds.Count > _batchOptions.MaxAsyncEmails)
            {
                TempData["ErrorMessage"] = _localizer["TooManyEmailsInAccount", emailIds.Count, _batchOptions.MaxAsyncEmails].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            // Entscheide basierend auf Schwellenwert
            if (emailIds.Count > _batchOptions.AsyncThreshold)
            {
                // Für große Mengen: Direkt zum asynchronen Batch-Restore
                _logger.LogInformation("Using background job for {Count} emails from account {AccountId}", emailIds.Count, id);
                return RedirectToAction("StartAsyncBatchRestoreFromAccount", "Emails", new
                {
                    accountId = id,
                    returnUrl = Url.Action("Details", new { id })
                });
            }
            else
            {
                // Für kleinere Mengen: Session-basierte Verarbeitung
                _logger.LogInformation("Using direct processing for {Count} emails from account {AccountId}", emailIds.Count, id);
                try
                {
                    HttpContext.Session.SetString("BatchRestoreIds", string.Join(",", emailIds));
                    HttpContext.Session.SetString("BatchRestoreReturnUrl", Url.Action("Details", new { id }));
                    return RedirectToAction("BatchRestore", "Emails");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store {Count} email IDs in session for account {AccountId}", emailIds.Count, id);
                    // Fallback zu Background Job
                    _logger.LogWarning("Session storage failed, redirecting to background job");
                    return RedirectToAction("StartAsyncBatchRestoreFromAccount", "Emails", new
                    {
                        accountId = id,
                        returnUrl = Url.Action("Details", new { id })
                    });
                }
            }
        }

        // GET: MailAccounts/ImportMBox
        public async Task<IActionResult> ImportMBox()
        {
            // Use the authentication service to get user info properly
            var authService = HttpContext.RequestServices.GetService<IAuthenticationService>();
            var currentUsername = authService.GetCurrentUser(HttpContext);
            var isAdmin = authService.IsCurrentUserAdmin(HttpContext);
            var isSelfManager = authService.IsCurrentUserSelfManager(HttpContext);

            IQueryable<MailAccount> mailAccountsQuery;

            // Check if user is admin (including legacy admin)
            if (isAdmin)
            {
                _logger.LogInformation("User is admin, showing all accounts");
                mailAccountsQuery = _context.MailAccounts;
            }
            else if (isSelfManager)
            {
                _logger.LogInformation("User is SelfManager, showing only assigned accounts");
                mailAccountsQuery = _context.MailAccounts
                    .Where(ma => ma.UserMailAccounts.Any(uma => uma.User.Username == currentUsername));
            }
            else
            {
                _logger.LogInformation("User has no special permissions, showing no accounts");
                mailAccountsQuery = _context.MailAccounts.Where(ma => false); // Empty query
            }

            var accounts = await mailAccountsQuery
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var model = new MBoxImportViewModel
            {
                AvailableAccounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} ({a.EmailAddress})"
                }).ToList(),
                MaxFileSize = _uploadOptions.MaxFileSizeBytes
            };

            return View(model);
        }

        // POST: MailAccounts/ImportMBox
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportMBox(MBoxImportViewModel model)
        {
            // Reload accounts for validation failure
            var accounts = await _context.MailAccounts
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.Name)
                .ToListAsync();

            model.AvailableAccounts = accounts.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.Name} ({a.EmailAddress})",
                Selected = a.Id == model.TargetAccountId
            }).ToList();

            // Ensure MaxFileSize is set for validation failures
            model.MaxFileSize = _uploadOptions.MaxFileSizeBytes;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate file
            if (model.MBoxFile == null || model.MBoxFile.Length == 0)
            {
                ModelState.AddModelError("MBoxFile", _localizer["SelectValidMBoxFile"]);
                return View(model);
            }

            if (model.MBoxFile.Length > model.MaxFileSize)
            {
                ModelState.AddModelError("MBoxFile", _localizer["MBoxFileTooLarge", model.MaxFileSizeFormatted].Value);
                return View(model);
            }

            // Validate target account
            var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
            if (targetAccount == null)
            {
                ModelState.AddModelError("TargetAccountId", _localizer["SelectedAccountNotFound"]);
                return View(model);
            }

            try
            {
                // Save uploaded file
                var filePath = await _mboxImportService.SaveUploadedFileAsync(model.MBoxFile);

                // Create import job
                var job = new MBoxImportJob
                {
                    FileName = model.MBoxFile.FileName,
                    FilePath = filePath,
                    FileSize = model.MBoxFile.Length,
                    TargetAccountId = model.TargetAccountId,
                    TargetFolder = model.TargetFolder,
                    UserId = HttpContext.User.Identity?.Name ?? "Anonymous"
                };

                // Estimate email count
                job.TotalEmails = await _mboxImportService.EstimateEmailCountAsync(filePath);

                // Queue the job
                var jobId = _mboxImportService.QueueImport(job);

                TempData["SuccessMessage"] = _localizer["MBoxImportStarted", model.MBoxFile.FileName, job.TotalEmails].Value;
                return RedirectToAction("MBoxImportStatus", new { jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MBox import for file {FileName}", model.MBoxFile.FileName);
                ModelState.AddModelError("", $"{_localizer["MBoxImportError"]}: {ex.Message}");
                return View(model);
            }
        }

        // GET: MailAccounts/MBoxImportStatus
        [HttpGet]
        public async Task<IActionResult> MBoxImportStatus(string jobId)
        {
            // Validate jobId parameter
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidMBoxID"].Value;
                return RedirectToAction(nameof(Index));
            }

            var job = _mboxImportService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = _localizer["MBoxImportJobNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Check if user has access to the target account
            if (!await HasAccessToAccountAsync(job.TargetAccountId))
            {
                return NotFound();
            }

            return View(job);
        }

        // POST: MailAccounts/CancelMBoxImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMBoxImport(string jobId, string returnUrl = null)
        {
            // Validate jobId parameter
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidMBoxID"].Value;
                // Wenn returnUrl angegeben ist, leite dorthin weiter, sonst zur Index-Seite
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            var job = _mboxImportService.GetJob(jobId);
            if (job != null)
            {
                // Check if user has access to the target account
                if (!await HasAccessToAccountAsync(job.TargetAccountId))
                {
                    return NotFound();
                }
            }

            var success = _mboxImportService.CancelJob(jobId);
            if (success)
            {
                TempData["SuccessMessage"] = _localizer["MBoxImportCancelled"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["MBoxImportCancelError"].Value;
            }

            // Wenn returnUrl angegeben ist, leite dorthin weiter, sonst zur Index-Seite
            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
        }

        // POST: MailAccounts/ResetSyncTime/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetSyncTime(int id)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // Set LastSync to current time
            account.LastSync = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["SyncTimeResetSuccess"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        // AJAX endpoint for folder loading
        [HttpGet]
        public async Task<JsonResult> GetFolders(int accountId)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(accountId))
            {
                return Json(new List<string> { "INBOX" });
            }

            try
            {
                var folders = await _emailService.GetMailFoldersAsync(accountId);
                if (!folders.Any())
                {
                    return Json(new List<string> { "INBOX" });
                }
                return Json(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading folders for account {AccountId}", accountId);
                return Json(new List<string> { "INBOX" });
            }
        }
    }
}
