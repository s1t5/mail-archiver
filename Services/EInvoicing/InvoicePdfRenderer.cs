using System.Globalization;
using System.Text;
using MailArchiver.Models.EInvoicing;
using SkiaSharp;

namespace MailArchiver.Services.EInvoicing;

/// <summary>
/// Erzeugt aus extrahierten Rechnungsdaten ein menschenlesbares PDF (A4,
/// IBM-Plex-Typografie). Das Ergebnis ist eine interne Ansicht, nicht das
/// rechtlich maßgebliche Original.
/// </summary>
public sealed class InvoicePdfRenderer : IDisposable
{
    private const float PageW = 595.28f;
    private const float PageH = 841.89f;
    private const float MarginL = 48f;
    private const float MarginR = 48f;
    private const float MarginT = 48f;
    private const float MarginB = 56f;
    private const float ContentW = PageW - MarginL - MarginR;
    private const int MaxRenderedLines = 1000;

    private static readonly CultureInfo DocCulture = CultureInfo.GetCultureInfo("de-DE");

    private static readonly SKColor CText = new(0x16, 0x16, 0x16);
    private static readonly SKColor CSecondary = new(0x52, 0x52, 0x52);
    private static readonly SKColor CTertiary = new(0x6F, 0x6F, 0x6F);
    private static readonly SKColor CBorder = new(0xE0, 0xE0, 0xE0);
    private static readonly SKColor CBlue = new(0x0F, 0x62, 0xFE);
    private static readonly SKColor CBlueLight = new(0xED, 0xF5, 0xFF);
    private static readonly SKColor CYellow = new(0x8E, 0x6A, 0x00);

    private readonly InvoiceFonts _fonts;
    private readonly InvoicePdfLabels _labels;
    private readonly SKPaint _paint = new() { IsAntialias = true };

    private SKDocument? _doc;
    private SKCanvas? _canvas;
    private int _page;
    private int _totalPages;
    private float _y;

    public InvoicePdfRenderer(InvoiceFonts fonts, InvoicePdfLabels labels)
    {
        _fonts = fonts;
        _labels = labels;
    }

    public byte[] Render(ExtractedInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        // Erster Durchlauf zählt die Seiten, der zweite schreibt "Seite X von Y".
        using var scratch = new MemoryStream();
        int total = RenderPass(scratch, null, invoice);

        using var ms = new MemoryStream();
        RenderPass(ms, total, invoice);
        return ms.ToArray();
    }

    private int RenderPass(Stream output, int? totalPages, ExtractedInvoice invoice)
    {
        _totalPages = totalPages ?? 0;
        _page = 0;
        _y = MarginT;

        string number = invoice.Header?.InvoiceNumber ?? "";
        string title = invoice.Header?.TypeCode == "381"
            ? _labels[InvoicePdfLabelKeys.CreditNote]
            : _labels[InvoicePdfLabelKeys.Invoice];

        using var doc = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata
        {
            Title = string.IsNullOrWhiteSpace(number) ? title : $"{title} {number}",
            Subject = "XRechnung",
            Creator = "Mail-Archiver",
            Producer = "Mail-Archiver",
            Creation = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        });
        _doc = doc;

        NewPage();
        RenderHeader(invoice);
        RenderParties(invoice);
        RenderMeta(invoice);
        RenderLines(invoice);
        RenderAllowancesCharges(invoice);
        RenderTotals(invoice);
        RenderPayment(invoice);
        RenderNotes(invoice);
        FinishPage();

