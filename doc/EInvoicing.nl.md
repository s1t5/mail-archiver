# XRechnung factuurvoorbeeld {#xrechnung-invoice-preview}

Mail-Archiver herkent XRechnung XML Bijlagen en rendert deze als een
leesbaar A4 PDF direct in de Bijlage-voorbeeldweergave. De PDF wordt op
aanvraag gegenereerd en wordt niet opgeslagen in het Archief.

## Wat wordt herkend {#what-is-recognised}

- UBL `Invoice` en `CreditNote` waarvan de `CustomizationID` `xrechnung` bevat.
- CII `CrossIndustryInvoice` waarvan
  `GuidelineSpecifiedDocumentContextParameter` `xrechnung` bevat.
- Zowel facturen als creditnota's.

Alleen XRechnung wordt gerenderd. Peppol BIS, plain EN 16931 documenten en
ZUGFeRD/Factur-X PDF's blijven ongewijzigd; hun XML wordt zoals voorheen getoond.

## Waar te vinden {#where-to-find-it}

1. Open een e-mail met een XRechnung XML Bijlage. De Bijlage-lijst toont een
   **XRechnung** badge.
2. Klik op de Bijlage. De preview-modal rendert de gegenereerde PDF inline.
3. **Download PDF** slaat de gegenereerde PDF op. **Download original XML** slaat de
   Bijlage op die is gearchiveerd.
4. Dezelfde PDF is beschikbaar via de REST API.

De PDF is een interne leeswijzer, niet het juridisch gezaghebbende origineel. Het
ingediende XML-bestand blijft gezaghebbend.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

De endpoint gebruikt dezelfde authenticatie en Account-scoping als de andere
[attachment endpoints](API.md#download-attachment). Het is alleen beschikbaar wanneer `Api:AllowAttachmentDownloads`
is ingeschakeld. Een niet-XRechnung Bijlage retourneert `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limieten {#limits}

- Alleen XML Bijlagen worden beschouwd, tot 5 MiB.
- Maximaal 1000 factuurregels worden gerenderd. Als de factuur er meer heeft, vermeldt de PDF
  hoeveel regels alleen in de XML aanwezig zijn.
- De PDF wordt per aanvraag gegenereerd. Er is geen cache en geen achtergrondproces.

## Geen validatie {#no-validation}

De preview valideert de factuur niet tegen XRechnung, EN 16931 of een andere
regelset. Het extraheert alleen de velden die het weergeeft. Een foutief of
onvolledig XML-bestand dat nog steeds een XRechnung-identifier draagt, kan een
gefragmenteerde PDF of helemaal geen PDF produceren. Gebruik een speciale validator wanneer u een
conformiteitsverklaring nodig heeft.
