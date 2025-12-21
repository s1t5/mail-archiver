using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.ViewModels;
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
    private readonly IGraphEmailService _graphEmailService;
    private readonly ILogger<MailAccountsController> _logger;
    private readonly BatchRestoreOptions _batchOptions;
    private readonly ISyncJobService _syncJobService;
    private readonly IMBoxImportService _mboxImportService;
    private readonly IEmlImportService _emlImportService;
    private readonly UploadOptions _uploadOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IExportService _exportService;
    private readonly IAccessLogService _accessLogService;
    private readonly IMailAccountDeletionService _mailAccountDeletionService;

    public MailAccountsController(
        MailArchiverDbContext context,
        IEmailService emailService,
        IGraphEmailService graphEmailService,
        ILogger<MailAccountsController> logger,
        IOptions<BatchRestoreOptions> batchOptions,
        ISyncJobService syncJobService,
        IMBoxImportService mboxImportService,
        IEmlImportService emlImportService,
        IOptions<UploadOptions> uploadOptions, 
        IStringLocalizer<SharedResource> localizer,
        IServiceScopeFactory serviceScopeFactory,
        IExportService exportService,
        IAccessLogService accessLogService,
        IMailAccountDeletionService mailAccountDeletionService)
    {
        _context = context;
        _emailService = emailService;
        _graphEmailService = graphEmailService;
        _logger = logger;
        _batchOptions = batchOptions.Value;
        _syncJobService = syncJobService;
        _mboxImportService = mboxImportService;
        _emlImportService = emlImportService;
        _uploadOptions = uploadOptions.Value;
        _localizer = localizer;
        _serviceScopeFactory = serviceScopeFactory;
        _exportService = exportService;
        _accessLogService = accessLogService;
        _mailAccountDeletionService = mailAccountDeletionService;
    }

        private async Task<bool> HasAccessToAccountAsync(int accountId)
        {
            // Use the authentication service to get user info properly
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
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
                    .AnyAsync(ma => ma.Id == accountId && ma.UserMailAccounts.Any(uma => uma.User.Username.ToLower() == currentUsername.ToLower()));
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
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
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
                    .Where(ma => ma.UserMailAccounts.Any(uma => uma.User.Username.ToLower() == currentUsername.ToLower()));
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
                    Provider = a.Provider
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
                Provider = account.Provider,
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
                UseSSL = true,
                Provider = ProviderType.IMAP
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
                    ImapServer = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.ImapServer,
                    ImapPort = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.ImapPort,
                    Username = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.Username,
                    Password = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.Password,
                    UseSSL = model.UseSSL,
                    IsEnabled = model.IsEnabled,
                    Provider = model.Provider,
                    ClientId = model.Provider == ProviderType.M365 ? model.ClientId : null,
                    ClientSecret = model.Provider == ProviderType.M365 ? model.ClientSecret : null,
                    TenantId = model.Provider == ProviderType.M365 ? model.TenantId : null,
                    ExcludedFolders = string.Empty,
                    DeleteAfterDays = model.DeleteAfterDays,
                    LocalRetentionDays = model.LocalRetentionDays,
                    LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                // Validate local retention policy
                if (account.LocalRetentionDays.HasValue && !account.DeleteAfterDays.HasValue)
                {
                    ModelState.AddModelError("LocalRetentionDays", 
                        _localizer["LocalRetentionRequiresServerRetention"].Value);
                    return View(model);
                }
                
                if (account.LocalRetentionDays.HasValue && account.DeleteAfterDays.HasValue &&
                    account.LocalRetentionDays.Value < account.DeleteAfterDays.Value)
                {
                    ModelState.AddModelError("LocalRetentionDays", 
                        _localizer["LocalRetentionMustBeGreaterOrEqual"].Value);
                    return View(model);
                }

                try
                {
                    _logger.LogInformation("Creating new account: {Name}, Provider: {Provider}",
                        model.Name, model.Provider);

                    // Test connection before saving (only for non-import-only accounts)
                    if (account.Provider == ProviderType.IMAP)
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
                    var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                    var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                    var currentUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == currentUsername.ToLower());
                    
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

                    // Log the account creation action
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                            searchParameters: $"Created mail account: {account.Name}",
                            mailAccountId: account.Id);
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
                LocalRetentionDays = account.LocalRetentionDays,
                Provider = account.Provider,
                ClientId = account.ClientId,
                ClientSecret = account.ClientSecret,
                TenantId = account.TenantId
            };

            // Set ViewBag properties
            ViewBag.Provider = account.Provider;
            
            // Note: Folders are now loaded on-demand via AJAX to improve page load performance
            // The GetFolders endpoint handles folder loading when the user clicks the "Load Folders" button

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

            // Store the current status before toggling for logging
            bool wasEnabled = account.IsEnabled;

            // Toggle the enabled status
            account.IsEnabled = !account.IsEnabled;
            await _context.SaveChangesAsync();

            // Log the account enable/disable action
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
            if (!string.IsNullOrEmpty(currentUsername))
            {
                await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                    searchParameters: $"{(account.IsEnabled ? "Enabled" : "Disabled")} mail account: {account.Name}",
                    mailAccountId: account.Id);
            }

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
                    account.ImapServer = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.ImapServer;
                    account.ImapPort = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.ImapPort;
                    account.Username = model.Provider == ProviderType.IMPORT || model.Provider != ProviderType.IMAP ? null : model.Username;
                    account.IsEnabled = model.IsEnabled;
                    account.Provider = model.Provider;
                    account.ClientId = model.Provider == ProviderType.M365 ? model.ClientId : null;
                    
                    // Only update ClientSecret if provided for M365 accounts
                    if (model.Provider == ProviderType.M365 && !string.IsNullOrEmpty(model.ClientSecret))
                    {
                        account.ClientSecret = model.ClientSecret;
                    }
                    else if (model.Provider == ProviderType.M365)
                    {
                        // If no new ClientSecret provided for M365, keep the existing one
                        // Do not overwrite with null
                    }
                    else
                    {
                        // For non-M365 accounts, set to null
                        account.ClientSecret = null;
                    }
                    
                    account.TenantId = model.Provider == ProviderType.M365 ? model.TenantId : null;

                    // Only update password if provided
                    if (!string.IsNullOrEmpty(model.Password))
                    {
                        account.Password = model.Password;
                    }

                    account.UseSSL = model.UseSSL;
                    account.ExcludedFolders = model.ExcludedFolders ?? string.Empty;
                    account.DeleteAfterDays = model.DeleteAfterDays;
                    account.LocalRetentionDays = model.LocalRetentionDays;

                    // Validate local retention policy
                    if (account.LocalRetentionDays.HasValue && !account.DeleteAfterDays.HasValue)
                    {
                        ModelState.AddModelError("LocalRetentionDays", 
                            _localizer["LocalRetentionRequiresServerRetention"].Value);
                        return View(model);
                    }
                    
                    if (account.LocalRetentionDays.HasValue && account.DeleteAfterDays.HasValue &&
                        account.LocalRetentionDays.Value < account.DeleteAfterDays.Value)
                    {
                        ModelState.AddModelError("LocalRetentionDays", 
                            _localizer["LocalRetentionMustBeGreaterOrEqual"].Value);
                        return View(model);
                    }

                    // Test connection before saving (only for IMAP accounts)
                    if (!string.IsNullOrEmpty(model.Password) && account.Provider == ProviderType.IMAP)
                    {
                        var connectionResult = await _emailService.TestConnectionAsync(account);
                        if (!connectionResult)
                        {
                            ModelState.AddModelError("", _localizer["EmailAccountError"]);
                            return View(model);
                        }
                    }

                    await _context.SaveChangesAsync();
                    
                    // Log the account update action
                    var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                    var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                            searchParameters: $"Updated mail account: {account.Name}",
                            mailAccountId: account.Id);
                    }
                    
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

            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            // Determine number of emails to delete
            var emailCount = await _context.ArchivedEmails.CountAsync(e => e.MailAccountId == id);

            _logger.LogInformation("Account {AccountId} has {Count} emails. Deletion threshold: {Threshold}",
                id, emailCount, _batchOptions.AsyncThreshold);

            // Get current user info for logging
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);

            // Check if async deletion is needed (for large accounts)
            if (emailCount > _batchOptions.AsyncThreshold)
            {
                _logger.LogInformation("Using async deletion for {Count} emails from account {AccountId}", emailCount, id);

                // Queue async deletion
                var jobId = _mailAccountDeletionService.QueueDeletion(id, account.Name, currentUsername ?? "System");

                // Log the deletion request
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                        searchParameters: $"Queued async deletion for mail account: {account.Name} with {emailCount} emails",
                        mailAccountId: account.Id);
                }

                TempData["SuccessMessage"] = _localizer["AccountDeletionQueued", account.Name].Value;
                return RedirectToAction("DeletionStatus", new { jobId });
            }

            // For smaller accounts, delete synchronously (original logic)
            _logger.LogInformation("Using sync deletion for {Count} emails from account {AccountId}", emailCount, id);

            // Cancel any running sync jobs for this account before deletion
            _syncJobService.CancelJobsForAccount(id);
            _logger.LogInformation("Cancelled any running sync jobs for account {AccountId} ({AccountName}) before deletion", id, account.Name);

            // Unlock all emails for this account (required for compliance mode)
            var lockedEmails = await _context.ArchivedEmails
                .Where(e => e.MailAccountId == id && e.IsLocked)
                .ToListAsync();

            if (lockedEmails.Any())
            {
                _logger.LogInformation("Unlocking {Count} locked emails for account {AccountId} ({AccountName}) before deletion", 
                    lockedEmails.Count, id, account.Name);
                
                foreach (var email in lockedEmails)
                {
                    email.IsLocked = false;
                }
                await _context.SaveChangesAsync();
            }

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

            // Log the account deletion action
            if (!string.IsNullOrEmpty(currentUsername))
            {
                await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                    searchParameters: $"Deleted mail account: {account.Name} with {emailCount} emails",
                    mailAccountId: account.Id);
            }

            TempData["SuccessMessage"] = _localizer["EmailAccountDeleteSuccess", emailCount].Value;

            return RedirectToAction(nameof(Index));
        }

        // GET: MailAccounts/DeletionStatus
        [HttpGet]
        public async Task<IActionResult> DeletionStatus(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidDeletionJobID"].Value;
                return RedirectToAction(nameof(Index));
            }

            var job = _mailAccountDeletionService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = _localizer["DeletionJobNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Check if user had access to the account (if account still exists)
            if (!job.IsCompleted)
            {
                if (!await HasAccessToAccountAsync(job.MailAccountId))
                {
                    return NotFound();
                }
            }

            return View(job);
        }

        // POST: MailAccounts/CancelDeletion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelDeletion(string jobId, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidDeletionJobID"].Value;
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            var success = _mailAccountDeletionService.CancelJob(jobId);
            if (success)
            {
                TempData["SuccessMessage"] = _localizer["DeletionCancelled"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["DeletionCancelError"].Value;
            }

            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
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
            if (account.Provider == ProviderType.IMPORT)
            {
                TempData["ErrorMessage"] = _localizer["ImportOnlyAccountNoSync"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                // Use the sync job service to start a sync with validation
                var jobId = await _syncJobService.StartSyncAsync(id, account.Name);
                if (!string.IsNullOrEmpty(jobId))
                {
                    // Actually perform the sync based on provider type
                    if (account.Provider == ProviderType.M365)
                    {
                        // For M365 accounts, use GraphEmailService
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Create a new service scope for the background task to avoid disposed context issues
                                using var scope = _serviceScopeFactory.CreateScope();
                                var graphEmailService = scope.ServiceProvider.GetRequiredService<IGraphEmailService>();
                                var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                
                                // Get a fresh copy of the account from the new context
                                var freshAccount = await dbContext.MailAccounts.FindAsync(account.Id);
                                if (freshAccount != null)
                                {
                                    await graphEmailService.SyncMailAccountAsync(freshAccount, jobId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during M365 sync for account {AccountName}: {Message}", account.Name, ex.Message);
                                _syncJobService.CompleteJob(jobId, false, ex.Message);
                            }
                        });
                    }
                    else
                    {
                        // For IMAP accounts, use EmailService
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Create a new service scope for the background task to avoid disposed context issues
                                using var scope = _serviceScopeFactory.CreateScope();
                                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                                var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                
                                // Get a fresh copy of the account from the new context
                                var freshAccount = await dbContext.MailAccounts.FindAsync(account.Id);
                                if (freshAccount != null)
                                {
                                    await emailService.SyncMailAccountAsync(freshAccount, jobId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during IMAP sync for account {AccountName}: {Message}", account.Name, ex.Message);
                                _syncJobService.CompleteJob(jobId, false, ex.Message);
                            }
                        });
                    }
                    
                    // Log the sync action
                    var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                    var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                            searchParameters: $"Started sync for mail account: {account.Name}",
                            mailAccountId: account.Id);
                    }
                    
                    TempData["SuccessMessage"] = _localizer["SyncStarted", account.Name].Value;
                }
                else
                {
                    TempData["ErrorMessage"] = _localizer["SyncFailed", account.Name].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting sync job for account {AccountName}: {Message}", account.Name, ex.Message);
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
            if (account.Provider == ProviderType.IMPORT)
            {
                TempData["ErrorMessage"] = _localizer["ImportOnlyAccountNoSync"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var success = await _emailService.ResyncAccountAsync(id);
                if (success)
                {
                    // Log the resync action
                    var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                    var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                            searchParameters: $"Started resync for mail account: {account.Name}",
                            mailAccountId: account.Id);
                    }
                    
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
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
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
                    .Where(ma => ma.UserMailAccounts.Any(uma => uma.User.Username.ToLower() == currentUsername.ToLower()));
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
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
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

                // Log the MBox import action
                var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                        searchParameters: $"Started MBox import for mail account: {targetAccount.Name}",
                        mailAccountId: targetAccount.Id);
                }

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

        // GET: MailAccounts/EditSyncTime/5
        public async Task<IActionResult> EditSyncTime(int id)
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

            var model = new EditSyncTimeViewModel
            {
                Id = account.Id,
                AccountName = account.Name,
                CurrentSyncTime = account.LastSync,
                NewSyncTime = account.LastSync
            };

            return View(model);
        }

        // POST: MailAccounts/EditSyncTime/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSyncTime(int id, EditSyncTimeViewModel model)
        {
            // Use proper authentication service
            if (!await HasAccessToAccountAsync(id))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                // Reload account name for display
                var account = await _context.MailAccounts.FindAsync(id);
                if (account != null)
                {
                    model.AccountName = account.Name;
                }
                return View(model);
            }

            var accountToUpdate = await _context.MailAccounts.FindAsync(id);
            if (accountToUpdate == null)
            {
                return NotFound();
            }

            // Set LastSync to the specified time (treat as local time and convert to UTC)
            accountToUpdate.LastSync = model.NewSyncTime.ToUniversalTime();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["SyncTimeUpdatedSuccess"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: MailAccounts/ImportEml
        public async Task<IActionResult> ImportEml()
        {
            // Use the authentication service to get user info properly
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
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
                    .Where(ma => ma.UserMailAccounts.Any(uma => uma.User.Username.ToLower() == currentUsername.ToLower()));
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

            var model = new EmlImportViewModel
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

        // POST: MailAccounts/ImportEml
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> ImportEml(EmlImportViewModel model)
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
            if (model.EmlFile == null || model.EmlFile.Length == 0)
            {
                ModelState.AddModelError("EmlFile", _localizer["SelectValidEmlFile"]);
                return View(model);
            }

            if (model.EmlFile.Length > model.MaxFileSize)
            {
                ModelState.AddModelError("EmlFile", _localizer["EmlFileTooLarge", model.MaxFileSizeFormatted].Value);
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
                var filePath = await _emlImportService.SaveUploadedFileAsync(model.EmlFile);

                // Create import job
                var job = new EmlImportJob
                {
                    FileName = model.EmlFile.FileName,
                    FilePath = filePath,
                    FileSize = model.EmlFile.Length,
                    TargetAccountId = model.TargetAccountId,
                    UserId = HttpContext.User.Identity?.Name ?? "Anonymous"
                };

                // Estimate email count
                job.TotalEmails = await _emlImportService.EstimateEmailCountAsync(filePath);

                // Queue the job
                var jobId = _emlImportService.QueueImport(job);

                // Log the EML import action
                var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Account, 
                        searchParameters: $"Started EML import for mail account: {targetAccount.Name}",
                        mailAccountId: targetAccount.Id);
                }

                TempData["SuccessMessage"] = _localizer["EmlImportStarted", model.EmlFile.FileName, job.TotalEmails].Value;
                return RedirectToAction("EmlImportStatus", new { jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting EML import for file {FileName}", model.EmlFile.FileName);
                ModelState.AddModelError("", $"{_localizer["EmlImportError"]}: {ex.Message}");
                return View(model);
            }
        }

        // GET: MailAccounts/EmlImportStatus
        [HttpGet]
        public async Task<IActionResult> EmlImportStatus(string jobId)
        {
            // Validate jobId parameter
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidEmlID"].Value;
                return RedirectToAction(nameof(Index));
            }

            var job = _emlImportService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = _localizer["EmlImportJobNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Check if user has access to the target account
            if (!await HasAccessToAccountAsync(job.TargetAccountId))
            {
                return NotFound();
            }

            return View(job);
        }

        // POST: MailAccounts/CancelEmlImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelEmlImport(string jobId, string returnUrl = null)
        {
            // Validate jobId parameter
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidEmlID"].Value;
                // Wenn returnUrl angegeben ist, leite dorthin weiter, sonst zur Index-Seite
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            var job = _emlImportService.GetJob(jobId);
            if (job != null)
            {
                // Check if user has access to the target account
                if (!await HasAccessToAccountAsync(job.TargetAccountId))
                {
                    return NotFound();
                }
            }

            var success = _emlImportService.CancelJob(jobId);
            if (success)
            {
                TempData["SuccessMessage"] = _localizer["EmlImportCancelled"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["EmlImportCancelError"].Value;
            }

            // Wenn returnUrl angegeben ist, leite dorthin weiter, sonst zur Index-Seite
            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
        }

        // GET: MailAccounts/Export/5
        [SelfManagerRequired]
        public async Task<IActionResult> Export(int id)
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

            // Get email counts
            var emailCount = await _emailService.GetEmailCountByAccountAsync(id);
            var incomingCount = await _context.ArchivedEmails
                .CountAsync(e => e.MailAccountId == id && !e.IsOutgoing);
            var outgoingCount = await _context.ArchivedEmails
                .CountAsync(e => e.MailAccountId == id && e.IsOutgoing);

            if (emailCount == 0)
            {
                TempData["ErrorMessage"] = _localizer["NoEmailsToExport"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new AccountExportViewModel
            {
                MailAccountId = id,
                MailAccountName = account.Name,
                TotalEmailsCount = emailCount,
                IncomingEmailsCount = incomingCount,
                OutgoingEmailsCount = outgoingCount
            };

            return View(model);
        }

        // POST: MailAccounts/Export
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfManagerRequired]
        public async Task<IActionResult> Export(AccountExportViewModel model)
        {
            if (!await HasAccessToAccountAsync(model.MailAccountId))
            {
                return NotFound();
            }

            var account = await _context.MailAccounts.FindAsync(model.MailAccountId);
            if (account == null)
            {
                return NotFound();
            }

            // Validate email count
            var emailCount = await _emailService.GetEmailCountByAccountAsync(model.MailAccountId);
            if (emailCount == 0)
            {
                TempData["ErrorMessage"] = _localizer["NoEmailsToExport"].Value;
                return RedirectToAction(nameof(Details), new { id = model.MailAccountId });
            }

            try
            {
                // Get current user info
                var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                var currentUsername = authService.GetCurrentUserDisplayName(HttpContext);

                    // Log the export action
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Download, 
                            searchParameters: $"Started export for mail account: {account.Name} in {model.Format} format",
                            mailAccountId: model.MailAccountId);
                    }

                // Queue the job
                var jobId = _exportService.QueueExport(model.MailAccountId, model.Format, currentUsername ?? "Anonymous");

                TempData["SuccessMessage"] = _localizer["ExportStarted", account.Name, model.Format].Value;
                return RedirectToAction("ExportStatus", new { jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting export for account {AccountName}", account.Name);
                TempData["ErrorMessage"] = $"{_localizer["ExportError"]}: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = model.MailAccountId });
            }
        }

        // GET: MailAccounts/ExportStatus
        [HttpGet]
        [SelfManagerRequired]
        public async Task<IActionResult> ExportStatus(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidExportID"].Value;
                return RedirectToAction(nameof(Index));
            }

            var job = _exportService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = _localizer["ExportJobNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Check if user has access to the account
            if (!await HasAccessToAccountAsync(job.MailAccountId))
            {
                return NotFound();
            }

            return View(job);
        }

        // GET: MailAccounts/DownloadExport
        [HttpGet]
        [SelfManagerRequired]
        public async Task<IActionResult> DownloadExport(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidExportID"].Value;
                return RedirectToAction(nameof(Index));
            }

            var job = _exportService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = _localizer["ExportJobNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Check if user has access to the account
            if (!await HasAccessToAccountAsync(job.MailAccountId))
            {
                return NotFound();
            }

            if (job.Status != AccountExportJobStatus.Completed)
            {
                TempData["ErrorMessage"] = _localizer["ExportFileNotFound"].Value;
                return RedirectToAction("ExportStatus", new { jobId });
            }

            try
            {
                var fileResult = _exportService.GetExportForDownload(jobId);
                if (fileResult == null || string.IsNullOrEmpty(fileResult.FilePath) || !System.IO.File.Exists(fileResult.FilePath))
                {
                    TempData["ErrorMessage"] = _localizer["ExportFileNotFound"].Value;
                    return RedirectToAction("ExportStatus", new { jobId });
                }
                
                // Stream the file directly without loading it into memory
                var fileStream = new FileStream(fileResult.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                
                // Mark as downloaded - the file will be deleted after download completes
                _exportService.MarkAsDownloaded(jobId);

                return File(fileStream, fileResult.ContentType, fileResult.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading export {JobId}", jobId);
                TempData["ErrorMessage"] = _localizer["ExportDownloadError"].Value;
                return RedirectToAction("ExportStatus", new { jobId });
            }
        }

        // POST: MailAccounts/CancelExport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfManagerRequired]
        public async Task<IActionResult> CancelExport(string jobId, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = _localizer["InvalidExportID"].Value;
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            var job = _exportService.GetJob(jobId);
            if (job != null)
            {
                // Check if user has access to the account
                if (!await HasAccessToAccountAsync(job.MailAccountId))
                {
                    return NotFound();
                }
            }

            var success = _exportService.CancelJob(jobId);
            if (success)
            {
                TempData["SuccessMessage"] = _localizer["ExportCancelled"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer["ExportCancelError"].Value;
            }

            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
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
                var account = await _context.MailAccounts.FindAsync(accountId);
                if (account?.Provider == ProviderType.M365)
                {
                    // Für M365-Konten den GraphEmailService verwenden
                    var folders = await _graphEmailService.GetMailFoldersAsync(account);
                    if (!folders.Any())
                    {
                        return Json(new List<string> { "INBOX" });
                    }
                    return Json(folders);
                }
                else
                {
                    // Für IMAP-Konten den bestehenden EmailService verwenden
                    var folders = await _emailService.GetMailFoldersAsync(accountId);
                    if (!folders.Any())
                    {
                        return Json(new List<string> { "INBOX" });
                    }
                    return Json(folders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading folders for account {AccountId}", accountId);
                return Json(new List<string> { "INBOX" });
            }
        }
    }
}
