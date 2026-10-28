using MailArchiver.Models;
using MailArchiver.Utilities;
using MimeKit;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Rebuilds a MIME message from the parsed fields of an archived email (addresses, stored
    /// raw headers, bodies, attachments). Shared by the account and selection exports and the
    /// API's .eml endpoint. The result is a reconstruction: the original message, where it
    /// was stored, is a <see cref="RawMessage"/>.
    /// </summary>
    public static class ArchivedEmailMimeBuilder
    {
        public static MimeMessage Build(ArchivedEmail email, DateTimeHelper dateTimeHelper)
        {
            var message = new MimeMessage();

            // Set headers. Apply the stored Message-ID only when it normalizes to a usable
            // value; MailKit 4.17.0 throws on an empty one (M3).
            MailContentHelper.ApplyRestorableMessageId(message, email.MessageId);
            message.Subject = email.Subject;

            // The SentDate in the database is already in the configured display timezone (converted during sync)
            // We need to specify this timezone when creating the DateTimeOffset to preserve the correct time
            message.Date = dateTimeHelper.ToDisplayTimeZoneOffset(email.SentDate);

            // Parse addresses
            if (!string.IsNullOrEmpty(email.From))
            {
                try
                {
                    var fromList = InternetAddressList.Parse(email.From);
                    MailContentHelper.ApplyDisplayNames(fromList, email.FromDisplayName);
                    message.From.AddRange(fromList);
                }
                catch
                {
                    message.From.Add(new MailboxAddress("", email.From));
                }
            }

            if (!string.IsNullOrEmpty(email.To))
            {
                try
                {
                    var toList = InternetAddressList.Parse(email.To);
                    MailContentHelper.ApplyDisplayNames(toList, email.ToDisplayNames);
                    message.To.AddRange(toList);
                }
                catch
                {
                    message.To.Add(new MailboxAddress("", email.To));
                }
            }

            if (!string.IsNullOrEmpty(email.Cc))
            {
                try
                {
                    var ccList = InternetAddressList.Parse(email.Cc);
                    MailContentHelper.ApplyDisplayNames(ccList, email.CcDisplayNames);
                    message.Cc.AddRange(ccList);
                }
                catch
                {
                    message.Cc.Add(new MailboxAddress("", email.Cc));
                }
            }

            if (!string.IsNullOrEmpty(email.Bcc))
            {
                try
                {
                    var bccList = InternetAddressList.Parse(email.Bcc);
                    MailContentHelper.ApplyDisplayNames(bccList, email.BccDisplayNames);
                    message.Bcc.AddRange(bccList);
                }
                catch
                {
                    message.Bcc.Add(new MailboxAddress("", email.Bcc));
                }
            }

            // Import raw headers if available (for forensic/compliance purposes)
            if (!string.IsNullOrEmpty(email.RawHeaders))
            {
                try
                {
                    using var reader = new StringReader(email.RawHeaders);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var headerName = line.Substring(0, colonIndex).Trim();
                            var headerValue = line.Substring(colonIndex + 1).Trim();

                            // Skip headers that are already set by MimeMessage properties to avoid duplicates
                            var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "Subject", "From", "To", "Cc", "Bcc", "Date", "Message-ID",
                                "MIME-Version", "Content-Type"
                            };

                            if (!skipHeaders.Contains(headerName))
                            {
                                try
                                {
                                    message.Headers.Add(headerName, headerValue);
                                }
                                catch
                                {
                                    // Ignore headers that cannot be added
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore raw header parsing errors to avoid breaking export
                }
            }

            // Create body - use untruncated versions if available for compliance
            var bodyBuilder = new BodyBuilder();

            // Priority: Original body (with null bytes) > Untruncated body > Regular body
            var textBody = email.OriginalBodyText != null
                ? System.Text.Encoding.UTF8.GetString(email.OriginalBodyText)
                : (!string.IsNullOrEmpty(email.BodyUntruncatedText) 
                    ? email.BodyUntruncatedText 
                    : email.Body);

            var htmlBody = email.OriginalBodyHtml != null
                ? System.Text.Encoding.UTF8.GetString(email.OriginalBodyHtml)
                : (!string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                    ? email.BodyUntruncatedHtml 
                    : email.HtmlBody);

            // Only emit a text/plain part when the content is genuine plain text.
            // When an email was archived without a real text/plain part, the archiving fallback stores the
            // raw HTML in the Body field; emitting that as text/plain would produce an HTML-in-plain-text part.
            if (!string.IsNullOrEmpty(textBody)
                && !MailArchiver.Services.Shared.MailContentHelper.IsHtmlContent(textBody, htmlBody))
            {
                bodyBuilder.TextBody = textBody;
            }

            if (!string.IsNullOrEmpty(htmlBody))
            {
                bodyBuilder.HtmlBody = htmlBody;
            }

            // Add attachments
            if (email.Attachments?.Any() == true)
            {
                // Separate inline attachments from regular attachments
                var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
            
                // Add inline attachments first so they can be referenced in the HTML body
                foreach (var attachment in inlineAttachments)
                {
                    try
                    {
                        // Extract just the filename in case attachment.FileName contains a path or URI
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                    
                        var contentType = ContentType.Parse(attachment.ContentType);
                        var mimePart = bodyBuilder.LinkedResources.Add(fileName, attachment.Content, contentType);
                        mimePart.ContentId = attachment.ContentId;
                    }
                    catch
                    {
                        // Fallback for invalid content types
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                    
                        var mimePart = bodyBuilder.LinkedResources.Add(fileName, attachment.Content);
                        mimePart.ContentId = attachment.ContentId;
                    }
                }
            
                // Add regular attachments
                foreach (var attachment in regularAttachments)
                {
                    try
                    {
                        // Extract just the filename in case attachment.FileName contains a path or URI
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                    
                        var contentType = ContentType.Parse(attachment.ContentType);
                        bodyBuilder.Attachments.Add(fileName, attachment.Content, contentType);
                    }
                    catch
                    {
                        // Fallback for invalid content types
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                    
                        bodyBuilder.Attachments.Add(fileName, attachment.Content);
                    }
                }
            }

            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }
    }
}
