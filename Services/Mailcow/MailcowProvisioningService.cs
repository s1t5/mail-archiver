using System.Security.Cryptography;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Security;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Mailcow;

public class MailcowProvisioningService(MailArchiverDbContext db, MailcowApiClient api,
    ICredentialProtector protector)
{
    // Keep 256 random bits while satisfying Mailcow's configurable character-class rules.
    internal static string GenerateAppPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";

    public void RequireEncryption()
    {
        if (!protector.IsEnabled)
            throw new MailcowException("MailcowEncryptionRequired");
    }

    public virtual async Task VerifyImapAsync(MailcowInstance instance, string address, string password, CancellationToken ct)
    {
        using var imap = new ImapClient { Timeout = 30000 };
        try
        {
            await imap.ConnectAsync(instance.ImapHost, instance.ImapPort, SecureSocketOptions.SslOnConnect, ct);
            await imap.AuthenticateAsync(address, password, ct);
            await imap.DisconnectAsync(true, ct);
        }
        catch (Exception e) when (e is not MailcowException)
        {
            throw new MailcowException("MailcowImapVerificationFailed");
        }
    }

    public async Task ImportAsync(MailcowInstance instance, string address, CancellationToken ct)
        => await SerializeAsync(instance.Id, () => ImportCoreAsync(instance, address, ct), ct);

    // Session advisory locks serialize provisioning/revocation across application replicas.
    // Each SaveChanges remains committed, so remote operations are recoverable after a crash.
    private async Task SerializeAsync(int instanceId, Func<Task> operation, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var locked = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(157642001, {instanceId})", ct);
            locked = true;
            await operation();
        }
        finally
        {
            try
            {
                if (locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(157642001, {instanceId})", CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    private async Task ImportCoreAsync(MailcowInstance instance, string address, CancellationToken ct)
    {
        RequireEncryption();
        address = address.Trim().ToLowerInvariant();
        // Revalidate posted selections against the server, including disabled mailbox state.
        var remote = (await api.GetMailboxesAsync(instance, ct)).SingleOrDefault(x => x.Address == address && x.Active)
            ?? throw new MailcowException("MailcowMailboxUnavailable");
        var binding = await db.MailcowMailboxes.Include(x => x.MailAccount)
            .SingleOrDefaultAsync(x => x.InstanceId == instance.Id && x.EmailAddress == address, ct);
        if (binding is { IsRevoked: false, MailAccount.IsEnabled: true }) return;
        if (binding != null && binding.MailAccount == null && !binding.IsRevoked)
            throw new MailcowException("MailcowRevokePreviousPassword");

        if (binding == null || binding.IsRevoked)
        {
            // Do not silently create a second archive for an already configured IMAP mailbox.
            var previousAccountId = binding?.MailAccountId;
            if (await db.MailAccounts.AnyAsync(x => x.EmailAddress.ToLower() == address &&
                x.Provider == ProviderType.IMAP && (!previousAccountId.HasValue || x.Id != previousAccountId.Value), ct))
                throw new MailcowException("MailcowImapAccountExists");
            binding ??= new MailcowMailbox { InstanceId = instance.Id, EmailAddress = address };
            binding.AppName = "MailArchiver-" + Guid.NewGuid().ToString("N");
            binding.AppPasswordId = null;
            binding.IsRevoked = false;
            binding.MailAccount ??= new MailAccount
            {
                Name = instance.Name + " – " + (string.IsNullOrWhiteSpace(remote.Name) ? address : remote.Name),
                EmailAddress = address, Username = address, ImapServer = instance.ImapHost,
                ImapPort = instance.ImapPort, UseSSL = true, Provider = ProviderType.IMAP,
                LastSync = DateTime.UnixEpoch
            };
            binding.MailAccount.IsEnabled = false;
            binding.MailAccount.Password = GenerateAppPassword();
            if (binding.Id == 0) db.MailcowMailboxes.Add(binding);
            // Persist the credential and unique operation name BEFORE contacting Mailcow.
            // Retries can reconcile even when the API times out after creating the password.
            await db.SaveChangesAsync(ct);
        }

        var password = binding.MailAccount!.Password;
        if (string.IsNullOrEmpty(password)) throw new MailcowException("MailcowPasswordUndecryptable");
        var appPasswords = await api.GetAppPasswordsAsync(instance, address, ct);
        var existing = appPasswords.SingleOrDefault(x => x.Name == binding.AppName);
        if (existing == null)
        {
            await api.CreateAppPasswordAsync(instance, address, binding.AppName, password, ct);
            existing = (await api.GetAppPasswordsAsync(instance, address, ct)).SingleOrDefault(x => x.Name == binding.AppName);
        }
        if (existing == null || string.IsNullOrEmpty(existing.Id))
            throw new MailcowException("MailcowAppPasswordIdMissing");
        binding.AppPasswordId = existing.Id;
        await db.SaveChangesAsync(ct);
        await VerifyImapAsync(instance, address, password, ct);
        binding.MailAccount.IsEnabled = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(MailcowMailbox binding, CancellationToken ct)
        => await SerializeAsync(binding.InstanceId, async () =>
        {
            await db.Entry(binding).ReloadAsync(ct);
            if (binding.MailAccount != null) await db.Entry(binding.MailAccount).ReloadAsync(ct);
            await RevokeCoreAsync(binding, ct);
        }, ct);

    private async Task RevokeCoreAsync(MailcowMailbox binding, CancellationToken ct)
    {
        // Stop future syncs first, including when Mailcow is temporarily unreachable.
        if (binding.MailAccount != null) binding.MailAccount.IsEnabled = false;
        await db.SaveChangesAsync(ct);
        var matches = (await api.GetAppPasswordsAsync(binding.Instance, binding.EmailAddress, ct))
            .Where(x => x.Name == binding.AppName).ToList();
        foreach (var app in matches)
        {
            if (string.IsNullOrWhiteSpace(app.Id)) throw new MailcowException("MailcowAppPasswordIdMissing");
            await api.DeleteAppPasswordAsync(binding.Instance, app.Id, ct);
        }
        if (binding.MailAccount != null) binding.MailAccount.Password = null;
        binding.IsRevoked = true;
        binding.AppPasswordId = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes a Mailcow instance together with its mailbox bindings.
    /// Managed app passwords must already be revoked: deleting the instance drops the
    /// encrypted API key, after which Mail Archiver could no longer revoke them remotely.
    /// The archive accounts themselves are kept so archived mail survives.
    /// </summary>
    public async Task DeleteInstanceAsync(int instanceId, CancellationToken ct)
        => await SerializeAsync(instanceId, () => DeleteInstanceCoreAsync(instanceId, ct), ct);

    private async Task DeleteInstanceCoreAsync(int instanceId, CancellationToken ct)
    {
        var instance = await db.MailcowInstances.FindAsync([instanceId], ct)
            ?? throw new MailcowException("MailcowInstanceNotFound");
        var bindings = await db.MailcowMailboxes
            .Include(x => x.MailAccount)
            .Where(x => x.InstanceId == instanceId)
            .ToListAsync(ct);

        // Refuse while any managed app password is still active in Mailcow.
        if (bindings.Any(x => !x.IsRevoked))
            throw new MailcowException("MailcowDeleteRequiresRevoke");

        foreach (var binding in bindings)
        {
            // Disable the archive account but keep its mail and row.
            if (binding.MailAccount != null)
            {
                binding.MailAccount.IsEnabled = false;
                binding.MailAccount.Password = null;
            }
        }
        // MailAccountId uses ON DELETE SET NULL, so bindings can be removed safely.
        db.MailcowMailboxes.RemoveRange(bindings);
        db.MailcowInstances.Remove(instance);
        await db.SaveChangesAsync(ct);
    }
}