        doc.Close();
        _doc = null;
        return _page;
    }

    // ------------------------------------------------------------------
    // Seiten und Text
    // ------------------------------------------------------------------

    private void NewPage()
    {
        FinishPage();
        _canvas = _doc!.BeginPage(PageW, PageH);
        _page++;
        _y = MarginT;
    }

    private void FinishPage()
    {
        if (_canvas == null) return;

        DrawFooter();
        _doc!.EndPage();
        _canvas.Dispose();
        _canvas = null;
    }

    private void EnsureSpace(float needed)
    {
        if (_y + needed > PageH - MarginB) NewPage();
    }

    private void Text(string? s, SKFont font, SKColor color, float x, float topY,
        SKTextAlign align = SKTextAlign.Left)
    {
        if (string.IsNullOrEmpty(s)) return;

        _paint.Color = color;
        float baseline = topY - font.Metrics.Ascent;

        var runs = Runs(s, font).ToList();
        float total = runs.Sum(r => Measure(r.Text, r.Font));
        float startX = align switch
        {
            SKTextAlign.Right => x - total,
            SKTextAlign.Center => x - total / 2,
            _ => x
        };

        foreach (var run in runs)
        {
            _canvas!.DrawText(run.Text, startX, baseline, SKTextAlign.Left, run.Font, _paint);
            startX += Measure(run.Text, run.Font);
        }
    }

    private float Measure(string s, SKFont font)
    {
        if (string.IsNullOrEmpty(s)) return 0;

        _paint.Color = CText;
        return Runs(s, font).Sum(r => r.Font.MeasureText(r.Text, _paint));
    }

    private IEnumerable<(string Text, SKFont Font)> Runs(string s, SKFont font)
    {
        var sb = new StringBuilder();
        SKFont? current = null;

        foreach (var rune in s.EnumerateRunes())
        {
            var runFont = _fonts.Resolve(rune.Value, font);
            if (current != null && !ReferenceEquals(runFont, current))
            {
                yield return (sb.ToString(), current);
                sb.Clear();
            }

            current = runFont;
            sb.Append(rune.ToString());
        }

        if (sb.Length > 0 && current != null) yield return (sb.ToString(), current);
    }

    private void Line(float x1, float y1, float x2, float y2, SKColor color, float width = 0.5f)
    {
        using var p = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width
        };
        _canvas!.DrawLine(x1, y1, x2, y2, p);
    }

    private void FillRect(float x, float y, float w, float h, SKColor color)
    {
        using var p = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        _canvas!.DrawRect(x, y, w, h, p);
    }

    private void DrawFooter()
    {
        float y = PageH - MarginB + 10;
        Line(MarginL, y, PageW - MarginR, y, CBorder, 0.6f);

        float ty = y + 6;
        foreach (var line in Wrap(_labels[InvoicePdfLabelKeys.Disclaimer], _fonts.Tiny, ContentW - 90))
        {
            Text(line, _fonts.Tiny, CTertiary, MarginL, ty);
            ty += 8;
        }

        string pageLabel = _labels.Format(
            InvoicePdfLabelKeys.PageOf,
            _page,
            _totalPages > 0 ? _totalPages : _page);
        Text(pageLabel, _fonts.Tiny, CTertiary, PageW - MarginR, y + 6, SKTextAlign.Right);
    }

    private List<string> Wrap(string? text, SKFont font, float maxWidth)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) { result.Add(string.Empty); continue; }

            string line = "";
            foreach (var w in words)
            {
                string candidate = line.Length == 0 ? w : line + " " + w;
                if (Measure(candidate, font) <= maxWidth) { line = candidate; continue; }
                if (line.Length > 0) result.Add(line);

                if (Measure(w, font) > maxWidth)
                {
                    var rest = w;
                    while (rest.Length > 0 && Measure(rest, font) > maxWidth)
                    {
                        int cut = rest.Length - 1;
                        while (cut > 1 && Measure(rest[..cut], font) > maxWidth) cut--;
                        result.Add(rest[..cut]);
                        rest = rest[cut..];
                    }
                    line = rest;
                }
                else line = w;
            }
            if (line.Length > 0) result.Add(line);
        }
        return result;
    }

    private float DrawWrapped(string text, SKFont font, SKColor color, float x, float y,
        float maxWidth, float lineHeight)
    {
        foreach (var line in Wrap(text, font, maxWidth))
        {
            Text(line, font, color, x, y);
            y += lineHeight;
        }
        return y;
    }

    // ------------------------------------------------------------------
    // Abschnitte
    // ------------------------------------------------------------------

    private void RenderHeader(ExtractedInvoice invoice)
    {
        var h = invoice.Header ?? new ExtractedHeader();
        bool creditNote = h.TypeCode == "381";

        Text(creditNote
                ? _labels[InvoicePdfLabelKeys.CreditNote]
                : _labels[InvoicePdfLabelKeys.Invoice],
            _fonts.Title, CText, MarginL, _y);
        _y += 30;

        float x = MarginL;
        DrawKeyValue(_labels[InvoicePdfLabelKeys.InvoiceNumber], h.InvoiceNumber ?? "-", ref x, _y);
        DrawKeyValue(_labels[InvoicePdfLabelKeys.InvoiceDate], FormatDate(h.IssueDate), ref x, _y);
        if (!string.IsNullOrWhiteSpace(h.DueDate))
            DrawKeyValue(_labels[InvoicePdfLabelKeys.DueShort], FormatDate(h.DueDate), ref x, _y);

        _y += 26;
        Line(MarginL, _y, PageW - MarginR, _y, CBorder, 1);
        _y += 16;
    }

    private void DrawKeyValue(string key, string? value, ref float x, float y)
    {
        Text(key.ToUpperInvariant(), _fonts.Tiny, CTertiary, x, y);
        Text(value, _fonts.Mono, CText, x, y + 9);
        x += 165;
    }

    private void RenderParties(ExtractedInvoice invoice)
    {
        float colW = (ContentW - 24) / 2;
        float leftY = _y;
        float rightY = _y;

        DrawParty(_labels[InvoicePdfLabelKeys.Seller], invoice.Seller, MarginL, ref leftY, colW);
        DrawParty(_labels[InvoicePdfLabelKeys.Buyer], invoice.Buyer,
            MarginL + colW + 24, ref rightY, colW);

        float blockBottom = Math.Max(leftY, rightY);

        var extras = new List<(string Label, ExtractedParty Party)>();
        if (invoice.Payee != null)
            extras.Add((_labels[InvoicePdfLabelKeys.PayeeDifferent], invoice.Payee));
        if (invoice.ShipTo != null)
            extras.Add((_labels[InvoicePdfLabelKeys.ShipTo], invoice.ShipTo));
        if (invoice.TaxRepresentative != null)
            extras.Add((_labels[InvoicePdfLabelKeys.TaxRepresentative], invoice.TaxRepresentative));

        if (extras.Count > 0)
        {
            EnsureSpace(90);
            if (_y <= MarginT + 1) blockBottom = _y;

            float cursor = blockBottom + 18;
            for (int i = 0; i < extras.Count; i += 2)
            {
                float leftEnd = cursor;
                float rightEnd = cursor;

                DrawParty(extras[i].Label, extras[i].Party, MarginL, ref leftEnd, colW);
                if (i + 1 < extras.Count)
                {
                    DrawParty(extras[i + 1].Label, extras[i + 1].Party,
                        MarginL + colW + 24, ref rightEnd, colW);
                }

                cursor = Math.Max(leftEnd, rightEnd) + 14;
            }

            blockBottom = cursor - 14;
        }

        _y = blockBottom + 14;
    }

    private void DrawParty(string label, ExtractedParty? p, float x, ref float y, float width)
    {
        Text(label.ToUpperInvariant(), _fonts.Tiny, CTertiary, x, y);
        y += 12;

        if (p == null)
        {
            Text("-", _fonts.Body, CTertiary, x, y);
            y += 14;
            return;
        }

        if (!IsPlaceholder(p.Name))
        {
            foreach (var line in Wrap(p.Name, _fonts.BodyBold, width))
            {
                Text(line, _fonts.BodyBold, CText, x, y);
                y += 12;
            }
        }

        if (!string.IsNullOrWhiteSpace(p.TradingName) &&
            !string.Equals(p.TradingName, p.Name, StringComparison.Ordinal))
        {
            foreach (var line in Wrap(p.TradingName, _fonts.Body, width))
            {
                Text(line, _fonts.Body, CSecondary, x, y);
                y += 11;
            }
        }

        foreach (var addressLine in AddressLines(p))
        {
            foreach (var line in Wrap(addressLine, _fonts.Body, width))
            {
                Text(line, _fonts.Body, CText, x, y);
                y += 11;
            }
        }

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.VatId))
            ids.Add($"{_labels[InvoicePdfLabelKeys.VatId]} {p.VatId}");
        if (!string.IsNullOrWhiteSpace(p.TaxNumber))
            ids.Add($"{_labels[InvoicePdfLabelKeys.TaxNumber]} {p.TaxNumber}");
        if (!string.IsNullOrWhiteSpace(p.LegalId))
            ids.Add($"{_labels[InvoicePdfLabelKeys.Register]} {p.LegalId}");
        foreach (var id in ids)
        {
            foreach (var line in Wrap(id, _fonts.Small, width))
            {
                Text(line, _fonts.Small, CSecondary, x, y);
                y += 10;
            }
        }

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.ContactName) && !IsPlaceholder(p.ContactName))
            contact.Add(p.ContactName);
        if (!string.IsNullOrWhiteSpace(p.Phone)) contact.Add(p.Phone);
        if (!string.IsNullOrWhiteSpace(p.Email)) contact.Add(p.Email);
        if (contact.Count > 0)
        {
            y += 2;
            var contactLine = $"{_labels[InvoicePdfLabelKeys.Contact]}: {string.Join(" · ", contact)}";
            foreach (var line in Wrap(contactLine, _fonts.Small, width))
            {
                Text(line, _fonts.Small, CSecondary, x, y);
                y += 10;
            }
        }

        y += 4;
    }

    private static IEnumerable<string> AddressLines(ExtractedParty p)
    {
        if (!string.IsNullOrWhiteSpace(p.Street)) yield return p.Street;
        if (!string.IsNullOrWhiteSpace(p.Street2)) yield return p.Street2;
        if (!string.IsNullOrWhiteSpace(p.Street3)) yield return p.Street3;

        string zipCity = string.Join(" ", new[] { p.Zip, p.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (zipCity.Length > 0) yield return zipCity;

        string country = CountryName(p.Country);
        if (!string.IsNullOrWhiteSpace(country)) yield return country;
    }

    private void RenderMeta(ExtractedInvoice invoice)
    {
        var h = invoice.Header ?? new ExtractedHeader();
        var r = invoice.References ?? new ExtractedReferences();

        var rows = new List<(string Label, string? Value)>
        {
            (_labels[InvoicePdfLabelKeys.InvoiceNumber], h.InvoiceNumber),
            (_labels[InvoicePdfLabelKeys.InvoiceDate], FormatDate(h.IssueDate)),
            (_labels[InvoicePdfLabelKeys.BuyerReference], h.BuyerReference),
            (_labels[InvoicePdfLabelKeys.Currency], h.Currency),
            (_labels[InvoicePdfLabelKeys.DueDate], FormatDate(h.DueDate)),
            (_labels[InvoicePdfLabelKeys.DeliveryDate], FormatDate(h.DeliveryDate)),
            (_labels[InvoicePdfLabelKeys.BillingPeriod], FormatPeriod(h.PeriodStart, h.PeriodEnd)),
            (_labels[InvoicePdfLabelKeys.BuyerOrder], r.BuyerOrderReference),
            (_labels[InvoicePdfLabelKeys.SalesOrder], r.SalesOrderReference),
            (_labels[InvoicePdfLabelKeys.Contract], r.ContractReference),
            (_labels[InvoicePdfLabelKeys.Project], r.ProjectReference),
            (_labels[InvoicePdfLabelKeys.DespatchAdvice], r.DespatchAdviceReference),
            (_labels[InvoicePdfLabelKeys.BuyerAccount], r.BuyerAccountReference)
        };
        rows.RemoveAll(t => string.IsNullOrWhiteSpace(t.Value));

        // Nummer und Datum stehen bereits im Kopf.
        rows.RemoveAll(t => t.Label == _labels[InvoicePdfLabelKeys.InvoiceNumber]
                         || t.Label == _labels[InvoicePdfLabelKeys.InvoiceDate]);

        // Fehlende USt-IdNr. sichtbar machen statt stillschweigend weglassen.
        rows.Add((_labels[InvoicePdfLabelKeys.SellerVatId], NotStated(invoice.Seller?.VatId)));
        if (invoice.Buyer != null)
            rows.Add((_labels[InvoicePdfLabelKeys.BuyerVatId], NotStated(invoice.Buyer.VatId)));

        EnsureSpace(60);
        SectionTitle(_labels[InvoicePdfLabelKeys.Details]);

        float colW = (ContentW - 24) / 2;
        const float labelW = 150;
        float valueW = colW - labelW - 8;
        var cells = rows
            .Select(t => (Label: t.Label, Lines: Wrap(t.Value, _fonts.Body, valueW)))
            .ToList();

        int perCol = (cells.Count + 1) / 2;
        float startY = _y;
        float bottom = startY;

        for (int col = 0; col < 2; col++)
        {
            float x = MarginL + col * (colW + 24);
            float y = startY;

            for (int i = col * perCol; i < Math.Min((col + 1) * perCol, cells.Count); i++)
            {
                Text(cells[i].Label, _fonts.Small, CTertiary, x, y);

                float lineY = y;
                foreach (var line in cells[i].Lines)
                {
                    Text(line, _fonts.Body, CText, x + labelW, lineY);
                    lineY += 11;
                }

                y += Math.Max(16, cells[i].Lines.Count * 11 + 5);
            }

            bottom = Math.Max(bottom, y);
        }

        _y = bottom + 6;
    }

    private void SectionTitle(string title)
    {
        EnsureSpace(34);
        Text(title, _fonts.H1, CText, MarginL, _y);
        _y += 18;
        Line(MarginL, _y, PageW - MarginR, _y, CBlue, 1.2f);
        _y += 12;
    }

    private void RenderLines(ExtractedInvoice invoice)
    {
        var lines = invoice.Lines ?? new List<ExtractedLine>();
        SectionTitle(_labels[InvoicePdfLabelKeys.Lines]);

        const float cPos = 42;
        const float cQty = 46;
        const float cUnit = 38;
        const float cPrice = 58;
        const float cRate = 40;
        const float cTotal = 82;
        float cDesc = ContentW - cPos - cQty - cUnit - cPrice - cRate - cTotal;

        float HeaderRow()
        {
            float y = _y;
            Text("#", _fonts.Small, CTertiary, MarginL + cPos - 6, y, SKTextAlign.Right);
            Text(_labels[InvoicePdfLabelKeys.Description], _fonts.Small, CTertiary, MarginL + cPos, y);
            Text(_labels[InvoicePdfLabelKeys.Quantity], _fonts.Small, CTertiary,
                MarginL + cPos + cDesc, y, SKTextAlign.Right);
            Text(_labels[InvoicePdfLabelKeys.Unit], _fonts.Small, CTertiary,
                MarginL + cPos + cDesc + cQty, y);
            Text(_labels[InvoicePdfLabelKeys.UnitPrice], _fonts.Small, CTertiary,
                MarginL + cPos + cDesc + cQty + cUnit + cPrice, y, SKTextAlign.Right);
            Text(_labels[InvoicePdfLabelKeys.TaxRate], _fonts.Small, CTertiary,
                MarginL + cPos + cDesc + cQty + cUnit + cPrice + cRate, y, SKTextAlign.Right);
            Text(_labels[InvoicePdfLabelKeys.Amount], _fonts.Small, CTertiary,
                PageW - MarginR, y, SKTextAlign.Right);
            y += 12;
            Line(MarginL, y, PageW - MarginR, y, CBorder, 0.8f);
            return y + 4;
        }

        _y = HeaderRow();

        if (lines.Count == 0)
        {
            Text(_labels[InvoicePdfLabelKeys.NoLines], _fonts.Body, CTertiary, MarginL, _y);
            _y += 18;
            return;
        }

        string currency = invoice.Header?.Currency ?? "";
        int position = 0;

        foreach (var line in lines)
        {
            position++;
            if (position > MaxRenderedLines) break;

            string description = line.Name ?? "";
            var nameLines = Wrap(description, _fonts.Body, cDesc - 6);
            var noteLines = new List<string>();
            var notes = line.Notes is { Count: > 0 } ? line.Notes : new List<string>();
            if (notes.Count == 0 && !string.IsNullOrWhiteSpace(line.Note))
                notes = new List<string> { line.Note };

            foreach (var note in notes.Where(n => !string.IsNullOrWhiteSpace(n)))
                noteLines.AddRange(Wrap(note, _fonts.Small, cDesc - 6));

            if (!string.IsNullOrWhiteSpace(line.Description) &&
                !string.Equals(line.Description, line.Name, StringComparison.Ordinal))
            {
                noteLines.InsertRange(0, Wrap(line.Description, _fonts.Small, cDesc - 6));
            }

            var adjustmentLines = new List<string>();
            foreach (var allowance in line.Allowances)
            {
                adjustmentLines.Add(
                    $"- {_labels[InvoicePdfLabelKeys.Allowance]}: {allowance.Reason ?? "-"}" +
                    FormatAmountSuffix(allowance.Amount, currency));
            }
            foreach (var charge in line.Charges)
            {
                adjustmentLines.Add(
                    $"+ {_labels[InvoicePdfLabelKeys.Charge]}: {charge.Reason ?? "-"}" +
                    FormatAmountSuffix(charge.Amount, currency));
            }
            var adjustments = adjustmentLines
                .SelectMany(a => Wrap(a, _fonts.Small, cDesc - 6))
                .ToList();

            int textRows = nameLines.Count + noteLines.Count + adjustments.Count;
            float rowH = Math.Max(20, textRows * 10.5f + 8);

            EnsureSpace(rowH + 4);
            if (_y <= MarginT + 1) _y = HeaderRow();

            float rowTop = _y;
            float ty = rowTop + 3;

            Text(PositionLabel(line.LineId, position), _fonts.MonoSmall, CSecondary,
                MarginL + cPos - 6, ty, SKTextAlign.Right);

            float descX = MarginL + cPos;
            foreach (var n in nameLines) { Text(n, _fonts.Body, CText, descX, ty); ty += 10.5f; }
            foreach (var n in noteLines) { Text(n, _fonts.Small, CSecondary, descX, ty); ty += 10; }
            foreach (var a in adjustments) { Text(a, _fonts.Small, CYellow, descX, ty); ty += 10; }

            Text(FormatQty(line.Quantity), _fonts.MonoSmall, CText,
                MarginL + cPos + cDesc, rowTop + 3, SKTextAlign.Right);
            Text(UnitLabel(line.Unit), _fonts.Small, CSecondary,
                MarginL + cPos + cDesc + cQty, rowTop + 3);
            Text(FormatMoney(line.NetPrice, currency), _fonts.MonoSmall, CText,
                MarginL + cPos + cDesc + cQty + cUnit + cPrice, rowTop + 3, SKTextAlign.Right);
            Text(line.TaxRate.HasValue
                    ? $"{line.TaxRate.Value.ToString("0.##", DocCulture)} %"
                    : line.TaxCategory ?? "",
                _fonts.Small, CSecondary,
                MarginL + cPos + cDesc + cQty + cUnit + cPrice + cRate, rowTop + 3,
                SKTextAlign.Right);
            Text(FormatMoney(line.LineTotal, currency), _fonts.Mono, CText,
                PageW - MarginR, rowTop + 3, SKTextAlign.Right);

            _y = rowTop + rowH;
            Line(MarginL, _y, PageW - MarginR, _y, CBorder, 0.4f);
            _y += 3;
        }

        if (lines.Count > MaxRenderedLines)
        {
            EnsureSpace(20);
            Text(_labels.Format(InvoicePdfLabelKeys.MoreLines, lines.Count - MaxRenderedLines),
                _fonts.Small, CTertiary, MarginL, _y);
            _y += 14;
        }
    }

    private void RenderAllowancesCharges(ExtractedInvoice invoice)
    {
        var allowances = invoice.Allowances ?? new List<ExtractedAllowanceCharge>();
        var charges = invoice.Charges ?? new List<ExtractedAllowanceCharge>();
        if (allowances.Count == 0 && charges.Count == 0) return;

        string currency = invoice.Header?.Currency ?? "";
        EnsureSpace(60);
        SectionTitle(_labels[InvoicePdfLabelKeys.AllowancesCharges]);

        foreach (var allowance in allowances)
        {
            RowText($"- {allowance.Reason ?? _labels[InvoicePdfLabelKeys.Allowance]}",
                FormatMoney(allowance.Amount, currency));
        }
        foreach (var charge in charges)
        {
            RowText($"+ {charge.Reason ?? _labels[InvoicePdfLabelKeys.Charge]}",
                FormatMoney(charge.Amount, currency));
        }
    }

    private void RowText(string label, string value, SKFont? labelFont = null, SKFont? valueFont = null)
    {
        EnsureSpace(16);
        Text(label, labelFont ?? _fonts.Body, CText, MarginL, _y);
        Text(value, valueFont ?? _fonts.Mono, CText, PageW - MarginR, _y, SKTextAlign.Right);
        _y += 14;
    }

    private void RenderTotals(ExtractedInvoice invoice)
    {
        var totals = invoice.Totals ?? new ExtractedTotals();
        string currency = invoice.Header?.Currency ?? "";

        EnsureSpace(120);
        _y += 14;

        const float boxW = 250;
        float boxX = PageW - MarginR - boxW;

        void TotalRow(string label, decimal? value, bool emphasis = false, bool topBorder = false)
        {
            if (!value.HasValue) return;
            if (topBorder)
            {
                Line(boxX, _y, PageW - MarginR, _y, CBorder, 0.8f);
                _y += 6;
            }
            Text(label, emphasis ? _fonts.BodyBold : _fonts.Body,
                emphasis ? CText : CSecondary, boxX, _y);
            Text(FormatMoney(value, currency), emphasis ? _fonts.Mono : _fonts.MonoSmall,
                CText, PageW - MarginR, _y, SKTextAlign.Right);
            _y += emphasis ? 16 : 14;
        }

        TotalRow(_labels[InvoicePdfLabelKeys.SumLines], totals.LineTotal);
        TotalRow(_labels[InvoicePdfLabelKeys.Charges], totals.ChargeTotal);
        TotalRow(_labels[InvoicePdfLabelKeys.Allowances], totals.AllowanceTotal);
        TotalRow(_labels[InvoicePdfLabelKeys.TaxBasis], totals.TaxBasisTotal, topBorder: true);

        foreach (var tax in invoice.TaxBreakdown)
            TotalRow(TaxLabel(tax), tax.TaxAmount);

        TotalRow(_labels[InvoicePdfLabelKeys.GrandTotal], totals.GrandTotal, topBorder: true);
        TotalRow(_labels[InvoicePdfLabelKeys.Prepaid], totals.TotalPrepaid);
        TotalRow(_labels[InvoicePdfLabelKeys.Rounding], totals.RoundingAmount);

        if (totals.DuePayable.HasValue)
        {
            _y += 4;
            const float h = 24;
            FillRect(boxX, _y, boxW, h, CBlueLight);
            FillRect(boxX, _y, 3, h, CBlue);
            Text(_labels[InvoicePdfLabelKeys.Payable], _fonts.BodyBold, CBlue, boxX + 10, _y + 8);
            Text(FormatMoney(totals.DuePayable, currency), _fonts.H2, CBlue,
                PageW - MarginR - 6, _y + 7, SKTextAlign.Right);
            _y += h + 8;
        }
    }

    private string TaxLabel(ExtractedTax tax)
    {
        string label = _labels[InvoicePdfLabelKeys.TaxRate];
        if (tax.Rate.HasValue)
            label += $" {tax.Rate.Value.ToString("0.##", DocCulture)} %";
        if (!string.IsNullOrWhiteSpace(tax.Category))
            label += $" ({tax.Category})";
        return label;
    }

    private void RenderPayment(ExtractedInvoice invoice)
    {
        var payment = invoice.Payment;
        if (payment == null) return;

        string means = _labels.Culture.TwoLetterISOLanguageName == "de"
            ? payment.PaymentMeansText ?? payment.PaymentMeansCode ?? ""
            : payment.PaymentMeansCode ?? "";

        var rows = new List<(string Label, string? Value)>
        {
            (_labels[InvoicePdfLabelKeys.PaymentMeans], means),
            (_labels[InvoicePdfLabelKeys.Iban], payment.Iban),
            (_labels[InvoicePdfLabelKeys.Bic], payment.Bic),
            (_labels[InvoicePdfLabelKeys.AccountHolder], payment.AccountName),
            (_labels[InvoicePdfLabelKeys.PaymentReference], payment.PaymentReference),
            (_labels[InvoicePdfLabelKeys.MandateReference], payment.MandateReference),
            (_labels[InvoicePdfLabelKeys.PaymentTerms], payment.PaymentTerms)
        };
        rows.RemoveAll(r => string.IsNullOrWhiteSpace(r.Value));
        if (rows.Count == 0) return;

        EnsureSpace(70);
        SectionTitle(_labels[InvoicePdfLabelKeys.PaymentInfo]);

        foreach (var (label, value) in rows)
        {
            var wrapped = Wrap(value, _fonts.Body, ContentW - 150);
            EnsureSpace(Math.Max(14, wrapped.Count * 11 + 2));
            Text(label, _fonts.Small, CTertiary, MarginL, _y);
            float valueY = _y;
            foreach (var line in wrapped)
            {
                Text(line, _fonts.Body, CText, MarginL + 150, valueY);
                valueY += 11;
            }
            _y = Math.Max(_y + 14, valueY + 2);
        }
    }

    private void RenderNotes(ExtractedInvoice invoice)
    {
        var notes = (invoice.Notes ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (notes.Count == 0) return;

        EnsureSpace(50);
        SectionTitle(_labels[InvoicePdfLabelKeys.Notes]);

        foreach (var note in notes)
        {
            var wrapped = Wrap(note, _fonts.Body, ContentW);
            EnsureSpace(wrapped.Count * 11 + 8);
            _y = DrawWrapped(note, _fonts.Body, CSecondary, MarginL, _y, ContentW, 11);
            _y += 6;
        }
    }

    // ------------------------------------------------------------------
    // Formatierung
    // ------------------------------------------------------------------

    private static string? FormatDate(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        return DateTime.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? dt.ToString("dd.MM.yyyy", DocCulture)
            : iso;
    }

    private static string? FormatPeriod(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)) return null;
        if (string.IsNullOrWhiteSpace(from)) return $"- {FormatDate(to)}";
        if (string.IsNullOrWhiteSpace(to)) return $"{FormatDate(from)} -";
        return $"{FormatDate(from)} - {FormatDate(to)}";
    }

    private static string PositionLabel(string? lineId, int position)
    {
        if (string.IsNullOrWhiteSpace(lineId)) return position.ToString(DocCulture);
        var id = lineId.Trim();
        if (id.Length <= 6 && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '/'))
            return id;
        return position.ToString(DocCulture);
    }

    private static string FormatQty(decimal? q)
        => q?.ToString("#,##0.####", DocCulture) ?? "";

    private static string FormatMoney(decimal? value, string currency)
        => value.HasValue
            ? $"{value.Value.ToString("N2", DocCulture)} {currency}".Trim()
            : "";

    private static string FormatAmountSuffix(decimal? value, string currency)
        => value.HasValue
            ? $" ({value.Value.ToString("N2", DocCulture)} {currency})".TrimEnd()
            : "";

    private static bool IsPlaceholder(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        var t = s.Trim();
        return t.Equals("nicht vorhanden", StringComparison.OrdinalIgnoreCase)
            || t.Equals("keine Angabe", StringComparison.OrdinalIgnoreCase)
            || t.Equals("k.A.", StringComparison.OrdinalIgnoreCase)
            || t.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || t.Equals("-", StringComparison.Ordinal)
            || t.Equals("...", StringComparison.Ordinal);
    }

    private string NotStated(string? value)
        => string.IsNullOrWhiteSpace(value) ? _labels[InvoicePdfLabelKeys.NotStated] : value.Trim();

    /// <summary>
    /// UN/ECE-Einheitencodes werden unverändert ausgegeben. Eine Übersetzung
    /// der Kürzel würde je Sprache eigene Tabellen erfordern; der Code ist
    /// eindeutig und bleibt in allen Sprachen gleich.
    /// </summary>
    private static string UnitLabel(string? code) => code ?? "";

    private static string? CountryName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            return new RegionInfo(code.ToUpperInvariant()).EnglishName;
        }
        catch
        {
            return code;
        }
    }

    public void Dispose() => _paint.Dispose();
}
