using System.Globalization;

namespace MailArchiver.Services.EInvoicing;

public sealed class InvoicePdfLabels
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public InvoicePdfLabels(IReadOnlyDictionary<string, string> values, CultureInfo culture)
    {
        _values = values;
        Culture = culture;
    }

    public CultureInfo Culture { get; }

    public string this[string key] => _values.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object[] args) => string.Format(this[key], args);
}

public static class InvoicePdfLabelKeys
{
    public const string Invoice = "InvoicePdf.Invoice";
    public const string CreditNote = "InvoicePdf.CreditNote";
    public const string InvoiceNumber = "InvoicePdf.InvoiceNumber";
    public const string InvoiceDate = "InvoicePdf.InvoiceDate";
    public const string DueShort = "InvoicePdf.DueShort";
    public const string Seller = "InvoicePdf.Seller";
    public const string Buyer = "InvoicePdf.Buyer";
    public const string PayeeDifferent = "InvoicePdf.PayeeDifferent";
    public const string ShipTo = "InvoicePdf.ShipTo";
    public const string TaxRepresentative = "InvoicePdf.TaxRepresentative";
    public const string Contact = "InvoicePdf.Contact";
    public const string VatId = "InvoicePdf.VatId";
    public const string TaxNumber = "InvoicePdf.TaxNumber";
    public const string Register = "InvoicePdf.Register";
    public const string Details = "InvoicePdf.Details";
    public const string BuyerReference = "InvoicePdf.BuyerReference";
    public const string Currency = "InvoicePdf.Currency";
    public const string DueDate = "InvoicePdf.DueDate";
    public const string DeliveryDate = "InvoicePdf.DeliveryDate";
    public const string BillingPeriod = "InvoicePdf.BillingPeriod";
    public const string BuyerOrder = "InvoicePdf.BuyerOrder";
    public const string SalesOrder = "InvoicePdf.SalesOrder";
    public const string Contract = "InvoicePdf.Contract";
    public const string Project = "InvoicePdf.Project";
    public const string DespatchAdvice = "InvoicePdf.DespatchAdvice";
    public const string BuyerAccount = "InvoicePdf.BuyerAccount";
    public const string SellerVatId = "InvoicePdf.SellerVatId";
    public const string BuyerVatId = "InvoicePdf.BuyerVatId";
    public const string Lines = "InvoicePdf.Lines";
    public const string Description = "InvoicePdf.Description";
    public const string Quantity = "InvoicePdf.Quantity";
    public const string Unit = "InvoicePdf.Unit";
    public const string UnitPrice = "InvoicePdf.UnitPrice";
    public const string TaxRate = "InvoicePdf.TaxRate";
    public const string Amount = "InvoicePdf.Amount";
    public const string NoLines = "InvoicePdf.NoLines";
    public const string AllowancesCharges = "InvoicePdf.AllowancesCharges";
    public const string Allowance = "InvoicePdf.Allowance";
    public const string Charge = "InvoicePdf.Charge";
    public const string SumLines = "InvoicePdf.SumLines";
    public const string Charges = "InvoicePdf.Charges";
    public const string Allowances = "InvoicePdf.Allowances";
    public const string TaxBasis = "InvoicePdf.TaxBasis";
    public const string GrandTotal = "InvoicePdf.GrandTotal";
    public const string Prepaid = "InvoicePdf.Prepaid";
    public const string Rounding = "InvoicePdf.Rounding";
    public const string Payable = "InvoicePdf.Payable";
    public const string PaymentMeans = "InvoicePdf.PaymentMeans";
    public const string Iban = "InvoicePdf.Iban";
    public const string Bic = "InvoicePdf.Bic";
    public const string AccountHolder = "InvoicePdf.AccountHolder";
    public const string PaymentReference = "InvoicePdf.PaymentReference";
    public const string MandateReference = "InvoicePdf.MandateReference";
    public const string PaymentTerms = "InvoicePdf.PaymentTerms";
    public const string PaymentInfo = "InvoicePdf.PaymentInfo";
    public const string Notes = "InvoicePdf.Notes";
    public const string PageOf = "InvoicePdf.PageOf";
    public const string MoreLines = "InvoicePdf.MoreLines";
    public const string Disclaimer = "InvoicePdf.Disclaimer";
    public const string NotStated = "InvoicePdf.NotStated";

    public static readonly string[] All =
    {
        Invoice, CreditNote, InvoiceNumber, InvoiceDate, DueShort, Seller, Buyer,
        PayeeDifferent, ShipTo, TaxRepresentative, Contact, VatId, TaxNumber,
        Register, Details, BuyerReference, Currency, DueDate, DeliveryDate,
        BillingPeriod, BuyerOrder, SalesOrder, Contract, Project, DespatchAdvice,
        BuyerAccount, SellerVatId, BuyerVatId, Lines, Description, Quantity, Unit,
        UnitPrice, TaxRate, Amount, NoLines, AllowancesCharges, Allowance, Charge,
        SumLines, Charges, Allowances, TaxBasis, GrandTotal, Prepaid, Rounding,
        Payable, PaymentMeans, Iban, Bic, AccountHolder, PaymentReference,
        MandateReference, PaymentTerms, PaymentInfo, Notes, PageOf, MoreLines,
        Disclaimer, NotStated
    };
}
