using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// INTERNALDATE has two branches that need opposite timezone treatment, and getting them the
/// wrong way round produces no error: mail simply lands on the target server with the wrong
/// arrival time, which clients hide because they sort by the Date header instead.
/// </summary>
public class InternalDateResolverTests
{
    private static DateTimeHelper Helper(string timeZoneId)
        => new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = timeZoneId }));

    private static ArchivedEmail Row(DateTime sentDate, string? rawHeaders = null) => new()
    {
        SentDate = sentDate,
        RawHeaders = rawHeaders,
        // Deliberately set to something obviously wrong. This is the value the old
        // implementation used, and it is the archiving timestamp rather than a delivery time.
        ReceivedDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    // ------------------------------------------------------------------ chain parsing

    [Fact]
    public void TryExtractDeliveryTime_TakesTheTopmostReceivedHeader()
    {
        // The chain is prepended to as a message travels, so the first Received header is the
        // final delivery. ExtractEmailDate walks the other way on purpose, which is why it
        // cannot be reused here.
        var raw = string.Join("\r\n", new[]
        {
            "Received: from mx3.target by next with ESMTP id C; Sun, 12 Jul 2026 10:00:00 +0000",
            "Received: from mx2.relay by next with ESMTP id B; Sun, 12 Jul 2026 09:58:00 +0000",
            "Received: from mx1.source by next with ESMTP id A; Sun, 12 Jul 2026 09:55:00 +0000",
            "Subject: hello",
        });

        var result = InternalDateResolver.TryExtractDeliveryTime(raw);

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero), result!.Value);
    }

    [Fact]
    public void TryExtractDeliveryTime_SkipsAnUnparseableTopHopAndUsesTheNextOne()
    {
        var raw = string.Join("\r\n", new[]
        {
            "Received: from broken by next with ESMTP id X",
            "Received: from mx2.relay by next with ESMTP id B; Sun, 12 Jul 2026 09:58:00 +0000",
        });

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 9, 58, 0, TimeSpan.Zero),
            InternalDateResolver.TryExtractDeliveryTime(raw)!.Value);
    }

    [Fact]
    public void TryExtractDeliveryTime_PreservesTheOffsetOfTheHeader()
    {
        var raw = "Received: from mx by next with ESMTP id A; Sun, 12 Jul 2026 12:00:00 +0200";
        var result = InternalDateResolver.TryExtractDeliveryTime(raw)!.Value;

        Assert.Equal(TimeSpan.FromHours(2), result.Offset);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero), result.ToUniversalTime());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Subject: no received headers here\r\nFrom: a@b.c")]
    public void TryExtractDeliveryTime_WithoutAUsableChain_ReturnsNull(string? raw)
    {
        Assert.Null(InternalDateResolver.TryExtractDeliveryTime(raw));
    }

    [Fact]
    public void TryExtractDeliveryTime_IgnoresAHeaderThatMerelyContainsTheWordReceived()
    {
        // "X-Received" and "Received-SPF" are not delivery hops.
        var raw = string.Join("\r\n", new[]
        {
            "Received-SPF: pass; Sun, 12 Jul 2026 08:00:00 +0000",
            "X-Received: by something; Sun, 12 Jul 2026 07:00:00 +0000",
            "Received: from mx by next with ESMTP id A; Sun, 12 Jul 2026 10:00:00 +0000",
        });

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero),
            InternalDateResolver.TryExtractDeliveryTime(raw)!.Value);
    }

    // ------------------------------------------------------------------ the two branches

    [Fact]
    public void Resolve_WithReceivedChain_UsesItUnconverted()
    {
        // A time recovered from a Received header is already an absolute instant. Passing it
        // through the display timezone conversion would shift it by the offset, which is the
        // second half of the original defect.
        var raw = "Received: from mx by next with ESMTP id A; Sun, 12 Jul 2026 10:00:00 +0000";
        var email = Row(new DateTime(2026, 7, 12, 9, 0, 0), raw);

        var result = InternalDateResolver.Resolve(email, Helper("Europe/Berlin"));

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero), result.ToUniversalTime());
    }

    [Fact]
    public void Resolve_WithoutChain_ConvertsSentDateOutOfTheDisplayTimeZone()
    {
        // SentDate is stored display-local and naive. Under Europe/Berlin in July the offset is
        // +02:00, so 12:00 local is 10:00 UTC. A test under the shipped Etc/UCT default would
        // pass whether or not this conversion happens at all, and so proves nothing.
        var email = Row(new DateTime(2026, 7, 12, 12, 0, 0));

        var result = InternalDateResolver.Resolve(email, Helper("Europe/Berlin"));

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero), result.ToUniversalTime());
    }

    [Fact]
    public void Resolve_WithoutChain_HonoursWinterOffsetToo()
    {
        // +01:00 in January, so the conversion is not a fixed constant.
        var email = Row(new DateTime(2026, 1, 12, 12, 0, 0));

        var result = InternalDateResolver.Resolve(email, Helper("Europe/Berlin"));

        Assert.Equal(new DateTimeOffset(2026, 1, 12, 11, 0, 0, TimeSpan.Zero), result.ToUniversalTime());
    }

    [Fact]
    public void Resolve_UnderUtcDisplayTimeZone_BothBranchesAgreeWithTheirInput()
    {
        var withChain = Row(new DateTime(2026, 7, 12, 9, 0, 0),
            "Received: from mx by next with ESMTP id A; Sun, 12 Jul 2026 10:00:00 +0000");
        var withoutChain = Row(new DateTime(2026, 7, 12, 9, 0, 0));

        var helper = Helper("Etc/UCT");

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero),
            InternalDateResolver.Resolve(withChain, helper).ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero),
            InternalDateResolver.Resolve(withoutChain, helper).ToUniversalTime());
    }

    [Fact]
    public void Resolve_NeverUsesReceivedDate()
    {
        // ReceivedDate is DateTime.UtcNow at archiving time (MailImporter), so it must not reach
        // INTERNALDATE by either branch. Both rows below carry an obviously wrong ReceivedDate.
        var helper = Helper("Etc/UCT");

        var withChain = InternalDateResolver.Resolve(
            Row(new DateTime(2026, 7, 12, 9, 0, 0),
                "Received: from mx by next with ESMTP id A; Sun, 12 Jul 2026 10:00:00 +0000"), helper);
        var withoutChain = InternalDateResolver.Resolve(Row(new DateTime(2026, 7, 12, 9, 0, 0)), helper);

        Assert.NotEqual(2030, withChain.Year);
        Assert.NotEqual(2030, withoutChain.Year);
    }
}
