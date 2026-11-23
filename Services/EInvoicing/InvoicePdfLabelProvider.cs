using System.Globalization;
using System.Resources;

namespace MailArchiver.Services.EInvoicing;

public sealed class InvoicePdfLabelProvider
{
    private static readonly ResourceManager Resources = new(
        "MailArchiver.Resources.SharedResource", typeof(SharedResource).Assembly);

    public InvoicePdfLabels For(CultureInfo culture)
    {
        var values = InvoicePdfLabelKeys.All
            .ToDictionary(k => k, k => Resources.GetString(k, culture) ?? k);
        return new InvoicePdfLabels(values, culture);
    }
}
