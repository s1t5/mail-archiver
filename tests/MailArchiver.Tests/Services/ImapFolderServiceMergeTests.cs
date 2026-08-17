using MailArchiver.Services.Providers.Imap;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Pure unit tests for <see cref="ImapFolderService.MergeFolderLists{T}"/>,
/// the helper used to build the union of recursive-LIST and per-level
/// folder discovery results.
/// </summary>
public class ImapFolderServiceMergeTests
{
    [Fact]
    public void Merge_DisjointLists_ReturnsUnion()
    {
        var primary = new List<string> { "INBOX", "Sent" };
        var secondary = new List<string> { "travel", "travel/2022" };

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(4, merged.Count);
        Assert.Equal(new[] { "INBOX", "Sent", "travel", "travel/2022" }, merged);
    }

    [Fact]
    public void Merge_OverlappingLists_DedupesByPrimaryKeyKeepingPrimaryOrder()
    {
        var primary = new List<string> { "INBOX", "Sent" };
        var secondary = new List<string> { "Sent", "Drafts", "INBOX" };

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(3, merged.Count);
        Assert.Equal(new[] { "INBOX", "Sent", "Drafts" }, merged);
    }

    [Fact]
    public void Merge_EmptyPrimary_ReturnsSecondary()
    {
        // Simulates a recursive LIST that failed or was discarded entirely.
        var primary = new List<string>();
        var secondary = new List<string> { "INBOX", "travel/2022/NYC" };

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(secondary, merged);
    }

    [Fact]
    public void Merge_EmptySecondary_ReturnsPrimary()
    {
        var primary = new List<string> { "INBOX", "Sent" };
        var secondary = new List<string>();

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(primary, merged);
    }

    [Fact]
    public void Merge_TruncatedPrimary_FillsGapFromSecondary()
    {
        // The core recovery scenario: the recursive LIST stopped mid-response at a
        // special-character folder; the per-level traversal found the missing tail.
        var primary = new List<string> { "INBOX", "travel", "travel/2022" };
        var secondary = new List<string>
        {
            "INBOX", "travel", "travel/2022",
            "travel/2022/NYC", "travel/2023", "travel/2024"
        };

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(6, merged.Count);
        Assert.Contains("travel/2023", merged);
        Assert.Contains("travel/2024", merged);
        Assert.Contains("travel/2022/NYC", merged);
    }

    [Fact]
    public void Merge_SkipsNullItemsAndNullPaths()
    {
        var items = new List<Holder>
        {
            new Holder("INBOX"),
            new Holder(null),
            new Holder("Sent")
        };
        var secondary = new List<Holder>
        {
            new Holder("Sent"),
            null
        };

        var merged = ImapFolderService.MergeFolderLists(items, secondary, h => h?.Path);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new[] { "INBOX", "Sent" }, merged.Select(h => h!.Path));
    }

    [Fact]
    public void Merge_WithinSecondList_duplicatesAreDropped()
    {
        var primary = new List<string>();
        var secondary = new List<string> { "a", "a", "b" };

        var merged = ImapFolderService.MergeFolderLists(primary, secondary, s => s);

        Assert.Equal(new[] { "a", "b" }, merged);
    }

    private class Holder
    {
        public Holder(string? path) => Path = path;
        public string? Path { get; }
    }
}
