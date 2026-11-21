using MailArchiver.Models.EInvoicing;
using MailArchiver.Services.EInvoicing;

namespace MailArchiver.Tests.Services.EInvoicing;

public class InvoiceExtractorTests
{
    private static ExtractedInvoice Extract(string name)
        => InvoiceExtractor.Extract(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EInvoicing", name)));

    [Fact]
    public void Extract_Ubl_ReadsHeaderPartiesTotalsAndLines()
    {
        var invoice = Extract("xrechnung-ubl.xml");

        Assert.Equal("RE-2026-0042", invoice.Header.InvoiceNumber);
        Assert.Equal("380", invoice.Header.TypeCode);
        Assert.Equal("2026-11-02", invoice.Header.IssueDate);
        Assert.Equal("EUR", invoice.Header.Currency);
        Assert.Equal("04011000-12345-34", invoice.Header.BuyerReference);
        Assert.Equal("Beispiel Systemhaus GmbH", invoice.Seller.Name);
        Assert.Equal("Stadt Bremen", invoice.Buyer.Name);
        Assert.Equal("Treuhand Nord GmbH", invoice.Payee.Name);
        Assert.Equal("Steuerbüro Nord", invoice.TaxRepresentative.Name);
        Assert.Equal("Hamburg", invoice.ShipTo.City);
        Assert.Equal(120.00m, invoice.Totals.LineTotal);
        Assert.Equal(125.00m, invoice.Totals.TaxBasisTotal);
        Assert.Equal(21.35m, invoice.Totals.TaxTotal);
        Assert.Equal(146.35m, invoice.Totals.GrandTotal);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(2, invoice.TaxBreakdown.Count);
        Assert.Single(invoice.Allowances);
        Assert.Single(invoice.Charges);
        Assert.Equal("DE02120300000000202051", invoice.Payment.Iban);
        Assert.Equal("BYLADEM1001", invoice.Payment.Bic);
        Assert.Equal("Zahlbar innerhalb 30 Tagen ohne Abzug.", invoice.Payment.PaymentTerms);
    }

    [Fact]
    public void Extract_Cii_ReadsHeaderAndLines()
    {
        var invoice = Extract("xrechnung-cii.xml");

        Assert.Equal("RE-2026-0043", invoice.Header.InvoiceNumber);
        Assert.Equal("2026-11-05", invoice.Header.IssueDate);
        Assert.Equal("2026-12-05", invoice.Header.DueDate);
        Assert.Equal("Beispiel Systemhaus GmbH", invoice.Seller.Name);
        Assert.Equal("Stadt Bremen", invoice.Buyer.Name);
        Assert.Equal("Wartung", invoice.Lines[0].Name);
        Assert.Equal(100.00m, invoice.Lines[0].LineTotal);
        Assert.Equal(19.00m, invoice.Lines[0].TaxRate);
        Assert.Equal(119.00m, invoice.Totals.GrandTotal);
        Assert.Equal("PO-7788", invoice.References.BuyerOrderReference);
        Assert.Equal("V-2026-11", invoice.References.ContractReference);
    }

    [Fact]
    public void Extract_CreditNote_UsesCreditNoteLine()
    {
        var invoice = Extract("xrechnung-creditnote.xml");

        Assert.Equal("GS-2026-0007", invoice.Header.InvoiceNumber);
        Assert.Equal("381", invoice.Header.TypeCode);
        Assert.Single(invoice.Lines);
        Assert.Equal("Gutschrift Beratungsleistung", invoice.Lines[0].Name);
        Assert.Equal(59.50m, invoice.Totals.GrandTotal);
        Assert.Equal("RE-2026-0042", invoice.PrecedingInvoices[0].InvoiceNumber);
    }
}
