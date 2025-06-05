using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers
{
    public class MailAccountsController : Controller
    {
        private readonly MailArchiverDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<MailAccountsController> _logger;
        private readonly BatchRestoreOptions _batchOptions;

        public MailAccountsController(
            MailArchiverDbContext context,
            IEmailService emailService,
            ILogger<MailAccountsController> logger,
            IOptions<BatchRestoreOptions> batchOptions)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _batchOptions = batchOptions.Value;
        }

        // GET: MailAccounts
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.MailAccounts
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
                    LastSync = a.LastSync
                })
                .ToListAsync();

            return View(accounts);
        }

        // GET: MailAccounts/Details/5
        public async Task<IActionResult> Details(int id)
        {
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
                LastSync = account.LastSync
            };

            return View(model);
        }

        // GET: MailAccounts/Create
        public IActionResult Create()
        {
            var model = new MailAccountViewModel
            {
                ImapPort = 993, // Standard values
                UseSSL = true
            };

            return View(model);
        }

        // POST: MailAccounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MailAccountViewModel model)
        {
            if (ModelState.IsValid)
            {
                var account = new MailAccount
                {
                    Name = model.Name,
                    EmailAddress = model.EmailAddress,
                    ImapServer = model.ImapServer,
                    ImapPort = model.ImapPort,
                    Username = model.Username,
                    Password = model.Password,
                    UseSSL = model.UseSSL,
                    IsEnabled = model.IsEnabled,
                    LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                try
                {
                    _logger.LogInformation("Testing connection for new account: {Name}, Server: {Server}:{Port}",
                        model.Name, model.ImapServer, model.ImapPort);

                    // Test connection before saving
                    var connectionResult = await _emailService.TestConnectionAsync(account);

                    if (!connectionResult)
                    {
                        _logger.LogWarning("Connection test failed for account {Name}", model.Name);
                        ModelState.AddModelError("", "Connection to email server could not be established. Please check your settings and ensure the server is reachable.");
                        return View(model);
                    }

                    _logger.LogInformation("Connection test successful, saving account");

                    _context.MailAccounts.Add(account);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Email account created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating email account: {Message}", ex.Message);
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                    return View(model);
                }
            }

            // Wenn ModelState ungültig ist, zurück zur Ansicht mit Fehlern
            return View(model);
        }

        // GET: MailAccounts/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
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
                LastSync = account.LastSync
            };

            return View(model);
        }

        // MailAccountsController.cs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEnabled(int id)
        {
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
                ? $"Account '{account.Name}' has been enabled for synchronization."
                : $"Account '{account.Name}' has been disabled for synchronization.";

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

                    // Test connection before saving
                    if (!string.IsNullOrEmpty(model.Password))
                    {
                        var connectionResult = await _emailService.TestConnectionAsync(account);
                        if (!connectionResult)
                        {
                            ModelState.AddModelError("", "Connection to email server could not be established. Please check your settings.");
                            return View(model);
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Email account updated successfully.";
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
            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            var model = new MailAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                EmailAddress = account.EmailAddress
            };

            return View(model);
        }

        // POST: MailAccounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
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

                TempData["SuccessMessage"] = $"Email account and {emailCount} related emails have been successfully deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: MailAccounts/Sync/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync(int id)
        {
            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            try
            {
                await _emailService.SyncMailAccountAsync(account);
                TempData["SuccessMessage"] = $"Synchronization for {account.Name} was successfully completed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing account {AccountName}: {Message}", account.Name, ex.Message);
                TempData["ErrorMessage"] = $"Error during synchronization: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: MailAccounts/MoveAllEmails/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveAllEmails(int id)
        {
            var account = await _context.MailAccounts.FindAsync(id);
            if (account == null) return NotFound();

            var emailIds = await _context.ArchivedEmails
                .Where(e => e.MailAccountId == id)
                .Select(e => e.Id)
                .ToListAsync();

            if (!emailIds.Any())
            {
                TempData["ErrorMessage"] = "No emails found to copy for this account.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _logger.LogInformation("Account {AccountId} has {Count} emails. Thresholds: Async={AsyncThreshold}, MaxAsync={MaxAsync}",
                id, emailIds.Count, _batchOptions.AsyncThreshold, _batchOptions.MaxAsyncEmails);

            // Prüfe absolute Limits
            if (emailIds.Count > _batchOptions.MaxAsyncEmails)
            {
                TempData["ErrorMessage"] = $"Too many emails in this account ({emailIds.Count:N0}). Maximum allowed is {_batchOptions.MaxAsyncEmails:N0} emails per operation. Please use manual selection with smaller batches.";
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
    }
}