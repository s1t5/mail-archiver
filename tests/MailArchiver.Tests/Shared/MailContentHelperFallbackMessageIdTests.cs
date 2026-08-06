using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="MailContentHelper.GenerateFallbackMessageId"/> and
/// <see cref="MailContentHelper.BuildCanonicalHeaders"/>.
/// The fallback ID is the dedupe key for messages without a Message-ID header;
/// the golden vectors pin the exact algorithm so the IMAP, import and M365/Graph
/// pipelines keep producing identical keys.
/// </summary>
public class MailContentHelperFallbackMessageIdTests
{
    [Fact]
    public void Generate_GoldenVector_MatchesGraphAndImportAlgorithm()
    {
        // SHA-256 over "from|to|subject|ticks", Base64URL, 16 chars - the exact
        // construction historically used by GraphMailArchiver/MailImporter.
        var id = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", "Hello", 632000000000000000L);

        Assert.Equal("generated-RfI7OBcChOwOLxME@mail-archiver.local", id);
    }

    [Fact]
    public void Generate_NullSubject_TreatedAsEmpty()
    {
        var withNull = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", null, 0L);
        var withEmpty = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", "", 0L);

        Assert.Equal("generated-avH-1bhLEHFzKZni@mail-archiver.local", withNull);
        Assert.Equal(withEmpty, withNull);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var once = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L);
        var twice = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L);

        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("carol@x.com", "b@x.com", "s", 42L)] // different from
    [InlineData("a@x.com", "dave@x.com", "s", 42L)] // different to
    [InlineData("a@x.com", "b@x.com", "other", 42L)] // different subject
    [InlineData("a@x.com", "b@x.com", "s", 43L)] // different date
    public void Generate_DifferentComponents_ProduceDifferentIds(string from, string to, string subject, long ticks)
    {
        var baseline = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L);
        var other = MailContentHelper.GenerateFallbackMessageId(from, to, subject, ticks);

        Assert.NotEqual(baseline, other);
    }

    [Fact]
    public void Generate_SameComponentsButDifferentHeaders_ProduceDifferentIds()
    {
        // The reporter's scenario: distinct deliveries with identical From/To/Subject/Date
        // must NOT collapse onto one key - their Received chains differ.
        var headers1 = "Received: from mx1 by hub; Mon, 05 Jan 2004 10:00:00 +0100\nFrom: alice@x.com";
        var headers2 = "Received: from mx2 by hub; Mon, 05 Jan 2004 10:00:00 +0100\nFrom: alice@x.com";

        var id1 = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", null, 123L, headers1);
        var id2 = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", null, 123L, headers2);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Generate_SameComponentsAndSameHeaders_ProduceSameId()
    {
        var headers = "Received: from mx1 by hub; Mon, 05 Jan 2004 10:00:00 +0100\nFrom: alice@x.com";

        var id1 = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", null, 123L, headers);
        var id2 = MailContentHelper.GenerateFallbackMessageId("alice@x.com", "bob@x.com", null, 123L, headers);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void Generate_NullOrEmptyHeaders_MatchesFourComponentForm()
    {
        var fourComponent = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L);
        var withNull = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L, null);
        var withEmpty = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L, "");

        Assert.Equal(fourComponent, withNull);
        Assert.Equal(fourComponent, withEmpty);
    }

    [Fact]
    public void Generate_HasExpectedFormat()
    {
        var id = MailContentHelper.GenerateFallbackMessageId("a@x.com", "b@x.com", "s", 42L);

        Assert.StartsWith("generated-", id);
        Assert.EndsWith("@mail-archiver.local", id);
        Assert.Equal("generated-".Length + 16 + "@mail-archiver.local".Length, id.Length);
    }

    [Fact]
    public void BuildCanonicalHeaders_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MailContentHelper.BuildCanonicalHeaders(null));
    }

    [Fact]
    public void BuildCanonicalHeaders_PreservesOrderAndFormat()
    {
        var raw = "From: alice@x.com\r\nTo: bob@x.com\r\nSubject: Hi\r\n\r\nbody";
        var message = MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)));

        var canonical = MailContentHelper.BuildCanonicalHeaders(message.Headers);

        Assert.Equal("From: alice@x.com\nTo: bob@x.com\nSubject: Hi", canonical);
    }
}
