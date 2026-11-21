
namespace MailArchiver.Models.EInvoicing;

/// <summary>Strukturierte Rechnungsdaten (Kopf, Parteien, Summen, Steuern und Positionen).</summary>
public sealed class ExtractedInvoice
{
    public ExtractedHeader Header { get; set; } = new();
    public ExtractedParty Seller { get; set; }
    public ExtractedParty Buyer { get; set; }
    /// <summary>Abweichender Zahlungsempfänger (BG-10), falls angegeben</summary>
    public ExtractedParty Payee { get; set; }
    /// <summary>Steuervertreter des Verkäufers (BG-11), falls angegeben</summary>
    public ExtractedParty TaxRepresentative { get; set; }
    /// <summary>Lieferadresse (BG-13), falls angegeben</summary>
    public ExtractedParty ShipTo { get; set; }
    public ExtractedTotals Totals { get; set; }
    public ExtractedPayment Payment { get; set; }
    public ExtractedReferences References { get; set; }
    public List<ExtractedTax> TaxBreakdown { get; set; } = new();
    /// <summary>Dokumentweite Abschläge (BG-20)</summary>
    public List<ExtractedAllowanceCharge> Allowances { get; set; } = new();
    /// <summary>Dokumentweite Zuschläge (BG-21)</summary>
    public List<ExtractedAllowanceCharge> Charges { get; set; } = new();
    /// <summary>Belegnotizen (BT-22)</summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>Notiz-Subject-Codes (BT-21), falls vorhanden</summary>
    public List<string> NoteSubjectCodes { get; set; } = new();
    /// <summary>Weitere Belegdokumente/Anhänge (BG-24, nur Metadaten — keine Binärdaten)</summary>
    public List<ExtractedSupportingDocument> SupportingDocuments { get; set; } = new();
    /// <summary>Vorgänger-Rechnungen bei Korrekturen (BT-25/26)</summary>
    public List<ExtractedPrecedingInvoice> PrecedingInvoices { get; set; } = new();
    public List<ExtractedLine> Lines { get; set; } = new();


}

public sealed class ExtractedHeader
{
    /// <summary>BT-1 Rechnungsnummer</summary>
    public string InvoiceNumber { get; set; }
    /// <summary>BT-3 Dokumentart (Code, z.B. 380 Rechnung / 381 Gutschrift)</summary>
    public string TypeCode { get; set; }
    /// <summary>Lesbare Dokumentart</summary>
    public string TypeText { get; set; }
    /// <summary>BT-2 Rechnungsdatum (ISO yyyy-MM-dd)</summary>
    public string IssueDate { get; set; }
    /// <summary>BT-9 Fälligkeitsdatum (falls vorhanden)</summary>
    public string DueDate { get; set; }
    /// <summary>BT-5 Währung</summary>
    public string Currency { get; set; }
    /// <summary>BT-6 Buchungswährung (falls abweichend)</summary>
    public string TaxCurrency { get; set; }
    /// <summary>BT-7 USt-Datum (falls vorhanden)</summary>
    public string TaxPointDate { get; set; }
    /// <summary>BT-8 USt-Datum als Code (Monatskürzel, falls vorhanden)</summary>
    public string TaxPointDateCode { get; set; }
    /// <summary>BT-23 Geschäftsprozess (z. B. Peppol BIS)</summary>
    public string BusinessProcess { get; set; }
    /// <summary>BT-10 Käuferreferenz (Leitweg-ID bei XRechnung)</summary>
    public string BuyerReference { get; set; }
    /// <summary>BT-69 tatsächliches Liefer-/Leistungsdatum</summary>
    public string DeliveryDate { get; set; }
    /// <summary>BT-73 Abrechnungszeitraum Beginn</summary>
    public string PeriodStart { get; set; }
    /// <summary>BT-74 Abrechnungszeitraum Ende</summary>
    public string PeriodEnd { get; set; }
}

