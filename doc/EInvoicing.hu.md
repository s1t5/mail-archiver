# XRechnung számla előnézet {#xrechnung-invoice-preview}

A Mail-Archiver felismeri az XRechnung XML csatolmányokat, és azok olvasható A4 PDF formátumában megjelenítését biztosítja közvetlenül a csatolmány előnézetében. A PDF igény szerint generálódik, és nem tárolódik az archívumban.

## Mit ismer fel {#what-is-recognised}

- UBL `Invoice` és `CreditNote`, melynek `CustomizationID` tartalmaz `xrechnung`.
- CII `CrossIndustryInvoice`, melynek `GuidelineSpecifiedDocumentContextParameter` tartalmaz `xrechnung`.
- Mind a számlákat, mind a jóváírásokat.

Csak az XRechnung kerül megjelenítésre. A Peppol BIS, a plain EN 16931 dokumentumok és a ZUGFeRD/Factur-X PDF-ek figyelmen kívül maradnak; azok XML fájlja úgy marad megtekinthető, mint korábban.

## Hol található meg {#where-to-find-it}

1. Nyisson meg egy e-mailt, amely XRechnung XML csatolmányt tartalmaz. A csatolmány listája egy **XRechnung** jelzést mutat.
2. Kattintson a csatolmányra. A előnézeti modális ablak az elkészült PDF-et közvetlenül megjeleníti.
3. A **Download PDF** menti a generált PDF-et. A **Download original XML** menti a felarchívált csatolmányt.
4. Ugyan a PDF elérhető a REST API keresztül is.

A PDF belső olvasási segéd%， nem pedig a jogilag hiteles eredeti dokumentum. A benyújtott XML fájl marad a hiteles dokumentum.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

A végpont ugyanazt a hitelesítést és fiókkörnyezetet használja, mint a többi csatolmány végpont. Csak akkor elérhető, ha a `Api:AllowAttachmentDownloads` engedélyezve van. A nem XRechnung típusú csatolmány `404`-et ad vissza.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Korlátok {#limits}

- Csak az XML csatolmányokat veszik figyelembe, legfeljebb 5 MiB méretig.
- Legfeljebb 1000 számlavonal kerül megjelenítésre. Ha a számla több, a PDF jelzi, hogy hány sor került csak megtekintésre az XML-ben.
- A PDF minden kérésごとに generálódik. Nincs gyorsítótár (cache) és nincs háttérfolyamat.

## Validálás nélkül {#no-validation}

Az előnézet nem ellenőríti a számlát az XRechnung, EN 16931 vagy bármely más szabálykészlet alapján. Csak azokat a mezőket extraálja, amelyeket megjelenít. Egy hibás vagy hiányos XML fájl, amely még rendelkezik XRechnung azonosítóval, részleges PDF-et vagy egyáltalán nem generál PDF-et. Használjon dedikált validátort, ha megfelelőségi nyilatkoztatra van szüksége.
