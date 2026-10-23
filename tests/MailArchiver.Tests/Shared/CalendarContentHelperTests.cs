using MailArchiver.Services.Shared;
using MimeKit;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="CalendarContentHelper.ParseICalSummary"/>.
/// </summary>
public class CalendarContentHelperParseICalSummaryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrWhitespace_ReturnsEmpty(string? content)
        => Assert.Equal(string.Empty, CalendarContentHelper.ParseICalSummary(content));

    [Fact]
    public void Parse_FullVEvent_RendersAllFields()
    {
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:Quarterly Review
DTSTART:20240601T100000Z
DTEND:20240601T110000Z
LOCATION:Room 4
ORGANIZER;CN=Alice:mailto:alice@example.com
ATTENDEE;CN=Bob:mailto:bob@example.com
ATTENDEE;CN=Carol:mailto:carol@example.com
DESCRIPTION:Project status update
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics);

        Assert.Contains("Quarterly Review", summary);
        Assert.Contains("Start:", summary);
        Assert.Contains("End:", summary);
        Assert.Contains("Room 4", summary);
        Assert.Contains("alice@example.com", summary);
        Assert.Contains("bob@example.com", summary);
        Assert.Contains("Project status update", summary);
    }

    [Fact]
    public void Parse_OnlyFirstVEvent_IsUsed()
    {
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:First
END:VEVENT
BEGIN:VEVENT
SUMMARY:Second
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics);
        Assert.Contains("First", summary);
        Assert.DoesNotContain("Second", summary);
    }

    [Fact]
    public void Parse_DateOnlyValue_FormattedAsDate()
    {
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:All-day
DTSTART:20240601
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics);
        Assert.Contains("Start:", summary);
        Assert.Contains("All-day", summary);
    }

    [Fact]
    public void Parse_EscapedText_IsUnescaped()
    {
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:Hello\, World
DESCRIPTION:Line1\nLine2\, comma\; semi\\backslash
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics);
        Assert.Contains("Hello, World", summary);
        Assert.Contains("Line1", summary);
        Assert.Contains("comma", summary);
    }

    [Fact]
    public void Parse_LineFolding_IsUnfolded()
    {
        var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nSUMMARY:Long\r\n Title\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var summary = CalendarContentHelper.ParseICalSummary(ics);
        Assert.Contains("LongTitle", summary);
    }

    [Fact]
    public void Parse_LocalizedLabels_AreUsedWhenProvided()
    {
        var labels = new Dictionary<string, string>
        {
            { "MeetingInvitation", "Einladung" },
            { "MeetingStart", "Beginn" },
            { "MeetingEnd", "Ende" }
        };
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:Test
DTSTART:20240601T100000Z
DTEND:20240601T110000Z
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics, labels);
        Assert.Contains("===== Einladung =====", summary);
        Assert.Contains("Beginn:", summary);
        Assert.Contains("Ende:", summary);
    }

    [Fact]
    public void Parse_MissingLabels_FallsBackToEnglish()
    {
        var ics = """
BEGIN:VCALENDAR
BEGIN:VEVENT
SUMMARY:Test
END:VEVENT
END:VCALENDAR
""";
        var summary = CalendarContentHelper.ParseICalSummary(ics, new Dictionary<string, string>());
        Assert.Contains("Meeting Invitation", summary);
    }

    [Fact]
    public void Parse_NoVEvent_ReturnsJustHeader()
    {
        var ics = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n";
        var summary = CalendarContentHelper.ParseICalSummary(ics);
        Assert.Contains("Meeting Invitation", summary);
    }
}

/// <summary>
/// Unit tests for <see cref="CalendarContentHelper.TryExtractCalendar"/>.
/// </summary>
public class CalendarContentHelperTryExtractCalendarTests
{
    [Fact]
    public void TryExtract_NullMessage_ReturnsNull()
        => Assert.Null(CalendarContentHelper.TryExtractCalendar(null!));

    [Fact]
    public void TryExtract_NoCalendarPart_ReturnsNull()
    {
        var msg = new MimeMessage();
        msg.Body = new TextPart("plain") { Text = "no calendar here" };
        Assert.Null(CalendarContentHelper.TryExtractCalendar(msg));
    }

    [Fact]
    public void TryExtract_TextCalendarFloatingPart_ReturnsExtraction()
    {
        var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nSUMMARY:Test\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var msg = new MimeMessage();
        var multipart = new Multipart("mixed");
        multipart.Add(new TextPart("plain") { Text = "body" });
        multipart.Add(new TextPart("calendar") { Text = ics });
        msg.Body = multipart;

        var result = CalendarContentHelper.TryExtractCalendar(msg);
        Assert.NotNull(result);
        Assert.Contains("Test", result!.Content);
        Assert.Equal("text/calendar", result.MimeType);
    }

    [Fact]
    public void TryExtract_ApplicationIcs_ReturnsExtraction()
    {
        var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nSUMMARY:Ics\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var msg = new MimeMessage();
        var part = new MimePart("application", "ics")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(ics))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
        };
        msg.Body = part;

        var result = CalendarContentHelper.TryExtractCalendar(msg);
        Assert.NotNull(result);
        Assert.Contains("Ics", result!.Content);
    }

    [Fact]
    public void TryExtract_AttachmentDisposition_SkipsIt()
    {
        var ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nSUMMARY:Skipped\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var msg = new MimeMessage();
        var part = new MimePart("text", "calendar")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(ics))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            FileName = "invite.ics"
        };
        msg.Body = part;

        var result = CalendarContentHelper.TryExtractCalendar(msg);
        Assert.Null(result);
    }
}