using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using MimeKit;
using System.Text;

namespace MailArchiver.Services.Providers.MBox
{
    /// <summary>
    /// Processes an MBox file stream, parsing MimeMessages sequentially.
    /// Handles malformed-email recovery by seeking to the next "From " marker
    /// and recreating the MimeParser to avoid buffer desynchronization.
    /// </summary>
    public class MBoxStreamProcessor
    {
        private readonly ILogger<MBoxStreamProcessor> _logger;

        public MBoxStreamProcessor(ILogger<MBoxStreamProcessor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Processes an MBox file stream. For each email, calls the provided handler.
        /// Handles FormatException by skipping to the next mbox marker.
        /// </summary>
        public async Task ProcessMBoxFile(MBoxImportJob job, MailAccount targetAccount, CancellationToken ct,
            Func<MimeMessage, string, Task<ImportResult>> handler)
        {
            var stream = new FileStream(job.FilePath, FileMode.Open, FileAccess.Read);
            var parser = new MimeParser(stream, MimeFormat.Mbox);

            try
            {
                while (!parser.IsEndOfStream)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var message = await parser.ParseMessageAsync(ct);
                        job.CurrentEmailSubject = message.Subject;
                        job.ProcessedBytes = stream.Position;

                        var importResult = await handler(message, job.TargetFolder);
                        message?.Dispose();

                        if (importResult.Success) job.SuccessCount++;
                        else if (importResult.AlreadyExists) job.SkippedAlreadyExistsCount++;
                        else job.FailedCount++;

                        job.ProcessedEmails++;
                    }
                    catch (FormatException ex)
                    {
                        var currentPosition = stream.Position;
                        job.SkippedMalformedCount++;

                        var contextPreview = ExtractStreamContext(stream, currentPosition, 200);
                        _logger.LogWarning(ex, "Job {JobId}: Skipping malformed email #{SkipCount} at byte position {Position}. Context: {Context}",
                            job.JobId, job.SkippedMalformedCount, currentPosition, contextPreview);

                        var nextEmailPosition = FindNextMboxMarker(stream);
                        if (nextEmailPosition > currentPosition)
                        {
                            stream.Position = nextEmailPosition;
                            parser = new MimeParser(stream, MimeFormat.Mbox);
                            _logger.LogInformation("Job {JobId}: Advanced stream from {OldPos} to {NewPos} ({BytesSkipped} bytes skipped) and recreated parser",
                                job.JobId, currentPosition, nextEmailPosition, nextEmailPosition - currentPosition);
                        }
                        else
                        {
                            _logger.LogInformation("Job {JobId}: No more valid emails found after position {Position}, ending import",
                                job.JobId, currentPosition);
                            break;
                        }
                    }
                }
            }
            finally
            {
                parser = null;
                stream?.Dispose();
            }
        }

        /// <summary>
        /// Finds the next "From " marker in the mbox file stream, indicating the start of the next email.
        /// </summary>
        private long FindNextMboxMarker(FileStream stream)
        {
            var currentPosition = stream.Position;
            var buffer = new byte[8192];
            var lineBuilder = new StringBuilder();
            var lastByte = (byte)0;

            try
            {
                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) return -1;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        var currentByte = buffer[i];

                        if (currentByte == '\n')
                        {
                            var line = lineBuilder.ToString();
                            lineBuilder.Clear();

                            if (line.StartsWith("From ", StringComparison.Ordinal))
                            {
                                var markerPosition = stream.Position - (bytesRead - i) - Encoding.UTF8.GetByteCount(line);
                                if (lastByte == '\r') markerPosition -= 1;
                                return markerPosition;
                            }

                            lastByte = currentByte;
                            continue;
                        }

                        if (currentByte == '\r')
                        {
                            lastByte = currentByte;
                            continue;
                        }

                        lineBuilder.Append((char)currentByte);
                        lastByte = currentByte;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while searching for next mbox marker from position {Position}", currentPosition);
                return -1;
            }
        }

        /// <summary>
        /// Extracts a preview of bytes around a given stream position for diagnostic logging.
        /// The stream position is restored after reading.
        /// </summary>
        private string ExtractStreamContext(FileStream stream, long position, int maxBytes)
        {
            var originalPosition = stream.Position;
            try
            {
                var startPos = Math.Max(0, position - 50);
                stream.Position = startPos;

                var buffer = new byte[maxBytes];
                var bytesRead = stream.Read(buffer, 0, maxBytes);

                if (bytesRead == 0) return "(empty - end of file)";

                var preview = new StringBuilder();
                for (int i = 0; i < bytesRead; i++)
                {
                    var b = buffer[i];
                    if (b >= 32 && b < 127)
                        preview.Append((char)b);
                    else if (b == '\n')
                        preview.Append("\\n");
                    else if (b == '\r')
                        preview.Append("\\r");
                    else if (b == '\t')
                        preview.Append("\\t");
                    else
                        preview.Append($"[0x{b:X2}]");
                }

                return $"[pos {startPos}-{startPos + bytesRead}]: {preview}";
            }
            catch (Exception ex)
            {
                return $"(failed to extract context: {ex.Message})";
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }
    }
}