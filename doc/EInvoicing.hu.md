# XRechnung számlapreiszemléltetés {#xrechnung-invoice-preview}

A Mail-Archiver felismeri az XRechnung XML mellékleteket, és azokból olvasható A4 méretű PDF-et generál közvetlenül a melléklet előnézetében. A PDF igénylésごとに készül, és nem tárolódik az archívumban.

## Mit ismer fel {#what-is-recognised}

- UBL `Invoice` és `CreditNote`, melyek `CustomizationID` tartalma `xrechnung`.
- CII `CrossIndustryInvoice`, melynek 
  `GuidelineSpecifiedDocumentContextParameter` tartalma `xrechnung`.
- Mind a számlákat, mind a jóváírásokat.

Csak az XRechnung kerül megjelenítésre. A Peppol BIS, a sima EN 16931 dokumentumok és a ZUGFeRD/Factur-X PDF-ek változatlanok maradnak; azok XML fájlja úgy jelenik meg, mint korábban.

## Hol található meg {#where-to-find-it}

1. Nyisson meg egy e-mailt, amely XRechnung XML melléklettel rendelkezik. A mellékletlista egy **XRechnung** jelzést mutat.
2. Kattintson a mellékletre. Az előnézeti ablak (preview modal) az elkészült PDF-et közvetlenül rendereli.
3. A **Download PDF** menti az elkészült PDF-et. A **Download original XML** menti a mellékletet, amely az archívumba került.
4. Ugyen a PDF a REST API keresztül is elérhető.

A PDF belső olvasási segéd, nem pedig a törvényileg hiteles eredeti dokumentum. A beküldött XML fájl marad a hiteles forrás.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

A végpont ugyanazt a hitelesítést és fiókkörnyezetet (account scoping) használja, mint a többi
[melléklet végpont](API.md#download-attachment). Csak akkor elérhető, ha a `Api:AllowAttachmentDownloads`
engedélyezve van. Nem XRechnung típusú melléklet esetén a rendszer `404` választ ad vissza.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Korlátok {#limits}

- Csak XML mellékleteket vesznek figyelembe, maximum 5 MiB méretig.
- Legfeljebb 1000 számlavonal kerül megjelenítésre. Ha a számla több sort tartalmaz, a PDF tájékoztatja a felhasználót arról, hogy hány sor szerepel csak az XML-ben.
- A PDF minden egyes kérésre külön generálódik. Nincs cache és nincs háttérfolyamat.

## Nincs validáció {#no-validation}

Az előnézet nem ellenőríti a számlát az XRechnung, az EN 16931 vagy bármely más szabályrendszer alapján. Csak azokat a mezőket extraiálja ki, amelyeket megjelenít. Egy hibás vagy hiányos XML fájl, amely mégis XRechnung azonosítót hordoz, részleges PDF-et vagy egyáltalán sem PDF-et generálhat. Használjon kifejezetten erre szolgáló validátort, ha megfelelőségi igazolásra van szüksége.
