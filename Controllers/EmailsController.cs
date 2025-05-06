using MailArchiver.Data;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
                new SelectListItem { Text = "Alle Konten", Value = "" }
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



        // Hilfsmethode zur Bereinigung von HTML für die sichere Darstellung
        private string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Entfernen von potenziellen JavaScript-Elementen
            html = Regex.Replace(html, @"<script.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Entfernen von event handlers
            html = Regex.Replace(html, @"(on\w+)=([""']).*?\2", "", RegexOptions.IgnoreCase);

            // Entfernen von javascript: URLs
            html = Regex.Replace(html, @"href=([""'])javascript:.*?\1", "href=\"#\"", RegexOptions.IgnoreCase);

            // Einfügen einer Base-URL für Bilder, die relativen Pfade verwenden
            html = Regex.Replace(html, @"<head>", "<head><base target=\"_blank\">", RegexOptions.IgnoreCase);

            return html;
        }
    }
}