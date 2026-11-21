# XRechnung-Rechnungsvorschau

Mail-Archiver erkennt XRechnung-XML-Anhänge und stellt sie als lesbares
A4-PDF direkt in der Anhangs-Vorschau dar. Das PDF wird bei Bedarf erzeugt und
nicht im Archiv gespeichert.

## Was erkannt wird

- UBL `Invoice` und `CreditNote`, deren `CustomizationID` den Wert `xrechnung`
  enthält.
- CII `CrossIndustryInvoice`, dessen
  `GuidelineSpecifiedDocumentContextParameter` den Wert `xrechnung` enthält.
- Rechnungen und Gutschriften.

Nur XRechnung wird dargestellt. Peppol BIS, reine EN-16931-Dokumente und
ZUGFeRD-/Factur-X-PDFs bleiben unverändert; deren XML wird wie bisher angezeigt.

## Wo die Vorschau zu finden ist

1. Eine E-Mail mit einem XRechnung-XML-Anhang öffnen. In der Anhangsliste steht
   der Badge **XRechnung**.
2. Den Anhang anklicken. Das Vorschaufenster zeigt das erzeugte PDF inline.
3. **PDF herunterladen** speichert das erzeugte PDF. **Original-XML
   herunterladen** speichert den archivierten Anhang.
4. Dasselbe PDF ist über die REST-API abrufbar.

Das PDF ist eine interne Lesehilfe, nicht das rechtlich maßgebliche Original.
Maßgeblich bleibt die übermittelte XML-Datei.

## REST-API

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Der Endpunkt nutzt dieselbe Authentifizierung und Kontozuordnung wie die
übrigen Anhangs-Endpunkte. Er ist nur verfügbar, wenn
`Api:AllowAttachmentDownloads` aktiviert ist. Bei einem Anhang ohne XRechnung
liefert er `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Grenzen

- Es werden nur XML-Anhänge bis 5 MiB betrachtet.
- Höchstens 1000 Rechnungspositionen werden dargestellt. Bei mehr Positionen
  nennt das PDF die Anzahl der Positionen, die nur im XML enthalten sind.
- Das PDF wird pro Anfrage erzeugt. Es gibt keinen Cache und keinen
  Hintergrundjob.

## Keine Validierung

Die Vorschau prüft die Rechnung nicht gegen XRechnung, EN 16931 oder ein
anderes Regelwerk. Sie extrahiert nur die Felder, die sie anzeigt. Eine
fehlerhafte oder unvollständige XML-Datei mit XRechnung-Kennung kann zu einem
lückenhaften PDF oder gar keinem PDF führen. Für eine Konformitätsaussage ist
ein eigener Validator nötig.
