using System.Globalization;
using MailArchiver.Models.EInvoicing;
using MailArchiver.Services.EInvoicing;
using UglyToad.PdfPig;

namespace MailArchiver.Tests.Services.EInvoicing;

public class InvoicePdfRendererTests
{
    private static readonly string FontDir = Path.Combine(AppContext.BaseDirectory, "fonts");

    private static ExtractedInvoice Extract(string name)
        => InvoiceExtractor.Extract(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EInvoicing", name)));

    private static byte[] Render(ExtractedInvoice invoice, string culture = "de")
    {
        var labels = new InvoicePdfLabelProvider().For(CultureInfo.GetCultureInfo(culture));
        using var fonts = new InvoiceFonts(FontDir);
        using var renderer = new InvoicePdfRenderer(fonts, labels);
        return renderer.Render(invoice);
    }

    private static string AllText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join("\n", document.GetPages().Select(p => p.Text));
    }

    [Fact]
    public void Render_Ubl_ContainsHeaderPartiesAndTotals()
    {
        var pdf = Render(Extract("xrechnung-ubl.xml"));

        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        string text = AllText(pdf);
        Assert.Contains("RE-2026-0042", text);
        Assert.Contains("Beispiel Systemhaus GmbH", text);
        Assert.Contains("Stadt Bremen", text);
        Assert.Contains("146,35", text);
        Assert.Contains("Zahlbarer Betrag", text);
        Assert.Contains("Seite 1 von", text);
    }

    [Fact]
    public void Render_CreditNote_UsesCreditNoteTitle()
    {
        string text = AllText(Render(Extract("xrechnung-creditnote.xml")));

        Assert.Contains("Gutschrift", text);
        Assert.Contains("GS-2026-0007", text);
        Assert.Contains("59,50", text);
    }

    [Fact]
    public void Render_CjkText_IsExtractable()
    {
        var invoice = Extract("xrechnung-ubl.xml");
        invoice.Lines[0].Name = "テスト商品";

        string text = AllText(Render(invoice));

        Assert.Contains("テスト商品", text);
    }

    [Fact]
    public void Render_ManyLines_TruncatesAtThousand()
    {
        var invoice = Extract("xrechnung-ubl.xml");
        invoice.Lines = Enumerable.Range(1, 1001)
            .Select(i => new ExtractedLine
            {
                LineId = i.ToString(CultureInfo.InvariantCulture),
                Name = $"Position {i}",
                Quantity = 1,
                NetPrice = 1,
                LineTotal = 1,
                TaxRate = 19
            })
            .ToList();
        invoice.Totals = new ExtractedTotals
        {
            LineTotal = 1001,
            TaxBasisTotal = 1001,
            TaxTotal = 190.19m,
            GrandTotal = 1191.19m,
            DuePayable = 1191.19m
        };
        invoice.TaxBreakdown = new List<ExtractedTax>
        {
            new() { Category = "S", Rate = 19, BasisAmount = 1001, TaxAmount = 190.19m }
        };

        string text = AllText(Render(invoice));

        Assert.Contains("Weitere 1 Positionen", text);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("hu")]
    [InlineData("it")]
    [InlineData("ja")]
    [InlineData("nl")]
    [InlineData("pl")]
    [InlineData("pt-BR")]
    [InlineData("ru")]
    [InlineData("sl")]
    [InlineData("zh")]
    public void Render_AllCultures_ProducePdf(string culture)
    {
        var pdf = Render(Extract("xrechnung-ubl.xml"), culture);

        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages >= 1);
    }
}
