using System.Globalization;
using MailArchiver.Attributes;
using MailArchiver.Services.EInvoicing;
using Microsoft.AspNetCore.Mvc;

namespace MailArchiver.Controllers;

[UserAccessRequired]
[Route("InvoicePreview")]
public class InvoicePreviewController : Controller
{
    private readonly InvoicePdfService _invoicePdf;
    private readonly ILogger<InvoicePreviewController> _logger;

    public InvoicePreviewController(
        InvoicePdfService invoicePdf,
        ILogger<InvoicePreviewController> logger)
    {
        _invoicePdf = invoicePdf;
        _logger = logger;
    }

    [HttpGet("{emailId:int}/{attachmentId:int}")]
    [EmailAccessRequired]
    public Task<IActionResult> Inline(int emailId, int attachmentId)
        => RenderAsync(emailId, attachmentId, download: false);

    [HttpGet("{emailId:int}/{attachmentId:int}/download")]
    [EmailAccessRequired]
    public Task<IActionResult> Download(int emailId, int attachmentId)
        => RenderAsync(emailId, attachmentId, download: true);

    private async Task<IActionResult> RenderAsync(int emailId, int attachmentId, bool download)
    {
        var result = await _invoicePdf.RenderAsync(
            emailId, attachmentId, CultureInfo.CurrentUICulture);

        if (result == null)
        {
            _logger.LogInformation(
                "No XRechnung preview for attachment {AttachmentId} of email {EmailId}",
                attachmentId, emailId);
            return NotFound();
        }

        if (download)
            return File(result.Content, "application/pdf", result.FileName);

        return File(result.Content, "application/pdf");
    }
}