public sealed class ExtractedParty
{
    public string Name { get; set; }
    /// <summary>Handelsname (BT-28/BT-45), falls abweichend</summary>
    public string TradingName { get; set; }
    /// <summary>USt-IdNr. (BT-31/BT-48)</summary>
    public string VatId { get; set; }
    /// <summary>Steuernummer (BT-32/BT-49, schemeID=FC)</summary>
    public string TaxNumber { get; set; }
    /// <summary>Legal-/Registrierungs-ID (Handelsregister etc.)</summary>
    public string LegalId { get; set; }
    /// <summary>Zusätzliche rechtliche Angaben (BT-33)</summary>
    public string LegalInformation { get; set; }
    /// <summary>Globale ID (GLN, DUNS etc.) inkl. Schema-Kürzel</summary>
    public string GlobalId { get; set; }
    /// <summary>Global-ID-Schema (z.B. 0088=GLN, 0204=SIRET)</summary>
    public string GlobalIdScheme { get; set; }
    /// <summary>Elektronische Adresse (BT-34/BT-49)</summary>
    public string ElectronicAddress { get; set; }
    /// <summary>Schema der elektronischen Adresse (z.B. EM=E-Mail, AN=EAN)</summary>
    public string ElectronicAddressScheme { get; set; }
    public string Street { get; set; }
    /// <summary>Adresszeile 2 (falls vorhanden)</summary>
    public string Street2 { get; set; }
    /// <summary>Adresszeile 3 (falls vorhanden)</summary>
    public string Street3 { get; set; }
    public string Zip { get; set; }
    public string City { get; set; }
    /// <summary>Bundesland/Region (falls vorhanden)</summary>
    public string Region { get; set; }
    public string Country { get; set; }
    /// <summary>Kontakt-E-Mail (BT-43/BT-58); alternativ elektronische Adresse mit schemeID=EM</summary>
    public string Email { get; set; }
    public string Phone { get; set; }
    /// <summary>Ansprechpartner (DefinedTradeContact/PersonName)</summary>
    public string ContactName { get; set; }
}

public sealed class ExtractedTotals
{
    /// <summary>BT-106 Summe Positionen netto</summary>
    public decimal? LineTotal { get; set; }
    /// <summary>BT-108 Summe Zuschläge (BG-21)</summary>
    public decimal? ChargeTotal { get; set; }
    /// <summary>BT-107 Summe Abschläge (BG-20)</summary>
    public decimal? AllowanceTotal { get; set; }
    /// <summary>BT-109 Steuerbasis</summary>
    public decimal? TaxBasisTotal { get; set; }
    /// <summary>BT-110 Steuerbetrag</summary>
    public decimal? TaxTotal { get; set; }
    /// <summary>BT-111 Steuerbetrag in Buchungswährung (falls abweichend)</summary>
    public decimal? TaxTotalAccounting { get; set; }
    /// <summary>BT-112 Gesamtsumme brutto</summary>
    public decimal? GrandTotal { get; set; }
    /// <summary>BT-113 bereits gezahlt (falls vorhanden)</summary>
    public decimal? TotalPrepaid { get; set; }
    /// <summary>BT-114 Rundungsbetrag</summary>
    public decimal? RoundingAmount { get; set; }
    /// <summary>BT-115 zahlbarer Betrag</summary>
    public decimal? DuePayable { get; set; }
}

public sealed class ExtractedTax
{
    /// <summary>Kategorie (S, Z, E, K, G, O, AE...)</summary>
    public string Category { get; set; }
    /// <summary>Steuersatz in Prozent</summary>
    public decimal? Rate { get; set; }
    /// <summary>BT-116 Steuerbasis der Kategorie</summary>
    public decimal? BasisAmount { get; set; }
    /// <summary>BT-117 Steuerbetrag der Kategorie</summary>
    public decimal? TaxAmount { get; set; }
    /// <summary>Steuerbefreiungsgrund (BT-120)</summary>
    public string ExemptionReason { get; set; }
    /// <summary>Steuerbefreiungs-Code (BT-121)</summary>
    public string ExemptionReasonCode { get; set; }
}

