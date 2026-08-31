using MailArchiver.Models.Api;
using MailArchiver.Services;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The database size describes the whole PostgreSQL instance, not the caller's scope,
/// so it must only be reported for admin scope (M5).
/// </summary>
public class StatsScopeTests
{
    [Fact]
    public void ComposeStats_AdminScope_ReportsDatabaseSize()
    {
        var dto = StatsComposer.Compose(
            emails: 10, accounts: 2, attachments: 5, databaseSizeMb: 4242, adminScope: true);
        Assert.Equal("4242", dto.DatabaseSizeInMB);
    }

    [Fact]
    public void ComposeStats_RestrictedScope_HidesDatabaseSize()
    {
        var dto = StatsComposer.Compose(
            emails: 10, accounts: 2, attachments: 5, databaseSizeMb: 4242, adminScope: false);
        Assert.Equal(string.Empty, dto.DatabaseSizeInMB);
    }

    [Fact]
    public void ComposeStats_CopiesTheCounts()
    {
        var dto = StatsComposer.Compose(
            emails: 10, accounts: 2, attachments: 5, databaseSizeMb: 4242, adminScope: true);
        Assert.Equal(10, dto.Emails);
        Assert.Equal(2, dto.Accounts);
        Assert.Equal(5, dto.Attachments);
    }
}