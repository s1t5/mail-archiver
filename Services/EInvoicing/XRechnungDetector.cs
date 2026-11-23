using System.Xml;

namespace MailArchiver.Services.EInvoicing;

/// <summary>
/// Erkennt XRechnungen an Wurzelelement und Guideline-URN. Es wird nur
/// erkannt, nicht validiert.
/// </summary>
public static class XRechnungDetector
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public const string NsCii = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    public const string NsRam = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    public const string NsUdt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
    public const string NsQdt = "urn:un:unece:uncefact:data:standard:QualifiedDataType:100";
    public const string NsUblInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string NsUblCreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public const string NsCbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string NsCac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    /// <summary>Prüft billig, ob ein Anhang überhaupt XML sein kann.</summary>
    public static bool LooksLikeXml(string? fileName, string? contentType)
    {
        if (fileName?.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) == true) return true;

        var ct = contentType?.Split(';', 2)[0].Trim();
        return string.Equals(ct, "application/xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ct, "text/xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Liefert null, wenn der Inhalt keine XRechnung ist oder nicht lesbar ist.</summary>
    public static XRechnungInfo? Detect(byte[]? content)
    {
        if (content is not { Length: > 0 } || content.Length > MaxBytes) return null;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true
            };

            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new MemoryStream(content, writable: false), settings))
            {
                doc.Load(reader);
            }

            var root = doc.DocumentElement;
            if (root == null) return null;

            string ns = root.NamespaceURI ?? string.Empty;
            string? urn = ns switch
            {
                NsUblInvoice or NsUblCreditNote =>
                    root.SelectSingleNode("cbc:CustomizationID", Ns(doc))?.InnerText,
                NsCii =>
                    root.SelectSingleNode(
                        "rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID",
                        Ns(doc))?.InnerText,
                _ => null
            };

            urn = urn?.Trim();
            if (string.IsNullOrEmpty(urn) ||
                !urn.Contains("xrechnung", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string format = ns == NsCii ? "CII" : "UBL";
            string standard = urn.Contains("xrechnung_3.0", StringComparison.OrdinalIgnoreCase)
                ? "XRechnung 3.0"
                : "XRechnung 1.2/2.x";
            string profile = urn.Contains("cvd", StringComparison.OrdinalIgnoreCase) ? "CVD"
                : urn.Contains("extension", StringComparison.OrdinalIgnoreCase) ? "Extension"
                : "Standard";

            return new XRechnungInfo(format, standard, profile);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static XmlNamespaceManager Ns(XmlDocument doc)
    {
        var n = new XmlNamespaceManager(doc.NameTable);
        n.AddNamespace("rsm", NsCii);
        n.AddNamespace("ram", NsRam);
        n.AddNamespace("cbc", NsCbc);
        return n;
    }
}

public sealed record XRechnungInfo(string Format, string Standard, string Profile);
