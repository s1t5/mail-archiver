using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for the static helpers of <see cref="MailImporter"/>.
/// </summary>
public class MailImporterStaticTests
{
    [Theory]
    [InlineData("Sent", true)]
    [InlineData("INBOX", false)]
    [InlineData("Gesendet", true)]
    [InlineData("Enviados", true)]
    [InlineData("sent items", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOutgoingFolderByName_Variants(string? folder, bool expected)
        => Assert.Equal(expected, MailImporter.IsOutgoingFolderByName(folder!));

    [Theory]
    [InlineData("Drafts", true)]
    [InlineData("draft", true)]
    [InlineData("Brouillons", true)]
    [InlineData("Bozze", true)]
    [InlineData("INBOX", false)]
    [InlineData("Sent", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDraftsFolder_Variants(string? folder, bool expected)
        => Assert.Equal(expected, MailImporter.IsDraftsFolder(folder!));

    [Fact]
    public void DetermineIfOutgoing_FromMatchesAccountAndSentFolder_True()
    {
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("Me", "me@x.com"));
        Assert.True(MailImporter.DetermineIfOutgoing(msg, account, "Sent"));
    }

    [Fact]
    public void DetermineIfOutgoing_FromMatchesAccountButDraftsFolder_False()
    {
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("Me", "me@x.com"));
        Assert.False(MailImporter.DetermineIfOutgoing(msg, account, "Drafts"));
    }

    [Fact]
    public void DetermineIfOutgoing_FromDifferentAndInbox_False()
    {
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("Other", "other@y.com"));
        Assert.False(MailImporter.DetermineIfOutgoing(msg, account, "INBOX"));
    }

    [Fact]
    public void DetermineIfOutgoing_FromDifferentButSentFolder_True()
    {
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("Other", "other@y.com"));
        Assert.True(MailImporter.DetermineIfOutgoing(msg, account, "Sent"));
    }

    [Fact]
    public void DetermineIfOutgoing_NoFromAddressInInbox_False()
    {
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        Assert.False(MailImporter.DetermineIfOutgoing(msg, account, "INBOX"));
    }

    [Fact]
    public void DetermineIfOutgoing_NoFromAddressInSentFolder_True()
    {
        // No From address but Sent folder: (isOutgoingEmail=false || isOutgoingFolder=true) && !drafts => true
        var account = new MailAccount { EmailAddress = "me@x.com" };
        var msg = new MimeMessage();
        Assert.True(MailImporter.DetermineIfOutgoing(msg, account, "Sent"));
    }

    [Fact]
    public void DetermineIfOutgoing_CaseInsensitiveFromMatch_True()
    {
        var account = new MailAccount { EmailAddress = "Me@X.com" };
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("", "me@x.com"));
        Assert.True(MailImporter.DetermineIfOutgoing(msg, account, "INBOX"));
    }

    [Fact]
    public void ExtractRawHeaders_FreshMessage_ReturnsHeaders()
    {
        var msg = new MimeMessage();
        // MimeMessage auto-populates Date and From on construction.
        var result = MailImporter.ExtractRawHeaders(msg);
        Assert.NotNull(result);
        Assert.Contains("Date:", result);
    }

    [Fact]
    public void ExtractRawHeaders_WithHeaders_ReturnsJoinedString()
    {
        var msg = new MimeMessage();
        msg.Subject = "Test";
        msg.From.Add(new MailboxAddress("", "a@x.com"));
        msg.To.Add(new MailboxAddress("", "b@x.com"));
        var result = MailImporter.ExtractRawHeaders(msg);
        Assert.NotNull(result);
        Assert.Contains("Subject: Test", result);
        Assert.Contains("From:", result);
    }

    [Fact]
    public void ExtractRawHeaders_Over100kChars_GetsTruncated()
    {
        var msg = new MimeMessage();
        msg.Headers.Add("X-Long", new string('x', 150_000));
        var result = MailImporter.ExtractRawHeaders(msg);
        Assert.NotNull(result);
        Assert.True(result!.Length <= 100_000 + 100);
        Assert.Contains("[...truncated...]", result);
    }

    [Fact]
    public void ImportResult_Factories_SetCorrectFlags()
    {
        Assert.True(ImportResult.CreateSuccess().Success);
        Assert.True(ImportResult.CreateAlreadyExists().AlreadyExists);
        var failed = ImportResult.CreateFailed("boom");
        Assert.Equal("boom", failed.Error);
        Assert.False(failed.Success);
        Assert.False(failed.AlreadyExists);
    }
}