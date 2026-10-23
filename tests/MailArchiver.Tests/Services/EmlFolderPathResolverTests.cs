using MailArchiver.Services.Providers.Eml;
using Xunit;

namespace MailArchiver.Tests.Services;

public class EmlFolderPathResolverTests
{
    [Fact]
    public void Resolve_NestedPath_ReturnsFullHierarchy()
    {
        var result = EmlFolderPathResolver.Resolve("Test Folder/Lectures/_AD 2006/message-1-34393.eml");

        Assert.Equal("Test Folder/Lectures/_AD 2006", result);
    }

    [Fact]
    public void Resolve_TwoLevels_KeepsBothLevels()
    {
        var result = EmlFolderPathResolver.Resolve("Inbox/Subfolder/message.eml");

        Assert.Equal("Inbox/Subfolder", result);
    }

    [Fact]
    public void Resolve_SingleParentDirectory_KeepsIt()
    {
        var result = EmlFolderPathResolver.Resolve("Lectures/message-1-188182.eml");

        Assert.Equal("Lectures", result);
    }

    [Fact]
    public void Resolve_RootEntry_FallsBackToInbox()
    {
        Assert.Equal("INBOX", EmlFolderPathResolver.Resolve("message-1-188182.eml"));
        Assert.Equal("INBOX", EmlFolderPathResolver.Resolve("archive/message.eml".Replace("archive/", "")));
    }

    [Fact]
    public void Resolve_EmptyOrNull_FallsBackToInbox()
    {
        Assert.Equal("INBOX", EmlFolderPathResolver.Resolve(null));
        Assert.Equal("INBOX", EmlFolderPathResolver.Resolve(string.Empty));
    }

    [Fact]
    public void Resolve_WindowsBackslashPaths_NormalizedToSlash()
    {
        var result = EmlFolderPathResolver.Resolve("Test Folder\\Lectures\\_AD 2006\\message.eml");

        Assert.Equal("Test Folder/Lectures/_AD 2006", result);
    }

    [Fact]
    public void Resolve_MixedSeparators_NormalizedToSlash()
    {
        var result = EmlFolderPathResolver.Resolve("Test Folder/Lectures\\_AD 2006/message.eml");

        Assert.Equal("Test Folder/Lectures/_AD 2006", result);
    }

    [Fact]
    public void Resolve_SegmentsAreTrimmed()
    {
        var result = EmlFolderPathResolver.Resolve(" Test Folder / Lectures /message.eml");

        Assert.Equal("Test Folder/Lectures", result);
    }

    [Fact]
    public void Resolve_EmptySegments_Removed()
    {
        var result = EmlFolderPathResolver.Resolve("Test Folder//_AD 2006//message.eml");

        Assert.Equal("Test Folder/_AD 2006", result);
    }

    [Fact]
    public void Resolve_DotAndDotDotSegments_Removed()
    {
        // "." and ".." are dropped as unsafe segments (no parent resolution) — the
        // archive stores plain folder labels, and the folder tree rejects ".." anyway.
        var result = EmlFolderPathResolver.Resolve("Test Folder/./Lectures/../_AD 2006/message.eml");

        Assert.Equal("Test Folder/Lectures/_AD 2006", result);
    }

    [Fact]
    public void Resolve_DirectoryTraversalOnly_FallsBackToInbox()
    {
        var result = EmlFolderPathResolver.Resolve("../../message.eml");

        Assert.Equal("INBOX", result);
    }

    [Fact]
    public void Resolve_TrailingSlashEntry_KeepsDirectoryPath()
    {
        var result = EmlFolderPathResolver.Resolve("Test Folder/Lectures/");

        Assert.Equal("Test Folder/Lectures", result);
    }

    [Fact]
    public void Resolve_OnlyFileNameWithNoDirectory_FallsBackToInbox()
    {
        var result = EmlFolderPathResolver.Resolve("message.eml");

        Assert.Equal("INBOX", result);
    }

    [Fact]
    public void Resolve_OverlongPath_TruncatedFromTheLeft()
    {
        var segments = new List<string>();
        for (var i = 0; i < 30; i++)
            segments.Add($"level-{i:D2}-with-a-reasonably-long-folder-name");
        var entry = string.Join("/", segments) + "/message.eml";

        var result = EmlFolderPathResolver.Resolve(entry);

        Assert.True(result.Length <= 490, $"resolved path too long: {result.Length}");
        Assert.StartsWith("level-", result);
        // Deepest segment (leaf) must survive truncation.
        Assert.EndsWith("level-29-with-a-reasonably-long-folder-name", result);
    }

    [Fact]
    public void Resolve_PathAtLimit_PassesThroughUnchanged()
    {
        var leaf = "leaf";
        var root = new string('r', 490 - leaf.Length - 1);

        var result = EmlFolderPathResolver.Resolve($"{root}/{leaf}/message.eml");

        Assert.Equal($"{root}/{leaf}", result);
    }
}