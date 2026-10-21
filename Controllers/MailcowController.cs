using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Mailcow;
using MailArchiver.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers
{
    [AdminRequired]
    public class MailcowController : Controller
    {
        private readonly MailArchiverDbContext _db;
        private readonly MailcowApiClient _api;
        private readonly MailcowProvisioningService _provisioning;
        private readonly ILogger<MailcowController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IAccessLogService _accessLog;
        private readonly IAuthenticationService _authService;
        private readonly MailcowOptions _mailcowOptions;

        public MailcowController(MailArchiverDbContext db, MailcowApiClient api,
            MailcowProvisioningService provisioning, ILogger<MailcowController> logger,
            IStringLocalizer<SharedResource> localizer, IAccessLogService accessLog,
            IAuthenticationService authService, IOptions<MailcowOptions> mailcowOptions)
        {
            _db = db;
            _api = api;
            _provisioning = provisioning;
            _logger = logger;
            _localizer = localizer;
            _accessLog = accessLog;
            _authService = authService;
            _mailcowOptions = mailcowOptions.Value;
        }

        public async Task<IActionResult> Index(CancellationToken ct) => View(await _db.MailcowInstances
            .Select(x => new MailcowInstance { Id = x.Id, Name = x.Name, ApiUrl = x.ApiUrl, ImapHost = x.ImapHost, ImapPort = x.ImapPort })
            .OrderBy(x => x.Name).ToListAsync(ct));

        public IActionResult Create() => View(new CreateMailcowInstanceViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMailcowInstanceViewModel model, CancellationToken ct)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _provisioning.RequireEncryption();
                    var uri = MailcowApiClient.ValidateUrl(model.ApiUrl.Trim());
                    var imapHost = model.ImapHost.Trim();
                    if (Uri.CheckHostName(imapHost) == UriHostNameType.Unknown || imapHost.Contains(':'))
                        throw new MailcowException("MailcowImapHostInvalid");
                    var instance = new MailcowInstance
                    {
                        Name = model.Name.Trim(), ApiUrl = uri.AbsoluteUri, ApiKey = model.ApiKey.Trim(),
                        ImapHost = imapHost, ImapPort = model.ImapPort
                    };
                    if (await _db.MailcowInstances.AnyAsync(x => x.ApiUrl == instance.ApiUrl, ct))
                        throw new MailcowException("MailcowInstanceAlreadyConnected");
                    await _api.GetMailboxesAsync(instance, ct);
                    _db.MailcowInstances.Add(instance);
                    await _db.SaveChangesAsync(ct);
                    await LogAsync($"Connected Mailcow instance {instance.Name} ({instance.ApiUrl})");
                    return RedirectToAction(nameof(Mailboxes), new { id = instance.Id });
                }
                catch (MailcowException e) { ModelState.AddModelError("", Localize(e)); }
                catch (DbUpdateException) { ModelState.AddModelError("", _localizer["MailcowInstanceSaveFailed"].Value); }
            }
            // Credentials must never be repopulated into the response HTML or validation errors.
            ModelState.Remove(nameof(model.ApiKey));
            model.ApiKey = string.Empty;
            return View(model);
        }

        public async Task<IActionResult> Mailboxes(int id, CancellationToken ct)
        {
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            var model = new MailcowMailboxesViewModel { InstanceId = id, Name = instance.Name };
            var bindings = await _db.MailcowMailboxes.Include(x => x.MailAccount).Where(x => x.InstanceId == id).ToListAsync(ct);
            model.OrphanedBindings = bindings.Where(x => x.MailAccount == null && !x.IsRevoked).ToList();
            try
            {
                var remote = await _api.GetMailboxesAsync(instance, ct);
                var existing = (await _db.MailAccounts.Where(x => x.Provider == ProviderType.IMAP).Select(x => x.EmailAddress).ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                model.Mailboxes = remote.Select(x => new MailcowMailboxSelection(x,
                    bindings.SingleOrDefault(b => b.EmailAddress == x.Address), existing.Contains(x.Address))).ToList();
                // Keep access revocation available even when a mailbox was removed from Mailcow.
                model.OrphanedBindings.AddRange(bindings.Where(x => !x.IsRevoked && x.MailAccount != null && !remote.Any(r => r.Address == x.EmailAddress)));
            }
            catch (MailcowException e)
            {
                ViewData["Error"] = Localize(e);
                model.OrphanedBindings = bindings.Where(x => !x.IsRevoked).ToList();
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(int id, List<string> selectedMailboxes, CancellationToken ct)
        {
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            var selected = selectedMailboxes.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();
            var maxSelected = _mailcowOptions.MaxSelectedMailboxes;
            if (selected.Count == 0 || selected.Count > maxSelected)
            {
                TempData["ErrorMessage"] = _localizer["MailcowSelectMailboxCount", maxSelected].Value;
                return RedirectToAction(nameof(Mailboxes), new { id });
            }
            var added = 0;
            var failed = 0;
            string? lastError = null;
            foreach (var address in selected)
            {
                try { await _provisioning.ImportAsync(instance, address, ct); added++; }
                catch (MailcowException e)
                {
                    // Do not include response bodies, API keys or passwords in logs or TempData.
                    _logger.LogWarning("Mailcow provisioning failed for instance {InstanceId}", id);
                    lastError = Localize(e);
                    failed++;
                    _db.ChangeTracker.Clear();
                }
                catch (DbUpdateException)
                {
                    _logger.LogWarning("Mailcow provisioning conflicted for instance {InstanceId}", id);
                    lastError = _localizer["MailcowConcurrentChange"].Value;
                    failed++;
                    _db.ChangeTracker.Clear();
                }
            }
            if (lastError != null) TempData["ErrorMessage"] = lastError;
            if (added > 0) TempData["SuccessMessage"] = _localizer["MailcowImportResult", added, failed].Value;
            await LogAsync($"Provisioned {added} Mailcow mailbox(es) on instance {instance.Name}; {failed} failed");
            return RedirectToAction(nameof(Mailboxes), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id, CancellationToken ct)
        {
            var binding = await _db.MailcowMailboxes.Include(x => x.Instance).Include(x => x.MailAccount).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (binding == null) return NotFound();
            try
            {
                await _provisioning.RevokeAsync(binding, ct);
                TempData["SuccessMessage"] = _localizer["MailcowRevokedSuccess"].Value;
                await LogAsync($"Revoked Mailcow app password for {binding.EmailAddress} on instance {binding.Instance?.Name ?? binding.InstanceId.ToString()}");
            }
            catch (MailcowException e) { TempData["ErrorMessage"] = Localize(e); }
            return RedirectToAction(nameof(Mailboxes), new { id = binding.InstanceId });
        }

        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            // The API key is intentionally not copied into the view model.
            return View(new EditMailcowInstanceViewModel
            {
                Id = instance.Id, Name = instance.Name, ApiUrl = instance.ApiUrl,
                ImapHost = instance.ImapHost, ImapPort = instance.ImapPort, ApiKey = null
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditMailcowInstanceViewModel model, CancellationToken ct)
        {
            if (id != model.Id) return NotFound();
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    var uri = MailcowApiClient.ValidateUrl(model.ApiUrl.Trim());
                    var imapHost = model.ImapHost.Trim();
                    if (Uri.CheckHostName(imapHost) == UriHostNameType.Unknown || imapHost.Contains(':'))
                        throw new MailcowException("MailcowImapHostInvalid");
                    if (await _db.MailcowInstances.AnyAsync(x => x.ApiUrl == uri.AbsoluteUri && x.Id != id, ct))
                        throw new MailcowException("MailcowInstanceAlreadyConnected");

                    // Verify the new connection details before persisting them.
                    var candidate = new MailcowInstance
                    {
                        Id = instance.Id, Name = model.Name.Trim(), ApiUrl = uri.AbsoluteUri,
                        ApiKey = string.IsNullOrWhiteSpace(model.ApiKey) ? instance.ApiKey : model.ApiKey.Trim(),
                        ImapHost = imapHost, ImapPort = model.ImapPort
                    };
                    await _api.GetMailboxesAsync(candidate, ct);

                    instance.Name = candidate.Name;
                    instance.ApiUrl = candidate.ApiUrl;
                    instance.ApiKey = candidate.ApiKey;
                    instance.ImapHost = candidate.ImapHost;
                    instance.ImapPort = candidate.ImapPort;
                    await _db.SaveChangesAsync(ct);
                    await LogAsync($"Updated Mailcow instance {instance.Name} ({instance.ApiUrl})");
                    TempData["SuccessMessage"] = _localizer["MailcowInstanceUpdated"].Value;
                    return RedirectToAction(nameof(Index));
                }
                catch (MailcowException e) { ModelState.AddModelError("", Localize(e)); }
                catch (DbUpdateException) { ModelState.AddModelError("", _localizer["MailcowInstanceSaveFailed"].Value); }
            }
            // Credentials must never be repopulated into the response HTML or validation errors.
            ModelState.Remove(nameof(model.ApiKey));
            model.ApiKey = string.Empty;
            return View(model);
        }

        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            var bindings = await _db.MailcowMailboxes.Include(x => x.MailAccount)
                .Where(x => x.InstanceId == id).ToListAsync(ct);
            return View(new DeleteMailcowInstanceViewModel
            {
                Id = instance.Id, Name = instance.Name, ApiUrl = instance.ApiUrl,
                ImapHost = instance.ImapHost, ImapPort = instance.ImapPort,
                BindingCount = bindings.Count,
                ActiveBindingCount = bindings.Count(x => !x.IsRevoked),
                ArchiveAccountCount = bindings.Count(x => x.MailAccount != null)
            });
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
        {
            var instance = await _db.MailcowInstances.FindAsync([id], ct);
            if (instance == null) return NotFound();
            var name = instance.Name;
            try
            {
                await _provisioning.DeleteInstanceAsync(id, ct);
                TempData["SuccessMessage"] = _localizer["MailcowInstanceDeleted"].Value;
                await LogAsync($"Deleted Mailcow instance {name}");
            }
            catch (MailcowException e)
            {
                TempData["ErrorMessage"] = Localize(e);
                return RedirectToAction(nameof(Delete), new { id });
            }
            catch (DbUpdateException)
            {
                _logger.LogWarning("Mailcow instance deletion conflicted for instance {InstanceId}", id);
                TempData["ErrorMessage"] = _localizer["MailcowConcurrentChange"].Value;
                return RedirectToAction(nameof(Delete), new { id });
            }
            return RedirectToAction(nameof(Index));
        }

        private string Localize(MailcowException e) => _localizer[e.ResourceKey, e.ResourceArgs].Value;

        private async Task LogAsync(string description)
        {
            var username = _authService.GetCurrentUserDisplayName(HttpContext);
            if (!string.IsNullOrEmpty(username))
                await _accessLog.LogAccessAsync(username, AccessLogType.Account, searchParameters: description);
        }
    }
}
