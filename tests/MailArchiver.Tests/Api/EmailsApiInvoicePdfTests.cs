using System.Globalization;
using MailArchiver.Controllers.Api.V1;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.EInvoicing;
using MailArchiver.Services.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MailArchiver.Tests.Api;

public class EmailsApiInvoicePdfTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;
    private readonly InvoicePdfService _invoicePdf;
    private readonly Mock<IAccountAccessResolver> _resolver = new();

    public EmailsApiInvoicePdfTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<MailArchiverDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new MailArchiverDbContext(options);
        _context.Database.EnsureCreated();

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var environment = new TestEnvironment
        {
            WebRootPath = Path.Combine(root, "wwwroot"),
            ContentRootPath = root
        };
        _invoicePdf = new InvoicePdfService(
            _context,
            new InvoicePdfLabelProvider(),
            environment,
            NullLogger<InvoicePdfService>.Instance);
    }

    private EmailsApiController CreateController(bool allowDownloads = true)
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.GetCurrentUserDisplayName(It.IsAny<HttpContext>())).Returns("tester");

        var controller = new EmailsApiController(
            _context,
            null!,
            Options.Create(new ApiOptions { AllowAttachmentDownloads = allowDownloads }),
            Mock.Of<IAccessLogService>(),
            auth.Object,
            null!,
            Mock.Of<IRawMessageStore>(),
            _invoicePdf);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        services.AddSingleton(_resolver.Object);
        var provider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider }
        };

        return controller;
    }

    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EInvoicing", name));

    private async Task<(ArchivedEmail Email, EmailAttachment Attachment)> SeedAsync(string fixture)
    {
        var account = new MailAccount
        {
            Name = "Test",
            EmailAddress = "test@example.org",
            ImapServer = "imap.example.org",
            ImapPort = 993,
            Username = "test",
            Password = "secret"
        };
        _context.MailAccounts.Add(account);
        await _context.SaveChangesAsync();

        var email = new ArchivedEmail
        {
            MailAccountId = account.Id,
            MessageId = "<1@example.org>",
            Subject = "Rechnung",
            Body = "Text",
            HtmlBody = "<p>Text</p>",
            From = "sender@example.org",
            To = "test@example.org",
            Cc = "",
            Bcc = "",
            FolderName = "INBOX",
            HasAttachments = true
        };
        _context.ArchivedEmails.Add(email);
        await _context.SaveChangesAsync();

        var content = Fixture(fixture);
        var attachment = new EmailAttachment
        {
            ArchivedEmailId = email.Id,
            FileName = "rechnung.xml",
            ContentType = "application/xml",
            Size = content.Length,
            AttachmentContent = new AttachmentContent
            {
                Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant(),
                Content = content,
                Size = content.Length
            }
        };
        _context.EmailAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return (email, attachment);
    }

    [Fact]
    public async Task InvoicePdf_ForXRechnung_ReturnsPdf()
    {
        var (email, attachment) = await SeedAsync("xrechnung-ubl.xml");
        _resolver.Setup(r => r.GetAllowedAccountIdsAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync((List<int>?)null);

        var result = Assert.IsType<FileContentResult>(
            await CreateController().DownloadInvoicePdf(email.Id, attachment.Id));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("Rechnung-RE-2026-0042.pdf", result.FileDownloadName);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(result.FileContents, 0, 5));
    }

    [Fact]
    public async Task InvoicePdf_WhenDownloadsDisabled_Returns403()
    {
        var (email, attachment) = await SeedAsync("xrechnung-ubl.xml");

        var result = Assert.IsType<ObjectResult>(
            await CreateController(allowDownloads: false).DownloadInvoicePdf(email.Id, attachment.Id));

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task InvoicePdf_ForNonXRechnung_ReturnsNotFound()
    {
        var (email, attachment) = await SeedAsync("ubl-peppol.xml");
        _resolver.Setup(r => r.GetAllowedAccountIdsAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync((List<int>?)null);

        Assert.IsType<NotFoundResult>(
            await CreateController().DownloadInvoicePdf(email.Id, attachment.Id));
    }

    [Fact]
    public async Task InvoicePdf_ForForeignAccount_ReturnsNotFound()
    {
        var (email, attachment) = await SeedAsync("xrechnung-ubl.xml");
        _resolver.Setup(r => r.GetAllowedAccountIdsAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new List<int> { email.MailAccountId + 1 });

        Assert.IsType<NotFoundResult>(
            await CreateController().DownloadInvoicePdf(email.Id, attachment.Id));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MailArchiver.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
