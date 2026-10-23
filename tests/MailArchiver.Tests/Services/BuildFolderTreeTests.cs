using MailArchiver.Models.ViewModels;
using MailArchiver.Services.Core;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Pure unit tests for <see cref="EmailCoreService.BuildFolderTree"/>.
/// These tests do not require a database connection.
/// </summary>
public class BuildFolderTreeTests
{
    private static List<FolderTreeNode> Flatten(List<FolderTreeNode> nodes)
    {
        var result = new List<FolderTreeNode>();
        foreach (var node in nodes)
        {
            result.Add(node);
            if (node.Children.Any())
                result.AddRange(Flatten(node.Children));
        }
        return result;
    }

    private static FolderTreeNode? FindByFullPath(List<FolderTreeNode> nodes, string fullPath)
        => Flatten(nodes).FirstOrDefault(n => string.Equals(n.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void EmptyList_ReturnsEmptyTree()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>());
        Assert.Empty(tree);
    }

    [Fact]
    public void SingleRootFolder_ReturnsOneNode()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)> { ("INBOX", 5) });

        var node = Assert.Single(tree);
        Assert.Equal("INBOX", node.Name);
        Assert.Equal("INBOX", node.FullPath);
        Assert.Equal(5, node.TotalCount);
        Assert.Equal(0, node.Level);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void TwoRootFolders_ReturnsTwoNodes()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("INBOX", 5),
            ("Sent", 3)
        });

        Assert.Equal(2, tree.Count);
        Assert.Contains(tree, n => n.Name == "INBOX");
        Assert.Contains(tree, n => n.Name == "Sent");
    }

    [Fact]
    public void ParentAndChild_WithSlashSeparator_BuildsHierarchy()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel", 0),
            ("travel/2022", 0),
            ("travel/2022/France", 10),
            ("travel/2022/Germany", 5)
        });

        var travel = FindByFullPath(tree, "travel");
        Assert.NotNull(travel);
        Assert.Equal(0, travel!.Level);

        var year2022 = Assert.Single(travel.Children);
        Assert.Equal("2022", year2022.Name);
        Assert.Equal("travel/2022", year2022.FullPath);
        Assert.Equal(1, year2022.Level);

        Assert.Equal(2, year2022.Children.Count);
        Assert.Contains(year2022.Children, c => c.Name == "France" && c.FullPath == "travel/2022/France" && c.Level == 2);
        Assert.Contains(year2022.Children, c => c.Name == "Germany" && c.FullPath == "travel/2022/Germany" && c.Level == 2);
    }

    [Fact]
    public void ChildWithoutParentInData_CreatesIntermediateNodes()
    {
        // "travel/2022/France" exists but "travel" and "travel/2022" do NOT exist in the data.
        // This simulates container folders that were never stored because they contain no emails.
        // BuildFolderTree creates empty intermediate nodes so the full hierarchy is displayed.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel/2022/France", 10)
        });

        var travel = Assert.Single(tree);
        Assert.Equal("travel", travel.Name);
        Assert.Equal("travel", travel.FullPath);
        Assert.Equal(0, travel.TotalCount);
        Assert.Equal(0, travel.Level);

        var year2022 = Assert.Single(travel.Children);
        Assert.Equal("2022", year2022.Name);
        Assert.Equal("travel/2022", year2022.FullPath);
        Assert.Equal(0, year2022.TotalCount);
        Assert.Equal(1, year2022.Level);

        var france = Assert.Single(year2022.Children);
        Assert.Equal("France", france.Name);
        Assert.Equal("travel/2022/France", france.FullPath);
        Assert.Equal(10, france.TotalCount);
        Assert.Equal(2, france.Level);
    }

    [Fact]
    public void CaseInsensitiveFolders_MergeCounts()
    {
        // "INBOX" and "Inbox" from different accounts should merge into one node.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("INBOX", 5),
            ("Inbox", 3)
        });

        var node = Assert.Single(tree);
        Assert.Equal(8, node.TotalCount);
    }

    [Fact]
    public void DotSeparator_BuildsHierarchy()
    {
        // Some IMAP servers use '.' as hierarchy separator (e.g. Courier, Dovecot with legacy config).
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("INBOX", 0),
            ("INBOX.Sent", 5),
            ("INBOX.Sent.Work", 2)
        });

        var inbox = FindByFullPath(tree, "INBOX");
        Assert.NotNull(inbox);

        var sent = Assert.Single(inbox!.Children);
        Assert.Equal("Sent", sent.Name);
        Assert.Equal("INBOX.Sent", sent.FullPath);
        Assert.Equal(1, sent.Level);

        var work = Assert.Single(sent.Children);
        Assert.Equal("Work", work.Name);
        Assert.Equal("INBOX.Sent.Work", work.FullPath);
        Assert.Equal(2, work.Level);
    }

    [Fact]
    public void DeepNesting_FiveLevels_BuildsCorrectHierarchy()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel", 0),
            ("travel/2022", 0),
            ("travel/2022/Europe", 0),
            ("travel/2022/Europe/France", 0),
            ("travel/2022/Europe/France/Paris", 7)
        });

        var paris = FindByFullPath(tree, "travel/2022/Europe/France/Paris");
        Assert.NotNull(paris);
        Assert.Equal(4, paris!.Level);
        Assert.Equal(7, paris.TotalCount);
    }

    [Fact]
    public void FolderWithSlashInNameButNoParentAsFolder_NestsUnderCreatedParent()
    {
        // "a/b" contains a slash and "a" does not exist as a separate folder in the data.
        // Since '/' is a hard hierarchy separator, an empty intermediate "a" node is created.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("a/b", 1)
        });

        var parent = Assert.Single(tree);
        Assert.Equal("a", parent.Name);
        Assert.Equal("a", parent.FullPath);
        Assert.Equal(0, parent.Level);
        Assert.Equal(0, parent.TotalCount);

        var child = Assert.Single(parent.Children);
        Assert.Equal("b", child.Name);
        Assert.Equal("a/b", child.FullPath);
        Assert.Equal(1, child.Level);
    }

    [Fact]
    public void DottedFolder_SingleOccurrence_StaysFlat()
    {
        // A single dotted name must NOT be split: "Mr. Smith" is most likely a literal
        // folder name, not a "Mr" container. Dotted prefixes require >=2 siblings.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("Mr. Smith", 3)
        });

        var node = Assert.Single(tree);
        Assert.Equal("Mr. Smith", node.Name);
        Assert.Equal("Mr. Smith", node.FullPath);
        Assert.Equal(3, node.TotalCount);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void DottedPrefix_TwoSiblings_CreatesIntermediateNode()
    {
        // Two folders sharing a dotted prefix are treated as a real hierarchy
        // (dot-separating IMAP servers like Courier/Dovecot legacy).
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("Projects.A", 2),
            ("Projects.B", 3)
        });

        var projects = Assert.Single(tree);
        Assert.Equal("Projects", projects.Name);
        Assert.Equal("Projects", projects.FullPath);
        Assert.Equal(0, projects.TotalCount);

        Assert.Equal(2, projects.Children.Count);
        Assert.Contains(projects.Children, c => c.Name == "A" && c.FullPath == "Projects.A");
        Assert.Contains(projects.Children, c => c.Name == "B" && c.FullPath == "Projects.B");
    }

    [Fact]
    public void IntermediateNodes_AreCaseInsensitive_MergedAcrossCases()
    {
        // "Travel/x" and "travel/x/y" differ only in case of the shared prefix.
        // They must collapse into one "travel" chain, not two parallel trees.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("Travel/x", 1),
            ("travel/x/y", 2)
        });

        var root = Assert.Single(tree);
        Assert.Equal("travel", root.FullPath.ToLowerInvariant());
        var x = Assert.Single(root.Children);
        Assert.Equal("x", x.Name.ToLowerInvariant());
        Assert.Equal(1, x.TotalCount);

        var y = Assert.Single(x.Children);
        Assert.Equal("y", y.Name);
        Assert.Equal(2, y.TotalCount);
    }

    [Fact]
    public void MultipleChildrenUnderDifferentYears_EachYearGetsIntermediateNode()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel/2022/France", 10),
            ("travel/2023/Japan", 4),
            ("travel/2024/NYC", 7)
        });

        var travel = Assert.Single(tree);
        Assert.Equal("travel", travel.Name);
        Assert.Equal(0, travel.TotalCount);

        Assert.Equal(3, travel.Children.Count);
        var y2022 = travel.Children.Single(c => c.FullPath == "travel/2022");
        Assert.Equal("2022", y2022.Name);
        Assert.Equal(0, y2022.TotalCount);
        Assert.Equal("France", Assert.Single(y2022.Children).Name);
    }

    [Fact]
    public void ParentExistsInData_NotDuplicated_WhenAlsoIntermediate()
    {
        // "travel" exists in the data (0 emails) AND has nested children. It must appear once.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel", 0),
            ("travel/2022/France", 10)
        });

        var travel = Assert.Single(tree);
        Assert.Equal("travel", travel.FullPath);
        var y2022 = Assert.Single(travel.Children);
        Assert.Equal("travel/2022", y2022.FullPath);
        Assert.Equal("France", Assert.Single(y2022.Children).Name);
    }

    [Fact]
    public void SpecialFolders_SortedByPriority()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("Trash", 1),
            ("Drafts", 1),
            ("Junk", 1),
            ("INBOX", 1),
            ("Sent", 1),
            ("Custom", 1),
            ("Archive", 1)
        });

        // INBOX should be first, then Drafts, Sent, Junk, Trash, Archive, then custom folders.
        Assert.Equal("INBOX", tree[0].Name);
        Assert.Equal("Drafts", tree[1].Name);
        Assert.Equal("Sent", tree[2].Name);
        Assert.Equal("Junk", tree[3].Name);
        Assert.Equal("Trash", tree[4].Name);
        Assert.Equal("Archive", tree[5].Name);
        // Custom folders come after special folders, sorted alphabetically.
        Assert.Equal("Custom", tree[6].Name);
    }

    [Fact]
    public void CountsAreSummed_WhenFolderHasDirectCount()
    {
        // A folder can have its own emails AND children with emails.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("INBOX", 10),
            ("INBOX/SubA", 5),
            ("INBOX/SubB", 3)
        });

        var inbox = FindByFullPath(tree, "INBOX");
        Assert.NotNull(inbox);
        Assert.Equal(10, inbox!.TotalCount);
        Assert.Equal(2, inbox.Children.Count);

        var subA = inbox.Children.First(c => c.Name == "SubA");
        Assert.Equal(5, subA.TotalCount);
        Assert.Equal(1, subA.Level);
    }

    [Fact]
    public void SiblingFolders_AtSameLevel_AreSortedAlphabetically()
    {
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel", 0),
            ("travel/Zimbabwe", 1),
            ("travel/Argentina", 1),
            ("travel/Brazil", 1)
        });

        var travel = FindByFullPath(tree, "travel");
        Assert.NotNull(travel);
        var childNames = travel!.Children.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "Argentina", "Brazil", "Zimbabwe" }, childNames);
    }

    [Fact]
    public void FolderNameWithParentheses_NotSplit()
    {
        // Folder names containing special characters like parentheses should not be
        // treated differently — parentheses are not hierarchy separators.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("NYC (work, June)", 5)
        });

        var node = Assert.Single(tree);
        Assert.Equal("NYC (work, June)", node.Name);
        Assert.Equal("NYC (work, June)", node.FullPath);
        Assert.Equal(0, node.Level);
    }

    [Fact]
    public void ParentWithSpecialChars_HasChildrenNestedUnderIt()
    {
        // A folder with special characters can still have children if the parent path matches.
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("NYC (work, June)", 0),
            ("NYC (work, June)/Subfolder", 5)
        });

        var parent = FindByFullPath(tree, "NYC (work, June)");
        Assert.NotNull(parent);
        Assert.Equal(0, parent!.Level);

        var child = Assert.Single(parent.Children);
        Assert.Equal("Subfolder", child.Name);
        Assert.Equal("NYC (work, June)/Subfolder", child.FullPath);
        Assert.Equal(1, child.Level);
    }

    [Fact]
    public void MultipleSeparators_FindsNearestExistingParent()
    {
        // "travel/2022/France/Paris" — "travel/2022/France" exists but "travel/2022" also exists.
        // The nearest parent should be "travel/2022/France", not "travel/2022".
        var tree = EmailCoreService.BuildFolderTree(new List<(string, int)>
        {
            ("travel", 0),
            ("travel/2022", 0),
            ("travel/2022/France", 0),
            ("travel/2022/France/Paris", 7)
        });

        var france = FindByFullPath(tree, "travel/2022/France");
        Assert.NotNull(france);
        var paris = Assert.Single(france!.Children);
        Assert.Equal("Paris", paris.Name);
        Assert.Equal(3, paris.Level);
    }
}