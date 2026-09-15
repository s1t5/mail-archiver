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
        IEnumerable<string>? global = null,
        bool subfolders = true)
        => FolderExclusionMatcher.IsExcluded(fullName, name, account, global, subfolders);

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
        Assert.False(FolderExclusionMatcher.IsExcluded(null, null, null, new[] { "Kalender" }, true));
    }

    // ---- an entry reaches what lies below the folder it names --------------------------------

    [Fact]
    public void An_entry_covers_the_folders_below_it()
    {
        Assert.True(Excluded("Kalender/KfW", "KfW", global: new[] { "Kalender" }));
    }

    [Fact]
    public void An_entry_covers_the_folders_below_it_on_a_dot_separator()
    {
        Assert.True(Excluded("INBOX.Kalender.KfW", "KfW", global: new[] { "Kalender" }));
    }

    [Fact]
    public void An_entry_covers_a_folder_several_levels_down()
    {
        Assert.True(Excluded("Deleted Items/2024/Q1", "Q1", global: new[] { "Deleted Items" }));
    }

    [Fact]
    public void An_entry_written_as_a_path_covers_what_is_below_that_path()
    {
        Assert.True(Excluded("INBOX/Kalender/KfW", "KfW", global: new[] { "INBOX/Kalender" }));
    }

    [Fact]
    public void An_entry_spanning_two_levels_covers_what_is_below_it()
    {
        // Entries like "Sync Issues/Conflicts" are written out because the folder's own name says
        // nothing. They have to reach downwards the same way a single-segment entry does.
        Assert.True(Excluded("INBOX/Sync Issues/Conflicts/Old", "Old",
            global: new[] { "Sync Issues/Conflicts" }));
    }

    // ---- and stops at the separator, which is what keeps real mail folders in ------------------

    [Fact]
    public void A_folder_that_merely_carries_the_word_is_not_covered()
    {
        // A real customer mail folder that happens to contain "Kalender". A substring rule would
        // take it out of the archive without anyone noticing.
        Assert.False(Excluded("INBOX/SDA DO-Kalender Performance", "SDA DO-Kalender Performance",
            global: new[] { "Kalender" }));
    }

    [Fact]
    public void Nothing_below_such_a_folder_is_covered_either()
    {
        Assert.False(Excluded("INBOX/SDA DO-Kalender Performance/2026", "2026",
            global: new[] { "Kalender" }));
    }

    [Fact]
    public void The_unanchored_name_rule_is_not_handed_down_to_children()
    {
        // "AltKalender" itself is excluded, by the name rule, which is unanchored on purpose. Its
        // children are not: ancestors are compared by path only, so the one rule that judges on a
        // resemblance cannot spread down a tree.
        Assert.True(Excluded("INBOX/AltKalender", "AltKalender", global: new[] { "Kalender" }));
        Assert.False(Excluded("INBOX/AltKalender/KfW", "KfW", global: new[] { "Kalender" }));
    }

    [Fact]
    public void A_dot_inside_a_folder_name_is_read_as_a_separator()
    {
        // Known and accepted: from the path alone, "Kalender.ics" on a server that delimits with
        // "/" cannot be told from a folder "ics" below "Kalender". The suffix rule has always read
        // "." the same way, and for an exclusion list taking the folder is the safe direction.
        Assert.True(Excluded("Kalender.ics", "Kalender.ics", global: new[] { "Kalender" }));
    }

    [Fact]
    public void A_trailing_separator_does_not_make_a_folder_its_own_parent()
    {
        // "Underneath" has to mean that something is below. A path ending in a separator has
        // nothing there, and treating it as a parent would let rule 4 answer for a folder the path
        // rules just declined. The entry is written as a path and the folder's own name differs, so
        // rule 4 is the only rule that could speak here.
        Assert.False(Excluded("INBOX/Kalender/", "Kalender", global: new[] { "INBOX/Kalender" }));
    }

    // ---- the switch that turns the reach off ---------------------------------------------------

    [Fact]
    public void With_the_switch_off_an_entry_does_not_reach_below_the_folder_it_names()
    {
        Assert.False(Excluded("Kalender/KfW", "KfW", global: new[] { "Kalender" }, subfolders: false));
        Assert.False(Excluded("INBOX.Kalender.KfW", "KfW", global: new[] { "Kalender" }, subfolders: false));
    }

    [Fact]
    public void With_the_switch_off_the_folder_the_entry_names_is_still_excluded()
    {
        Assert.True(Excluded("INBOX/Kalender", "Kalender", global: new[] { "Kalender" }, subfolders: false));
        Assert.True(Excluded("INBOX.Kontakte", "Kontakte", account: new[] { "Kontakte" }, subfolders: false));
    }

    // ---- which entry did it ---------------------------------------------------------------------

    [Fact]
    public void The_entry_responsible_is_reported()
    {
        // The exclusion editor names it. For a folder below an excluded one the entry is neither
        // the folder's name nor its path, so "excluded" on its own would not be actionable.
        Assert.Equal("Deleted Items", FolderExclusionMatcher.FindCoveringEntry(
            "Deleted Items/2024/Q1", "Q1", null, new[] { "Kalender", "Deleted Items" }, true));
    }

    [Fact]
    public void Nothing_is_reported_when_no_entry_matches()
    {
        Assert.Null(FolderExclusionMatcher.FindCoveringEntry(
            "INBOX/Invoices", "Invoices", null, new[] { "Kalender" }, true));
    }

    [Fact]
    public void The_account_entry_is_reported_when_both_lists_would_match()
    {
        // Both lists are searched, the account's own first, so the user is pointed at the list they
        // can actually edit.
        Assert.Equal("Kalender", FolderExclusionMatcher.FindCoveringEntry(
            "Kalender/KfW", "KfW", new[] { "Kalender" }, new[] { "INBOX/Kalender" }, true));
    }

    // ---- the two Graph inconsistencies this matcher is meant to remove -----------------------

    [Fact]
    public void An_entry_written_as_a_path_matches_even_when_the_short_name_does_not()
    {
        // Graph's retention deletion compared the folder's own name only, so an exclusion given
        // as a path kept a folder out of the sync but not out of the deletion. Both paths now
        // pass the full path, so the same entry decides both.
        Assert.True(Excluded("Inbox/Kalender", "Kalender", account: new[] { "Inbox/Kalender" }));
        Assert.False(Excluded("Inbox/Kalender", "Kalender", account: new[] { "Posteingang/Kalender" }));
    }

    [Fact]
    public void A_short_name_matches_a_nested_graph_folder_by_suffix()
    {
        // Graph previously compared exact strings only, so "Kalender" did not match a nested
        // "Inbox/Kalender" unless it was spelled out in full. It does now, the same way it always
        // did for IMAP. This can exclude more than before — never less.
        Assert.True(Excluded("Inbox/Kalender", "Kalender", global: new[] { "Kalender" }));
    }

    [Fact]
    public void An_empty_display_name_still_matches_on_the_full_path()
    {
        // The Graph sync path used to skip the whole check when DisplayName was empty. A folder
        // whose path matches an exclusion entry should be excluded regardless.
        Assert.True(Excluded("Inbox/Kalender", "", global: new[] { "Inbox/Kalender" }));
    }
}
