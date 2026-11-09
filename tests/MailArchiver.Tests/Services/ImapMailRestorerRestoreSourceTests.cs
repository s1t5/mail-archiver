using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Restore follows the same cascade as the single-email export: the stored original wins when
/// one exists, the rebuild is only the fallback, and a stored original that fails its checksum
/// aborts the restore instead of silently replacing it with a rebuilt message.
/// </summary>
public class ImapMailRestorerRestoreSourceTests
{
    private static ImapMailRestorer CreateRestorer(IRawMessageStore? rawMessageStore) => new(
        context: null!,
        NullLogger<ImapMailRestorer>.Instance,
        connectionFactory: null!,
        new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" })),
        Options.Create(new BatchOperationOptions()),
        Options.Create(new OffloadOptions()),
        folderService: null!,
        rawMessageStore);

    private static byte[] CreateOriginalBytes(string subject, string body, string headerValue)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Original Sender", "original@example.com"));
        message.To.Add(new MailboxAddress("Original Recipient", "recipient@example.com"));
        message.Subject = subject;
        message.Headers.Add("X-Original-Header", headerValue);
        message.Body = new TextPart("plain") { Text = body };

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    private static ArchivedEmail CreateEmail(int id) => new()
    {
        Id = id,
        Subject = "Rebuilt subject",
        From = "rebuilt@example.com",
        To = "rebuilt-recipient@example.com",
        Body = "Rebuilt body",
        SentDate = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task CreateRestoreMessage_WithStoredOriginal_UsesOriginalContentAndHeaders()
    {
        var bytes = CreateOriginalBytes("Original subject", "Original body", "original-header-value");
        var store = new Mock<IRawMessageStore>();
        store.Setup(s => s.ReadAsync(42))
            .ReturnsAsync(new RawMessageContent(bytes, "checksum", "imap", "postgres"));

        var message = await CreateRestorer(store.Object).CreateRestoreMessageAsync(CreateEmail(42), "target");

        Assert.Equal("Original subject", message.Subject);
        Assert.Equal("original-header-value", message.Headers["X-Original-Header"]);
        Assert.StartsWith("Original body", message.TextBody);
        Assert.NotEqual("Rebuilt subject", message.Subject);
    }

    [Fact]
    public async Task CreateRestoreMessage_WithoutStoredOriginal_RebuildsFromArchivedFields()
    {
        var store = new Mock<IRawMessageStore>();
        store.Setup(s => s.ReadAsync(42)).ReturnsAsync((RawMessageContent?)null);

        var message = await CreateRestorer(store.Object).CreateRestoreMessageAsync(CreateEmail(42), "target");

        Assert.Equal("Rebuilt subject", message.Subject);
        Assert.Null(message.Headers["X-Original-Header"]);
        Assert.Equal("Rebuilt body", message.TextBody);
    }

    [Fact]
    public async Task CreateRestoreMessage_WithoutRawMessageStore_RebuildsFromArchivedFields()
    {
        var message = await CreateRestorer(null).CreateRestoreMessageAsync(CreateEmail(42), "target");

        Assert.Equal("Rebuilt subject", message.Subject);
    }

    [Fact]
    public async Task CreateRestoreMessage_WithChecksumMismatch_ThrowsInsteadOfFallingBack()
    {
        var store = new Mock<IRawMessageStore>();
        store.Setup(s => s.ReadAsync(42))
            .ThrowsAsync(new RawMessageIntegrityException(42, "expected", "actual"));

        var restorer = CreateRestorer(store.Object);

        await Assert.ThrowsAsync<RawMessageIntegrityException>(
            () => restorer.CreateRestoreMessageAsync(CreateEmail(42), "target"));
    }
}
