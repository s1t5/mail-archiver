using System.Globalization;
using System.Resources;
using MailArchiver.Services.EInvoicing;

namespace MailArchiver.Tests.Services.EInvoicing;

public class InvoicePdfLabelProviderTests
{
    [Fact]
    public void Resx_AllCultures_ContainAllKeys()
    {
        var rm = new ResourceManager("MailArchiver.Resources.SharedResource",
            typeof(SharedResource).Assembly);

        string[] cultures =
        {
            "de", "en", "en-GB", "es", "fr", "hu", "it", "ja",
            "nl", "pl", "pt-BR", "ru", "sl", "zh"
        };

        foreach (var culture in cultures)
        {
            var ci = CultureInfo.GetCultureInfo(culture);
            foreach (var key in InvoicePdfLabelKeys.All.Concat(new[]
                     {
                         "XRechnung", "DownloadPdf", "DownloadOriginalXml", "InvoicePreviewNotice"
                     }))
            {
                Assert.False(string.IsNullOrWhiteSpace(rm.GetString(key, ci)), $"{culture}:{key}");
            }
        }
    }
}
