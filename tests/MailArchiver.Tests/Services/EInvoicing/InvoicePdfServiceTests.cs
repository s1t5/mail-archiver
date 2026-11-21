using System.Globalization;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.EInvoicing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailArchiver.Tests.Services.EInvoicing;

public class InvoicePdfServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MailArchiverDbContext _context;
    private readonly InvoicePdfService _service;

    public InvoicePdfServiceTests()
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

        _service = new InvoicePdfService(
            _context,
            new InvoicePdfLabelProvider(),
            environment,
            NullLogger<InvoicePdfService>.Instance);
    }

    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EInvoicing", name));

    private async Task<(ArchivedEmail Email, EmailAttachment Attachment)> SeedAsync(
        string fileName, string contentType, byte[] content)
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

        var attachment = new EmailAttachment
        {
            ArchivedEmailId = email.Id,
            FileName = fileName,
            ContentType = contentType,
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
    public async Task RenderAsync_XRechnung_ReturnsPdfAndFileName()
    {
        var (email, attachment) = await SeedAsync(
            "rechnung.xml", "application/xml", Fixture("xrechnung-ubl.xml"));

        var result = await _service.RenderAsync(
            email.Id, attachment.Id, CultureInfo.GetCultureInfo("de"));

        Assert.NotNull(result);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(result!.Content, 0, 5));
        Assert.Equal("Rechnung-RE-2026-0042.pdf", result.FileName);
        Assert.Equal("UBL", result.Info.Format);
    }

    [Fact]
    public async Task RenderAsync_CreditNote_UsesCreditNoteFileName()
    {
        var (email, attachment) = await SeedAsync(
            "gutschrift.xml", "application/xml", Fixture("xrechnung-creditnote.xml"));

        var result = await _service.RenderAsync(
            email.Id, attachment.Id, CultureInfo.GetCultureInfo("de"));

        Assert.NotNull(result);
        Assert.Equal("Gutschrift-GS-2026-0007.pdf", result!.FileName);
    }

    [Fact]
    public async Task RenderAsync_PeppolWithoutXRechnung_ReturnsNull()
    {
        var (email, attachment) = await SeedAsync(
            "rechnung.xml", "application/xml", Fixture("ubl-peppol.xml"));

        Assert.Null(await _service.RenderAsync(
            email.Id, attachment.Id, CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public async Task RenderAsync_NonXmlAttachment_ReturnsNull()
    {
        var (email, attachment) = await SeedAsync(
            "rechnung.pdf", "application/pdf", Fixture("xrechnung-ubl.xml"));

        Assert.Null(await _service.RenderAsync(
            email.Id, attachment.Id, CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public async Task RenderAsync_ForeignAttachment_ReturnsNull()
    {
        var (email, _) = await SeedAsync(
            "rechnung.xml", "application/xml", Fixture("xrechnung-ubl.xml"));
        var (_, otherAttachment) = await SeedAsync(
            "andere.xml", "application/xml", Fixture("xrechnung-cii.xml"));

        Assert.Null(await _service.RenderAsync(
            email.Id, otherAttachment.Id, CultureInfo.GetCultureInfo("de")));
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
