using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The per-account exclusion list has always used three matching rules, and the installation-wide
/// list must use exactly the same ones — two sources compared two different ways would be a bug
/// nobody notices until a folder is archived that should not have been.
///
/// So the first block pins the pre-existing account-only behaviour, the second checks the global
/// list on its own, and the third checks that they are additive rather than one overriding the
/// other. The default has to stay "exclude nothing", because that is what makes the option
/// invisible to installations that never set it.
/// </summary>
public class FolderExclusionMatcherTests
{
    private static bool Excluded(
        string fullName,
        string name,
        IEnumerable<string>? account = null,
        IEnumerable<string>? global = null)
        => FolderExclusionMatcher.IsExcluded(fullName, name, account, global);

    // ---- default: nothing configured excludes nothing --------------------------------------

    [Fact]
    public void No_lists_at_all_excludes_nothing()
    {
        Assert.False(Excluded("INBOX/Kalender", "Kalender"));
    }

    [Fact]
    public void Empty_lists_exclude_nothing()
    {
        Assert.False(Excluded("INBOX/Kalender", "Kalender",
            account: new List<string>(), global: new List<string>()));
    }

    // ---- the pre-existing account-only rules ------------------------------------------------

    [Fact]
    public void Account_list_matches_the_full_path_exactly()
    {
        Assert.True(Excluded("INBOX/Drafts", "Drafts", account: new[] { "INBOX/Drafts" }));
    }

    [Fact]
    public void Account_list_matches_the_short_name_exactly()
    {
        Assert.True(Excluded("INBOX/Drafts", "Drafts", account: new[] { "Drafts" }));
    }

    [Fact]
    public void Account_list_matches_a_dot_separated_suffix()
    {
        Assert.True(Excluded("INBOX.Drafts", "Drafts", account: new[] { "Drafts" }));
    }

    [Fact]
    public void Account_list_matches_a_gmail_style_name()
    {
        Assert.True(Excluded("[Gmail]/Drafts", "[Gmail]/Drafts", account: new[] { "Drafts" }));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        Assert.True(Excluded("INBOX/Drafts", "Drafts", account: new[] { "drafts" }));
        Assert.True(Excluded("INBOX/Drafts", "Drafts", global: new[] { "DRAFTS" }));
    }

    [Fact]
    public void An_unrelated_folder_is_not_excluded()
    {
        Assert.False(Excluded("INBOX/Projects", "Projects", account: new[] { "Drafts" }));
    }

    [Fact]
    public void A_similarly_named_folder_is_not_excluded_by_a_partial_word()
    {
        // "Kalender" must not take "Kalenderwoche" with it: the suffix rule anchors on the
        // separator, and the name rule is an exact match.
        Assert.False(Excluded("INBOX/Kalenderwoche", "Kalenderwoche", global: new[] { "Kalender" }));
    }

    // ---- the global list on its own ---------------------------------------------------------

    [Fact]
    public void Global_list_matches_the_full_path_exactly()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender", global: new[] { "INBOX/Kalender" }));
    }

    [Fact]
    public void Global_list_matches_the_short_name_exactly()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender", global: new[] { "Kalender" }));
    }

    [Fact]
    public void Global_list_matches_a_dot_separated_suffix()
    {
        Assert.True(Excluded("INBOX.Kontakte", "Kontakte", global: new[] { "Kontakte" }));
    }

    // ---- additive, not overriding -----------------------------------------------------------

    [Fact]
    public void Account_entry_still_matches_when_a_global_list_is_configured()
    {
        Assert.True(Excluded("INBOX/Projects", "Projects",
            account: new[] { "Projects" }, global: new[] { "Kalender" }));
    }

    [Fact]
    public void Global_entry_still_matches_when_an_account_list_is_configured()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender",
            account: new[] { "Projects" }, global: new[] { "Kalender" }));
    }

    [Fact]
    public void A_folder_in_neither_list_is_not_excluded()
    {
        Assert.False(Excluded("INBOX/Invoices", "Invoices",
            account: new[] { "Projects" }, global: new[] { "Kalender" }));
    }

    [Fact]
    public void The_same_entry_in_both_lists_is_harmless()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender",
            account: new[] { "Kalender" }, global: new[] { "kalender" }));
    }

    // ---- configuration hygiene --------------------------------------------------------------

    [Fact]
    public void Whitespace_around_a_configured_entry_is_ignored()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender", global: new[] { "  Kalender  " }));
    }

    [Fact]
    public void A_blank_entry_does_not_exclude_everything()
    {
        // A stray empty string in configuration would otherwise match through the suffix rule
        // and silently stop the sync from archiving anything.
        Assert.False(Excluded("INBOX/Invoices", "Invoices", global: new[] { "", "   " }));
    }

    [Fact]
    public void A_null_folder_path_does_not_throw()
    {
        Assert.False(FolderExclusionMatcher.IsExcluded(null, null, null, new[] { "Kalender" }));
    }
}
