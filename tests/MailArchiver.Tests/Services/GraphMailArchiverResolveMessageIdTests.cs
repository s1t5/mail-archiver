using MailArchiver.Services.Providers.Graph;
using Microsoft.Graph.Models;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GraphMailArchiver.ResolveMessageId"/>.
/// Ensures the stored Message-ID is always the bracket-free canonical form that
/// the retention-deletion lookup computes via NormalizeMessageId, regardless of
/// whether Graph returns the Message-ID with or without surrounding angle brackets.
/// </summary>
public class GraphMailArchiverResolveMessageIdTests
{
    [Theory]
    [InlineData("<example-message-id@example.com>", "example-message-id@example.com")]
    [InlineData("example-message-id@example.com", "example-message-id@example.com")]
    [InlineData("<<example-message-id@example.com>>", "example-message-id@example.com")]
    [InlineData("<example-message-id@example.com>>", "example-message-id@example.com")]
    [InlineData(" <example-message-id@example.com> ", "example-message-id@example.com")]
    public void ResolveMessageId_BracketedInternetMessageId_ReturnsBareToken(string internetMessageId, string expected)
    {
        var message = new Message { InternetMessageId = internetMessageId, Id = "graph-internal-id" };

        Assert.Equal(expected, GraphMailArchiver.ResolveMessageId(message));
    }

    [Fact]
    public void ResolveMessageId_NoInternetMessageId_FallsBackToGraphId()
    {
        var message = new Message { InternetMessageId = null, Id = "AAMkAGI2" };

        Assert.Equal("AAMkAGI2", GraphMailArchiver.ResolveMessageId(message));
    }

    [Fact]
    public void ResolveMessageId_EmptyInternetMessageId_GeneratesDeterministicFallback()
    {
        var message = new Message
        {
            InternetMessageId = "",
            Id = null,
            From = new Recipient { EmailAddress = new EmailAddress { Address = "from@x.com" } },
            ToRecipients = new List<Recipient> { new() { EmailAddress = new EmailAddress { Address = "to@x.com" } } },
            Subject = "Subject",
            SentDateTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        };

        var resolved = GraphMailArchiver.ResolveMessageId(message);

        Assert.StartsWith("generated-", resolved);
        Assert.EndsWith("@mail-archiver.local", resolved);
        // Same input must produce the same key (deterministic).
        Assert.Equal(resolved, GraphMailArchiver.ResolveMessageId(message));
    }

    [Fact]
    public void ResolveMessageId_NoIdentifiersAtAll_GeneratesDeterministicFallback()
    {
        var message = new Message
        {
            InternetMessageId = null,
            Id = null,
            Subject = "Subject",
            SentDateTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        };

        var resolved = GraphMailArchiver.ResolveMessageId(message);

        Assert.StartsWith("generated-", resolved);
        Assert.EndsWith("@mail-archiver.local", resolved);
    }
}
