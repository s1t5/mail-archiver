using MailArchiver.Attributes;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Mailcow;
using MailArchiver.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Controllers;

[AdminRequired]
public class MailcowController(MailArchiverDbContext db, MailcowApiClient api,
    MailcowProvisioningService provisioning, ILogger<MailcowController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.MailcowInstances
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
                provisioning.RequireEncryption();
                var uri = MailcowApiClient.ValidateUrl(model.ApiUrl.Trim());
                if (Uri.CheckHostName(model.ImapHost) == UriHostNameType.Unknown || model.ImapHost.Contains(':'))
                    throw new MailcowException("Enter an IMAP hostname without a scheme or port.");
                var instance = new MailcowInstance
                {
                    Name = model.Name.Trim(), ApiUrl = uri.AbsoluteUri, ApiKey = model.ApiKey.Trim(),
                    ImapHost = model.ImapHost.Trim(), ImapPort = model.ImapPort
                };
                if (await db.MailcowInstances.AnyAsync(x => x.ApiUrl == instance.ApiUrl, ct))
                    throw new MailcowException("This Mailcow instance is already connected.");
                await api.GetMailboxesAsync(instance, ct);
                db.MailcowInstances.Add(instance);
                await db.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Mailboxes), new { id = instance.Id });
            }
            catch (MailcowException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The instance could not be saved. Check whether it is already connected."); }
        }
        // Credentials must never be repopulated into the response HTML or validation errors.
        ModelState.Remove(nameof(model.ApiKey));
        model.ApiKey = string.Empty;
        return View(model);
    }

    public async Task<IActionResult> Mailboxes(int id, CancellationToken ct)
    {
        var instance = await db.MailcowInstances.FindAsync([id], ct);
        if (instance == null) return NotFound();
        var model = new MailcowMailboxesViewModel { InstanceId = id, Name = instance.Name };
        var bindings = await db.MailcowMailboxes.Include(x => x.MailAccount).Where(x => x.InstanceId == id).ToListAsync(ct);
        model.OrphanedBindings = bindings.Where(x => x.MailAccount == null && !x.IsRevoked).ToList();
        try
        {
            var remote = await api.GetMailboxesAsync(instance, ct);
            var existing = (await db.MailAccounts.Where(x => x.Provider == ProviderType.IMAP).Select(x => x.EmailAddress).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            model.Mailboxes = remote.Select(x => new MailcowMailboxSelection(x,
                bindings.SingleOrDefault(b => b.EmailAddress == x.Address), existing.Contains(x.Address))).ToList();
            // Keep access revocation available even when a mailbox was removed from Mailcow.
            model.OrphanedBindings.AddRange(bindings.Where(x => !x.IsRevoked && x.MailAccount != null && !remote.Any(r => r.Address == x.EmailAddress)));
        }
        catch (MailcowException e)
        {
            ViewData["Error"] = e.Message;
            model.OrphanedBindings = bindings.Where(x => !x.IsRevoked).ToList();
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(int id, List<string> selectedMailboxes, CancellationToken ct)
    {
        var instance = await db.MailcowInstances.FindAsync([id], ct);
        if (instance == null) return NotFound();
        var selected = selectedMailboxes.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();
        if (selected.Count is 0 or > 1000)
        {
            TempData["MailcowError"] = "Select between 1 and 1000 mailboxes.";
            return RedirectToAction(nameof(Mailboxes), new { id });
        }
        var added = 0;
        var failed = 0;
        foreach (var address in selected)
        {
            try { await provisioning.ImportAsync(instance, address, ct); added++; }
            catch (Exception e) when (e is MailcowException or DbUpdateException)
            {
                // Do not include response bodies, API keys or passwords in logs or TempData.
                logger.LogWarning("Mailcow provisioning failed for instance {InstanceId}", id);
                TempData["MailcowError"] = e is MailcowException safe ? safe.Message : "A concurrent change prevented provisioning. Reload the mailbox list and retry.";
                failed++;
                db.ChangeTracker.Clear();
            }
        }
        TempData["MailcowResult"] = $"{added} mailbox(es) ready; {failed} could not be activated. Pending access can be retried or revoked.";
        return RedirectToAction(nameof(Mailboxes), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var binding = await db.MailcowMailboxes.Include(x => x.Instance).Include(x => x.MailAccount).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (binding == null) return NotFound();
        try
        {
            await provisioning.RevokeAsync(binding, ct);
            TempData["MailcowResult"] = "App password revoked. The archive is retained and synchronization is disabled.";
        }
        catch (MailcowException e) { TempData["MailcowError"] = e.Message; }
        return RedirectToAction(nameof(Mailboxes), new { id = binding.InstanceId });
    }
}
