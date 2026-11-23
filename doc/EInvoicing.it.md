# Anteprima fattura XRechnung {#xrechnung-invoice-preview}

Mail-Archiver riconosce gli Allegati XML XRechnung e li renderizza come un
PDF A4 leggibile direttamente nell'anteprima dell'Allegato. Il PDF viene generato su
domanda e non viene memorizzato nell'Archivio.

## Cosa viene riconosciuto {#what-is-recognised}

- UBL `Invoice` e `CreditNote` il cui `CustomizationID` contiene `xrechnung`.
- CII `CrossIndustryInvoice` il cui
  `GuidelineSpecifiedDocumentContextParameter` contiene `xrechnung`.
- Sia fatture che note di credito.

Viene renderizzata solo XRechnung. I documenti Peppol BIS, EN 16931 semplici e
ZUGFeRD/Factur-X PDF vengono lasciati invariati; il loro XML viene mostrato come in precedenza.

## Dove trovarlo {#where-to-find-it}

1. Apri un'e-mail con un Allegato XML XRechnung. L'elenco degli Allegati mostra un
   badge **XRechnung**.
2. Clicca sull'Allegato. La finestra modale di anteprima renderizza il PDF generato inline.
3. **Download PDF** salva il PDF generato. **Download original XML** salva l'
   Allegato che è stato archiviato.
4. Lo stesso PDF è disponibile tramite l'API REST.

Il PDF è un ausilio alla lettura interno, non l'originale legalmente autorevole. Il
file XML inviato rimane l'originale autorevole.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

L'endpoint utilizza la stessa autenticazione e lo stesso scoping dell'Account degli altri
endpoint degli Allegati. È disponibile solo quando `Api:AllowAttachmentDownloads`
è abilitato. Un Allegato non XRechnung restituisce `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limiti {#limits}

- Vengono considerati solo gli Allegati XML, fino a 5 MiB.
- Vengono renderizzate al massimo 1000 righe di fattura. Se la fattura ne ha di più, il PDF
  indica quante righe sono presenti solo nell'XML.
- Il PDF viene generato per ogni richiesta. Non esiste una cache né un job in background.

## Nessuna validazione {#no-validation}

L'anteprima non valida la fattura rispetto a XRechnung, EN 16931 o qualsiasi
altro set di regole. Estrae solo i campi che visualizza. Un file XML malformato o
incompleto che contiene comunque un identificatore XRechnung può produrre un
PDF parziale o nessun PDF affatto. Utilizza un validatore dedicato quando hai bisogno di una
dichiarazione di conformità.
