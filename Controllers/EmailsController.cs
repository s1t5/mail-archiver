using MailArchiver.Data;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Web;

namespace MailArchiver.Controllers
{
    public class EmailsController : Controller
    {
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailsController> _logger;

        public EmailsController(
            MailArchiverDbContext context,
            IEmailService emailService,
            ILogger<EmailsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
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
        // Also fix the single email export method:
        public async Task<IActionResult> Export(int id, ExportFormat format = ExportFormat.Eml)
        {
            var email = await _context.ArchivedEmails.FindAsync(id);
            if (email == null)
            {
                return NotFound();
            }

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
                    fileName = $"email-{id}.csv";
                    break;
                case ExportFormat.Json:
                    contentType = "application/json";
                    fileName = $"email-{id}.json";
                    break;
                case ExportFormat.Eml:
                    contentType = "message/rfc822";
                    fileName = $"email-{id}.eml";
                    break;
                default:
                    contentType = "application/octet-stream";
                    fileName = $"email-{id}.bin";
                    break;
            }

            // Add this header to ensure the file is downloaded with the correct name and extension
            Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

            return File(fileBytes, contentType, fileName);
        }

        // Controllers/EmailsController.cs - Neue Methoden hinzufügen

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

        // Controllers/EmailsController.cs - Neue Methoden hinzufügen

        // GET: Emails/BatchRestore
        [HttpGet]
        public async Task<IActionResult> BatchRestore(List<int> ids, string returnUrl = null)
        {
            if (ids == null || !ids.Any())
            {
                TempData["ErrorMessage"] = "No emails selected for batch operation.";
                return Redirect(returnUrl ?? Url.Action("Index"));
            }

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
        // In EmailsController.cs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportSearchResults(ExportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var fileBytes = await _emailService.ExportEmailsAsync(model);

            string contentType;
            string fileName;

            switch (model.Format)
            {
                case ExportFormat.Csv:
                    contentType = "text/csv";
                    fileName = "emails.csv";
                    break;
                case ExportFormat.Json:
                    contentType = "application/json";
                    fileName = "emails.json";
                    break;
                case ExportFormat.Eml:
                    contentType = "message/rfc822";
                    fileName = "email.eml";
                    break;
                default:
                    contentType = "application/octet-stream";
                    fileName = "export.dat";
                    break;
            }

            // Add this header to ensure the file is downloaded with the correct name and extension
            Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

            return File(fileBytes, contentType, fileName);
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