public sealed class ExtractedLine
{
    /// <summary>Positionsnummer (BT-126)</summary>
    public string LineId { get; set; }
    /// <summary>Artikelbezeichnung (BT-153)</summary>
    public string Name { get; set; }
    /// <summary>Beschreibung (BT-154)</summary>
    public string Description { get; set; }
    /// <summary>Erster Positionshinweis (BT-127, Kompatibilität)</summary>
    public string Note { get; set; }
    /// <summary>Alle Positionshinweise (BT-127)</summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>Menge (BT-129/BT-130)</summary>
    public decimal? Quantity { get; set; }
    public string Unit { get; set; }
    /// <summary>Netto-Einzelpreis (BT-146)</summary>
    public decimal? NetPrice { get; set; }
    /// <summary>BT-148 Brutto-Einzelpreis (vor Rabatt), falls vorhanden</summary>
    public decimal? GrossPrice { get; set; }
    /// <summary>Preisbasis-Menge (Preis gilt pro X Einheiten, BT-149/150)</summary>
    public decimal? PriceBasisQuantity { get; set; }
    public string PriceBasisUnit { get; set; }
    /// <summary>Preisrabatt auf den Brutto-Preis (falls vorhanden)</summary>
    public decimal? PriceDiscount { get; set; }
    /// <summary>Positionssumme netto (BT-131)</summary>
    public decimal? LineTotal { get; set; }
    /// <summary>Steuersatz der Position</summary>
    public decimal? TaxRate { get; set; }
    public string TaxCategory { get; set; }
    /// <summary>BT-155 Verkäufer-Artikelnummer</summary>
    public string SellerAssignedId { get; set; }
    /// <summary>BT-156 Käufer-Artikelnummer</summary>
    public string BuyerAssignedId { get; set; }
    /// <summary>BT-157 globale Artikel-ID (GTIN etc.)</summary>
    public string GlobalId { get; set; }
    public string GlobalIdScheme { get; set; }
    /// <summary>BT-159 Herkunftsland</summary>
    public string CountryOfOrigin { get; set; }
    /// <summary>Bestellposition des Käufers (BT-132)</summary>
    public string OrderLineReference { get; set; }
    /// <summary>BT-128 Objektreferenz der Position (z. B. Zählerstand)</summary>
    public string LineObjectReference { get; set; }
    /// <summary>BT-133 Käufer-Buchungsreferenz der Position</summary>
    public string BuyerAccountReference { get; set; }
    /// <summary>Positions-Abschläge (BG-26)</summary>
    public List<ExtractedAllowanceCharge> Allowances { get; set; } = new();
    /// <summary>Positions-Zuschläge (BG-27)</summary>
    public List<ExtractedAllowanceCharge> Charges { get; set; } = new();
    /// <summary>Artikelmerkmale (BT-160/161)</summary>
    public List<ExtractedLineAttribute> Attributes { get; set; } = new();
    /// <summary>Abrechnungszeitraum der Position (falls vorhanden)</summary>
    public string PeriodStart { get; set; }
    public string PeriodEnd { get; set; }
    /// <summary>Produktklassifizierungen (BT-158): Schema-Kennung + Code</summary>
    public List<ExtractedClassification> Classifications { get; set; } = new();
}

public sealed class ExtractedClassification
{
    /// <summary>Schema-Kennung der Klassifizierung (ListID)</summary>
    public string Scheme { get; set; }
    /// <summary>Klassifizierungs-Code</summary>
    public string Code { get; set; }
    public string Name { get; set; }
}

/// <summary>Dokumentweite Abschläge (BG-20) / Zuschläge (BG-21).</summary>
public sealed class ExtractedAllowanceCharge
{
    /// <summary>Betrag</summary>
    public decimal? Amount { get; set; }
    /// <summary>Grund (BT-97 Abschlag / BT-104 Zuschlag)</summary>
    public string Reason { get; set; }
    /// <summary>Grund-Code (BT-98 Abschlag / BT-105 Zuschlag)</summary>
    public string ReasonCode { get; set; }
    /// <summary>Prozentsatz (BT-94 Abschlag / BT-101 Zuschlag)</summary>
    public decimal? Percent { get; set; }
    /// <summary>Steuerkategorie der Position (BT-95/BT-102)</summary>
    public string TaxCategory { get; set; }
    /// <summary>Steuersatz (BT-96/BT-103)</summary>
    public decimal? TaxRate { get; set; }
}

/// <summary>Vorgänger-Rechnung (BT-25/26) bei Korrekturen/Gutschriften.</summary>
public sealed class ExtractedPrecedingInvoice
{
    /// <summary>BT-25 Rechnungsnummer der Vorgängerrechnung</summary>
    public string InvoiceNumber { get; set; }
    /// <summary>BT-26 Datum der Vorgängerrechnung</summary>
    public string IssueDate { get; set; }
}

