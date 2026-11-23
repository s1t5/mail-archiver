# XRechnung preview računa {#xrechnung-invoice-preview}

Mail-Archiver prepozna XML priloge XRechnung in jih prikazuje kot čitljivo A4 PDF dokumentacijo neposredno v predogledu priloge. PDF se generira po zahtevi in ni shranjen v arhivu.

## Kaj je prepoznano {#what-is-recognised}

- UBL `Invoice` in `CreditNote`, kjer `CustomizationID` vsebuje `xrechnung`.
- CII `CrossIndustryInvoice`, kjer
  `GuidelineSpecifiedDocumentContextParameter` vsebuje `xrechnung`.
- Tako račune kot kreditne note.

Renderira se le XRechnung. Dokumenti Peppol BIS, navadni EN 16931 in ZUGFeRD/Factur-X PDF ostanejo nespremenjeni; njihov XML se prikazuje kot prej.

## Kje ga najdete {#where-to-find-it}

1. Odprite e-pošto z XML prilogo XRechnung. Na seznamu prilog se prikaže oznaka
   **XRechnung**.
2. Kliknite na prilogo. Okno predogleda v liniji prikazuje generirani PDF.
3. **Download PDF** shrani generirani PDF. **Download original XML** shrani
   prilogo, ki je bila arhivirana.
4. Isto PDF je na voljo preko REST API.

PDF je le notran pripomoček za branje, ne pa pravno veljavni izvirnik.
Poslan XML dokument ostaja pristavilen.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Končališče uporablja isto avtentikacijo in obseg računa kot druge
[končališče za priloge](API.md#download-attachment). Na voljo je le, ko je `Api:AllowAttachmentDownloads`
omogočen. Za prilogo, ki ni XRechnung, se vrne `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Omejitve {#limits}

- Upravičene so le XML priloge do 5 MiB.
- Renderiranih je največ 1000 postav računa. Če ima račun več postav,
  PDF navaja, koliko vrstic je prisotnih le v XML-u.
- PDF se generira po zahtevi. Ni predpomika (cache) in ni posredujočega dela (background job).

## Brez validacije {#no-validation}

Predogled ne validira računa glede na XRechnung, EN 16931 ali kakršna koli
drugo množico pravil. Izvleče le polja, ki jih prikazuje. Naporno ali
nepopolno XML datoteko, ki še vedno vsebuje identifikator XRechnung, lahko
izproduci redko PDF ali sploh noen PDF. Za pridobitev izjave o skladnosti
uporabite namenski validator.
