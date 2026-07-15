using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Extracts and parses iCalendar (text/calendar) parts from MIME messages.
    /// Outlook/M365 meeting invitations carry the actual event details (start, end,
    /// location, attendees, description) inside a text/calendar MIME part that is
    /// neither a regular attachment nor inline content. Without explicit handling
    /// these parts are silently dropped by the attachment collectors, leaving the
    /// archived email with an empty body.
    /// This helper preserves the raw .ics payload as an attachment and optionally
    /// produces a human-readable plain-text summary for the email body.
    /// </summary>
    public static class CalendarContentHelper
    {
        /// <summary>
        /// Result of a calendar extraction attempt.
        /// </summary>
        public sealed class CalendarExtraction
        {
            /// <summary>Raw iCalendar content (UTF-8 decoded), never null when Found is true.</summary>
            public string Content { get; init; } = string.Empty;

            /// <summary>File name to use for the .ics attachment (e.g. "invite.ics").</summary>
            public string FileName { get; init; } = "invite.ics";

            /// <summary>MIME subtype of the calendar part, e.g. "calendar" or "calendar+xml".</summary>
            public string MimeType { get; init; } = "text/calendar";
        }

        /// <summary>
        /// Walks the MIME tree of a message looking for text/calendar (or application/ics) parts.
        /// Returns the first match. Calendar parts embedded as attachments (Content-Disposition:
        /// attachment) are intentionally skipped here because they are already captured by the
        /// regular attachment collector; only "floating" calendar parts (no disposition or
        /// disposition "inline" without Content-ID) are rescued.
        /// </summary>
        public static CalendarExtraction? TryExtractCalendar(MimeMessage message)
        {
            if (message?.Body == null) return null;
            return WalkForCalendar(message.Body);
        }

        private static CalendarExtraction? WalkForCalendar(MimeEntity entity)
        {
            switch (entity)
            {
                case MimePart part:
                    {
                        var mt = part.ContentType?.MimeType?.ToLowerInvariant() ?? string.Empty;
                        var isCalendar = mt.StartsWith("text/calendar", StringComparison.OrdinalIgnoreCase)
                                         || mt.StartsWith("application/ics", StringComparison.OrdinalIgnoreCase)
                                         || (part.FileName?.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) ?? false);

                        if (!isCalendar) return null;

                        // Skip parts already handled as real attachments.
                        if (part.IsAttachment
                            && part.ContentDisposition?.Disposition?.Equals("attachment", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return null;
                        }

                        try
                        {
                            using var ms = new MemoryStream();
                            part.Content.DecodeToAsync(ms).GetAwaiter().GetResult();
                            var content = Encoding.UTF8.GetString(ms.ToArray());

                            var fileName = !string.IsNullOrEmpty(part.FileName)
                                ? part.FileName
                                : (!string.IsNullOrEmpty(part.ContentId)
                                    ? $"{part.ContentId.Trim('<', '>')}.ics"
                                    : "invite.ics");

                            return new CalendarExtraction
                            {
                                Content = content,
                                FileName = fileName,
                                MimeType = mt
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    }
                case Multipart multipart:
                    {
                        foreach (var child in multipart)
                        {
                            var found = WalkForCalendar(child);
                            if (found != null) return found;
                        }
                        return null;
                    }
                case MessagePart messagePart:
                    return WalkForCalendar(messagePart.Message.Body);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Parses an iCalendar payload and returns a human-readable plain-text summary
        /// of the first VEVENT block. Localised labels are passed in via the
        /// <paramref name="labels"/> dictionary (keys: MeetingInvitation, MeetingStart,
        /// MeetingEnd, MeetingLocation, MeetingOrganizer, MeetingAttendees,
        /// MeetingDescription). Missing labels fall back to English defaults.
        /// </summary>
        public static string ParseICalSummary(string icsContent, IReadOnlyDictionary<string, string>? labels = null)
        {
            if (string.IsNullOrWhiteSpace(icsContent)) return string.Empty;

            var unfolded = UnfoldLines(icsContent);
            var lines = unfolded.Split('\n');

            // Localised labels with English fallback
            var label = (string key, string fallback) =>
                labels != null && labels.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

            var meetingLabel = label("MeetingInvitation", "Meeting Invitation");
            var startLabel = label("MeetingStart", "Start");
            var endLabel = label("MeetingEnd", "End");
            var locationLabel = label("MeetingLocation", "Location");
            var organizerLabel = label("MeetingOrganizer", "Organizer");
            var attendeesLabel = label("MeetingAttendees", "Attendees");
            var descriptionLabel = label("MeetingDescription", "Description");

            var sb = new StringBuilder();
            sb.AppendLine($"===== {meetingLabel} =====");
            sb.AppendLine();

            var inEvent = false;
            var attendees = new List<string>();
            string? summary = null, location = null, description = null,
                organizer = null, dtStart = null, dtEnd = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');

                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    inEvent = true;
                    attendees.Clear();
                    summary = location = description = organizer = dtStart = dtEnd = null;
                    continue;
                }
                if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    break; // first VEVENT only
                }
                if (!inEvent) continue;

                // Split "NAME;params:VALUE" — value starts at first unquoted colon
                var colonIdx = FindValueColon(line);
                if (colonIdx < 0) continue;

                var nameSegment = line.Substring(0, colonIdx);
                var value = line.Substring(colonIdx + 1);
                var nameParts = nameSegment.Split(';', 2, StringSplitOptions.RemoveEmptyEntries);
                var name = nameParts[0].ToUpperInvariant();

                switch (name)
                {
                    case "SUMMARY":
                        summary = Unescape(value);
                        break;
                    case "LOCATION":
                        location = Unescape(value);
                        break;
                    case "DESCRIPTION":
                        description = Unescape(value);
                        break;
                    case "ORGANIZER":
                        organizer = FormatAddress(value);
                        break;
                    case "ATTENDEE":
                        attendees.Add(FormatAddress(value));
                        break;
                    case "DTSTART":
                        dtStart = value;
                        break;
                    case "DTEND":
                        dtEnd = value;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(summary))
                sb.AppendLine($"{summary}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(dtStart))
                sb.AppendLine($"{startLabel}: {FormatDateTime(dtStart)}");
            if (!string.IsNullOrEmpty(dtEnd))
                sb.AppendLine($"{endLabel}: {FormatDateTime(dtEnd)}");
            if (!string.IsNullOrEmpty(location))
                sb.AppendLine($"{locationLabel}: {location}");
            if (!string.IsNullOrEmpty(organizer))
                sb.AppendLine($"{organizerLabel}: {organizer}");
            if (attendees.Count > 0)
                sb.AppendLine($"{attendeesLabel}: {string.Join(", ", attendees)}");
            if (!string.IsNullOrEmpty(description))
            {
                sb.AppendLine();
                sb.AppendLine($"{descriptionLabel}:");
                sb.AppendLine(description);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// RFC 5545 line unfolding: a line beginning with a space/tab is a continuation
        /// of the previous line. Returns the unfolded content with \n line endings.
        /// </summary>
        private static string UnfoldLines(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;
            // Normalise CRLF to LF first
            var normalised = content.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalised.Split('\n');
            var result = new StringBuilder(normalised.Length);
            foreach (var line in lines)
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    result.Append(line, 1, line.Length - 1);
                }
                else
                {
                    if (result.Length > 0) result.Append('\n');
                    result.Append(line);
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Finds the index of the colon that separates the value from the name/params.
        /// Colons inside quoted parameter values are skipped.
        /// </summary>
        private static int FindValueColon(string line)
        {
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ':' && !inQuotes) return i;
            }
            return -1;
        }

        /// <summary>
        /// Unescapes iCalendar text values (\\n, \\, \\, \\,).
        /// </summary>
        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value
                .Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("\\N", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("\\,", ",")
                .Replace("\\;", ";")
                .Replace("\\\\", "\\");
        }

        /// <summary>
        /// Formats an ORGANIZER/ATTENDEE value into a readable "Name &lt;email&gt;" form.
        /// Input examples: "CN=John Doe:mailto:john@example.com"
        /// </summary>
        private static string FormatAddress(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            string? cn = null;
            var cnMatch = Regex.Match(value, @"CN\s*=\s*""?([^"":;]+)""?", RegexOptions.IgnoreCase);
            if (cnMatch.Success) cn = cnMatch.Groups[1].Value;

            var mailto = Regex.Match(value, @"mailto:([^;\s]+)", RegexOptions.IgnoreCase);
            var email = mailto.Success ? mailto.Groups[1].Value : value;

            return !string.IsNullOrEmpty(cn) ? $"{cn} <{email}>" : email;
        }

        /// <summary>
        /// Formats a DTSTART/DTEND value into a localised date/time string.
        /// Supports the common forms:
        ///   - YYYYMMDDTHHMMSSZ           (UTC)
        ///   - YYYYMMDDTHHMMSS            (floating)
        ///   - YYYYMMDDTHHMMSS            with TZID=... parameter (already stripped before call)
        ///   - YYYYMMDD                   (date only)
        /// </summary>
        private static string FormatDateTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // Strip any trailing parameters that may have slipped through
            var dtPart = value;
            var semiIdx = dtPart.IndexOf(';');
            if (semiIdx >= 0) dtPart = dtPart.Substring(0, semiIdx);

            // Date only: YYYYMMDD
            if (dtPart.Length == 8 && int.TryParse(dtPart, out _))
            {
                if (DateTime.TryParseExact(dtPart, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var dateOnly))
                {
                    return dateOnly.ToString("D", CultureInfo.CurrentUICulture);
                }
                return dtPart;
            }

            // Date+time: YYYYMMDDTHHMMSS[Z]
            var isUtc = dtPart.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            var timeStr = isUtc ? dtPart.Substring(0, dtPart.Length - 1) : dtPart;

            if (timeStr.Length >= 15 && DateTime.TryParseExact(timeStr, "yyyyMMddTHHmmss",
                    CultureInfo.InvariantCulture,
                    isUtc ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal,
                    out var dt))
            {
                var display = isUtc ? dt.ToLocalTime() : dt;
                return display.ToString("g", CultureInfo.CurrentUICulture)
                       + (isUtc ? " (UTC)" : "");
            }

            return value;
        }
    }
}