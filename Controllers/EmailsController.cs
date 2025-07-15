using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Web;

namespace MailArchiver.Controllers
{
    public class EmailsController : Controller
    {
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailsController> _logger;
        private readonly IBatchRestoreService? _batchRestoreService;
        private readonly BatchRestoreOptions _batchOptions;
        private readonly ISyncJobService _syncJobService;

        public EmailsController(
            MailArchiverDbContext context,
            IEmailService emailService,
            ILogger<EmailsController> logger,
            IOptions<BatchRestoreOptions> batchOptions,
            IBatchRestoreService? batchRestoreService = null,
            ISyncJobService? syncJobService = null)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _batchRestoreService = batchRestoreService;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
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
            if (model.PageSize <= 0) model.PageSize = 20;

            // Konten für die Dropdown-Liste laden
            var accounts = await _context.MailAccounts.ToListAsync();
            model.AccountOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = "All Accounts", Value = "" }
            };
            model.AccountOptions.AddRange(accounts.Select(a =>
                new SelectListItem
                {
                    Text = $"{a.Name} ({a.EmailAddress})",
                    Value = a.Id.ToString(),
                    Selected = model.SelectedAccountId == a.Id
                }));

            // Berechnen der Anzahl zu überspringender Elemente für die Paginierung
            int skip = (model.PageNumber - 1) * model.PageSize;

            // Suche durchführen
            var (emails, totalCount) = await _emailService.SearchEmailsAsync(
                model.SearchTerm,
                model.FromDate,
                model.ToDate,
                model.SelectedAccountId,
                model.IsOutgoing,
                skip,
                model.PageSize);

            model.SearchResults = emails;
            model.TotalResults = totalCount;

            // Log the state of ShowSelectionControls for debugging
            _logger.LogInformation("Selection mode is {SelectionMode}", model.ShowSelectionControls ? "enabled" : "disabled");

            // Batch-Optionen für die View verfügbar machen
            ViewBag.AsyncThreshold = _batchOptions.AsyncThreshold;
            ViewBag.MaxSyncEmails = _batchOptions.MaxSyncEmails;
            ViewBag.MaxAsyncEmails = _batchOptions.MaxAsyncEmails;

            // Aktive Jobs für die View
            if (_batchRestoreService != null)
            {
                var activeJobs = _batchRestoreService.GetActiveJobs();
                ViewBag.ActiveJobsCount = activeJobs.Count;
            }

            return View(model);
        }

        // GET: Emails/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                return NotFound();
            }

            var model = new EmailDetailViewModel
            {
                Email = email,
                AccountName = email.MailAccount?.Name ?? "Unknown account",
                FormattedHtmlBody = SanitizeHtml(email.HtmlBody)
            };

            return View(model);
        }

        // GET: Emails/Attachment/5/1
        public async Task<IActionResult> Attachment(int emailId, int attachmentId)
        {
            var attachment = await _context.EmailAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ArchivedEmailId == emailId);

            if (attachment == null)
            {
                return NotFound();
            }

            return File(attachment.Content, attachment.ContentType, attachment.FileName);
        }

        // GET: Emails/Export/5
        public async Task<IActionResult> Export(int id, ExportFormat format = ExportFormat.Eml)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                return NotFound();
            }

            try
            {
                var exportParams = new ExportViewModel
                {
                    Format = format,
                    EmailId = id
                };

                var fileBytes = await _emailService.ExportEmailsAsync(exportParams);

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
        }

        // GET: Emails/Restore/5
        [HttpGet]
        public async Task<IActionResult> Restore(int id)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                return NotFound();
            }

            // Liste aller aktiven E-Mail-Konten abrufen
            var accounts = await _context.MailAccounts
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.Name)
                .ToListAsync();

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
                // Load folders for this account
                var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
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
                    var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
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

                var result = await _emailService.RestoreEmailToFolderAsync(
                    model.EmailId,
                    model.TargetAccountId,
                    model.TargetFolder);

                if (result)
                {
                    _logger.LogInformation("Email restoration successful");
                    TempData["SuccessMessage"] = "The email has been successfully copied to the specified folder.";
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

            // Get active email accounts for dropdown
            var accounts = await _context.MailAccounts
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.Name)
                .ToListAsync();

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
                // Load folders for this account
                var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
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
                    var folders = await _emailService.GetMailFoldersAsync(model.TargetAccountId);
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

                var (successful, failed) = await _emailService.RestoreMultipleEmailsAsync(
                    model.SelectedEmailIds,
                    model.TargetAccountId,
                    model.TargetFolder);

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

            var useBackgroundJob = ShouldUseBackgroundJob(emailIds.Count);

            if (useBackgroundJob)
            {
                return await StartAsyncBatchRestore(emailIds, returnUrl);
            }
            else
            {
                // Verwende normale Session-basierte Verarbeitung
                try
                {
                    HttpContext.Session.SetString("BatchRestoreIds", string.Join(",", emailIds));
                    HttpContext.Session.SetString("BatchRestoreReturnUrl", returnUrl ?? "");
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

            // Get active email accounts
            var accounts = await _context.MailAccounts
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.Name)
                .ToListAsync();

            if (!accounts.Any())
            {
                TempData["ErrorMessage"] = "No enabled email accounts found.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

            var model = new AsyncBatchRestoreViewModel
            {
                EmailIds = ids,
                ReturnUrl = returnUrl,
                AvailableAccounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} ({a.EmailAddress})"
                }).ToList()
            };

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

                return View("AsyncBatchRestore", model);
            }

            try
            {
                var job = new BatchRestoreJob
                {
                    EmailIds = model.EmailIds,
                    TargetAccountId = model.TargetAccountId,
                    TargetFolder = model.TargetFolder,
                    ReturnUrl = model.ReturnUrl ?? "",
                    UserId = HttpContext.User.Identity?.Name ?? "Anonymous"
                };

                var jobId = _batchRestoreService.QueueJob(job);

                TempData["SuccessMessage"] = $"Batch restore job started with {model.EmailIds.Count:N0} emails. Job ID: {jobId}";

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
            if (_batchRestoreService == null)
            {
                TempData["ErrorMessage"] = "Batch restore service is not available.";
                return RedirectToAction("Index");
            }

            var job = _batchRestoreService.GetJob(jobId);
            if (job == null)
            {
                TempData["ErrorMessage"] = "Batch restore job not found.";
                return RedirectToAction("Index");
            }

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

        // GET: Emails/Jobs
        [HttpGet]
        public IActionResult Jobs()
        {
            var batchJobs = new List<BatchRestoreJob>();
            var syncJobs = new List<SyncJob>();
            var mboxJobs = new List<MBoxImportJob>();

            if (_batchRestoreService != null)
            {
                batchJobs = _batchRestoreService.GetActiveJobs();
            }

            if (_syncJobService != null)
            {
                // Begrenze auf die letzten 20 Sync-Jobs
                syncJobs = _syncJobService.GetAllJobs().Take(20).ToList();
            }

            // MBox Import Jobs hinzufügen
            try
            {
                var mboxService = HttpContext.RequestServices.GetService<IMBoxImportService>();
                if (mboxService != null)
                {
                    mboxJobs = mboxService.GetActiveJobs();
                }
            }
            catch
            {
                // Ignore if service not available
            }

            ViewBag.BatchJobs = batchJobs;
            ViewBag.SyncJobs = syncJobs;
            ViewBag.MBoxJobs = mboxJobs;

            return View(batchJobs);
        }

        // API endpoint for AJAX status updates
        [HttpGet]
        public JsonResult GetBatchRestoreStatus(string jobId)
        {
            if (_batchRestoreService == null)
            {
                return Json(new { error = "Batch restore service not available" });
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
                return Json(new List<string> { "INBOX" });
            }

            try
            {
                var folders = await _emailService.GetMailFoldersAsync(accountId);
                if (folders == null || !folders.Any())
                {
                    _logger.LogWarning("No folders found for account {AccountId}, returning default", accountId);
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

            try
            {
                var fileBytes = await _emailService.ExportEmailsAsync(model);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email export");
                TempData["ErrorMessage"] = $"Export failed: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Emails/RawContent/5
        public async Task<IActionResult> RawContent(int id)
        {
            var email = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.Id == id);

            if (email == null)
            {
                return NotFound();
            }

            // Bereiten Sie das HTML für die direkte Anzeige vor
            string html = !string.IsNullOrEmpty(email.HtmlBody)
                ? SanitizeHtml(email.HtmlBody)
                : $"<pre>{HttpUtility.HtmlEncode(email.Body)}</pre>";

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

            return Content(html, "text/html");
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
    }
}