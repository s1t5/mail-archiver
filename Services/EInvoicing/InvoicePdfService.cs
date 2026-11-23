using System.Globalization;
using System.Xml;
using MailArchiver.Data;
using MailArchiver.Models.EInvoicing;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.EInvoicing;

public sealed record InvoicePdfResult(byte[] Content, string FileName, XRechnungInfo Info);

/// <summary>
/// Erzeugt die PDF-Vorschau einer XRechnung bei Bedarf. Es wird nichts
/// gespeichert und nichts validiert; Grundlage ist ausschließlich der
/// XML-Anhang.
/// </summary>
public sealed class InvoicePdfService
{
    private readonly MailArchiverDbContext _context;
    private readonly InvoicePdfLabelProvider _labels;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<InvoicePdfService> _logger;

    public InvoicePdfService(
        MailArchiverDbContext context,
        InvoicePdfLabelProvider labels,
        IWebHostEnvironment environment,
        ILogger<InvoicePdfService> logger)
    {
        _context = context;
        _labels = labels;
        _environment = environment;
        _logger = logger;
    }

    public async Task<InvoicePdfResult?> RenderAsync(
        int emailId,
        int attachmentId,
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _context.EmailAttachments
            .AsNoTracking()
            .Include(a => a.AttachmentContent)
            .Include(a => a.ArchivedEmail)
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.ArchivedEmailId == emailId,
                cancellationToken);

        if (attachment == null) return null;

        if (!XRechnungDetector.LooksLikeXml(attachment.FileName, attachment.ContentType))
            return null;

        byte[] content = attachment.Content;
        if (content.Length == 0 || content.Length > XRechnungDetector.MaxBytes)
            return null;

        var info = XRechnungDetector.Detect(content);
        if (info == null) return null;

        ExtractedInvoice invoice;
        try
        {
            invoice = InvoiceExtractor.Extract(content);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex,
                "XRechnung attachment {AttachmentId} of email {EmailId} could not be extracted",
                attachmentId, emailId);
            return null;
        }

        string fontDir = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "fonts");

        var labels = _labels.For(culture);
        using var fonts = new InvoiceFonts(fontDir);
        using var renderer = new InvoicePdfRenderer(fonts, labels);
        byte[] pdf = renderer.Render(invoice);

        return new InvoicePdfResult(pdf, BuildFileName(invoice, attachment.FileName), info);
    }

    private static string BuildFileName(ExtractedInvoice invoice, string? attachmentFileName)
    {
        string number = Sanitize(invoice.Header?.InvoiceNumber);
        if (!string.IsNullOrWhiteSpace(number))
        {
            string prefix = invoice.Header?.TypeCode == "381" ? "Gutschrift" : "Rechnung";
            return $"{prefix}-{number}.pdf";
        }

        string baseName = Sanitize(Path.GetFileNameWithoutExtension(attachmentFileName ?? ""));
        return string.IsNullOrWhiteSpace(baseName) ? "Rechnung.pdf" : $"{baseName}.pdf";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim()
            .Select(c => invalid.Contains(c) || c is '/' or '\\' or ':' ? '-' : c)
            .ToArray());

        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);

        return cleaned.Trim('-', '.', ' ');
    }
}
