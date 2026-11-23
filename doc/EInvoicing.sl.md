# Pregled računa XRechnung {#xrechnung-invoice-preview}

Mail-Archiver prepozna XML priloge XRechnung in jih prikazuje kot čitljivo A4 PDF dokumentacijo neposredno v predogledu priloge. PDF se generira po potrebi in ni shranjen v arhivu.

## Kaj je prepoznano {#what-is-recognised}

- UBL `Invoice` in `CreditNote`, kjer vsebuje `CustomizationID` `xrechnung`.
- CII `CrossIndustryInvoice`, kjer
  `GuidelineSpecifiedDocumentContextParameter` vsebuje `xrechnung`.
- Tako račune kot tudi knjižnice (credit notes).

Renderira se le XRechnung. Dokumenti Peppol BIS, navadni EN 16931 in ZUGFeRD/Factur-X PDF ostanejo nespremenjeni; njihove XML datoteke se prikazujejo kot prej.

## Kje najdete {#where-to-find-it}

1. Odprite e-pošto z XML prilogo XRechnung. Na seznamu prilog se prikaže oznaka **XRechnung**.
2. Kliknite na prilogo. Okno predogleda v liniji prikazuje generirani PDF.
3. **Download PDF** shrani generirani PDF. **Download original XML** shrani prilogo, ki je bila arhivirana.
4. Sestavni PDF je na voljo tudi preko REST API.

PDF je le notran pripomoček za branje, ne pa pravno veljavni original. Predložena XML datoteka ostaja uradna.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Končnica (endpoint) uporablja isto avtentikacijo in obseg računa kot druge končnice za priloge. Na voljo je le, ko je omogočen `Api:AllowAttachmentDownloads`. Priloga, ki ni XRechnung, vrne `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Omejitve {#limits}

- Upravljajo se le XML priloge, največ do 5 MiB.
- Renderira se največ 1000 postavk računa. Če ima račun več postavk, PDF navaja, koliko postavk je prisotnih le v XML.
- PDF se generira za vsako zahtevo. Ni predpomnjave (cache) in ni posreduga v ozadju (background job).

## Brez validacije {#no-validation}

Predogled ne validira računa glede na XRechnung, EN 16931 ali koli drugo pravilo. Izvleče le tista polja, ki jih prikazuje. Naporno ali nepopolno XML datoteko, ki še vedno vsebuje identifikator XRechnung, lahko proizvede redke PDF dokumente ali sploh no Slovenega PDF-a. Za potrditev skladnosti uporabite namenski validator.
