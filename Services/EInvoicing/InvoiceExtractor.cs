using System.Globalization;
using System.Xml;
using MailArchiver.Models.EInvoicing;

namespace MailArchiver.Services.EInvoicing;

/// <summary>
/// Extrahiert Rechnungsdaten aus CII- und UBL-XML (Invoice und CreditNote)
/// in ein strukturiertes Modell: Parteien, Belegnotizen, Vorgängerrechnungen,
/// Abschläge/Zuschläge, Zahlungsdaten und Positionen.
/// </summary>
public static class InvoiceExtractor
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        XmlResolver = null
    };

    public static ExtractedInvoice Extract(byte[] xmlBytes)
    {
        var doc = new XmlDocument();
        using (var r = XmlReader.Create(new MemoryStream(xmlBytes), ReaderSettings))
        {
            doc.Load(r);
        }
        var root = doc.DocumentElement ?? throw new XmlException("Kein Wurzelelement");
        string ns = root.NamespaceURI ?? "";

        return ns switch
        {
            XRechnungDetector.NsCii => ExtractCii(doc),
            XRechnungDetector.NsUblInvoice => ExtractUbl(doc, isCreditNote: false),
            XRechnungDetector.NsUblCreditNote => ExtractUbl(doc, isCreditNote: true),
            _ => throw new XmlException($"Nicht unterstütztes Format: Wurzelelement '{root.LocalName}' (erwartet CII oder UBL)")
        };
    }

    // ---------------- CII (XRechnung) ----------------

    private static ExtractedInvoice ExtractCii(XmlDocument doc)
    {
        var n = new XmlNamespaceManager(doc.NameTable);
        n.AddNamespace("rsm", XRechnungDetector.NsCii);
        n.AddNamespace("ram", XRechnungDetector.NsRam);
        n.AddNamespace("udt", XRechnungDetector.NsUdt);
        n.AddNamespace("qdt", XRechnungDetector.NsQdt);

        const string Rsm = "/rsm:CrossIndustryInvoice";
        var inv = new ExtractedInvoice();

        // ---- Kopf ----
        var typeCode = Val(doc, Rsm + "/rsm:ExchangedDocument/ram:TypeCode", n);
        inv.Header = new ExtractedHeader
        {
            InvoiceNumber = Val(doc, Rsm + "/rsm:ExchangedDocument/ram:ID", n),
            TypeCode = typeCode,
            TypeText = TypeCodeText(typeCode),
            IssueDate = NormalizeDate(Val(doc, Rsm + "/rsm:ExchangedDocument/ram:IssueDateTime/udt:DateTimeString", n)),
            Currency = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:InvoiceCurrencyCode", n),
            TaxCurrency = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:TaxCurrencyCode", n),
            TaxPointDate = NormalizeDate(Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:ApplicableTradeTax/ram:DueDateDateTime/udt:DateTimeString", n)),
            TaxPointDateCode = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:DueDateTypeCode", n),
            // BT-10 Käuferreferenz (CII: ApplicableHeaderTradeAgreement/BuyerReference = Leitweg-ID)
            BuyerReference = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:BuyerReference", n),
            // BT-23 Geschäftsprozess (CII: BusinessProcessSpecifiedDocumentContextParameter)
            BusinessProcess = Val(doc, Rsm + "/rsm:ExchangedDocumentContext/ram:BusinessProcessSpecifiedDocumentContextParameter/ram:ID", n)
        };

        // Fälligkeit
        inv.Header.DueDate = NormalizeDate(Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradePaymentTerms/ram:DueDateDateTime/udt:DateTimeString", n));

        // Belegnotizen (BT-22) + Subject-Codes (BT-21)
        foreach (var note in Nodes(doc, Rsm + "/rsm:ExchangedDocument/ram:IncludedNote", n))
        {
            var content = Val(note, "ram:Content", n);
            var subject = Val(note, "ram:SubjectCode", n);
            if (content != null) inv.Notes.Add(content);
            if (subject != null) inv.NoteSubjectCodes.Add(subject);
        }

        // BG-24: Weitere Belegdokumente (nur Metadaten); TypeCode 50 = Vorgängerrechnung (BT-25/26)
        foreach (var ard in Nodes(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:AdditionalReferencedDocument", n))
        {
            var tc = Val(ard, "ram:TypeCode", n);
            var sd = new ExtractedSupportingDocument
            {
                Id = Val(ard, "ram:IssuerAssignedID", n),
                TypeCode = tc,
                Name = Val(ard, "ram:Name", n),
                Uri = Val(ard, "ram:URIID", n),
                IssueDate = NormalizeDate(Val(ard, "ram:FormatterIssueDateTime/udt:DateTimeString", n))
            };
            inv.SupportingDocuments.Add(sd);
            if (tc == "50")
                inv.PrecedingInvoices.Add(new ExtractedPrecedingInvoice { InvoiceNumber = sd.Id, IssueDate = sd.IssueDate });
        }

        // ---- Parteien ----
        inv.Seller = ExtractCiiParty(doc, n, "ram:SellerTradeParty");
        inv.Buyer = ExtractCiiParty(doc, n, "ram:BuyerTradeParty");
        inv.Payee = ExtractCiiParty(doc, n, "ram:PayeeTradeParty");
        inv.TaxRepresentative = ExtractCiiParty(doc, n, "ram:SpecifiedTaxRepresentativeTradeParty");
        inv.ShipTo = ExtractCiiParty(doc, n, null, "ram:ApplicableHeaderTradeDelivery/ram:ShipToTradeParty");

        // ---- Zahlungsdaten (Payment) ----
        string settle = Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/";
        var means = doc.SelectSingleNode(settle + "ram:SpecifiedTradeSettlementPaymentMeans", n);
        var meansCode = means?["ram:TypeCode"]?.InnerText?.Trim();
        inv.Payment = new ExtractedPayment
        {
            PaymentMeansCode = meansCode,
            PaymentMeansText = PaymentMeansText(meansCode),
            Iban = means?.SelectSingleNode("ram:PayeePartyCreditorFinancialAccount/ram:IBANID", n)?.InnerText?.Trim(),
            AccountName = means?.SelectSingleNode("ram:PayeePartyCreditorFinancialAccount/ram:AccountName", n)?.InnerText?.Trim(),
            Bic = means?.SelectSingleNode("ram:PayeePartyCreditorFinancialAccount/ram:PayeePartyCreditorFinancialInstitution/ram:BICID", n)?.InnerText?.Trim()
               ?? doc.SelectSingleNode(settle + "ram:PayeeSpecifiedCreditorFinancialInstitution/ram:BICID", n)?.InnerText?.Trim(),
            MandateReference = doc.SelectSingleNode(settle + "ram:SpecifiedTradePaymentTerms/ram:DirectDebitMandateID/ram:IssuerAssignedID", n)?.InnerText?.Trim(),
            DebitedAccountIban = means?.SelectSingleNode("ram:PayerPartyDebtorFinancialAccount/ram:IBANID", n)?.InnerText?.Trim(),
            CardNumber = means?.SelectSingleNode("ram:ApplicableTradeSettlementPaymentCard/ram:ID", n)?.InnerText?.Trim(),
            CardHolderName = means?.SelectSingleNode("ram:ApplicableTradeSettlementPaymentCard/ram:CardHolderName", n)?.InnerText?.Trim(),
            CardNetwork = means?.SelectSingleNode("ram:ApplicableTradeSettlementPaymentCard/ram:TypeCode", n)?.InnerText?.Trim(),
            PaymentTerms = doc.SelectSingleNode(settle + "ram:SpecifiedTradePaymentTerms/ram:Description", n)?.InnerText?.Trim(),
            DueDate = inv.Header.DueDate,
            PaymentReference = doc.SelectSingleNode(settle + "ram:PaymentReference", n)?.InnerText?.Trim(),
            IsDirectDebit = IsDirectDebit(meansCode, doc.SelectSingleNode(settle + "ram:SpecifiedTradePaymentTerms/ram:DirectDebitMandateID", n) != null),
            IsCreditCard = IsCreditCard(meansCode),
            AmountPaid = Dec(doc, settle + "ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:TotalPrepaidAmount", n),
            Paid = (Dec(doc, settle + "ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:DuePayableAmount", n) ?? 1m) == 0m
        };

        // ---- Referenzen ----
        string agree = Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/";
        inv.References = new ExtractedReferences
        {
            BuyerOrderReference = Val(doc, agree + "ram:BuyerOrderReferencedDocument/ram:IssuerAssignedID", n),
            SalesOrderReference = Val(doc, agree + "ram:SalesOrderReferencedDocument/ram:IssuerAssignedID", n),
            TenderLotReference = Val(doc, agree + "ram:TenderReferencedDocument/ram:IssuerAssignedID", n),
            ContractReference = Val(doc, agree + "ram:ContractReferencedDocument/ram:IssuerAssignedID", n),
            ProjectReference = Val(doc, agree + "ram:SpecifiedProcuringProject/ram:ID", n),
            ReceiptAdviceReference = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeDelivery/ram:ReceivingAdviceReferencedDocument/ram:IssuerAssignedID", n),
            DespatchAdviceReference = Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeDelivery/ram:DespatchAdviceReferencedDocument/ram:IssuerAssignedID", n),
            BuyerAccountReference = Val(doc, settle + "ram:ReceivableSpecifiedBookingAccount/ram:ID", n)
        };

        // ---- Liefer-/Leistungsdatum + Abrechnungszeitraum ----
        inv.Header.DeliveryDate = NormalizeDate(Val(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeDelivery/ram:ActualDeliverySupplyChainEvent/ram:OccurrenceDateTime/udt:DateTimeString", n));
        var billingPeriod = doc.SelectSingleNode(settle + "ram:BillingSpecifiedPeriod", n);
        inv.Header.PeriodStart = NormalizeDate(Val(billingPeriod, "ram:StartDateTime/udt:DateTimeString", n));
        inv.Header.PeriodEnd = NormalizeDate(Val(billingPeriod, "ram:EndDateTime/udt:DateTimeString", n));

        // ---- Summen ----
        string tot = settle + "ram:SpecifiedTradeSettlementHeaderMonetarySummation/";
        inv.Totals = new ExtractedTotals
        {
            LineTotal = Dec(doc, tot + "ram:LineTotalAmount", n),
            ChargeTotal = Dec(doc, tot + "ram:ChargeTotalAmount", n),
            AllowanceTotal = Dec(doc, tot + "ram:AllowanceTotalAmount", n),
            TaxBasisTotal = Dec(doc, tot + "ram:TaxBasisTotalAmount", n),
            TaxTotal = Dec(doc, tot + "ram:TaxTotalAmount", n),
            GrandTotal = Dec(doc, tot + "ram:GrandTotalAmount", n),
            TotalPrepaid = Dec(doc, tot + "ram:TotalPrepaidAmount", n),
            RoundingAmount = Dec(doc, tot + "ram:RoundingAmount", n),
            DuePayable = Dec(doc, tot + "ram:DuePayableAmount", n)
        };

        // BT-110/BT-111: TaxTotalAmount je currencyID der Rechnungswährung (BT-110)
        // bzw. Buchungswährung (BT-111) zuordnen — CII kann beide parallel tragen
        foreach (XmlElement tta in Nodes(doc, tot + "ram:TaxTotalAmount", n))
        {
            var cur = tta.GetAttribute("currencyID");
            if (decimal.TryParse(tta.InnerText?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt))
            {
                if (!string.IsNullOrEmpty(inv.Header.TaxCurrency) && cur == inv.Header.TaxCurrency)
                    inv.Totals.TaxTotalAccounting = amt;
                else
                    inv.Totals.TaxTotal = amt;
            }
        }

        // ---- Steuern ----
        foreach (XmlElement tax in Nodes(doc, settle + "ram:ApplicableTradeTax", n))
        {
            inv.TaxBreakdown.Add(new ExtractedTax
            {
                Category = Val(tax, "ram:CategoryCode", n),
                Rate = DecEl(tax, "ram:RateApplicablePercent", n),
                BasisAmount = DecEl(tax, "ram:BasisAmount", n),
                TaxAmount = DecEl(tax, "ram:CalculatedAmount", n),
                ExemptionReason = Val(tax, "ram:ExemptionReason", n),
                ExemptionReasonCode = Val(tax, "ram:ExemptionReasonCode", n)
            });
        }

        // ---- Dokumentweite Abschläge (BG-20) / Zuschläge (BG-21) ----
        ExtractCiiAllowanceCharges(doc, n, settle, inv);

        // ---- Positionen ----
        foreach (XmlElement line in Nodes(doc, Rsm + "/rsm:SupplyChainTradeTransaction/ram:IncludedSupplyChainTradeLineItem", n))
        {
            var qty = line.SelectSingleNode("ram:SpecifiedLineTradeDelivery/ram:BilledQuantity", n) as XmlElement;
            var netPriceEl = line.SelectSingleNode("ram:SpecifiedLineTradeAgreement/ram:NetPriceProductTradePrice", n) as XmlElement;
            var grossPriceEl = line.SelectSingleNode("ram:SpecifiedLineTradeAgreement/ram:GrossPriceProductTradePrice", n) as XmlElement;
            var netCharge = DecEl(netPriceEl, "ram:ChargeAmount", n);
            var grossCharge = DecEl(grossPriceEl, "ram:ChargeAmount", n);

            var li = new ExtractedLine
            {
                LineId = Val(line, "ram:AssociatedDocumentLineDocument/ram:LineID", n),
                Name = Val(line, "ram:SpecifiedTradeProduct/ram:Name", n),
                Description = Val(line, "ram:SpecifiedTradeProduct/ram:Description", n),
                Quantity = DecEl(qty, ".", n),
                Unit = qty?.GetAttribute("unitCode"),
                NetPrice = netCharge,
                LineTotal = DecEl(line.SelectSingleNode("ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount", n) as XmlElement, ".", n),
                TaxRate = DecEl(line.SelectSingleNode("ram:SpecifiedLineTradeSettlement/ram:ApplicableTradeTax/ram:RateApplicablePercent", n) as XmlElement, ".", n),
                TaxCategory = Val(line, "ram:SpecifiedLineTradeSettlement/ram:ApplicableTradeTax/ram:CategoryCode", n),
                SellerAssignedId = Val(line, "ram:SpecifiedTradeProduct/ram:SellerAssignedID", n),
                BuyerAssignedId = Val(line, "ram:SpecifiedTradeProduct/ram:BuyerAssignedID", n),
                GlobalId = (line.SelectSingleNode("ram:SpecifiedTradeProduct/ram:GlobalID", n) as XmlElement)?.InnerText?.Trim(),
                GlobalIdScheme = (line.SelectSingleNode("ram:SpecifiedTradeProduct/ram:GlobalID", n) as XmlElement)?.GetAttribute("schemeID"),
                CountryOfOrigin = Val(line, "ram:SpecifiedTradeProduct/ram:OriginTradeCountry/ram:ID", n),
                GrossPrice = grossCharge,
                PriceDiscount = DiscountFrom(grossCharge, netCharge),
                OrderLineReference = Val(line, "ram:SpecifiedLineTradeAgreement/ram:BuyerOrderReferencedDocument/ram:LineID", n),
                PeriodStart = NormalizeDate(Val(line, "ram:SpecifiedLineTradeSettlement/ram:BillingSpecifiedPeriod/ram:StartDateTime/udt:DateTimeString", n)),
                PeriodEnd = NormalizeDate(Val(line, "ram:SpecifiedLineTradeSettlement/ram:BillingSpecifiedPeriod/ram:EndDateTime/udt:DateTimeString", n))
            };

            // Preisbasis (Netto bevorzugt, sonst Brutto)
            var basisEl = netPriceEl?.SelectSingleNode("ram:BasisQuantity", n) as XmlElement
                       ?? grossPriceEl?.SelectSingleNode("ram:BasisQuantity", n) as XmlElement;
            if (basisEl != null)
            {
                li.PriceBasisQuantity = decimal.TryParse(basisEl.InnerText?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var bq) ? bq : null;
                li.PriceBasisUnit = basisEl.GetAttribute("unitCode");
            }

            // Positionsrabatt: AppliedTradeAllowanceCharge (ChargeIndicator false) am Brutto-Preis
            var lineDiscount = DecEl(grossPriceEl, "ram:AppliedTradeAllowanceCharge[ram:ChargeIndicator/udt:Indicator='false']/ram:ActualAmount", n);
            li.PriceDiscount = lineDiscount ?? DiscountFrom(grossCharge, netCharge);

            // Alle Hinweise
            foreach (var c in Nodes(line, "ram:AssociatedDocumentLineDocument/ram:IncludedNote/ram:Content", n))
                li.Notes.Add(c.InnerText?.Trim());
            li.Note = li.Notes.Count > 0 ? li.Notes[0] : null;

            // BT-128 Objektreferenz / BT-133 Buchungsreferenz der Position
            li.LineObjectReference = Val(line, "ram:SpecifiedLineTradeSettlement/ram:AdditionalReferencedDocument/ram:IssuerAssignedID", n);
            li.BuyerAccountReference = Val(line, "ram:SpecifiedLineTradeSettlement/ram:ReceivableSpecifiedBookingAccount/ram:ID", n);

            // BG-26/27: Positions-Abschläge / Zuschläge
            foreach (XmlElement lac in Nodes(line, "ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeAllowanceCharge", n))
            {
                bool isCharge = Val(lac, "ram:ChargeIndicator/udt:Indicator", n) == "true";
                var item = new ExtractedAllowanceCharge
                {
                    Amount = DecEl(lac, "ram:ActualAmount", n),
                    Reason = Val(lac, "ram:Reason", n),
                    ReasonCode = Val(lac, "ram:ReasonCode", n),
                    Percent = DecEl(lac, "ram:CalculationPercent", n),
                    TaxCategory = Val(lac, "ram:CategoryTradeTax/ram:CategoryCode", n),
                    TaxRate = DecEl(lac, "ram:CategoryTradeTax/ram:RateApplicablePercent", n)
                };
                if (isCharge) li.Charges.Add(item); else li.Allowances.Add(item);
            }

            // BT-160/161: Artikelmerkmale
            foreach (var attr in Nodes(line, "ram:SpecifiedTradeProduct/ram:ApplicableProductCharacteristic", n))
            {
                li.Attributes.Add(new ExtractedLineAttribute { Name = Val(attr, "ram:Description", n), Value = Val(attr, "ram:Value", n) });
            }

            // Produktklassifizierungen
            foreach (var cl in Nodes(line, "ram:SpecifiedTradeProduct/ram:DesignatedProductClassification", n))
            {
                var codeEl = cl.SelectSingleNode("ram:ClassCode", n) as XmlElement;
                if (codeEl == null) continue;
                li.Classifications.Add(new ExtractedClassification
                {
                    Scheme = codeEl.GetAttribute("listID"),
                    Code = codeEl.InnerText?.Trim(),
                    Name = Val(cl, "ram:ClassName", n)
                });
            }

            inv.Lines.Add(li);
        }

        return inv;
    }

    private static void ExtractCiiAllowanceCharges(XmlDocument doc, XmlNamespaceManager n, string settle, ExtractedInvoice inv)
    {
        foreach (XmlElement ac in Nodes(doc, settle + "ram:SpecifiedTradeAllowanceCharge", n))
        {
            bool isCharge = Val(ac, "ram:ChargeIndicator/udt:Indicator", n) == "true";
            var item = new ExtractedAllowanceCharge
            {
                Amount = DecEl(ac, "ram:ActualAmount", n),
                Reason = Val(ac, "ram:Reason", n),
                ReasonCode = Val(ac, "ram:ReasonCode", n),
                Percent = DecEl(ac, "ram:CalculationPercent", n),
                TaxCategory = Val(ac, "ram:CategoryTradeTax/ram:CategoryCode", n),
                TaxRate = DecEl(ac, "ram:CategoryTradeTax/ram:RateApplicablePercent", n)
            };
            if (isCharge) inv.Charges.Add(item); else inv.Allowances.Add(item);
        }
    }

    private static ExtractedParty ExtractCiiParty(XmlDocument doc, XmlNamespaceManager n, string partyPath, string deliveryPrefix = null)
    {
        string path = deliveryPrefix != null
            ? "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/" + deliveryPrefix
            : "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/" + partyPath;
        var p = doc.SelectSingleNode(path, n) as XmlElement;
        if (p == null) return null;
        var vat = p.SelectSingleNode("ram:SpecifiedTaxRegistration/ram:ID[@schemeID='VA']", n);
        var tax = p.SelectSingleNode("ram:SpecifiedTaxRegistration/ram:ID[@schemeID='FC']", n);
        var gid = p.SelectSingleNode("ram:GlobalID", n) as XmlElement;
        var uri = p.SelectSingleNode("ram:URIUniversalCommunication/ram:URIID", n) as XmlElement;
        var email = p.SelectSingleNode("ram:DefinedTradeContact/ram:EmailURIUniversalCommunication/ram:URIID", n);
        var contactEmail = email?.InnerText?.Trim();
        return new ExtractedParty
        {
            Name = Val(p, "ram:Name", n),
            TradingName = Val(p, "ram:SpecifiedLegalOrganization/ram:TradingBusinessName", n),
            VatId = vat?.InnerText?.Trim(),
            TaxNumber = tax?.InnerText?.Trim(),
            LegalId = Val(p, "ram:SpecifiedLegalOrganization/ram:ID", n),
            LegalInformation = Val(p, "ram:Description", n),
            GlobalId = gid?.InnerText?.Trim(),
            GlobalIdScheme = gid?.GetAttribute("schemeID"),
            ElectronicAddress = uri?.InnerText?.Trim(),
            ElectronicAddressScheme = uri?.GetAttribute("schemeID"),
            Street = Val(p, "ram:PostalTradeAddress/ram:LineOne", n),
            Street2 = Val(p, "ram:PostalTradeAddress/ram:LineTwo", n),
            Street3 = Val(p, "ram:PostalTradeAddress/ram:LineThree", n),
            Zip = Val(p, "ram:PostalTradeAddress/ram:PostcodeCode", n),
            City = Val(p, "ram:PostalTradeAddress/ram:CityName", n),
            Region = Val(p, "ram:PostalTradeAddress/ram:CountrySubDivisionName", n),
            Country = Val(p, "ram:PostalTradeAddress/ram:CountryID", n),
            Email = contactEmail ?? (uri?.GetAttribute("schemeID") == "EM" ? uri?.InnerText?.Trim() : null),
            Phone = Val(p, "ram:TelephoneUniversalCommunication/ram:CompleteNumber", n)
                 ?? Val(p, "ram:DefinedTradeContact/ram:TelephoneUniversalCommunication/ram:CompleteNumber", n),
            ContactName = Val(p, "ram:DefinedTradeContact/ram:PersonName", n)
        };
    }

    // ---------------- UBL (Invoice / CreditNote) ----------------

    private static ExtractedInvoice ExtractUbl(XmlDocument doc, bool isCreditNote)
    {
        var n = new XmlNamespaceManager(doc.NameTable);
        n.AddNamespace("inv", isCreditNote ? XRechnungDetector.NsUblCreditNote : XRechnungDetector.NsUblInvoice);
        n.AddNamespace("cbc", XRechnungDetector.NsCbc);
        n.AddNamespace("cac", XRechnungDetector.NsCac);

        string root = isCreditNote ? "/inv:CreditNote" : "/inv:Invoice";
        var inv = new ExtractedInvoice();

        var typeCode = Val(doc, root + $"/cbc:{(isCreditNote ? "CreditNoteTypeCode" : "InvoiceTypeCode")}", n);
        inv.Header = new ExtractedHeader
        {
            InvoiceNumber = Val(doc, root + "/cbc:ID", n),
            TypeCode = typeCode,
            TypeText = TypeCodeText(typeCode),
            IssueDate = NormalizeDate(Val(doc, root + "/cbc:IssueDate", n)),
            DueDate = NormalizeDate(Val(doc, root + "/cbc:DueDate", n)),
            Currency = Val(doc, root + "/cbc:DocumentCurrencyCode", n),
            TaxCurrency = Val(doc, root + "/cbc:TaxCurrencyCode", n),
            TaxPointDate = NormalizeDate(Val(doc, root + "/cbc:TaxPointDate", n)),
            BuyerReference = Val(doc, root + "/cbc:BuyerReference", n),
            BusinessProcess = Val(doc, root + "/cbc:ProfileID", n)
        };

        // Belegnotizen (BT-22) + Subject-Codes (BT-21, UBL-Format "code##Text")
        foreach (var note in Nodes(doc, root + "/cbc:Note", n))
        {
            var txt = note.InnerText?.Trim();
            if (string.IsNullOrEmpty(txt)) continue;
            var hash = txt.IndexOf("##", StringComparison.Ordinal);
            if (hash > 0)
            {
                inv.NoteSubjectCodes.Add(txt[..hash]);
                inv.Notes.Add(txt[(hash + 2)..].Trim());
            }
            else inv.Notes.Add(txt);
        }

        // Vorgänger-Rechnungen (BT-25/26)
        foreach (var br in Nodes(doc, root + "/cac:BillingReference/cac:InvoiceDocumentReference", n))
        {
            inv.PrecedingInvoices.Add(new ExtractedPrecedingInvoice
            {
                InvoiceNumber = Val(br, "cbc:ID", n),
                IssueDate = NormalizeDate(Val(br, "cbc:IssueDate", n))
            });
        }

        inv.Seller = ExtractUblParty(doc, n, root + "/cac:AccountingSupplierParty/cac:Party");
        inv.Buyer = ExtractUblParty(doc, n, root + "/cac:AccountingCustomerParty/cac:Party");
        inv.Payee = ExtractUblParty(doc, n, root + "/cac:PayeeParty");
        inv.TaxRepresentative = ExtractUblParty(doc, n, root + "/cac:TaxRepresentativeParty");
        inv.ShipTo = ExtractUblShipTo(doc, n, root);

        // ---- Zahlungsdaten ----
        var means = doc.SelectSingleNode(root + "/cac:PaymentMeans", n);
        var meansCode = means?["cbc:PaymentMeansCode"]?.InnerText?.Trim();
        string totPath = root + "/cac:LegalMonetaryTotal/";
        inv.Payment = new ExtractedPayment
        {
            PaymentMeansCode = meansCode,
            PaymentMeansText = PaymentMeansText(meansCode),
            Iban = means?.SelectSingleNode("cac:PayeeFinancialAccount/cbc:ID", n)?.InnerText?.Trim(),
            AccountName = means?.SelectSingleNode("cac:PayeeFinancialAccount/cbc:Name", n)?.InnerText?.Trim(),
            Bic = means?.SelectSingleNode("cac:PayeeFinancialAccount/cac:FinancialInstitutionBranch/cbc:ID", n)?.InnerText?.Trim(),
            MandateReference = means?.SelectSingleNode("cac:PaymentMandate/cbc:ID", n)?.InnerText?.Trim(),
            DebitedAccountIban = means?.SelectSingleNode("cac:DebitedAccount/cbc:ID", n)?.InnerText?.Trim(),
            CardNumber = means?.SelectSingleNode("cac:CardAccount/cbc:PrimaryAccountNumberID", n)?.InnerText?.Trim(),
            CardHolderName = means?.SelectSingleNode("cac:CardAccount/cbc:HolderName", n)?.InnerText?.Trim(),
            CardNetwork = means?.SelectSingleNode("cac:CardAccount/cbc:NetworkID", n)?.InnerText?.Trim(),
            PaymentTerms = doc.SelectSingleNode(root + "/cac:PaymentTerms/cbc:Note", n)?.InnerText?.Trim(),
            DueDate = inv.Header.DueDate,
            PaymentReference = doc.SelectSingleNode(root + "/cbc:PaymentID", n)?.InnerText?.Trim(),
            IsDirectDebit = IsDirectDebit(meansCode, doc.SelectSingleNode(root + "/cac:PaymentMeans/cac:PaymentMandate", n) != null),
            IsCreditCard = IsCreditCard(meansCode),
            AmountPaid = Dec(doc, totPath + "cbc:PrepaidAmount", n),
            Paid = (Dec(doc, totPath + "cbc:PayableAmount", n) ?? 1m) == 0m
        };

        // ---- Referenzen ----
        inv.References = new ExtractedReferences
        {
            BuyerOrderReference = Val(doc, root + "/cac:OrderReference/cbc:ID", n),
            SalesOrderReference = Val(doc, root + "/cac:OrderReference/cbc:SalesOrderID", n),
            TenderLotReference = Val(doc, root + "/cac:OriginatorDocumentReference/cbc:ID", n),
            ContractReference = Val(doc, root + "/cac:ContractDocumentReference/cbc:ID", n),
            ProjectReference = Val(doc, root + "/cac:ProjectReference/cbc:ID", n),
            ReceiptAdviceReference = Val(doc, root + "/cac:ReceiptDocumentReference/cbc:ID", n),
            DespatchAdviceReference = Val(doc, root + "/cac:DespatchDocumentReference/cbc:ID", n),
            InvoicedObjectReference = Val(doc, root + "/cac:AdditionalDocumentReference/cbc:ID", n)
        };
        // BG-24: AdditionalDocumentReference-Gruppe als Metadaten
        foreach (var adr in Nodes(doc, root + "/cac:AdditionalDocumentReference", n))
        {
            inv.SupportingDocuments.Add(new ExtractedSupportingDocument
            {
                Id = Val(adr, "cbc:ID", n),
                TypeCode = Val(adr, "cbc:DocumentTypeCode", n),
                Name = Val(adr, "cbc:DocumentDescription", n),
                Uri = Val(adr, "cac:Attachment/cac:ExternalReference/cbc:URI", n)
            });
        }

        // ---- Liefer-/Leistungsdatum + Abrechnungszeitraum ----
        inv.Header.DeliveryDate = NormalizeDate(Val(doc, root + "/cac:Delivery/cbc:ActualDeliveryDate", n));
        var period = doc.SelectSingleNode(root + "/cac:InvoicePeriod", n);
        inv.Header.PeriodStart = NormalizeDate(Val(period, "cbc:StartDate", n));
        inv.Header.PeriodEnd = NormalizeDate(Val(period, "cbc:EndDate", n));

        // ---- Summen ----
        inv.Totals = new ExtractedTotals
        {
            LineTotal = Dec(doc, totPath + "cbc:LineExtensionAmount", n),
            ChargeTotal = Dec(doc, totPath + "cbc:ChargeTotalAmount", n),
            AllowanceTotal = Dec(doc, totPath + "cbc:AllowanceTotalAmount", n),
            TaxBasisTotal = Dec(doc, totPath + "cbc:TaxExclusiveAmount", n),
            TaxTotal = Dec(doc, totPath + "cbc:TaxAmount", n) ?? Dec(doc, root + "/cac:TaxTotal/cbc:TaxAmount", n),
            GrandTotal = Dec(doc, totPath + "cbc:TaxInclusiveAmount", n),
            TotalPrepaid = Dec(doc, totPath + "cbc:PrepaidAmount", n),
            RoundingAmount = Dec(doc, totPath + "cbc:PayableRoundingAmount", n),
            DuePayable = Dec(doc, totPath + "cbc:PayableAmount", n)
        };

        // BT-110/BT-111: TaxTotal je Währung (Rechnungswährung vs. Buchungswährung BT-6)
        foreach (XmlElement tt in Nodes(doc, root + "/cac:TaxTotal", n))
        {
            if (decimal.TryParse(tt.SelectSingleNode("cbc:TaxAmount", n)?.InnerText?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt))
            {
                var cur = (tt.SelectSingleNode("cbc:TaxAmount", n) as XmlElement)?.GetAttribute("currencyID");
                if (!string.IsNullOrEmpty(inv.Header.TaxCurrency) && cur == inv.Header.TaxCurrency)
                    inv.Totals.TaxTotalAccounting = amt;
                else if (!inv.Totals.TaxTotal.HasValue)
                    inv.Totals.TaxTotal = amt;
            }
        }

        // ---- Steuern ----
        foreach (XmlElement tax in Nodes(doc, root + "/cac:TaxTotal/cac:TaxSubtotal", n))
        {
            inv.TaxBreakdown.Add(new ExtractedTax
            {
                Category = Val(tax, "cac:TaxCategory/cbc:ID", n),
                Rate = DecEl(tax.SelectSingleNode("cac:TaxCategory/cbc:Percent", n) as XmlElement, ".", n),
                BasisAmount = DecEl(tax.SelectSingleNode("cbc:TaxableAmount", n) as XmlElement, ".", n),
                TaxAmount = DecEl(tax.SelectSingleNode("cbc:TaxAmount", n) as XmlElement, ".", n),
                ExemptionReason = Val(tax, "cac:TaxCategory/cbc:TaxExemptionReason", n),
                ExemptionReasonCode = Val(tax, "cac:TaxCategory/cbc:TaxExemptionReasonCode", n)
            });
        }

        // ---- Dokumentweite Abschläge / Zuschläge ----
        foreach (XmlElement ac in Nodes(doc, root + "/cac:AllowanceCharge", n))
        {
            bool isCharge = Val(ac, "cbc:ChargeIndicator", n) == "true";
            var item = new ExtractedAllowanceCharge
            {
                Amount = DecEl(ac, "cbc:Amount", n),
                Reason = Val(ac, "cbc:AllowanceChargeReason", n),
                ReasonCode = Val(ac, "cbc:AllowanceChargeReasonCode", n),
                Percent = DecEl(ac, "cbc:MultiplierFactorNumeric", n),
                TaxCategory = Val(ac, "cac:TaxCategory/cbc:ID", n),
                TaxRate = DecEl(ac.SelectSingleNode("cac:TaxCategory/cbc:Percent", n) as XmlElement, ".", n)
            };
            if (isCharge) inv.Charges.Add(item); else inv.Allowances.Add(item);
        }

        // ---- Positionen ----
        string linePath = isCreditNote ? "/cac:CreditNoteLine" : "/cac:InvoiceLine";
        foreach (XmlElement line in Nodes(doc, root + linePath, n))
        {
            var qtyEl = line.SelectSingleNode("cbc:InvoicedQuantity|cbc:CreditedQuantity", n) as XmlElement;
            var priceAmount = DecEl(line.SelectSingleNode("cac:Price/cbc:PriceAmount", n) as XmlElement, ".", n);
            var grossAmount = DecEl(line.SelectSingleNode("cac:Price/cbc:GrossPriceAmount", n) as XmlElement, ".", n);
            var basisEl = line.SelectSingleNode("cac:Price/cbc:BaseQuantity", n) as XmlElement;

            var li = new ExtractedLine
            {
                LineId = Val(line, "cbc:ID", n),
                Name = Val(line, "cac:Item/cbc:Name", n),
                Description = Val(line, "cac:Item/cbc:Description", n),
                Quantity = DecEl(qtyEl, ".", n),
                Unit = qtyEl?.GetAttribute("unitCode"),
                NetPrice = priceAmount,
                LineTotal = DecEl(line.SelectSingleNode("cbc:LineExtensionAmount", n) as XmlElement, ".", n),
                TaxRate = DecEl(line.SelectSingleNode("cac:Item/cac:ClassifiedTaxCategory/cbc:Percent", n) as XmlElement, ".", n),
                TaxCategory = Val(line, "cac:Item/cac:ClassifiedTaxCategory/cbc:ID", n),
                SellerAssignedId = Val(line, "cac:Item/cac:SellersItemIdentification/cbc:ID", n),
                BuyerAssignedId = Val(line, "cac:Item/cac:BuyersItemIdentification/cbc:ID", n),
                GlobalId = (line.SelectSingleNode("cac:Item/cac:StandardItemIdentification/cbc:ID", n) as XmlElement)?.InnerText?.Trim(),
                GlobalIdScheme = (line.SelectSingleNode("cac:Item/cac:StandardItemIdentification/cbc:ID", n) as XmlElement)?.GetAttribute("schemeID"),
                CountryOfOrigin = Val(line, "cac:Item/cac:OriginAddress/cac:Country/cbc:IdentificationCode", n),
                GrossPrice = grossAmount,
                PriceDiscount = DiscountFrom(grossAmount, priceAmount),
                OrderLineReference = Val(line, "cac:OrderLineReference/cbc:LineID", n),
                PeriodStart = NormalizeDate(line.SelectSingleNode("cac:InvoicePeriod/cbc:StartDate", n)?.InnerText?.Trim()),
                PeriodEnd = NormalizeDate(line.SelectSingleNode("cac:InvoicePeriod/cbc:EndDate", n)?.InnerText?.Trim())
            };
            if (basisEl != null)
            {
                li.PriceBasisQuantity = decimal.TryParse(basisEl.InnerText?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var bq) ? bq : null;
                li.PriceBasisUnit = basisEl.GetAttribute("unitCode");
            }
            // Positionsrabatt am Brutto-Preis (UBL: AllowanceCharge unter Price)
            var ublLineDiscount = DecEl(line.SelectSingleNode("cac:Price/cac:AllowanceCharge[cbc:ChargeIndicator='false']/cbc:Amount", n) as XmlElement, ".", n);
            li.PriceDiscount = ublLineDiscount ?? li.PriceDiscount;

            foreach (var noteEl in Nodes(line, "cbc:Note", n))
                li.Notes.Add(noteEl.InnerText?.Trim());
            li.Note = li.Notes.Count > 0 ? li.Notes[0] : null;

            // BT-128 / BT-133
            li.LineObjectReference = Val(line, "cac:DocumentReference/cbc:ID", n);
            li.BuyerAccountReference = Val(line, "cbc:AccountingCost", n);

            // BG-26/27: Positions-Abschläge / Zuschläge
            foreach (XmlElement lac in Nodes(line, "cac:AllowanceCharge", n))
            {
                bool isCharge = Val(lac, "cbc:ChargeIndicator", n) == "true";
                var item = new ExtractedAllowanceCharge
                {
                    Amount = DecEl(lac, "cbc:Amount", n),
                    Reason = Val(lac, "cbc:AllowanceChargeReason", n),
                    ReasonCode = Val(lac, "cbc:AllowanceChargeReasonCode", n),
                    Percent = DecEl(lac, "cbc:MultiplierFactorNumeric", n),
                    TaxCategory = Val(lac, "cac:TaxCategory/cbc:ID", n),
                    TaxRate = DecEl(lac.SelectSingleNode("cac:TaxCategory/cbc:Percent", n) as XmlElement, ".", n)
                };
                if (isCharge) li.Charges.Add(item); else li.Allowances.Add(item);
            }

            // BT-160/161: Artikelmerkmale
            foreach (var ap in Nodes(line, "cac:Item/cac:AdditionalItemProperty", n))
            {
                li.Attributes.Add(new ExtractedLineAttribute { Name = Val(ap, "cbc:Name", n), Value = Val(ap, "cbc:Value", n) });
            }

            foreach (var cc in Nodes(line, "cac:Item/cac:CommodityClassification/cbc:ItemClassificationCode", n))
            {
                li.Classifications.Add(new ExtractedClassification
                {
                    Scheme = (cc as XmlElement)?.GetAttribute("listID"),
                    Code = cc.InnerText?.Trim(),
                    Name = null
                });
            }

            inv.Lines.Add(li);
        }

        return inv;
    }

    private static ExtractedParty ExtractUblParty(XmlDocument doc, XmlNamespaceManager n, string path)
    {
        var p = doc.SelectSingleNode(path, n) as XmlElement;
        if (p == null) return null;
        var pid = p.SelectSingleNode("cac:PartyIdentification/cbc:ID", n) as XmlElement;
        var ep = p.SelectSingleNode("cbc:EndpointID", n) as XmlElement;

        // USt-IdNr. steht nicht bei allen Erzeugern in PartyTaxScheme. Üblich
        // sind zusätzlich PartyIdentification/cbc:ID[@schemeID='VA'] und
        // PartyLegalEntity/cbc:CompanyID[@schemeID='VA'] (EN 16931 erlaubt
        // beide Wege). Ohne diese Fallbacks bleibt BT-31/BT-48 leer, obwohl
        // die Nummer in der Rechnung steht.
        var vatIdent = p.SelectSingleNode(
            "cac:PartyIdentification/cbc:ID[@schemeID='VA' or @schemeID='VAT']", n) as XmlElement;

        var legalCompany = p.SelectSingleNode("cac:PartyLegalEntity/cbc:CompanyID", n) as XmlElement;
        string legalScheme = legalCompany?.GetAttribute("schemeID") ?? "";
        bool legalIsVat = legalScheme.Equals("VA", StringComparison.OrdinalIgnoreCase)
                       || legalScheme.Equals("VAT", StringComparison.OrdinalIgnoreCase);

        return new ExtractedParty
        {
            Name = Val(p, "cac:PartyLegalEntity/cbc:RegistrationName", n) ?? Val(p, "cac:PartyName/cbc:Name", n),
            TradingName = Val(p, "cac:PartyName/cbc:Name", n),
            VatId = Val(p, "cac:PartyTaxScheme/cbc:CompanyID[../cbc:TaxScheme/cbc:ID='VAT']", n)
                    ?? vatIdent?.InnerText?.Trim()
                    ?? (legalIsVat ? legalCompany.InnerText.Trim() : null)
                    ?? Val(p, "cac:PartyTaxScheme/cbc:CompanyID", n),
            TaxNumber = Val(p, "cac:PartyTaxScheme/cbc:CompanyID[../cbc:TaxScheme/cbc:ID!='VAT']", n),
            // Eine CompanyID mit schemeID VA/VAT ist die USt-IdNr., keine
            // Handelsregisternummer — sonst stünde sie doppelt im Dokument.
            LegalId = legalIsVat ? null : Val(p, "cac:PartyLegalEntity/cbc:CompanyID", n),
            LegalInformation = Val(p, "cac:PartyLegalEntity/cbc:CompanyLegalForm", n),
            GlobalId = pid?.InnerText?.Trim(),
            GlobalIdScheme = pid?.GetAttribute("schemeID"),
            ElectronicAddress = ep?.InnerText?.Trim(),
            ElectronicAddressScheme = ep?.GetAttribute("schemeID"),
            Street = Val(p, "cac:PostalAddress/cbc:StreetName", n),
            Street2 = Val(p, "cac:PostalAddress/cbc:AdditionalStreetName", n),
            Street3 = Val(p, "cac:PostalAddress/cac:AddressLine/cbc:Line", n),
            Zip = Val(p, "cac:PostalAddress/cbc:PostalZone", n),
            City = Val(p, "cac:PostalAddress/cbc:CityName", n),
            Region = Val(p, "cac:PostalAddress/cbc:CountrySubentity", n),
            Country = Val(p, "cac:PostalAddress/cac:Country/cbc:IdentificationCode", n),
            Email = Val(p, "cac:Contact/cbc:ElectronicMail", n),
            Phone = Val(p, "cac:Contact/cbc:Telephone", n),
            ContactName = Val(p, "cac:Contact/cbc:Name", n)
        };
    }

    private static ExtractedParty ExtractUblShipTo(XmlDocument doc, XmlNamespaceManager n, string root)
    {
        // BG-13: cac:Delivery — Party bevorzugt, sonst Location-Adresse
        var party = ExtractUblParty(doc, n, root + "/cac:Delivery/cac:DeliveryParty");
        if (party != null) return party;
        var loc = doc.SelectSingleNode(root + "/cac:Delivery/cac:DeliveryLocation", n);
        if (loc == null) return null;
        return new ExtractedParty
        {
            GlobalId = Val(loc, "cbc:ID", n),
            Street = Val(loc, "cac:Address/cbc:StreetName", n),
            Street2 = Val(loc, "cac:Address/cbc:AdditionalStreetName", n),
            Zip = Val(loc, "cac:Address/cbc:PostalZone", n),
            City = Val(loc, "cac:Address/cbc:CityName", n),
            Region = Val(loc, "cac:Address/cbc:CountrySubentity", n),
            Country = Val(loc, "cac:Address/cac:Country/cbc:IdentificationCode", n)
        };
    }

    // ---------------- Helfer ----------------

    private static System.Collections.Generic.IEnumerable<XmlElement> Nodes(XmlDocument doc, string xpath, XmlNamespaceManager n)
    {
        var list = doc.SelectNodes(xpath, n);
        if (list == null) yield break;
        foreach (XmlElement el in list) yield return el;
    }

    private static System.Collections.Generic.IEnumerable<XmlElement> Nodes(XmlElement el, string xpath, XmlNamespaceManager n)
    {
        var list = el.SelectNodes(xpath, n);
        if (list == null) yield break;
        foreach (var item in list) if (item is XmlElement e) yield return e;
    }

    private static bool IsDirectDebit(string? code, bool hasMandate)
        => code is "49" or "59" || hasMandate;

    private static bool IsCreditCard(string? code) => code == "48";

    private static decimal? DiscountFrom(decimal? gross, decimal? net)
        => gross.HasValue && net.HasValue && gross > net ? gross - net : null;

    private static string Val(XmlNode el, string path, XmlNamespaceManager n)
        => el?.SelectSingleNode(path, n)?.InnerText?.Trim();

    private static decimal? Dec(XmlDocument doc, string path, XmlNamespaceManager n)
        => DecEl(doc.SelectSingleNode(path, n) as XmlElement, ".", n);

    private static decimal? DecEl(XmlElement el, string path, XmlNamespaceManager n)
    {
        var v = Val(el, path, n);
        return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string NormalizeDate(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        // CII: 20160404 → 2016-04-04 ; UBL: bereits ISO
        return s.Length == 8 && int.TryParse(s, out _)
            ? $"{s[..4]}-{s.Substring(4, 2)}-{s.Substring(6, 2)}"
            : s;
    }

    private static string PaymentMeansText(string code) => code switch
    {
        "30" => "Überweisung",
        "42" => "Zahlung an Bankkonto",
        "48" => "Kreditkarte",
        "49" => "Lastschrift",
        "57" => "Dauerauftrag",
        "58" => "SEPA-Überweisung",
        "59" => "SEPA-Lastschrift",
        "68" => "Zahlung per Online-Konto",
        _ => null
    };

    private static string TypeCodeText(string code) => code switch
    {
        "380" => "Rechnung",
        "381" => "Gutschrift",
        "383" => "Zahlungserinnerung",
        "384" => "Mahnung",
        "386" => "Vorauszahlungsrechnung",
        "389" => "Selbstabrechnung",
        _ => null
    };
}