using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Each case here is a way to get the folder pipeline wrong. The order of the three steps
/// (exclude, rename, resolve) is load bearing, and the rename is a segment-boundary prefix
/// rewrite rather than a string match.
/// </summary>
public class FolderMapperTests
{
    private static readonly Dictionary<string, string> SentMap =
        new() { ["Sent Items"] = "Sent" };

    // ------------------------------------------------------------------ rename

    [Fact]
    public void ApplyRenameMap_ExactMatch_IsRewritten()
    {
        Assert.Equal("Sent", FolderMapper.ApplyRenameMap("Sent Items", SentMap));
    }

    [Fact]
    public void ApplyRenameMap_Subfolder_RewritesOnlyThePrefix()
    {
        // A whole-string match would leave this untouched and produce a second sent tree.
        Assert.Equal("Sent/2019", FolderMapper.ApplyRenameMap("Sent Items/2019", SentMap));
    }

    [Fact]
    public void ApplyRenameMap_SubstringButNotSegmentBoundary_IsUntouched()
    {
        // "Sent Items Archive" starts with "Sent Items" as a substring but is a different folder.
        Assert.Equal("Sent Items Archive",
            FolderMapper.ApplyRenameMap("Sent Items Archive", SentMap));
    }

    [Fact]
    public void ApplyRenameMap_IsCaseInsensitive()
    {
        Assert.Equal("Sent/2019", FolderMapper.ApplyRenameMap("sent items/2019", SentMap));
    }

    [Fact]
    public void ApplyRenameMap_BackslashSeparator_IsSplitLikeForwardSlash()
    {
        Assert.Equal("Sent/2019", FolderMapper.ApplyRenameMap(@"Sent Items\2019", SentMap));
    }

    [Fact]
    public void ApplyRenameMap_LongestMatchingPrefixWins()
    {
        var map = new Dictionary<string, string>
        {
            ["Sent Items"] = "Sent",
            ["Sent Items/2019"] = "Archive/2019",
        };
        Assert.Equal("Archive/2019", FolderMapper.ApplyRenameMap("Sent Items/2019", map));
        Assert.Equal("Sent", FolderMapper.ApplyRenameMap("Sent Items", map));
    }

    [Fact]
    public void ApplyRenameMap_EmptyMap_IsIdentity()
    {
        Assert.Equal("Sent Items/2019",
            FolderMapper.ApplyRenameMap("Sent Items/2019", new Dictionary<string, string>()));
        Assert.Equal("Sent Items/2019", FolderMapper.ApplyRenameMap("Sent Items/2019", null));
    }

    [Fact]
    public void ApplyRenameMap_TwoSourcesOntoOneTarget_ResolveIdentically()
    {
        var map = new Dictionary<string, string>
        {
            ["Sent Items"] = "Sent",
            ["Gesendete Elemente"] = "Sent",
        };
        Assert.Equal(FolderMapper.ApplyRenameMap("Sent Items", map),
                     FolderMapper.ApplyRenameMap("Gesendete Elemente", map));
    }

    // ------------------------------------------------------------------ exclusion

    [Fact]
    public void IsExcluded_ExactMatch()
    {
        Assert.True(FolderMapper.IsExcluded("Deleted Items", new[] { "Deleted Items" }));
    }

    [Fact]
    public void IsExcluded_CoversDescendants()
    {
        Assert.True(FolderMapper.IsExcluded("Deleted Items/Old", new[] { "Deleted Items" }));
    }

    [Fact]
    public void IsExcluded_DoesNotMatchOnSubstring()
    {
        Assert.False(FolderMapper.IsExcluded("Deleted Items Archive", new[] { "Deleted Items" }));
    }

    [Fact]
    public void IsExcluded_IsCaseInsensitive()
    {
        Assert.True(FolderMapper.IsExcluded("deleted items/old", new[] { "Deleted Items" }));
    }

    [Fact]
    public void IsExcluded_EmptyExclusions_IsNeverExcluded()
    {
        Assert.False(FolderMapper.IsExcluded("Deleted Items", Array.Empty<string>()));
        Assert.False(FolderMapper.IsExcluded("Deleted Items", null));
    }

    // ------------------------------------------------------------------ target path

    [Fact]
    public void ResolveTargetPath_InboxRoot_UsesRenamedPathAsIs()
    {
        Assert.Equal("Sent/2019", FolderMapper.ResolveTargetPath("Sent/2019", "INBOX", true));
    }

    [Fact]
    public void ResolveTargetPath_OtherRoot_NestsBelowIt()
    {
        Assert.Equal("Archive/Sent/2019",
            FolderMapper.ResolveTargetPath("Sent/2019", "Archive", true));
    }

    [Fact]
    public void ResolveTargetPath_WithoutStructurePreservation_EverythingLandsInTheRoot()
    {
        Assert.Equal("Archive", FolderMapper.ResolveTargetPath("Sent/2019", "Archive", false));
        Assert.Equal("INBOX", FolderMapper.ResolveTargetPath("Sent/2019", "INBOX", false));
    }

    // ------------------------------------------------------------------ full pipeline

    [Fact]
    public void Resolve_ExclusionIsEvaluatedBeforeRename()
    {
        // If the rename ran first, "Deleted Items" would already be "Trash" and an exclusion
        // keyed on the source name would never fire.
        var map = new Dictionary<string, string> { ["Deleted Items"] = "Trash" };
        var excluded = new[] { "Deleted Items" };

        Assert.Null(FolderMapper.Resolve("Deleted Items", "INBOX", true, excluded, map));
        Assert.Null(FolderMapper.Resolve("Deleted Items/Old", "INBOX", true, excluded, map));
    }

    [Fact]
    public void Resolve_TwoSourceFoldersCollapsingOntoOneTarget_ProduceTheSamePath()
    {
        var map = new Dictionary<string, string> { ["Sent Items"] = "Sent" };

        var a = FolderMapper.Resolve("Sent Items", "INBOX", true, null, map);
        var b = FolderMapper.Resolve("Sent", "INBOX", true, null, map);

        Assert.Equal("Sent", a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Resolve_NotExcluded_ReturnsResolvedPath()
    {
        var map = new Dictionary<string, string> { ["Junk E-Mail"] = "Junk" };
        Assert.Equal("Junk", FolderMapper.Resolve("Junk E-Mail", "INBOX", true, null, map));
    }
}
