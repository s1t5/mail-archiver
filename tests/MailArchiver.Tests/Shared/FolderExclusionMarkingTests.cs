using MailArchiver.Models;
using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The matching rules themselves are covered by <see cref="FolderExclusionMatcherTests"/>. What is
/// pinned here is what this helper decides on top of them, because both decisions are invisible in
/// the result and both would fail silently.
///
/// One: which list is consulted. Marking a folder makes it unselectable in the picker, so consulting
/// the account's own list would strike through the entry the user just added and leave no way to
/// take it back. Only the global list is out of reach from that page.
///
/// Two: that the folder's name is used as the provider reported it. The name rule is unanchored, so
/// it is exactly the folders whose name is not the tail of their path that a derived name would
/// judge wrongly, and those are the ones worth showing.
/// </summary>
public class FolderExclusionMarkingTests
{
    private static MailFolderInfo Folder(string fullName, string name)
        => new() { FullName = fullName, Name = name };

    private static IReadOnlyList<MailFolderInfo> Mark(
        IEnumerable<string>? global, params MailFolderInfo[] folders)
    {
        FolderExclusionMarking.MarkGloballyExcluded(folders, global);
        return folders;
    }

    // ---- nothing configured marks nothing ---------------------------------------------------

    [Fact]
    public void No_global_list_marks_nothing()
    {
        var folders = Mark(null, Folder("INBOX/Kalender", "Kalender"));
        Assert.False(folders[0].GloballyExcluded);
    }

    [Fact]
    public void Empty_global_list_marks_nothing()
    {
        var folders = Mark(new List<string>(), Folder("INBOX/Kalender", "Kalender"));
        Assert.False(folders[0].GloballyExcluded);
    }

    [Fact]
    public void A_null_collection_of_folders_is_not_an_error()
    {
        FolderExclusionMarking.MarkGloballyExcluded(null!, new[] { "Kalender" });
    }

    // ---- only the global list decides --------------------------------------------------------

    [Fact]
    public void A_folder_the_global_list_covers_is_marked()
    {
        var folders = Mark(new[] { "Kalender" }, Folder("INBOX/Kalender", "Kalender"));
        Assert.True(folders[0].GloballyExcluded);
    }

    [Fact]
    public void A_folder_no_global_entry_covers_stays_selectable()
    {
        // The account may well exclude this one already. That is the list being edited on this
        // page, and a struck-through entry there could not be taken back.
        var folders = Mark(new[] { "Kalender" }, Folder("INBOX/Drafts", "Drafts"));
        Assert.False(folders[0].GloballyExcluded);
    }

    [Fact]
    public void The_flag_is_assigned_not_accumulated()
    {
        // A folder that arrives pre-marked - a reused DTO, a second pass - must come back honest.
        var stale = Folder("INBOX/Drafts", "Drafts");
        stale.GloballyExcluded = true;

        Mark(new[] { "Kalender" }, stale);

        Assert.False(stale.GloballyExcluded);
    }

    // ---- why the decision is not made in the browser ------------------------------------------

    [Fact]
    public void An_entry_reaches_a_folder_whose_name_merely_ends_with_it()
    {
        // Neither path rule fires here: the path does not end in "/Kontakte". Only the unanchored
        // rule on the folder's own name does, and that is the shape a comparison in JavaScript
        // would be written as an equality and miss.
        var folders = Mark(new[] { "Kontakte" },
            Folder("Kontakte/Unsortierte Kontakte", "Unsortierte Kontakte"));
        Assert.True(folders[0].GloballyExcluded);
    }

    [Fact]
    public void A_folder_that_only_looks_similar_is_left_alone()
    {
        // "Performance" ends a real customer mail folder's name; nothing here may take it out of
        // the picker on a resemblance.
        var folders = Mark(new[] { "Kalender" }, Folder("INBOX/Kalenderwoche", "Kalenderwoche"));
        Assert.False(folders[0].GloballyExcluded);
    }

    // ---- a whole listing at once --------------------------------------------------------------

    [Fact]
    public void Each_folder_is_judged_on_its_own()
    {
        var folders = Mark(new[] { "Kalender", "Kontakte" },
            Folder("INBOX", "INBOX"),
            Folder("INBOX/Kalender", "Kalender"),
            Folder("INBOX/Drafts", "Drafts"),
            Folder("Kontakte/Unsortierte Kontakte", "Unsortierte Kontakte"));

        Assert.Equal(
            new[] { false, true, false, true },
            folders.Select(f => f.GloballyExcluded));
    }
}
