using System.Net.Http;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Graph;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions.Store;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The Graph restore prefers the stored original and posts it as MIME, the same cascade the
/// IMAP restore and the single-email export use. These tests pin the wire contract: base64
/// body, text/plain, and the folder-scoped create-message endpoint.
/// </summary>
public class GraphMailRestorerMimeTests : IDisposable
{
    private static readonly byte[] Original = System.Text.Encoding.ASCII.GetBytes(
        "From: sender@example.org\r\n" +
        "To: rcpt@example.org\r\n" +
        "Subject: Stored original\r\n" +
        "Message-ID: <original@example.org>\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain; charset=utf-8\r\n" +
        "\r\n" +
        "The bytes that arrived.\r\n");

    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;
    private readonly GraphMailRestorer _restorer;
    private readonly RecordingLogger<GraphMailRestorer> _logger = new();

    public GraphMailRestorerMimeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new MailArchiverDbContext(options);
        _context.Database.EnsureCreated();

        _restorer = new GraphMailRestorer(
            authFactory: null!,
            folderService: null!,
            _context,
            _logger,
            new DateTimeHelper(Microsoft.Extensions.Options.Options.Create(new TimeZoneOptions())),
            rawMessageStore: null);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ArchivedEmail Email() => new()
    {
        Id = 42,
        MailAccountId = 1,
        Subject = "Stored original",
        From = "sender@example.org",
        To = "rcpt@example.org",
        Body = "rebuilt body",
        SentDate = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)
    };

    private static MailAccount Account() => new()
    {
        Id = 1,
        Name = "Target",
        EmailAddress = "target@example.org"
    };

    [Fact]
    public async Task TryCreateFromMimeAsync_PostsBase64MimeAsTextPlain()
    {
        var adapter = new CapturingAdapter();
        var client = new GraphServiceClient(adapter);

        var result = await _restorer.TryCreateFromMimeAsync(
            client,
            Account(),
            new MailFolder { Id = "folder-1" },
            Email(),
            new RawMessageContent(Original, "checksum", "imap", RawMessageStorage.Postgres));

        Assert.True(result, _logger.Describe());
        Assert.Equal(1, adapter.SendCount);

        var request = adapter.Captured!;

        // Kiota 2.0 keeps the body as a stream; the MIME form sets text/plain explicitly.
        Assert.True(request.Headers.TryGetValue("Content-Type", out var contentType), "no Content-Type header");
        Assert.Contains(contentType, value => value.Contains("text/plain", StringComparison.OrdinalIgnoreCase));

        var body = await new StreamReader(Assert.IsAssignableFrom<Stream>(request.Content)).ReadToEndAsync();
        Assert.Equal(Original, Convert.FromBase64String(body));
    }

    [Fact]
    public async Task TryCreateFromMimeAsync_TargetsTheFolderMessagesEndpoint()
    {
        var adapter = new CapturingAdapter();
        var client = new GraphServiceClient(adapter);

        await _restorer.TryCreateFromMimeAsync(
            client,
            Account(),
            new MailFolder { Id = "folder-1" },
            Email(),
            new RawMessageContent(Original, "checksum", "imap", RawMessageStorage.Postgres));

        var request = adapter.Captured!;
        Assert.Equal(Method.POST, request.HttpMethod);

        // The address is URL-encoded in the template, so compare the decoded form.
        var url = Uri.UnescapeDataString(request.URI.ToString());
        Assert.Contains("users/target@example.org/mailFolders/folder-1/messages", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryCreateFromMimeAsync_ReturnsFalseWhenGraphRejectsTheRequest()
    {
        var adapter = new CapturingAdapter { ThrowOnSend = new InvalidOperationException("413 Request Entity Too Large") };
        var client = new GraphServiceClient(adapter);

        var result = await _restorer.TryCreateFromMimeAsync(
            client,
            Account(),
            new MailFolder { Id = "folder-1" },
            Email(),
            new RawMessageContent(Original, "checksum", "imap", RawMessageStorage.Postgres));

        // False lets the caller rebuild from the archived fields instead of failing the restore.
        Assert.False(result);
    }

    /// <summary>Collects log entries so a failing assertion can show why the call bailed out.</summary>
    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        private readonly List<string> _entries = new();

        public string Describe() => string.Join(Environment.NewLine, _entries);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add($"{logLevel}: {formatter(state, exception)} {exception}");
        }
    }

    /// <summary>
    /// Captures the request the SDK builds and answers with a created message. Every other
    /// member is unused by the create-message call.
    /// </summary>
    private sealed class CapturingAdapter : IRequestAdapter
    {
        public RequestInformation? Captured { get; private set; }
        public int SendCount { get; private set; }
        public Exception? ThrowOnSend { get; init; }

        public ISerializationWriterFactory SerializationWriterFactory { get; } =
            new Microsoft.Kiota.Serialization.Json.JsonSerializationWriterFactory();
        public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

        public void EnableBackingStore(IBackingStoreFactory backingStoreFactory) { }

        public Task<ModelType?> SendAsync<ModelType>(
            RequestInformation requestInfo,
            ParsableFactory<ModelType> factory,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default) where ModelType : IParsable
        {
            SendCount++;
            Captured = requestInfo;

            if (ThrowOnSend != null)
            {
                throw ThrowOnSend;
            }

            return Task.FromResult<ModelType?>((ModelType)(object)new Message { Id = "created-1" });
        }

        public Task<IEnumerable<ModelType>?> SendCollectionAsync<ModelType>(
            RequestInformation requestInfo,
            ParsableFactory<ModelType> factory,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default) where ModelType : IParsable
            => throw new NotSupportedException();

        public Task<ModelType?> SendPrimitiveAsync<ModelType>(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<ModelType>?> SendPrimitiveCollectionAsync<ModelType>(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendNoContentAsync(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ModelType?> ConvertToNativeRequestAsync<ModelType>(
            RequestInformation requestInfo,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
