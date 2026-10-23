using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// MailKit 4.17.0 throws ArgumentException when MessageId is set to an empty string.
/// The export paths used to assign NormalizeMessageId(...) unguarded; rows whose stored
/// Message-ID reduces to empty crashed the export (M3).
/// </summary>
public class MailContentHelperApplyMessageIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<>")]
    [InlineData(">>")]
    [InlineData("<<>>")]
    public void ApplyRestorableMessageId_EmptyNormalized_DoesNotThrowAndDoesNotApplyStored(string? stored)
    {
        // The getter invents a value when none was set (MimeKit generates one on write),
        // so null is not observable. What is observable: the applied id differs between
        // two fresh messages, i.e. it is MimeKit's own, not the stored value.
        var first = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(first, stored);
        var second = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(second, stored);
        Assert.NotEqual(first.MessageId, second.MessageId);
    }

    [Fact]
    public void ApplyRestorableMessageId_UsableId_SetsNormalized()
    {
        var message = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(message, "<abc@host.example>");
        Assert.Equal("abc@host.example", message.MessageId);
    }

    [Fact]
    public void ApplyRestorableMessageId_UsableIdWithoutBrackets_SetsNormalized()
    {
        var message = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(message, "abc@host.example");
        Assert.Equal("abc@host.example", message.MessageId);
    }

    [Fact]
    public void ApplyRestorableMessageId_IdWithoutAtSign_IsNotApplied()
    {
        // The append path drops ids without a domain so MimeKit generates a fresh one;
        // the export must agree with it.
        var message = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(message, "no-at-sign");

        var first = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(first, "no-at-sign");
        var second = new MimeMessage();
        MailContentHelper.ApplyRestorableMessageId(second, "no-at-sign");
        Assert.NotEqual(first.MessageId, second.MessageId);
    }
}