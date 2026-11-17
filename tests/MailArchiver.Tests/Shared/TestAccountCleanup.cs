using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Removes the rows a test created, so a run leaves the test database as it found it.
/// <para>
/// Scoped by the account ids the test owns, never by a pattern such as the email domain.
/// xUnit runs test classes in parallel, and a pattern-based cleanup deletes another class's
/// rows while its test is still running.
/// </para>
/// <para>
/// Unlocks before deleting: the compliance trigger refuses to delete a locked email, and a
/// single locked row makes the whole <c>SaveChanges</c> fail, which used to leave every row
/// of the batch behind. The exception is deliberately not swallowed; a cleanup that cannot
/// remove its rows must fail the test that produced them instead of surfacing a run later as
/// a dashboard test that sees foreign data.
/// </para>
/// </summary>
internal static class TestAccountCleanup
{
    /// <summary>
    /// Deletes the given accounts with their emails, storage caches, backfill states and
    /// user links. Ids that are zero or duplicated are ignored, so a test that failed before
    /// creating its account can call this unconditionally.
    /// </summary>
    public static async Task DeleteAccountsAsync(MailArchiverDbContext context, params int[] accountIds)
    {
        var ids = accountIds.Where(id => id != 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        // Unlock first, the way the retention path does. The UPDATE trigger ignores
        // IsLocked, so this is allowed even on a locked row.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE mail_archiver.""ArchivedEmails"" SET ""IsLocked"" = false
               WHERE ""MailAccountId"" = ANY({ids})");

        context.ArchivedEmails.RemoveRange(
            await context.ArchivedEmails.Where(e => ids.Contains(e.MailAccountId)).ToListAsync());

        context.AccountStorageCaches.RemoveRange(
            await context.AccountStorageCaches.Where(c => ids.Contains(c.MailAccountId)).ToListAsync());

        context.AccountStorageBackfillStates.RemoveRange(
            await context.AccountStorageBackfillStates.Where(s => ids.Contains(s.MailAccountId)).ToListAsync());

        context.UserMailAccounts.RemoveRange(
            await context.UserMailAccounts.Where(u => ids.Contains(u.MailAccountId)).ToListAsync());

        context.MailAccounts.RemoveRange(
            await context.MailAccounts.Where(a => ids.Contains(a.Id)).ToListAsync());

        await context.SaveChangesAsync();
    }
}
