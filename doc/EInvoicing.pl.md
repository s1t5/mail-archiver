# Podgląd faktury XRechnung {#xrechnung-invoice-preview}

Mail-Archiver rozpoznaje XML Załącznik typu XRechnung i renderuje je jako
czytelny plik PDF A4 bezpośrednio w podglądzie załącznika. Plik PDF jest generowany
na żądanie i nie jest przechowywany w Archiwum.

## Co jest rozpoznawane {#what-is-recognised}

- UBL `Invoice` i `CreditNote`, których `CustomizationID` zawiera `xrechnung`.
- CII `CrossIndustryInvoice`, którego
  `GuidelineSpecifiedDocumentContextParameter` zawiera `xrechnung`.
- Zarówno faktury, jak i noty kredytowe.

Renderowane jest wyłącznie XRechnung. Dokumenty Peppol BIS, zwykłe EN 16931 oraz
pliki PDF ZUGFeRD/Factur-X nie są modyfikowane; ich XML jest wyświetlany jak dotychczas.

## Gdzie je znaleźć {#where-to-find-it}

1. Otwórz e-mail z XML Załącznik typu XRechnung. Lista załączników wyświetla
   znacznik **XRechnung**.
2. Kliknij załącznik. Okno modalne podglądu renderuje wygenerowany plik PDF wewnątrz.
3. **Download PDF** zapisuje wygenerowany plik PDF. **Download original XML** zapisuje
   załącznik, który został zarchiwizowany.
4. Ten sam plik PDF jest dostępny poprzez REST API.

Plik PDF jest wewnętrzną pomocą w czytaniu, a nie prawnie wiążącym oryginałem.
Przesłany plik XML pozostaje dokumentem wiążącym.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Punkt końcowy (endpoint) używa tej samej uwierzytelniania i zakresu Konta co inne
punkty końcowe załączników. Jest on dostępny tylko wtedy, gdy włączone jest `Api:AllowAttachmentDownloads`.
Załącznik, który nie jest XRechnung, zwraca `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limity {#limits}

- Uwzględniane są tylko XML Załącznik o rozmiarze do 5 MiB.
- Renderowanych jest maksymalnie 1000 linii faktury. Jeśli faktura ma ich więcej, plik PDF
  informuje, ile linii znajduje się wyłącznie w XML.
- Plik PDF jest generowany dla każdego żądania. Nie ma pamięci podręcznej ani zadań w tle.

## Brak walidacji {#no-validation}

Podgląd nie waliduje faktury pod kątem XRechnung, EN 16931 ani żadnego innego
zbioru reguł. Wyodrębnia on jedynie pola, które wyświetla. Nieprawidłowy lub
niekompletny plik XML, który nadal posiada identyfikator XRechnung, może wygenerować
niepełny plik PDF lub nie wygenerować go wcale. W celu uzyskania oświadczenia o
zgodności należy użyć dedykowanego walidatora.
