using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using MailArchiver.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Controllers
{
    [UserAccessRequired]
    public class EmailsController : Controller
    {
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IGraphEmailService _graphEmailService;
        private readonly ILogger<EmailsController> _logger;
        private readonly IBatchRestoreService? _batchRestoreService;
        private readonly BatchRestoreOptions _batchOptions;
        private readonly ISyncJobService _syncJobService;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IExportService? _exportService;
        private readonly ISelectedEmailsExportService? _selectedEmailsExportService;
        private readonly SelectionOptions _selectionOptions;
        private readonly IAccessLogService _accessLogService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly MailArchiver.Services.IAuthenticationService _authService;

        public EmailsController(
            MailArchiverDbContext context,
            IEmailService emailService,
            IGraphEmailService graphEmailService,
            ILogger<EmailsController> logger,
            IOptions<BatchRestoreOptions> batchOptions,
            IOptions<SelectionOptions> selectionOptions,
            IBatchRestoreService? batchRestoreService = null,
            ISyncJobService? syncJobService = null,
            IStringLocalizer<SharedResource> localizer = null,
            IExportService? exportService = null,
            ISelectedEmailsExportService? selectedEmailsExportService = null,
            IAccessLogService? accessLogService = null,
            IServiceScopeFactory? serviceScopeFactory = null,
            MailArchiver.Services.IAuthenticationService? authService = null)

        {
            _context = context;
            _emailService = emailService;
            _graphEmailService = graphEmailService;
            _logger = logger;
            _batchRestoreService = batchRestoreService;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
            _selectionOptions = selectionOptions.Value;
            _localizer = localizer;
            _exportService = exportService;
            _selectedEmailsExportService = selectedEmailsExportService;
            _accessLogService = accessLogService;
            _serviceScopeFactory = serviceScopeFactory;
            _authService = authService;
        }

        // GET: Emails
        public async Task<IActionResult> Index(SearchViewModel model)
        {
            // Standardwerte für die Suche
            if (model == null)
            {
                model = new SearchViewModel();
            }
            if (model.PageNumber <= 0) model.PageNumber = 1;
            
            // Validate and set page size to allowed values
            var allowedPageSizes = new[] { 20, 50, 75, 100, 150 };
            if (!allowedPageSizes.Contains(model.PageSize))
            {
                model.PageSize = 20; // Default to 20 if invalid value
            }

            // Ensure DirectionOptions are localized if model was created by the binder (parameterless ctor)
            if (model.DirectionOptions == null || model.DirectionOptions.Count < 3)
            {
                model.DirectionOptions = new List<SelectListItem>
                {
                    new SelectListItem { Text = _localizer?["All"] ?? "All", Value = "" },
                    new SelectListItem { Text = _localizer?["Incoming"] ?? "Incoming", Value = "false" },
                    new SelectListItem { Text = _localizer?["Outgoing"] ?? "Outgoing", Value = "true" }
                };
            }
            else if (_localizer != null)
            {
                // Refresh texts for localization
                model.DirectionOptions[0].Text = _localizer["All"];
                model.DirectionOptions[1].Text = _localizer["Incoming"];
                model.DirectionOptions[2].Text = _localizer["Outgoing"];
            }

            // Store search state for return navigation
            StoreSearchState(model);

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                    
                    // Log for debugging
                    _logger.LogInformation("User {Username} has access to {Count} accounts: {AccountIds}", 
                        username, allowedAccountIds.Count, string.Join(", ", allowedAccountIds));
                }
                else
                {
                    // If user not found, set empty list to prevent access to any emails
                    allowedAccountIds = new List<int>();
                    _logger.LogWarning("User {Username} not found in database", username);
                }
            }

            // Konten für die Dropdown-Liste laden (nur erlaubte Konten für Nicht-Admins)
            var accountsQuery = _context.MailAccounts.AsQueryable();
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
                }
                else
                {
                    // User has no access to any accounts, return empty list
                    accountsQuery = accountsQuery.Where(a => false);
                }
            }
            var accounts = await accountsQuery.ToListAsync();
            
            model.AccountOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = _localizer["AllAccounts"], Value = "" }
            };
            model.AccountOptions.AddRange(accounts.Select(a =>
                new SelectListItem
                {
                    Text = $"{a.Name} ({a.EmailAddress})",
                    Value = a.Id.ToString(),
                    Selected = model.SelectedAccountId == a.Id
                }));

            // Folder options - only show if an account is selected
            model.FolderOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = _localizer["AllFolders"], Value = "" }
            };
            
            if (model.SelectedAccountId.HasValue)
            {
                // Get distinct folders for the selected account from archived emails
                var distinctFolders = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == model.SelectedAccountId.Value)
                    .Select(e => e.FolderName)
                    .Distinct()
                    .Where(f => !string.IsNullOrEmpty(f))
                    .OrderBy(f => f)
                    .ToListAsync();
                
                model.FolderOptions.AddRange(distinctFolders.Select(f =>
                    new SelectListItem
                    {
                        Text = f,
                        Value = f,
                        Selected = model.SelectedFolder == f
                    }));
            }

            // Berechnen der Anzahl zu überspringender Elemente für die Paginierung
            int skip = (model.PageNumber - 1) * model.PageSize;

            // For non-admin users, we need to ensure they only see emails from their assigned accounts
            // If they haven't selected a specific account, we still need to filter by their allowed accounts
            int? accountIdForSearch = model.SelectedAccountId;
            
            // Suche durchführen
            var (emails, totalCount) = await _emailService.SearchEmailsAsync(
                model.SearchTerm,
                model.FromDate,
                model.ToDate,
                accountIdForSearch,
                model.SelectedFolder,
                model.IsOutgoing,
                skip,
                model.PageSize,
                allowedAccountIds,
                model.SortBy ?? "SentDate",
                model.SortOrder ?? "desc");

            model.SearchResults = emails;
            model.TotalResults = totalCount;

                    // Log the search action
                    if (_accessLogService != null && _serviceScopeFactory != null)
                    {
                        // Capture the current username before starting the background task
                        var currentUsername = _authService?.GetCurrentUser(HttpContext);
                        
                        if (!string.IsNullOrEmpty(currentUsername))
                        {
                            // Create a separate scope for logging to avoid DbContext concurrency issues
                            Task.Run(async () =>
                            {
                                try
                                {
                                    using var scope = _serviceScopeFactory.CreateScope();
                                    var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                                    
                                    var searchParams = new List<string>();
                                    if (!string.IsNullOrEmpty(model.SearchTerm))
                                        searchParams.Add($"term:{model.SearchTerm}");
                                    if (model.FromDate.HasValue)
                                        searchParams.Add($"from:{model.FromDate.Value:yyyy-MM-dd}");
                                    if (model.ToDate.HasValue)
                                        searchParams.Add($"to:{model.ToDate.Value:yyyy-MM-dd}");
                                    if (model.SelectedAccountId.HasValue)
                                        searchParams.Add($"account:{model.SelectedAccountId}");
                                    if (!string.IsNullOrEmpty(model.SelectedFolder))
                                        searchParams.Add($"folder:{model.SelectedFolder}");
                                    if (model.IsOutgoing.HasValue)
                                        searchParams.Add($"direction:{(model.IsOutgoing.Value ? "out" : "in")}");
                                    
                                    var searchParamsString = string.Join(", ", searchParams);
                                    
                                    await accessLogService.LogAccessAsync(currentUsername, AccessLogType.Search, 
                                        searchParameters: searchParamsString.Length > 255 ? searchParamsString.Substring(0, 255) : searchParamsString);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error logging search action");
                                }
                            });
                        }
                    }

            // Log the state of ShowSelectionControls for debugging
            _logger.LogInformation("Selection mode is {SelectionMode}", model.ShowSelectionControls ? "enabled" : "disabled");

            // Batch-Optionen für die View verfügbar machen
            ViewBag.AsyncThreshold = _batchOptions.AsyncThreshold;
            ViewBag.MaxSyncEmails = _batchOptions.MaxSyncEmails;
            ViewBag.MaxAsyncEmails = _batchOptions.MaxAsyncEmails;
            
            // Selection-Optionen für die View verfügbar machen
            ViewBag.MaxSelectableEmails = _selectionOptions.MaxSelectableEmails;

            // Aktive Jobs für die View
            if (_batchRestoreService != null)
            {
                var activeJobs = _batchRestoreService.GetActiveJobs();
                ViewBag.ActiveJobsCount = activeJobs.Count;
            }

            return View(model);
        }

        // GET: Emails/Details/5
        [EmailAccessRequired]
        public async Task<IActionResult> Details(int id, string returnUrl = null)
        {
            _logger.LogInformation("User requesting details for email ID: {EmailId}", id);
            
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                return View("Details404");
            }

            _logger.LogInformation("Found email with ID {EmailId} from account {AccountId}", 
                id, email.MailAccountId);

            // Use untruncated body if available for compliance
            var htmlBodyToDisplay = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                ? email.BodyUntruncatedHtml 
                : email.HtmlBody;

            var model = new EmailDetailViewModel
            {
                Email = email,
                AccountName = email.MailAccount?.Name ?? "Unknown account",
                FormattedHtmlBody = !string.IsNullOrEmpty(htmlBodyToDisplay) 
                    ? ResolveInlineImagesInHtml(SanitizeHtml(htmlBodyToDisplay), email.Attachments) 
                    : string.Empty,
            };

            // Store return URL in ViewBag
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index");

            // Log the email open action
            if (_accessLogService != null && _serviceScopeFactory != null)
            {
                // Capture the current username before starting the background task
                var currentUsername = _authService?.GetCurrentUser(HttpContext);
                
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    // Create a separate scope for logging to avoid DbContext concurrency issues
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                            
                            await accessLogService.LogAccessAsync(currentUsername, AccessLogType.Open, 
                                emailId: email.Id, 
                                emailSubject: email.Subject.Length > 255 ? email.Subject.Substring(0, 255) : email.Subject,
                                emailFrom: email.From.Length > 255 ? email.From.Substring(0, 255) : email.From);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error logging email open action");
                        }
                    });
                }
            }

            return View(model);
        }

        // Helper method to store search state in session
        private void StoreSearchState(SearchViewModel searchModel)
        {
            try
            {
                var searchState = new
                {
                    SearchTerm = searchModel.SearchTerm,
                    FromDate = searchModel.FromDate?.ToString("yyyy-MM-dd"),
                    ToDate = searchModel.ToDate?.ToString("yyyy-MM-dd"),
                    SelectedAccountId = searchModel.SelectedAccountId,
                    IsOutgoing = searchModel.IsOutgoing,
                    PageNumber = searchModel.PageNumber,
                    PageSize = searchModel.PageSize,
                    ShowSelectionControls = searchModel.ShowSelectionControls
                };

                HttpContext.Session.SetString("LastSearchState",
                    System.Text.Json.JsonSerializer.Serialize(searchState));

                _logger.LogDebug("Stored search state in session");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store search state in session");
            }
        }

        // Helper method to restore search state from session
        private string? GetStoredReturnUrl()
        {
            try
            {
                var searchStateJson = HttpContext.Session.GetString("LastSearchState");
                if (string.IsNullOrEmpty(searchStateJson))
                    return null;

                var searchState = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(searchStateJson);

                var queryParams = new List<string>();

                foreach (var kvp in searchState)
                {
                    if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()))
                    {
                        queryParams.Add($"{kvp.Key}={Uri.EscapeDataString(kvp.Value.ToString())}");
                    }
                }

                if (queryParams.Any())
                {
                    return Url.Action("Index") + "?" + string.Join("&", queryParams);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore search state from session");
            }

            return Url.Action("Index");
        }

        // GET: Emails/Attachment/5/1
        [EmailAccessRequired]
        public async Task<IActionResult> Attachment(int emailId, int attachmentId)
        {
            var attachment = await _context.EmailAttachments
                .Include(a => a.ArchivedEmail)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ArchivedEmailId == emailId);

            if (attachment == null)
            {
                _logger.LogWarning("Attachment with ID {AttachmentId} for email {EmailId} not found", attachmentId, emailId);
                return View("Details404");
            }

            return File(attachment.Content, attachment.ContentType, attachment.FileName);
        }

        // GET: Emails/AttachmentPreview/5/1
        [EmailAccessRequired]
        public async Task<IActionResult> AttachmentPreview(int emailId, int attachmentId)
        {
            var attachment = await _context.EmailAttachments
                .Include(a => a.ArchivedEmail)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ArchivedEmailId == emailId);

            if (attachment == null)
            {
                _logger.LogWarning("Attachment with ID {AttachmentId} for email {EmailId} not found", attachmentId, emailId);
                return View("Details404");
            }

            // For PDF files, we need to ensure they can be displayed inline
            if (attachment.ContentType == "application/pdf")
            {
                // Add headers to ensure PDF can be displayed inline
                Response.Headers.Add("Content-Disposition", "inline");
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
            }

            // Return the attachment content without forcing download
            return File(attachment.Content, attachment.ContentType);
        }

        // GET: Emails/DownloadAllAttachments/5
        [EmailAccessRequired]
        public async Task<IActionResult> DownloadAllAttachments(int id)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                return View("Details404");
            }

            // Check if email has attachments
            if (!email.Attachments.Any())
            {
                TempData["ErrorMessage"] = "This email has no attachments.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Create a ZIP file containing all attachments
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var attachment in email.Attachments)
                    {
                        var entry = archive.CreateEntry(attachment.FileName, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.Write(attachment.Content, 0, attachment.Content.Length);
                        }
                    }
                }

                var zipBytes = memoryStream.ToArray();
                var fileName = $"attachments-{email.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";

                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                return File(zipBytes, "application/zip", fileName);
            }
        }

        // GET: Emails/Export/5
        [EmailAccessRequired]
        public async Task<IActionResult> Export(int id, ExportFormat format = ExportFormat.Eml)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                return View("Details404");
            }

            // Get current user's allowed accounts for filtering
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Check if user has access to this email's account
            if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(email.MailAccountId))
            {
                TempData["ErrorMessage"] = "You do not have access to this email.";
                return RedirectToAction("Index");
            }

            try
            {
                var exportParams = new ExportViewModel
                {
                    Format = format,
                    EmailId = id
                };

                var fileBytes = await _emailService.ExportEmailsAsync(exportParams, allowedAccountIds);

                string contentType;
                string fileName;

                switch (format)
                {
                    case ExportFormat.Csv:
                        contentType = "text/csv";
                        fileName = $"email-{id}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                        break;
                    case ExportFormat.Json:
                        contentType = "application/json";
                        fileName = $"email-{id}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                        break;
                    case ExportFormat.Eml:
                        contentType = "message/rfc822";
                        fileName = $"email-{id}-{DateTime.Now:yyyyMMdd-HHmmss}.eml";
                        break;
                    default:
                        contentType = "application/octet-stream";
                        fileName = $"email-{id}.bin";
                        break;
                }

                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting email {EmailId} as {Format}", id, format);
                TempData["ErrorMessage"] = $"Export failed: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
            finally
            {
                // Log the email export action
                if (_accessLogService != null && _serviceScopeFactory != null)
                {
                    // Capture the current username before starting the background task
                    var currentUsername = _authService?.GetCurrentUser(HttpContext);
                    
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        // Create a separate scope for logging to avoid DbContext concurrency issues
                        Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = _serviceScopeFactory.CreateScope();
                                var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                                
                                // Create a new context for this scope to avoid concurrency issues
                                using var newContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                var email = await newContext.ArchivedEmails.FindAsync(id);
                                if (email != null)
                                {
                                    await accessLogService.LogAccessAsync(currentUsername, AccessLogType.Download, 
                                        emailId: email.Id, 
                                        emailSubject: email.Subject.Length > 255 ? email.Subject.Substring(0, 255) : email.Subject,
                                        emailFrom: email.From.Length > 255 ? email.From.Substring(0, 255) : email.From);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error logging email export action");
                            }
                        });
                    }
                }
            }
        }

        // GET: Emails/Restore/5
        [HttpGet]
        [EmailAccessRequired]
        public async Task<IActionResult> Restore(int id)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                return View("Details404");
            }

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Liste aller aktiven E-Mail-Konten abrufen (ohne IMPORT-Konten)
            IQueryable<MailAccount> accountsQuery = _context.MailAccounts
                .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                .OrderBy(a => a.Name);
            
            // Filter by allowed accounts for non-admin users
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
                }
                else
                {
                    // User has no access to any accounts, return empty list
                    accountsQuery = accountsQuery.Where(a => false);
                }
            }
            
            var accounts = await accountsQuery.ToListAsync();

            var model = new EmailRestoreViewModel
            {
                EmailId = email.Id,
                EmailSubject = email.Subject,
                EmailDate = email.SentDate,
                EmailSender = email.From,
                AvailableAccounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} ({a.EmailAddress})"
                }).ToList()
            };

            // If there's only one account, select it by default and load its folders
            if (model.AvailableAccounts.Count == 1)
            {
                model.TargetAccountId = int.Parse(model.AvailableAccounts[0].Value);
                model.AvailableAccounts[0].Selected = true;
                // Load folders for this account using appropriate service
                var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                List<string> folders;
                
                if (targetAccount?.Provider == ProviderType.M365)
                {
                    folders = await _graphEmailService.GetMailFoldersAsync(targetAccount);
                }
                else
                {
                    folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                }
                
                model.AvailableFolders = folders.Select(f => new SelectListItem
                {
                    Value = f,
                    Text = f
                }).ToList();
                // Select INBOX by default if available
                var inbox = model.AvailableFolders.FirstOrDefault(f => f.Value.ToUpper() == "INBOX");
                if (inbox != null)
                {
                    inbox.Selected = true;
                    model.TargetFolder = inbox.Value;
                }
            }

            return View(model);
        }

        // POST: Emails/Restore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(EmailRestoreViewModel model)
        {
            _logger.LogInformation("Restore POST method called with Email ID: {EmailId}, Target Account ID: {AccountId}, Target Folder: {Folder}",
                model.EmailId, model.TargetAccountId, model.TargetFolder);

            // Ignore validation errors for the display-only fields
            if (ModelState.ContainsKey("EmailSender"))
                ModelState.Remove("EmailSender");
            if (ModelState.ContainsKey("EmailSubject"))
                ModelState.Remove("EmailSubject");

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Check if user is allowed to access the target account
            if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(model.TargetAccountId))
            {
                _logger.LogWarning("User {Username} attempted to restore email to account {AccountId} which they don't have access to", 
                    authService?.GetCurrentUser(HttpContext), model.TargetAccountId);
                TempData["ErrorMessage"] = "You do not have access to the selected account.";
                return RedirectToAction(nameof(Details), new { id = model.EmailId });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for email restoration");
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning("Validation error: {ErrorMessage}", error.ErrorMessage);
                    }
                }

                // Reload account list if validation fails
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

                // Reload folders for the selected account
                if (model.TargetAccountId > 0)
                {
                    var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                    List<string> folders;
                    
                    if (targetAccount?.Provider == ProviderType.M365)
                    {
                        folders = await _graphEmailService.GetMailFoldersAsync(targetAccount);
                    }
                    else
                    {
                        folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                    }
                    
                    model.AvailableFolders = folders.Select(f => new SelectListItem
                    {
                        Value = f,
                        Text = f,
                        Selected = f == model.TargetFolder
                    }).ToList();
                }

                // Reload email details if needed
                var email = await _context.ArchivedEmails.FindAsync(model.EmailId);
                if (email != null)
                {
                    model.EmailSubject = email.Subject;
                    model.EmailSender = email.From;
                    model.EmailDate = email.SentDate;
                }

                return View(model);
            }

            try
            {
                _logger.LogInformation("Attempting to restore email {EmailId} to folder '{Folder}' of account {AccountId}",
                    model.EmailId, model.TargetFolder, model.TargetAccountId);

                // Get target account to check provider type
                var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                bool result;

                // Route to appropriate service based on provider type
                if (targetAccount?.Provider == ProviderType.M365)
                {
                    _logger.LogInformation("Using Graph API service for M365 account {AccountId}", model.TargetAccountId);
                    var email = await _context.ArchivedEmails
                        .Include(e => e.Attachments)
                        .FirstOrDefaultAsync(e => e.Id == model.EmailId);
                    
                    if (email == null)
                    {
                        _logger.LogError("Email with ID {EmailId} not found", model.EmailId);
                        TempData["ErrorMessage"] = "The email could not be found.";
                        return RedirectToAction(nameof(Details), new { id = model.EmailId });
                    }
                    
                    result = await _graphEmailService.RestoreEmailToFolderAsync(email, targetAccount, model.TargetFolder);
                }
                else
                {
                    result = await _emailService.RestoreEmailToFolderAsync(
                        model.EmailId,
                        model.TargetAccountId,
                        model.TargetFolder);
                }

                if (result)
                {
                    _logger.LogInformation("Email restoration successful");
                    TempData["SuccessMessage"] = "The email has been successfully copied to the specified folder.";
                    
                    // Log the email restore action
                    if (_accessLogService != null && _serviceScopeFactory != null)
                    {
                        // Capture the current username before starting the background task
                        var currentUsername = _authService?.GetCurrentUser(HttpContext);
                        
                        if (!string.IsNullOrEmpty(currentUsername))
                        {
                            // Create a separate scope for logging to avoid DbContext concurrency issues
                            Task.Run(async () =>
                            {
                                try
                                {
                                    using var scope = _serviceScopeFactory.CreateScope();
                                    var accessLogService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
                                    
                                    // Create a new context for this scope to avoid concurrency issues
                                    using var newContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                                    var email = await newContext.ArchivedEmails.FindAsync(model.EmailId);
                                    if (email != null)
                                    {
                                        await accessLogService.LogAccessAsync(currentUsername, AccessLogType.Restore, 
                                            emailId: email.Id, 
                                            emailSubject: email.Subject.Length > 255 ? email.Subject.Substring(0, 255) : email.Subject,
                                            emailFrom: email.From.Length > 255 ? email.From.Substring(0, 255) : email.From,
                                            mailAccountId: model.TargetAccountId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error logging email restore action");
                                }
                            });
                        }
                    }
                    
                    return RedirectToAction(nameof(Details), new { id = model.EmailId });
                }
                else
                {
                    _logger.LogWarning("Email restoration failed, but no exception was thrown");
                    TempData["ErrorMessage"] = "The email could not be copied to the specified folder. Please check the logs.";
                    return RedirectToAction(nameof(Details), new { id = model.EmailId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during email restoration");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = model.EmailId });
            }
        }

        // POST: Emails/BatchRestoreStart - Startet Batch-Operation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchRestoreStart(List<int> ids, string returnUrl = null)
        {
            if (ids == null || !ids.Any())
            {
                TempData["ErrorMessage"] = "No emails selected for batch operation.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            _logger.LogInformation("BatchRestoreStart called with {Count} emails. Thresholds: Async={AsyncThreshold}, MaxSync={MaxSync}, MaxAsync={MaxAsync}",
                ids.Count, _batchOptions.AsyncThreshold, _batchOptions.MaxSyncEmails, _batchOptions.MaxAsyncEmails);

            // Prüfe absolute Limits
            if (ids.Count > _batchOptions.MaxAsyncEmails)
            {
                TempData["ErrorMessage"] = $"Too many emails selected ({ids.Count:N0}). Maximum allowed is {_batchOptions.MaxAsyncEmails:N0} emails per operation.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            // Entscheide basierend auf konfigurierten Schwellenwerten
            var useBackgroundJob = ShouldUseBackgroundJob(ids.Count);

            if (useBackgroundJob)
            {
                _logger.LogInformation("Using background job for {Count} emails (threshold: {Threshold})",
                    ids.Count, _batchOptions.AsyncThreshold);
                return await StartAsyncBatchRestore(ids, returnUrl);
            }

            // Direkte Verarbeitung über Session
            _logger.LogInformation("Using direct processing for {Count} emails (threshold: {Threshold})",
                ids.Count, _batchOptions.AsyncThreshold);

            try
            {
                HttpContext.Session.SetString("BatchRestoreIds", string.Join(",", ids));
                HttpContext.Session.SetString("BatchRestoreReturnUrl", returnUrl ?? "");
                return RedirectToAction("BatchRestore");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store {Count} email IDs in session", ids.Count);

                // Fallback zu Background Job wenn Session fehlschlägt
                if (_batchRestoreService != null && ids.Count <= _batchOptions.MaxAsyncEmails)
                {
                    _logger.LogWarning("Session storage failed, falling back to background job");
                    return await StartAsyncBatchRestore(ids, returnUrl);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Too many emails selected for direct processing ({ids.Count:N0}). Maximum for direct processing is {_batchOptions.MaxSyncEmails:N0} emails.";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }
            }
        }

        private bool ShouldUseBackgroundJob(int emailCount)
        {
            // Background Job wenn:
            // 1. Service verfügbar ist UND
            // 2. Anzahl über AsyncThreshold liegt UND
            // 3. Anzahl unter MaxAsyncEmails liegt
            if (_batchRestoreService == null)
            {
                _logger.LogDebug("Background service not available, using direct processing");
                return false;
            }

            if (emailCount > _batchOptions.AsyncThreshold)
            {
                _logger.LogDebug("Email count {Count} exceeds async threshold {Threshold}, using background job",
                    emailCount, _batchOptions.AsyncThreshold);
                return true;
            }

            _logger.LogDebug("Email count {Count} below async threshold {Threshold}, using direct processing",
                emailCount, _batchOptions.AsyncThreshold);
            return false;
        }

        // GET: Emails/BatchRestore - Zeigt das Form an
        [HttpGet]
        public async Task<IActionResult> BatchRestore()
        {
            var idsString = HttpContext.Session.GetString("BatchRestoreIds");
            var returnUrl = HttpContext.Session.GetString("BatchRestoreReturnUrl");

            if (string.IsNullOrEmpty(idsString))
            {
                TempData["ErrorMessage"] = "No emails selected for batch operation.";
                return RedirectToAction("Index");
            }

            var ids = idsString.Split(',').Select(int.Parse).ToList();

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Get active email accounts for dropdown (without IMPORT accounts)
            IQueryable<MailAccount> accountsQuery = _context.MailAccounts
                .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                .OrderBy(a => a.Name);
            
            // Filter by allowed accounts for non-admin users
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
                }
                else
                {
                    // User has no access to any accounts, return empty list
                    accountsQuery = accountsQuery.Where(a => false);
                }
            }
            
            var accounts = await accountsQuery.ToListAsync();

            var model = new BatchRestoreViewModel
            {
                SelectedEmailIds = ids,
                ReturnUrl = returnUrl,
                AvailableAccounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} ({a.EmailAddress})"
                }).ToList()
            };

            // If there's only one account, select it by default and load its folders
            if (model.AvailableAccounts.Count == 1)
            {
                model.TargetAccountId = int.Parse(model.AvailableAccounts[0].Value);
                // Load folders for this account using appropriate service
                var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                List<string> folders;
                
                if (targetAccount?.Provider == ProviderType.M365)
                {
                    folders = await _graphEmailService.GetMailFoldersAsync(targetAccount);
                }
                else
                {
                    folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                }
                
                model.AvailableFolders = folders.Select(f => new SelectListItem
                {
                    Value = f,
                    Text = f
                }).ToList();

                // Select INBOX by default if available
                var inbox = model.AvailableFolders.FirstOrDefault(f => f.Value.ToUpper() == "INBOX");
                if (inbox != null)
                {
                    inbox.Selected = true;
                    model.TargetFolder = inbox.Value;
                }
            }

            return View(model);
        }

        // POST: Emails/BatchRestore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchRestore(BatchRestoreViewModel model)
        {
            _logger.LogInformation("BatchRestore POST method called with {Count} emails, Target Account ID: {AccountId}, Target Folder: {Folder}",
                model.SelectedEmailIds.Count, model.TargetAccountId, model.TargetFolder);

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Check if user is allowed to access the target account
            if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(model.TargetAccountId))
            {
                _logger.LogWarning("User {Username} attempted to restore emails to account {AccountId} which they don't have access to", 
                    authService?.GetCurrentUser(HttpContext), model.TargetAccountId);
                TempData["ErrorMessage"] = "You do not have access to the selected account.";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }

            // Hole IDs aus Session falls sie nicht im Model sind
            if (!model.SelectedEmailIds.Any())
            {
                var idsString = HttpContext.Session.GetString("BatchRestoreIds");
                if (!string.IsNullOrEmpty(idsString))
                {
                    model.SelectedEmailIds = idsString.Split(',').Select(int.Parse).ToList();
                }
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for batch email restoration");
                // Reload account list if validation fails
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

                // Reload folders for the selected account
                if (model.TargetAccountId > 0)
                {
                    var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                    List<string> folders;
                    
                    if (targetAccount?.Provider == ProviderType.M365)
                    {
                        folders = await _graphEmailService.GetMailFoldersAsync(targetAccount);
                    }
                    else
                    {
                        folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                    }
                    
                    model.AvailableFolders = folders.Select(f => new SelectListItem
                    {
                        Value = f,
                        Text = f,
                        Selected = f == model.TargetFolder
                    }).ToList();
                }

                return View(model);
            }

            try
            {
                _logger.LogInformation("Attempting to restore {Count} emails to folder '{Folder}' of account {AccountId}",
                    model.SelectedEmailIds.Count, model.TargetFolder, model.TargetAccountId);

                // Log the batch restore action
                var currentUsername = _authService?.GetCurrentUser(HttpContext);
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Restore, 
                        searchParameters: $"Started batch restore for {model.SelectedEmailIds.Count} emails to account {model.TargetAccountId} in folder {model.TargetFolder}");
                }

                // Get target account to check provider type
                var targetAccount = await _context.MailAccounts.FindAsync(model.TargetAccountId);
                int successful, failed;

                // Route to appropriate service based on provider type
                if (targetAccount?.Provider == ProviderType.M365)
                {
                    _logger.LogInformation("Using Graph API service for M365 account {AccountId}", model.TargetAccountId);
                    
                    // For M365, we need to restore emails one by one using the Graph API
                    successful = 0;
                    failed = 0;
                    
                    foreach (var emailId in model.SelectedEmailIds)
                    {
                        try
                        {
                            var email = await _context.ArchivedEmails
                                .Include(e => e.Attachments)
                                .FirstOrDefaultAsync(e => e.Id == emailId);
                            
                            if (email == null)
                            {
                                _logger.LogWarning("Email with ID {EmailId} not found during batch restore", emailId);
                                failed++;
                                continue;
                            }
                            
                            var result = await _graphEmailService.RestoreEmailToFolderAsync(email, targetAccount, model.TargetFolder);
                            if (result)
                            {
                                successful++;
                                _logger.LogInformation("Successfully restored email {EmailId} to M365 account {AccountId}", emailId, model.TargetAccountId);
                            }
                            else
                            {
                                failed++;
                                _logger.LogWarning("Failed to restore email {EmailId} to M365 account {AccountId}", emailId, model.TargetAccountId);
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            _logger.LogError(ex, "Exception occurred during batch email restoration of email {EmailId} to M365 account {AccountId}", emailId, model.TargetAccountId);
                        }
                    }
                }
                else
                {
                    var result = await _emailService.RestoreMultipleEmailsWithProgressAsync(
                        model.SelectedEmailIds,
                        model.TargetAccountId,
                        model.TargetFolder,
                        (processed, successCount, failedCount) => {
                            // Empty progress callback for synchronous processing
                        });
                    
                    successful = result.Successful;
                    failed = result.Failed;
                }

                if (successful > 0)
                {
                    if (failed == 0)
                    {
                        TempData["SuccessMessage"] = $"All {successful} emails have been successfully copied to the specified folder.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"{successful} emails have been copied, but {failed} could not be copied. Check the logs for details.";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "None of the selected emails could be copied. Please check the logs for details.";
                }

                // Session-Daten löschen
                HttpContext.Session.Remove("BatchRestoreIds");
                HttpContext.Session.Remove("BatchRestoreReturnUrl");

                // Redirect to the return URL if provided, otherwise to the index
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during batch email restoration");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }
        }

        // GET: Emails/StartAsyncBatchRestoreFromAccount
        [HttpGet]
        public async Task<IActionResult> StartAsyncBatchRestoreFromAccount(int accountId, string returnUrl = null)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
            {
                TempData["ErrorMessage"] = "Mail account not found.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var emailIds = await _context.ArchivedEmails
                .Where(e => e.MailAccountId == accountId)
                .Select(e => e.Id)
                .ToListAsync();

            if (!emailIds.Any())
            {
                TempData["ErrorMessage"] = "No emails found to copy for this account.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            _logger.LogInformation("Account {AccountId} has {Count} emails to process", accountId, emailIds.Count);

            // Prüfe absolute Limits
            if (emailIds.Count > _batchOptions.MaxAsyncEmails)
            {
                TempData["ErrorMessage"] = $"Too many emails in this account ({emailIds.Count:N0}). Maximum allowed is {_batchOptions.MaxAsyncEmails:N0} emails per operation.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            // Für Account-Restores: Verwende Background-Job wenn verfügbar und sinnvoll
            // Session-basierte Verarbeitung nur für sehr kleine Accounts (< 50 Emails)
            var useBackgroundJob = _batchRestoreService != null && emailIds.Count >= _batchOptions.AsyncThreshold;

            if (useBackgroundJob)
            {
                _logger.LogInformation("Using background job for account restore with {Count} emails", emailIds.Count);
                return await StartAsyncBatchRestore(emailIds, returnUrl);
            }
            else
            {
                // Verwende normale Session-basierte Verarbeitung nur für sehr kleine Accounts
                // Berechne ungefähre Session-Größe (jede ID ca. 10 Zeichen + Komma)
                var estimatedSessionSize = emailIds.Count * 11; // konservative Schätzung
                var maxSafeSessionSize = 3000; // Sicherer Grenzwert unter typischen 4KB Session-Limits

                if (estimatedSessionSize > maxSafeSessionSize)
                {
                    _logger.LogWarning("Email count {Count} would exceed safe session size ({EstimatedSize} bytes), forcing background job", 
                        emailIds.Count, estimatedSessionSize);
                    
                    if (_batchRestoreService != null)
                    {
                        return await StartAsyncBatchRestore(emailIds, returnUrl);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Too many emails ({emailIds.Count:N0}) for direct processing and background service is not available. Please contact your administrator.";
                        return Redirect(returnUrl ?? Url.Action("Index"));
                    }
                }

                try
                {
                    HttpContext.Session.SetString("BatchRestoreIds", string.Join(",", emailIds));
                    HttpContext.Session.SetString("BatchRestoreReturnUrl", returnUrl ?? "");
                    _logger.LogInformation("Using session-based processing for {Count} emails", emailIds.Count);
                    return RedirectToAction("BatchRestore");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store {Count} email IDs in session", emailIds.Count);

                    if (_batchRestoreService != null)
                    {
                        _logger.LogWarning("Session storage failed, falling back to background job");
                        return await StartAsyncBatchRestore(emailIds, returnUrl);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Too many emails to process. Please try with a smaller selection.";
                        return Redirect(returnUrl ?? Url.Action("Index"));
                    }
                }
            }
        }

        // Asynchrone Batch-Restore-Methoden (nur wenn Service verfügbar)
        private async Task<IActionResult> StartAsyncBatchRestore(List<int> ids, string returnUrl)
        {
            if (_batchRestoreService == null)
            {
                // Fallback zur Session-basierten Verarbeitung
                try
                {
                    HttpContext.Session.SetString("BatchRestoreIds", string.Join(",", ids));
                    HttpContext.Session.SetString("BatchRestoreReturnUrl", returnUrl ?? "");
                    return RedirectToAction("BatchRestore");
                }
                catch
                {
                    TempData["ErrorMessage"] = "Too many emails selected. Please select fewer emails and try again.";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }
            }

            // Speichere die Email-IDs in der Session um HTTP 400-Fehler bei großen Listen zu vermeiden
            try
            {
                HttpContext.Session.SetString("AsyncBatchRestoreIds", string.Join(",", ids));
                HttpContext.Session.SetString("AsyncBatchRestoreReturnUrl", returnUrl ?? "");
                _logger.LogInformation("Stored {Count} email IDs in session for async batch restore", ids.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store {Count} email IDs in session for async batch restore", ids.Count);
                TempData["ErrorMessage"] = "Too many emails to process. Please contact your administrator.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Get active email accounts (without IMPORT accounts)
            IQueryable<MailAccount> accountsQuery = _context.MailAccounts
                .Where(a => a.IsEnabled && a.Provider != ProviderType.IMPORT)
                .OrderBy(a => a.Name);
            
            // Filter by allowed accounts for non-admin users
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    accountsQuery = accountsQuery.Where(a => allowedAccountIds.Contains(a.Id));
                }
                else
                {
                    // User has no access to any accounts, return empty list
                    accountsQuery = accountsQuery.Where(a => false);
                }
            }
            
            var accounts = await accountsQuery.ToListAsync();

            if (!accounts.Any())
            {
                TempData["ErrorMessage"] = "No enabled email accounts found.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var model = new AsyncBatchRestoreViewModel
            {
                // Nicht die IDs im ViewModel speichern, um HTTP 400 bei POST zu vermeiden
                EmailIds = new List<int>(),
                ReturnUrl = returnUrl,
                AvailableAccounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} ({a.EmailAddress})"
                }).ToList()
            };

            // Setze EmailCount für die View
            ViewBag.EmailCount = ids.Count;

            // Auto-select single account
            if (model.AvailableAccounts.Count == 1)
            {
                model.TargetAccountId = int.Parse(model.AvailableAccounts[0].Value);
                var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                model.AvailableFolders = folders.Select(f => new SelectListItem
                {
                    Value = f,
                    Text = f
                }).ToList();

                var inbox = model.AvailableFolders.FirstOrDefault(f => f.Value.ToUpper() == "INBOX");
                if (inbox != null)
                {
                    inbox.Selected = true;
                    model.TargetFolder = inbox.Value;
                }
            }

            return View("AsyncBatchRestore", model);
        }

        // POST: Emails/StartAsyncBatchRestore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAsyncBatchRestore(AsyncBatchRestoreViewModel model)
        {
            if (_batchRestoreService == null)
            {
                TempData["ErrorMessage"] = "Asynchronous batch restore is not available.";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }

            // Hole IDs aus Session (sie wurden dort gespeichert um HTTP 400 zu vermeiden)
            var idsString = HttpContext.Session.GetString("AsyncBatchRestoreIds");
            if (string.IsNullOrEmpty(idsString))
            {
                TempData["ErrorMessage"] = "No emails selected for async batch restore.";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }

            var emailIds = idsString.Split(',').Select(int.Parse).ToList();
            _logger.LogInformation("Retrieved {Count} email IDs from session for async batch restore", emailIds.Count);

            // Get current user's allowed accounts
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Check if user is allowed to access the target account
            if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(model.TargetAccountId))
            {
                _logger.LogWarning("User {Username} attempted to restore emails to account {AccountId} which they don't have access to", 
                    authService?.GetCurrentUser(HttpContext), model.TargetAccountId);
                TempData["ErrorMessage"] = "You do not have access to the selected account.";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }

            if (!ModelState.IsValid)
            {
                // Reload data
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

                if (model.TargetAccountId > 0)
                {
                    var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
                    model.AvailableFolders = folders.Select(f => new SelectListItem
                    {
                        Value = f,
                        Text = f,
                        Selected = f == model.TargetFolder
                    }).ToList();
                }

                // Setze EmailCount für die View
                ViewBag.EmailCount = emailIds.Count;

                return View("AsyncBatchRestore", model);
            }

            try
            {
                var job = new BatchRestoreJob
                {
                    EmailIds = emailIds, // Verwende IDs aus Session
                    TargetAccountId = model.TargetAccountId,
                    TargetFolder = model.TargetFolder,
                    ReturnUrl = model.ReturnUrl ?? "",
                    UserId = HttpContext.User.Identity?.Name ?? "Anonymous"
                };

                var jobId = _batchRestoreService.QueueJob(job);

                // Log the batch restore action
                var currentUsername = _authService?.GetCurrentUser(HttpContext);
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Restore, 
                        searchParameters: $"Started batch restore for {emailIds.Count} emails to account {model.TargetAccountId} in folder {model.TargetFolder}");
                }

                // Session-Daten löschen
                HttpContext.Session.Remove("AsyncBatchRestoreIds");
                HttpContext.Session.Remove("AsyncBatchRestoreReturnUrl");

                TempData["SuccessMessage"] = $"Batch restore job started with {emailIds.Count:N0} emails. Job ID: {jobId}";

                return RedirectToAction("BatchRestoreStatus", new { jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start async batch restore");
                TempData["ErrorMessage"] = $"Failed to start batch restore: {ex.Message}";
                return Redirect(model.ReturnUrl ?? Url.Action("Index"));
            }
        }

        // GET: Emails/BatchRestoreStatus
        [HttpGet]
        public IActionResult BatchRestoreStatus(string jobId)
        {
            _logger.LogDebug("BatchRestoreStatus called with jobId: {JobId}", jobId ?? "null");

            if (_batchRestoreService == null)
            {
                _logger.LogWarning("Batch restore service is not available");
                TempData["ErrorMessage"] = "Batch restore service is not available.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(jobId))
            {
                _logger.LogWarning("Empty or null jobId provided to BatchRestoreStatus");
                TempData["ErrorMessage"] = "Invalid job ID.";
                return RedirectToAction("Index");
            }

            var job = _batchRestoreService.GetJob(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job with ID {JobId} not found in BatchRestoreService", jobId);
                TempData["ErrorMessage"] = "Batch restore job not found. The job may have been completed or cleaned up.";
                return RedirectToAction("Jobs");
            }

            _logger.LogDebug("Successfully retrieved job {JobId} with status {Status}", jobId, job.Status);
            return View(job);
        }

        // POST: Emails/CancelBatchRestore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelBatchRestore(string jobId, string returnUrl = null)
        {
            if (_batchRestoreService == null)
            {
                TempData["ErrorMessage"] = "Batch restore service is not available.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = "Invalid job ID.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var success = _batchRestoreService.CancelJob(jobId);

            if (success)
            {
                TempData["SuccessMessage"] = "Batch restore job has been cancelled.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not cancel the batch restore job.";
            }

            return Redirect(returnUrl ?? Url.Action("Index"));
        }

        // POST: Emails/CancelSyncJob
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelSyncJob(string jobId, string returnUrl = null)
        {
            if (_syncJobService == null)
            {
                TempData["ErrorMessage"] = "Sync job service is not available.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var success = _syncJobService.CancelJob(jobId);

            if (success)
            {
                TempData["SuccessMessage"] = "Sync job has been cancelled.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not cancel the sync job.";
            }

            return Redirect(returnUrl ?? Url.Action("Jobs"));
        }

        // Helper method to get all batch jobs from the service
        private List<BatchRestoreJob> GetAllBatchJobsFromService()
        {
            var jobs = new List<BatchRestoreJob>();
            
            if (_batchRestoreService != null)
            {
                // Get all jobs including completed ones
                jobs = _batchRestoreService.GetAllJobs();
            }
            
            return jobs;
        }

        // GET: Emails/Jobs
        [HttpGet]
        [AdminRequired]
        public IActionResult Jobs()
        {
            var batchJobs = new List<BatchRestoreJob>();
            var syncJobs = new List<SyncJob>();
            var mboxJobs = new List<MBoxImportJob>();
            var exportJobs = new List<AccountExportJob>();
            var selectedEmailsExportJobs = new List<SelectedEmailsExportJob>();
            var emlImportJobs = new List<EmlImportJob>();

            if (_batchRestoreService != null)
            {
                // Get all jobs including finished ones to keep them in the list
                // We'll get all jobs from the service and sort them appropriately
                var allBatchJobs = GetAllBatchJobsFromService();
                batchJobs = allBatchJobs
                    .OrderByDescending(j => j.Status == BatchRestoreJobStatus.Queued || j.Status == BatchRestoreJobStatus.Running)
                    .ThenByDescending(j => j.Created)
                    .Take(20) // Apply top 20 restriction
                    .ToList();
            }

            if (_syncJobService != null)
            {
                // Get all jobs but prioritize running jobs at the top
                var allSyncJobs = _syncJobService.GetAllJobs();
                syncJobs = allSyncJobs
                    .OrderByDescending(j => j.Status == SyncJobStatus.Running) // Running jobs first
                    .ThenByDescending(j => j.Started) // Then by start time
                    .Take(20) // Apply top 20 restriction
                    .ToList();
            }

            // MBox Import Jobs hinzufügen
            try
            {
                var mboxService = HttpContext.RequestServices.GetService<IMBoxImportService>();
                if (mboxService != null)
                {
                    mboxJobs = mboxService.GetAllJobs()
                        .OrderByDescending(j => j.Status == MBoxImportJobStatus.Running || j.Status == MBoxImportJobStatus.Queued)
                        .ThenByDescending(j => j.Created)
                        .Take(20) // Apply top 20 restriction
                        .ToList();
                }
            }
            catch
            {
                // Ignore if service not available
            }

            // Account Export Jobs hinzufügen
            if (_exportService != null)
            {
                try
                {
                    exportJobs = _exportService.GetAllJobs()
                        .OrderByDescending(j => j.Status == AccountExportJobStatus.Running || j.Status == AccountExportJobStatus.Queued)
                        .ThenByDescending(j => j.Created)
                        .Take(20) // Apply top 20 restriction
                        .ToList();
                }
                catch
                {
                    // Ignore if service not available
                }
            }

            // Selected Emails Export Jobs hinzufügen
            try
            {
                var selectedEmailsExportService = HttpContext.RequestServices.GetService<ISelectedEmailsExportService>();
                if (selectedEmailsExportService != null)
                {
                    selectedEmailsExportJobs = selectedEmailsExportService.GetAllJobs()
                        .OrderByDescending(j => j.Status == SelectedEmailsExportJobStatus.Running || j.Status == SelectedEmailsExportJobStatus.Queued)
                        .ThenByDescending(j => j.Created)
                        .Take(20) // Apply top 20 restriction
                        .ToList();
                }
            }
            catch
            {
                // Ignore if service not available
            }

            // EML Import Jobs hinzufügen
            try
            {
                var emlImportService = HttpContext.RequestServices.GetService<IEmlImportService>();
                if (emlImportService != null)
                {
                    emlImportJobs = emlImportService.GetAllJobs()
                        .OrderByDescending(j => j.Status == EmlImportJobStatus.Running || j.Status == EmlImportJobStatus.Queued)
                        .ThenByDescending(j => j.Created)
                        .Take(20) // Apply top 20 restriction consistent with other job types
                        .ToList();
                }
            }
            catch
            {
                // Ignore if service not available
            }

            ViewBag.BatchJobs = batchJobs;
            ViewBag.SyncJobs = syncJobs;
            ViewBag.MBoxJobs = mboxJobs;
            ViewBag.ExportJobs = exportJobs;
            ViewBag.SelectedEmailsExportJobs = selectedEmailsExportJobs;
            ViewBag.EmlImportJobs = emlImportJobs;

            return View(batchJobs);
        }

        // GET: Emails/SelectedEmailsExportStatus
        [HttpGet]
        public IActionResult SelectedEmailsExportStatus(string jobId)
        {
            _logger.LogDebug("SelectedEmailsExportStatus called with jobId: {JobId}", jobId ?? "null");

            if (_selectedEmailsExportService == null)
            {
                _logger.LogWarning("Selected emails export service is not available");
                TempData["ErrorMessage"] = "Selected emails export service is not available.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(jobId))
            {
                _logger.LogWarning("Empty or null jobId provided to SelectedEmailsExportStatus");
                TempData["ErrorMessage"] = "Invalid job ID.";
                return RedirectToAction("Index");
            }

            var job = _selectedEmailsExportService.GetJob(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job with ID {JobId} not found in SelectedEmailsExportService", jobId);
                TempData["ErrorMessage"] = "Selected emails export job not found.";
                return RedirectToAction("Index");
            }

            _logger.LogDebug("Successfully retrieved job {JobId} with status {Status}", jobId, job.Status);
            return View(job);
        }

        // POST: Emails/CancelSelectedEmailsExport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelSelectedEmailsExport(string jobId, string returnUrl = null)
        {
            if (_selectedEmailsExportService == null)
            {
                TempData["ErrorMessage"] = "Selected emails export service is not available.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = "Invalid job ID.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var success = _selectedEmailsExportService.CancelJob(jobId);

            if (success)
            {
                TempData["SuccessMessage"] = "Selected emails export job has been cancelled.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not cancel the selected emails export job.";
            }

            return Redirect(returnUrl ?? Url.Action("Jobs"));
        }

        // GET: Emails/DownloadSelectedEmailsExport
        [HttpGet]
        public IActionResult DownloadSelectedEmailsExport(string jobId)
        {
            if (_selectedEmailsExportService == null)
            {
                TempData["ErrorMessage"] = "Selected emails export service is not available.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(jobId))
            {
                TempData["ErrorMessage"] = "Invalid job ID.";
                return RedirectToAction("Index");
            }

            var job = _selectedEmailsExportService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = "Selected emails export job not found.";
                return RedirectToAction("Index");
            }

            // Check if job is completed
            if (job.Status != SelectedEmailsExportJobStatus.Completed)
            {
                TempData["ErrorMessage"] = "Selected emails export job is not completed yet.";
                return RedirectToAction("Index");
            }

            try
            {
                var fileResult = _selectedEmailsExportService.GetExportForDownload(jobId);
                if (fileResult == null || string.IsNullOrEmpty(fileResult.FilePath) || !System.IO.File.Exists(fileResult.FilePath))
                {
                    TempData["ErrorMessage"] = "Export file not found.";
                    return RedirectToAction("Index");
                }

                // Stream the file directly without loading it into memory
                var fileStream = new FileStream(fileResult.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

                // Mark as downloaded (this will delete the file after download)
                _selectedEmailsExportService.MarkAsDownloaded(jobId);

                return File(fileStream, fileResult.ContentType, fileResult.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading selected emails export file for job {JobId}", jobId);
                TempData["ErrorMessage"] = "Error downloading export file.";
                return RedirectToAction("Index");
            }
        }

        // API endpoint for AJAX status updates
        [HttpGet]
        public JsonResult GetBatchRestoreStatus(string jobId)
        {
            if (_batchRestoreService == null)
            {
                return Json(new { error = "Batch restore service not available" });
            }

            if (string.IsNullOrEmpty(jobId))
            {
                return Json(new { error = "Invalid job ID" });
            }

            var job = _batchRestoreService.GetJob(jobId);
            if (job == null)
            {
                return Json(new { error = "Job not found" });
            }

            return Json(new
            {
                jobId = job.JobId,
                status = job.Status.ToString(),
                processed = job.ProcessedCount,
                total = job.EmailIds.Count,
                success = job.SuccessCount,
                failed = job.FailedCount,
                progressPercent = job.EmailIds.Count > 0 ? (job.ProcessedCount * 100.0 / job.EmailIds.Count) : 0,
                started = job.Started?.ToString("yyyy-MM-dd HH:mm:ss"),
                completed = job.Completed?.ToString("yyyy-MM-dd HH:mm:ss"),
                errorMessage = job.ErrorMessage
            });
        }

        [HttpGet]
        public async Task<JsonResult> GetFolders(int accountId)
        {
            _logger.LogInformation("GetFolders called with accountId: {AccountId}", accountId);

            if (accountId <= 0)
            {
                _logger.LogWarning("Invalid accountId provided: {AccountId}", accountId);
                return Json(new List<string>());
            }

            try
            {
                // Get the target account to check provider type
                var targetAccount = await _context.MailAccounts.FindAsync(accountId);
                if (targetAccount == null)
                {
                    _logger.LogWarning("Account {AccountId} not found", accountId);
                    return Json(new List<string> { "INBOX" });
                }

                // Get folders from the mail server using appropriate service
                List<string> folders;
                
                if (targetAccount.Provider == ProviderType.M365)
                {
                    _logger.LogInformation("Using Graph API service to get folders for M365 account {AccountId}", accountId);
                    folders = await _graphEmailService.GetMailFoldersAsync(targetAccount);
                }
                else if (targetAccount.Provider == ProviderType.IMAP)
                {
                    _logger.LogInformation("Using Email service to get folders for IMAP account {AccountId}", accountId);
                    folders = await _emailService.GetMailFoldersAsync(accountId);
                }
                else
                {
                    // For IMPORT accounts or unknown providers, return INBOX as default
                    _logger.LogWarning("Account {AccountId} has provider type {Provider}, returning default INBOX", 
                        accountId, targetAccount.Provider);
                    return Json(new List<string> { "INBOX" });
                }

                if (folders == null || !folders.Any())
                {
                    _logger.LogWarning("No folders found for account {AccountId}, returning default INBOX", accountId);
                    return Json(new List<string> { "INBOX" });
                }

                _logger.LogInformation("Successfully retrieved {Count} folders for account {AccountId}",
                    folders.Count, accountId);
                return Json(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while retrieving folders for account {AccountId}", accountId);
                return Json(new List<string> { "INBOX" });
            }
        }

        // POST: Emails/ExportSearchResults
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportSearchResults(ExportViewModel model)
        {
            // Entferne ModelState-Validierung für SearchTerm falls leer
            if (string.IsNullOrEmpty(model.SearchTerm))
            {
                ModelState.Remove("SearchTerm");
            }

            if (!ModelState.IsValid)
            {
                // Log validation errors für debugging
                _logger.LogWarning("Export validation failed. Errors: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                // Redirect zurück zur Index-Seite mit Fehlermeldung
                TempData["ErrorMessage"] = "Export parameters are invalid. Please try again.";
                return RedirectToAction("Index");
            }

            // Get current user's allowed accounts for filtering
            List<int> allowedAccountIds = null;
            var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
            var userService = HttpContext.RequestServices.GetService<IUserService>();
            
            if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
            {
                var username = authService.GetCurrentUser(HttpContext);
                var user = await userService.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                    allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                }
            }

            // Update the model with allowed account filtering
            if (allowedAccountIds != null && allowedAccountIds.Any())
            {
                // If user has selected a specific account, ensure they have access to it
                if (model.SelectedAccountId.HasValue && !allowedAccountIds.Contains(model.SelectedAccountId.Value))
                {
                    TempData["ErrorMessage"] = "You do not have access to the selected account.";
                    return RedirectToAction("Index");
                }
                
                // If no account is selected, we'll filter by allowed accounts in the search
                if (!model.SelectedAccountId.HasValue)
                {
                    // We'll handle this in the email service by passing allowedAccountIds
                }
            }

            try
            {
                // For single email export, we don't need to filter by accounts
                if (model.EmailId.HasValue)
                {
                    var fileBytes = await _emailService.ExportEmailsAsync(model, allowedAccountIds);
                    
                    string contentType;
                    string fileName;
                    switch (model.Format)
                    {
                        case ExportFormat.Csv:
                            contentType = "text/csv";
                            fileName = $"emails-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                            break;
                        case ExportFormat.Json:
                            contentType = "application/json";
                            fileName = $"emails-export-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                            break;
                        case ExportFormat.Eml:
                            contentType = "message/rfc822";
                            fileName = $"email-{DateTime.Now:yyyyMMdd-HHmmss}.eml";
                            break;
                        default:
                            contentType = "application/octet-stream";
                            fileName = $"export-{DateTime.Now:yyyyMMdd-HHmmss}.dat";
                            break;
                    }

                    Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    return File(fileBytes, contentType, fileName);
                }
                else
                {
                    // For search results export, we need to ensure proper filtering
                    var fileBytes = await _emailService.ExportEmailsAsync(model, allowedAccountIds);

                    string contentType;
                    string fileName;
                    switch (model.Format)
                    {
                        case ExportFormat.Csv:
                            contentType = "text/csv";
                            fileName = $"emails-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                            break;
                        case ExportFormat.Json:
                            contentType = "application/json";
                            fileName = $"emails-export-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                            break;
                        default:
                            contentType = "application/octet-stream";
                            fileName = $"export-{DateTime.Now:yyyyMMdd-HHmmss}.dat";
                            break;
                    }

                    Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    return File(fileBytes, contentType, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email export");
                TempData["ErrorMessage"] = $"Export failed: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Emails/RawContent/5
        [EmailAccessRequired]
        public async Task<IActionResult> RawContent(int id)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                return View("Details404");
            }

            // Use untruncated body if available for compliance
            var htmlBodyToDisplay = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                ? email.BodyUntruncatedHtml 
                : email.HtmlBody;
            
            var textBodyToDisplay = !string.IsNullOrEmpty(email.BodyUntruncatedText) 
                ? email.BodyUntruncatedText 
                : email.Body;

            // Bereiten Sie das HTML für die direkte Anzeige vor
            string html = !string.IsNullOrEmpty(htmlBodyToDisplay)
                ? ResolveInlineImagesInHtml(SanitizeHtml(htmlBodyToDisplay), email.Attachments)
                : $"<pre>{HttpUtility.HtmlEncode(textBodyToDisplay)}</pre>";

            // Fügen Sie die Basis-HTML-Struktur hinzu, wenn sie fehlt
            if (!html.Contains("<!DOCTYPE") && !html.Contains("<html"))
            {
                html = $@"<!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""utf-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <base target=""_blank"">
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 15px; }}
                        pre {{ white-space: pre-wrap; }}
                    </style>
                </head>
                <body>
                    {html}
                </body>
                </html>";
            }

            // Set proper content type with UTF-8 encoding to ensure correct character display
            return Content(html, "text/html; charset=utf-8");
        }

        // Hilfsmethode zur Bereinigung von HTML für die sichere Darstellung
        private string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Entfernen von potenziellen JavaScript-Elementen
            html = Regex.Replace(html, @"<script.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Entfernen von event handlers - behalte aber style-Attribute
            html = Regex.Replace(html, @"(on\w+)=([""']).*?\2", "", RegexOptions.IgnoreCase);

            // Entfernen von javascript: URLs
            html = Regex.Replace(html, @"href=([""'])javascript:.*?\1", "href=\"#\"", RegexOptions.IgnoreCase);

            // WICHTIG: Style-Tags und inline-style Attribute NICHT entfernen
            // Dadurch bleibt das originale Styling der E-Mail erhalten

            // Einfügen einer Base-URL für Bilder, die relativen Pfade verwenden
            if (!html.Contains("<base "))
            {
                html = Regex.Replace(html, @"<head>", "<head><base target=\"_blank\">", RegexOptions.IgnoreCase);
            }

            return html;
        }

        // Hilfsmethode zur Auflösung von Inline-Bildern in HTML
        private string ResolveInlineImagesInHtml(string htmlBody, ICollection<EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            // Finde alle cid: Referenzen im HTML
            var cidMatches = Regex.Matches(htmlBody, @"src\s*=\s*[""']cid:([^""']+)[""']", RegexOptions.IgnoreCase);

            foreach (Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                // Finde den entsprechenden Attachment
                var attachment = attachments.FirstOrDefault(a => 
                    !string.IsNullOrEmpty(a.ContentId) && 
                    (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

                // Wenn kein Attachment mit dem ContentId gefunden wird, versuche es mit dem Dateinamen
                if (attachment == null)
                {
                    attachment = attachments.FirstOrDefault(a => 
                        !string.IsNullOrEmpty(a.FileName) && 
                        (a.FileName.Equals($"inline_{cid}", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.StartsWith($"inline_{cid}.", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.Contains($"_{cid}")));
                }

                if (attachment != null && attachment.Content != null && attachment.Content.Length > 0)
                {
                    try
                    {
                        // Erstelle eine data URL für das Inline-Bild
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        var dataUrl = $"data:{attachment.ContentType ?? "image/png"};base64,{base64Content}";
                        
                        // Ersetze die cid: Referenz mit der data URL
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"{dataUrl}\"");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to resolve inline image with CID: {Cid}", cid);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find attachment for CID: {Cid}", cid);
                }
            }

            return resultHtml;
        }

        // POST: Emails/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfManagerRequired]
        public async Task<IActionResult> Delete(int id, string returnUrl = null)
        {
            _logger.LogInformation("Admin user requesting to delete email ID: {EmailId}", id);
            
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            if (email == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found for deletion", id);
                TempData["ErrorMessage"] = "Email not found.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }
            
            // Store email information for logging before deletion
            var emailSubject = email.Subject;
            var emailFrom = email.From;
            var emailAccountId = email.MailAccountId;
            
            try
            {
                _context.ArchivedEmails.Remove(email);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Email with ID {EmailId} successfully deleted by admin", id);
                
                // Log the deletion action
                if (_accessLogService != null && _serviceScopeFactory != null)
                {
                    var currentUsername = _authService?.GetCurrentUser(HttpContext);
                    if (!string.IsNullOrEmpty(currentUsername))
                    {
                        await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Deletion,
                            emailId: id,
                            emailSubject: emailSubject.Length > 255 ? emailSubject.Substring(0, 255) : emailSubject,
                            emailFrom: emailFrom.Length > 255 ? emailFrom.Substring(0, 255) : emailFrom,
                            mailAccountId: emailAccountId);
                    }
                }
                
                TempData["SuccessMessage"] = "Email successfully deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting email with ID {EmailId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the email.";
            }
            
            return Redirect(returnUrl ?? Url.Action("Index"));
        }
        
        // POST: Emails/DeleteSelected
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SelfManagerRequired]
        public async Task<IActionResult> DeleteSelected(List<int> ids, string returnUrl = null)
        {
            if (ids == null || !ids.Any())
            {
                TempData["ErrorMessage"] = "No emails selected for deletion.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }
            
            _logger.LogInformation("Admin user requesting to delete {Count} emails", ids.Count);
            
            var deletedCount = 0;
            var errorCount = 0;
            
            try
            {
                foreach (var id in ids)
                {
                    var email = await _context.ArchivedEmails
                        .Include(e => e.MailAccount)
                        .FirstOrDefaultAsync(e => e.Id == id);
                    
                    if (email == null)
                    {
                        _logger.LogWarning("Email with ID {EmailId} not found for deletion", id);
                        errorCount++;
                        continue;
                    }
                    
                    // Store email information for logging before deletion
                    var emailSubject = email.Subject;
                    var emailFrom = email.From;
                    var emailAccountId = email.MailAccountId;
                    
                    try
                    {
                        _context.ArchivedEmails.Remove(email);
                        await _context.SaveChangesAsync();
                        deletedCount++;
                        
                        _logger.LogInformation("Email with ID {EmailId} successfully deleted by admin", id);
                        
                        // Log the deletion action for each email
                        if (_accessLogService != null && _serviceScopeFactory != null)
                        {
                            var currentUsername = _authService?.GetCurrentUser(HttpContext);
                            if (!string.IsNullOrEmpty(currentUsername))
                            {
                                await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Deletion,
                                    emailId: id,
                                    emailSubject: emailSubject.Length > 255 ? emailSubject.Substring(0, 255) : emailSubject,
                                    emailFrom: emailFrom.Length > 255 ? emailFrom.Substring(0, 255) : emailFrom,
                                    mailAccountId: emailAccountId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting email with ID {EmailId}", id);
                        errorCount++;
                    }
                }
                
                // Save all changes at once for better performance
                // await _context.SaveChangesAsync();
                
                if (deletedCount > 0)
                {
                    TempData["SuccessMessage"] = $"{deletedCount} email(s) successfully deleted.";
                }
                
                if (errorCount > 0)
                {
                    TempData["ErrorMessage"] = $"{errorCount} email(s) could not be deleted. Please check the logs.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting selected emails");
                TempData["ErrorMessage"] = "An error occurred while deleting the emails.";
            }
            
            return Redirect(returnUrl ?? Url.Action("Index"));
        }
        
        // POST: Emails/ExportSelected
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportSelected(List<int> ids, string format = "EML", string returnUrl = null)
        {
            if (ids == null || !ids.Any())
            {
                TempData["ErrorMessage"] = "No emails selected for export.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            _logger.LogInformation("ExportSelected called with {Count} emails in {Format} format", ids.Count, format);

            // Check if the selected emails export service is available
            if (_selectedEmailsExportService == null)
            {
                TempData["ErrorMessage"] = "Selected emails export is not available.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            try
            {
                // Parse format parameter
                AccountExportFormat exportFormat;
                if (!Enum.TryParse<AccountExportFormat>(format, true, out exportFormat))
                {
                    exportFormat = AccountExportFormat.EML; // Default fallback
                    _logger.LogWarning("Invalid export format '{Format}' provided, falling back to EML", format);
                }

                // Get current user's allowed accounts for filtering
                List<int> allowedAccountIds = null;
                var authService = HttpContext.RequestServices.GetService<MailArchiver.Services.IAuthenticationService>();
                var userService = HttpContext.RequestServices.GetService<IUserService>();

                if (authService != null && userService != null && !authService.IsCurrentUserAdmin(HttpContext))
                {
                    var username = authService.GetCurrentUser(HttpContext);
                    var user = await userService.GetUserByUsernameAsync(username);
                    if (user != null)
                    {
                        var userAccounts = await userService.GetUserMailAccountsAsync(user.Id);
                        allowedAccountIds = userAccounts.Select(a => a.Id).ToList();
                    }
                }

                // Filter the email IDs based on user's allowed accounts
                if (allowedAccountIds != null && allowedAccountIds.Any())
                {
                    var allowedEmailIds = await _context.ArchivedEmails
                        .Where(e => ids.Contains(e.Id) && allowedAccountIds.Contains(e.MailAccountId))
                        .Select(e => e.Id)
                        .ToListAsync();

                    ids = allowedEmailIds;
                }

                if (!ids.Any())
                {
                    TempData["ErrorMessage"] = "You do not have access to any of the selected emails.";
                    return Redirect(returnUrl ?? Url.Action("Index"));
                }

                // Log the export action
                var currentUsername = _authService?.GetCurrentUser(HttpContext);
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await _accessLogService.LogAccessAsync(currentUsername, AccessLogType.Download, 
                        searchParameters: $"Started export of {ids.Count} selected emails in {exportFormat} format");
                }

                // Queue the export job with selected format
                var userId = HttpContext.User.Identity?.Name ?? "Anonymous";
                var jobId = _selectedEmailsExportService.QueueExport(ids, exportFormat, userId);

                TempData["SuccessMessage"] = $"Export job started with {ids.Count:N0} emails in {exportFormat} format. Job ID: {jobId}";

                return RedirectToAction("SelectedEmailsExportStatus", new { jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start selected emails export");
                TempData["ErrorMessage"] = $"Failed to start export: {ex.Message}";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }
        }
    }
}
