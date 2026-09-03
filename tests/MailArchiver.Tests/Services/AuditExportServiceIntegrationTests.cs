using System.IO.Compression;
using System.Text;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

[Collection(TestDbFixture.CollectionName)]
public class AuditExportServiceIntegrationTests
{
    private readonly TestDbFixture _fixture;
    private readonly string _outputDir;

    public AuditExportServiceIntegrationTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _outputDir = Path.Combine(Path.GetTempPath(), "audit-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
    }

    private sealed class TestHostingEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MailArchiver.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
    }

    private (AuditExportService Service, List<string> Logs) CreateService(MailArchiverDbContext sharedContext)
    {
        var logs = new List<string>();
        var services = new ServiceCollection();
        // Scoped DbContexts on the shared connection; the test cleans up its rows afterwards
        services.AddDbContext<MailArchiverDbContext>(options => options
            .UseNpgsql(sharedContext.Database.GetDbConnection())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddSingleton<IOptions<TimeZoneOptions>>(_ =>
            Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Etc/UCT" }));
        var provider = services.BuildServiceProvider();

        var service = new AuditExportService(
            provider,
            new CapturingLogger(logs),
            new TestHostingEnvironment(),
            Options.Create(new AuditExportOptions
            {
                DataSupplierName = "Testfirma GmbH",
                DataSupplierLocation = "Teststadt",
                Comment = "Kommentar",
                OutputDirectory = _outputDir
            }));
        return (service, logs);
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<AuditExportService>
    {
        private readonly List<string> _messages;
        public CapturingLogger(List<string> messages) => _messages = messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add($"[{logLevel}] {formatter(state, exception)} {exception?.Message ?? ""}");
        }
    }

    [Fact]
    public async Task StartJobAsync_CompletesAndPersistsHistoryWithValidZip()
    {
        var account = new MailAccount
        {
            Name = $"audit-export-test-{Guid.NewGuid():N}",
            EmailAddress = "audit-export@test.local",
            Username = "audit-export@test.local",
            Password = "x",
            ImapServer = "imap.test.local",
            ImapPort = 993,
            UseSSL = true,
            IsEnabled = false,
            LastSync = DateTime.UtcNow
        };

        using (var seedContext = _fixture.CreateContext())
        {
            seedContext.MailAccounts.Add(account);
            await seedContext.SaveChangesAsync();

            var utcNow = DateTime.UtcNow;
            seedContext.ArchivedEmails.AddRange(
                new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = "<a1@test.local>",
                    Subject = "Betreff mit; Semikolon und \"Anführungszeichen\"",
                    Body = "b", HtmlBody = string.Empty,
                    From = "from@test.local", To = "to@test.local", Cc = string.Empty, Bcc = string.Empty,
                    SentDate = utcNow.AddDays(-1), ReceivedDate = utcNow.AddDays(-1),
                    FolderName = "INBOX", IsOutgoing = false
                },
                new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = "<a2@test.local>",
                    Subject = "Umlaute: Grüße aus München",
                    Body = "b", HtmlBody = string.Empty,
                    From = "from@test.local", To = "to@test.local", Cc = string.Empty, Bcc = string.Empty,
                    SentDate = utcNow, ReceivedDate = utcNow,
                    FolderName = "INBOX", IsOutgoing = true
                },
                new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = "<outside@test.local>",
                    Subject = "Außerhalb des Zeitraums",
                    Body = "b", HtmlBody = string.Empty,
                    From = "from@test.local", To = "to@test.local", Cc = string.Empty, Bcc = string.Empty,
                    SentDate = utcNow.AddDays(-400), ReceivedDate = utcNow.AddDays(-400),
                    FolderName = "INBOX", IsOutgoing = false
                });
            await seedContext.SaveChangesAsync();
        }

        Guid jobId = Guid.Empty;
        AuditExportJob? current = null;
        try
        {
            using var setupContext = _fixture.CreateContext();
            var (service, logs) = CreateService(setupContext);

            // BackgroundService.ExecuteAsync only runs via the host; start it manually for the test
            using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await service.StartAsync(hostCts.Token);

            var job = await service.StartJobAsync(new AuditExportRequest
            {
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow.AddDays(1),
                MailAccountId = account.Id,
                IncludeAttachments = false
            }, "testadmin");
            jobId = job.Id;

            // Deterministic wait for the final state
            await service.WaitForJobAsync(job.Id, TimeSpan.FromSeconds(90));
            await service.StopAsync(CancellationToken.None);

            current = await service.GetJobAsync(job.Id);
            Assert.True(current!.Status == AuditExportJobStatus.Completed,
                $"Job status: {current.Status}, error: {current.ErrorMessage}, logs: {string.Join(" | ", logs)}");
            Assert.Equal(2, current.TotalEmails);
            Assert.True(File.Exists(current.OutputFilePath));

            // History listing returns the persisted job
            var recent = await service.GetRecentJobsAsync(20);
            Assert.Contains(recent, j => j.Id == job.Id);

            using var archive = ZipFile.OpenRead(current.OutputFilePath!);
            var entryNames = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
            Assert.Contains("INDEX.XML", entryNames);
            Assert.Contains("emails.csv", entryNames);
            Assert.Contains("index.dtd", entryNames);

            // CSV content: 2 rows, filtered period, quoted special characters, no header row
            var csvEntry = archive.GetEntry("emails.csv")!;
            using var reader = new StreamReader(csvEntry.Open(), new UTF8Encoding(false));
            var csv = await reader.ReadToEndAsync();
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain("Id;MessageId", csv); // no header
            Assert.Contains("\"Betreff mit; Semikolon und \"\"Anführungszeichen\"\"\"", csv);
            Assert.Contains("Umlaute: Grüße aus München", csv);
            Assert.DoesNotContain("Außerhalb des Zeitraums", csv);

            // Access log completion entry written
            using var logContext = _fixture.CreateContext();
            var logCount = logContext.AccessLogs
                .Count(l => l.Type == AccessLogType.AuditExport && l.Username == "testadmin");
            Assert.True(logCount >= 1, "Expected at least one AuditExport access log entry");
        }
        finally
        {
            // Cleanup: remove all rows created by this test (job history, access logs, emails, account).
            // Unlock first: the compliance trigger may block DELETEs on locked emails
            // (IsLocked changes are explicitly allowed by the trigger).
            using var cleanupContext = _fixture.CreateContext();
            cleanupContext.ArchivedEmails
                .Where(e => e.MailAccountId == account.Id && e.IsLocked)
                .ExecuteUpdate(s => s.SetProperty(e => e.IsLocked, false));
            var historyRows = cleanupContext.AuditExportJobs.Where(j => j.Id == jobId).ToList();
            foreach (var row in historyRows)
            {
                cleanupContext.AuditExportJobs.Remove(row);
            }
            var logRows = cleanupContext.AccessLogs
                .Where(l => l.Type == AccessLogType.AuditExport && l.Username == "testadmin")
                .ToList();
            cleanupContext.AccessLogs.RemoveRange(logRows);
            var emails = cleanupContext.ArchivedEmails.Where(e => e.MailAccountId == account.Id).ToList();
            cleanupContext.ArchivedEmails.RemoveRange(emails);
            var accountRow = cleanupContext.MailAccounts.FirstOrDefault(a => a.Id == account.Id);
            if (accountRow != null)
            {
                cleanupContext.MailAccounts.Remove(accountRow);
            }
            await cleanupContext.SaveChangesAsync();

            if (current?.OutputFilePath != null && File.Exists(current.OutputFilePath))
            {
                File.Delete(current.OutputFilePath);
            }
        }
    }
}