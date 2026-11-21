using System.Globalization;
using System.Resources;
using MailArchiver.Services.EInvoicing;

namespace MailArchiver.Tests.Services.EInvoicing;

public class InvoicePdfLabelProviderTests
{
    [Fact]
    public void Resx_GermanAndEnglish_ContainAllKeys()
    {
        var rm = new ResourceManager("MailArchiver.Resources.SharedResource",
            typeof(SharedResource).Assembly);

        foreach (var key in InvoicePdfLabelKeys.All.Concat(new[]
                 {
                     "XRechnung", "DownloadPdf", "DownloadOriginalXml", "InvoicePreviewNotice"
                 }))
        {
            Assert.False(string.IsNullOrWhiteSpace(rm.GetString(key, CultureInfo.GetCultureInfo("de"))), key);
            Assert.False(string.IsNullOrWhiteSpace(rm.GetString(key, CultureInfo.GetCultureInfo("en"))), key);
        }
    }
}
