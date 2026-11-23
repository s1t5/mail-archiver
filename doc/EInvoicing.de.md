# XRechnung Rechnungsvorschau {#xrechnung-invoice-preview}

Mail-Archiver erkennt XRechnung XML Anhänge und stellt diese als
lesbare A4 PDF direkt in der Anhangsvorschau dar. Das PDF wird bei
Bedarf generiert und wird nicht im Archiv gespeichert.

## Was erkannt wird {#what-is-recognised}

- UBL `Invoice` und `CreditNote`, deren `CustomizationID` `xrechnung` enthält.
- CII `CrossIndustryInvoice`, dessen
  `GuidelineSpecifiedDocumentContextParameter` `xrechnung` enthält.
- Sowohl Rechnungen als auch Gutschriften.

Nur XRechnung wird dargestellt. Peppol BIS, einfache EN 16931 Dokumente und
ZUGFeRD/Factur-X PDFs bleiben unberührt; deren XML wird wie bisher angezeigt.

## Wo Sie es finden {#where-to-find-it}

1. Öffnen Sie eine E-Mail mit einem XRechnung XML Anhang. Die Anhangsliste zeigt ein
   **XRechnung** Badge an.
2. Klicken Sie auf den Anhang. Das Vorschau-Modal stellt das generierte PDF inline dar.
3. **Download PDF** speichert das generierte PDF. **Download original XML** speichert den
   Anhang, der archiviert wurde.
4. Dasselbe PDF ist über die REST API verfügbar.

Das PDF ist eine interne Lesehilfe, nicht das rechtlich verbindliche Original. Die
eingereichte XML-Datei bleibt maßgeblich.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Der Endpunkt verwendet dieselbe Authentifizierung und das gleiche Konto-Scoping wie die anderen
[Anhang-Endpunkte](API.md#download-attachment). Er ist nur verfügbar, wenn `Api:AllowAttachmentDownloads`
aktiviert ist. Ein Nicht-XRechnung Anhang gibt `404` zurück.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limits {#limits}

- Es werden nur XML Anhänge bis zu 5 MiB berücksichtigt.
- Es werden maximal 1000 Rechnungspositionen dargestellt. Wenn die Rechnung mehr hat,
  gibt das PDF an, wie viele Positionen nur im XML vorhanden sind.
- Das PDF wird pro Anfrage generiert. Es gibt keinen Cache und keinen Hintergrundjob.

## Keine Validierung {#no-validation}

Die Vorschau validiert die Rechnung nicht gegen XRechnung, EN 16931 oder ein
anderes Regelsatz. Sie extrahiert lediglich die Felder, die sie anzeigt. Eine fehlerhafte oder
unvollständige XML-Datei, die dennoch einen XRechnung-Identifikator trägt, kann ein
unvollständiges PDF oder gar kein PDF erzeugen. Verwenden Sie einen dedizierten Validator, wenn Sie
eine Konformitätsbescheinigung benötigen.
