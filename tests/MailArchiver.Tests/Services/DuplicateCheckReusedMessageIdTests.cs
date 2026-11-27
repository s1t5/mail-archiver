using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Graph;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Moq;

namespace MailArchiver.Tests.Services;

/// <summary>
/// A Message-ID match must also agree on subject and send time. Outlook calendar
/// reminders have been observed to reuse the same Message-ID for later, distinct
/// messages; treating the identifier alone as conclusive skipped those later
/// mails as duplicates.
/// </summary>
public class DuplicateCheckReusedMessageIdTests : IDisposable
{
    private const int AccountId = 1;
    private const string StoredMessageId = "reused@outlook.example";

    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;
    private readonly DateTimeHelper _helper;
    private readonly RecordingLogger _logger = new();
    private readonly GraphMailArchiver _archiver;

    public DuplicateCheckReusedMessageIdTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new MailArchiverDbContext(options);
        _context.Database.EnsureCreated();

        _context.MailAccounts.Add(new MailAccount
        {
            Id = AccountId,
            Name = "Graph",
            EmailAddress = "archive@example.org"
        });
        _context.SaveChanges();

        _helper = new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Etc/UCT" }));
        _archiver = new GraphMailArchiver(
            _context,
            _logger,
            _helper,
            Mock.Of<IRawMessageStore>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static DateTimeOffset FirstSent =>
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private void Seed(string subject, DateTimeOffset sent, string from, string to)
    {
        _context.ArchivedEmails.Add(new ArchivedEmail
        {
            MailAccountId = AccountId,
            MessageId = StoredMessageId,
            Subject = MailMatchKey.NormalizeSubject(subject),
            From = MailMatchKey.NormalizeAddresses(new[] { from }, MailMatchKey.FromMaxBytes),
            To = MailMatchKey.NormalizeAddresses(new[] { to }, MailMatchKey.ToMaxBytes),
            Body = "",
            HtmlBody = "",
            Cc = "",
            Bcc = "",
            FolderName = "Inbox",
            SentDate = _helper.ConvertToDisplayTimeZone(sent),
            ReceivedDate = _helper.ConvertToDisplayTimeZone(sent)
        });
        _context.SaveChanges();
    }

    private static Message Incoming(string subject, DateTimeOffset sent, string from, string to) =>
        new()
        {
            InternetMessageId = "<" + StoredMessageId + ">",
            Subject = subject,
            SentDateTime = sent,
            From = new Recipient { EmailAddress = new EmailAddress { Address = from } },
            ToRecipients = new List<Recipient>
            {
                new() { EmailAddress = new EmailAddress { Address = to } }
            }
        };

    [Fact]
    public async Task SameMessageId_SameSubjectAndDate_IsDuplicate()
    {
        Seed("Weekly reminder", FirstSent, "a@example.org", "b@example.org");

        var isDuplicate = await _archiver.IsDuplicateAsync(
            AccountId,
            GraphMailArchiver.ResolveMessageId(Incoming("Weekly reminder", FirstSent, "a@example.org", "b@example.org")),
            Incoming("Weekly reminder", FirstSent, "a@example.org", "b@example.org"),
            "Inbox");

        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task SameMessageId_LaterDate_IsNotDuplicate()
    {
        Seed("Weekly reminder", FirstSent, "a@example.org", "b@example.org");

        var later = FirstSent.AddDays(7);
        var isDuplicate = await _archiver.IsDuplicateAsync(
            AccountId,
            GraphMailArchiver.ResolveMessageId(Incoming("Weekly reminder", later, "a@example.org", "b@example.org")),
            Incoming("Weekly reminder", later, "a@example.org", "b@example.org"),
            "Inbox");

        Assert.False(isDuplicate, _logger.Describe());
    }

    [Fact]
    public async Task SameMessageId_DifferentSubject_IsNotDuplicate()
    {
        Seed("Weekly reminder", FirstSent, "a@example.org", "b@example.org");

        var isDuplicate = await _archiver.IsDuplicateAsync(
            AccountId,
            GraphMailArchiver.ResolveMessageId(Incoming("Updated reminder", FirstSent, "a@example.org", "b@example.org")),
            Incoming("Updated reminder", FirstSent, "a@example.org", "b@example.org"),
            "Inbox");

        Assert.False(isDuplicate, _logger.Describe());
    }

    [Fact]
    public async Task FuzzyFromToSubjectDate_StillDetectsDuplicateWithoutMatchingMessageId()
    {
        Seed("Weekly reminder", FirstSent, "a@example.org", "b@example.org");

        var otherId = new Message
        {
            InternetMessageId = "<other@example.org>",
            Subject = "Weekly reminder",
            SentDateTime = FirstSent,
            From = new Recipient { EmailAddress = new EmailAddress { Address = "a@example.org" } },
            ToRecipients = new List<Recipient>
            {
                new() { EmailAddress = new EmailAddress { Address = "b@example.org" } }
            }
        };

        var isDuplicate = await _archiver.IsDuplicateAsync(
            AccountId,
            GraphMailArchiver.ResolveMessageId(otherId),
            otherId,
            "Inbox");

        Assert.True(isDuplicate, _logger.Describe());
    }

    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger<GraphMailArchiver>
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
}