/// <summary>Zahlungsdaten (BT-81..89, BT-9/20).</summary>
public sealed class ExtractedPayment
{
    /// <summary>BT-81 Zahlungsart-Code (30 Überweisung, 58 SEPA, 57 Dauerauftrag …)</summary>
    public string PaymentMeansCode { get; set; }
    /// <summary>Lesbare Zahlungsart</summary>
    public string PaymentMeansText { get; set; }
    /// <summary>BT-84 IBAN des Zahlungsempfängers</summary>
    public string Iban { get; set; }
    /// <summary>BT-85 Kontoinhaber</summary>
    public string AccountName { get; set; }
    /// <summary>BT-86 BIC</summary>
    public string Bic { get; set; }
    /// <summary>BT-89 SEPA-Mandatsreferenz</summary>
    public string MandateReference { get; set; }
    /// <summary>BT-90 abgebuchtes Konto (Lastschrift, falls angegeben)</summary>
    public string DebitedAccountIban { get; set; }
    /// <summary>BT-87 Kartennummer (maskierte PAN)</summary>
    public string CardNumber { get; set; }
    /// <summary>BT-88 Karteninhaber</summary>
    public string CardHolderName { get; set; }
    /// <summary>Kartennetzwerk (Zusatzinfo, kein BT)</summary>
    public string CardNetwork { get; set; }
    /// <summary>BT-20 Zahlungsbedingungen (Text)</summary>
    public string PaymentTerms { get; set; }
    /// <summary>BT-9 Fälligkeitsdatum</summary>
    public string DueDate { get; set; }
    /// <summary>BT-83 Verwendungszweck/Referenz bei der Zahlung</summary>
    public string PaymentReference { get; set; }
    /// <summary>Per Lastschrift eingezogen bzw. einziehbar (Code 49/59 oder Mandat vorhanden)</summary>
    public bool IsDirectDebit { get; set; }
    /// <summary>Per Kreditkarte bezahlt/eingezogen (Code 48)</summary>
    public bool IsCreditCard { get; set; }
    /// <summary>Bereits gezahlter Betrag (BT-113); > 0 = schon (teil-)eingezogen</summary>
    public decimal? AmountPaid { get; set; }
    /// <summary>true, wenn kein offener Betrag mehr vorhanden ist (schon vollständig eingezogen)</summary>
    public bool Paid { get; set; }
}

/// <summary>Beleg-/Projektreferenzen (BT-11..19).</summary>
public sealed class ExtractedReferences
{
    /// <summary>BT-13 Bestellnummer des Käufers</summary>
    public string BuyerOrderReference { get; set; }
    /// <summary>BT-14 Auftragsnummer des Verkäufers</summary>
    public string SalesOrderReference { get; set; }
    /// <summary>BT-17 Angebots-/Losreferenz (öffentliche Beschaffung)</summary>
    public string TenderLotReference { get; set; }
    /// <summary>BT-12 Vertragsnummer</summary>
    public string ContractReference { get; set; }
    /// <summary>BT-11 Projektreferenz</summary>
    public string ProjectReference { get; set; }
    /// <summary>BT-15 Empfangsbestätigung</summary>
    public string ReceiptAdviceReference { get; set; }
    /// <summary>BT-16 Lieferscheinnummer</summary>
    public string DespatchAdviceReference { get; set; }
    /// <summary>BT-14 Objektreferenz (Kfz-Kennzeichen etc.)</summary>
    public string InvoicedObjectReference { get; set; }
    /// <summary>BT-19 Käufer-Buchungsreferenz</summary>
    public string BuyerAccountReference { get; set; }
}
/// <summary>Weiteres Belegdokument (BG-24, BT-122..124) — nur Metadaten, keine Binärdaten.</summary>
public sealed class ExtractedSupportingDocument
{
    /// <summary>BT-122 Dokumenten-ID</summary>
    public string Id { get; set; }
    /// <summary>BT-123 Dokumenttyp-Code (130 = Anhang, 50 = Vorgängerrechnung …)</summary>
    public string TypeCode { get; set; }
    /// <summary>BT-124 Bezeichnung</summary>
    public string Name { get; set; }
    /// <summary>Externe Referenz (URL), falls angegeben</summary>
    public string Uri { get; set; }
    /// <summary>Datum des Dokuments</summary>
    public string IssueDate { get; set; }
}

/// <summary>Artikelmerkmal (BT-160/161).</summary>
public sealed class ExtractedLineAttribute
{
    /// <summary>BT-160 Merkmal-Name</summary>
    public string Name { get; set; }
    /// <summary>BT-161 Merkmal-Wert</summary>
    public string Value { get; set; }
}
