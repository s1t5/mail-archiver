# XRechnung invoice preview

Mail-Archiver recognises XRechnung XML attachments and renders them as a
readable A4 PDF directly in the attachment preview. The PDF is generated on
demand and is not stored in the archive.

## What is recognised

- UBL `Invoice` and `CreditNote` whose `CustomizationID` contains `xrechnung`.
- CII `CrossIndustryInvoice` whose
  `GuidelineSpecifiedDocumentContextParameter` contains `xrechnung`.
- Both invoices and credit notes.

Only XRechnung is rendered. Peppol BIS, plain EN 16931 documents and
ZUGFeRD/Factur-X PDFs are left alone; their XML is shown as before.

## Where to find it

1. Open an email with an XRechnung XML attachment. The attachment list shows an
   **XRechnung** badge.
2. Click the attachment. The preview modal renders the generated PDF inline.
3. **Download PDF** saves the generated PDF. **Download original XML** saves the
   attachment that was archived.
4. The same PDF is available through the REST API.

The PDF is an internal reading aid, not the legally authoritative original. The
submitted XML file remains authoritative.

## REST API

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

The endpoint uses the same authentication and account scoping as the other
attachment endpoints. It is only available when `Api:AllowAttachmentDownloads`
is enabled. A non-XRechnung attachment returns `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limits

- Only XML attachments are considered, up to 5 MiB.
- At most 1000 invoice lines are rendered. If the invoice has more, the PDF
  states how many lines are only present in the XML.
- The PDF is generated per request. There is no cache and no background job.

## No validation

The preview does not validate the invoice against XRechnung, EN 16931 or any
other rule set. It only extracts the fields it displays. A malformed or
incomplete XML file that still carries an XRechnung identifier may produce a
sparse PDF or no PDF at all. Use a dedicated validator when you need a
conformance statement.
