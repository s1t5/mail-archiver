using System.Globalization;
using MailArchiver.Controllers;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.EInvoicing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailArchiver.Tests.Controllers;

public class InvoicePreviewControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;
    private readonly InvoicePreviewController _controller;

    public InvoicePreviewControllerTests()
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
        var service = new InvoicePdfService(
            _context,
            new InvoicePdfLabelProvider(),
            environment,
            NullLogger<InvoicePdfService>.Instance);

        _controller = new InvoicePreviewController(
            service,
            NullLogger<InvoicePreviewController>.Instance);
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
    public async Task Inline_ForXRechnung_ReturnsInlinePdf()
    {
        var (email, attachment) = await SeedAsync("xrechnung-ubl.xml");

        var result = Assert.IsType<FileContentResult>(
            await _controller.Inline(email.Id, attachment.Id));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(string.IsNullOrEmpty(result.FileDownloadName));
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(result.FileContents, 0, 5));
    }

    [Fact]
    public async Task Download_ForXRechnung_ReturnsNamedPdf()
    {
        var (email, attachment) = await SeedAsync("xrechnung-ubl.xml");

        var result = Assert.IsType<FileContentResult>(
            await _controller.Download(email.Id, attachment.Id));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("Rechnung-RE-2026-0042.pdf", result.FileDownloadName);
    }

    [Fact]
    public async Task Inline_ForPeppol_ReturnsNotFound()
    {
        var (email, attachment) = await SeedAsync("ubl-peppol.xml");

        Assert.IsType<NotFoundResult>(await _controller.Inline(email.Id, attachment.Id));
    }

    [Fact]
    public async Task Inline_ForUnknownAttachment_ReturnsNotFound()
    {
        var (email, _) = await SeedAsync("xrechnung-ubl.xml");

        Assert.IsType<NotFoundResult>(await _controller.Inline(email.Id, 9999));
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
