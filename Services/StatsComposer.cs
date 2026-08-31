using MailArchiver.Models.Api;

namespace MailArchiver.Services;

/// <summary>
/// Fills the stats DTO. The database size describes the whole PostgreSQL instance,
/// not the caller's scope, so it is only reported for admin scope (M5).
/// </summary>
public static class StatsComposer
{
    public static StatsDto Compose(int emails, int accounts, int attachments, long databaseSizeMb, bool adminScope)
        => new()
        {
            Emails = emails,
            Accounts = accounts,
            Attachments = attachments,
            DatabaseSizeInMB = adminScope ? databaseSizeMb.ToString("0") : string.Empty,
        };
}