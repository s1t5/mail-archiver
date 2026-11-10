# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver udostępnia opcjonalne, **tylko do odczytu** REST API do programowego dostępu do zarchiwizowanej poczty. Jest ono przeznaczone do przypadków użycia związanych z automatyzacją i integracją (raportowanie, eksport e-discovery, monitorowanie) bez konieczności korzystania z interfejsu webowego.

Celem jest umożliwienie skryptowi lub agentowi AI odczytu zarchiwizowanej poczty **bez konieczności ujawniania danych uwierzytelniających Skrzynki pocztowej ani komunikowania się z dostawcą poczty.** Zakresowy klucz API przypisany do użytkownika to funkcja umożliwiająca *odczyt Archiwum*, a nie dane uwierzytelniające do *bycia Skrzynką pocztową* — dziedziczy on tylko uprawnienia Skrzynki pocztowej swojego właściciela, nie nadaje uprawnień do zapisu i może zostać natychmiast cofnięty bez wpływu na powiązaną Skrzynkę pocztową.

> ⚠️ **API jest domyślnie wyłączone.** Musi zostać jawnie włączone w konfiguracji (patrz [Enabling the API](#enabling-the-api)). Gdy jest wyłączone, każda trasa `/api/*` zwraca `404 Not Found`.

API jest **z założenia przeznaczone tylko do odczytu**: udostępnia konta, foldery, wyszukiwanie wiadomości, szczegóły wiadomości oraz pobieranie Załączników. Nigdy nie tworzy, nie modyfikuje ani nie usuwa danych.

## Spis treści {#table-of-contents}

- [Włączanie API](#enabling-the-api)
- [Uwierzytelnianie](#authentication)
  - [Format klucza API](#api-key-format)
  - [Cykl życia klucza](#key-lifecycle)
  - [Model uprawnień](#permission-model)
  - [Uzasadnienie bezpieczeństwa](#security-rationale)
- [Konwencje](#conventions)
  - [Podstawowy adres URL i wersjonowanie](#base-url-and-versioning)
  - [Stronicowanie](#pagination)
  - [Błędy](#errors)
  - [Ograniczanie liczby żądań](#rate-limiting)
- [Punkty końcowe](#endpoints)
  - [Lista kont](#list-accounts)
  - [Lista folderów](#list-folders)
  - [Wyszukiwanie wiadomości](#search-messages)
  - [Pobierz wiadomość](#get-message)
  - [Pobierz załącznik](#download-attachment)
  - [Pobierz wiadomość (.eml)](#download-message-eml)
  - [Pobierz oryginalną wiadomość](#download-original-message)
  - [Statystyki](#stats)
- [Typy danych](#data-types)
- [Logowanie dostępu](#access-logging)
- [OpenAPI i Swagger UI](#openapi-and-swagger-ui)
- [Dokumentacja API](API-Reference.md) — generowana dokumentacja punktów końcowych i typów danych

## Włączanie API {#enabling-the-api}

API jest konfigurowane w sekcji `Api` elementu `appsettings.json` (lub w odpowiadających mu zmiennych środowiskowych, np. `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Cykl życia klucza {#key-lifecycle}

1. **Tworzenie** — użytkownik tworzy klucz z opisowym `Name` oraz opcjonalnym terminem ważności. Odpowiedź wyświetla klucz w postaci jawnego tekstu tylko raz.
2. **Używanie** — wysyłanie klucza jako tokenu bearer. Każde zaakceptowane żądanie odświeża `LastUsedAt` klucza (zapis jest ograniczany do maksymalnie raz na około 5 minut, aby uniknąć zapisu przy każdym wywołaniu).
3. **Wygasanie** — jeśli `ExpiresAt` jest ustawione i termin minął, klucz zostaje odrzucony.
4. **Cofnięcie** — użytkownik właściciel lub administrator cofa klucz (`RevokedAt` jest ustawione); klucz jest natychmiast i trwale odrzucany.

Klucz jest **aktywny**, gdy nie został cofnięty, nie wygasł **oraz** jego użytkownik właściciel jest nadal aktywny. Klucz nieaktywny z jakiegokolwiek z tych powodów jest odrzucany z błędem `401`.

### Model uprawnień {#permission-model}

Klucz API dziedziczy dokładnie te same uprawnienia, które posiada użytkownik będący jego właścicielem. Uwierzytelniony podmiot posiada te same roszczenia (claims), które generuje normalne logowanie webowe, więc wszystkie istniejące uprawnienia...

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Główny przełącznik. Gdy `false`, wszystkie trasy `/api/*` zwracają `404`, a dokument OpenAPI / Swagger UI nie są mapowane. |
| `AllowAttachmentDownloads` | `true` | Gdy `false`, punkt końcowy pobierania załącznika zwraca `403`. Metadane dotyczące załączników są nadal zwracane przez punkt końcowy szczegółów wiadomości. |
| `AllowMessageDownloads` | `true` | Gdy `false`, punkty końcowe pełnej wiadomości (`/eml` i `/raw`) zwracają `403`. Pełna wiadomość zawiera swoje załączniki, więc **zarówno** ten przełącznik, jak i `AllowAttachmentDownloads` muszą być `true`; wyłączenie któregokolwiek z nich dezaktywuje oba punkty końcowe. |
| `EnableSwaggerUi` | `true` | Gdy `true` (i `Enabled` jest `true`), montuje Swagger UI na `/apidocs` oraz dokument OpenAPI na `/apidocs/spec/v1.json`. Oba wymagają uwierzytelnionej **sesji webowej (cookie)** — nie są one częścią `/api/` i nie są dostępne za pomocą klucza API. |
| `DefaultPageSize` | `20` | Rozmiar strony używany, gdy żądanie pomija `pageSize`. |
| `MaxPageSize` | `100` | Górna granica; większe wartości `pageSize` są ograniczane do tej wartości. |
| `RateLimitPerMinute` | `120` | Budżet żądań w oknie stałym na klucz API na minutę (patrz: [Rate limiting](#rate-limiting)). |

## Uwierzytelnianie {#authentication}

API uwierzytelnia się za pomocą **kluczy API przypisanych do użytkownika**, przesyłanych jako bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` akceptuje **tylko** klucze API. Ciasteczka przeglądarki nie są honorowane, dzięki czemu API nie posiada powierzchni ataku CSRF. Błędy uwierzytelniania zwracają `401` z nagłówkiem `WWW-Authenticate: Bearer` oraz ciałem [problem+json](#errors) — API **nigdy** nie przekierowuje do strony logowania.

Klucze są zarządzane samodzielnie w interfejsie webowym w sekcji **API Keys** (każdy użytkownik zarządza własnymi kluczami; administratorzy mogą przeglądać i unieważniać klucze dowolnego użytkownika).

### Format klucza API {#api-key-format}

Klucz to ciąg znaków `ma_`, po którym następuje 43 znaki base64 bezpieczne dla URL, kodujące 32 losowe bajty (256 bitów entropii):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Przechowywanie:

- **`KeyPrefix`** — pierwsze ~11 znaków (np. `ma_8Kf3pQ2`), przechowywanych w postaci jawnej. Służy do identyfikacji klucza w interfejsie użytkownika oraz w logach. Nie jest to element poufny.
- **`KeyHash`** — zakodowany w formacie hex skrót SHA-256 pełnego klucza, przechowywany pod **unikalnym indeksem**. Klucz w postaci jawnej nigdy nie jest przechowywany.

Pełny klucz jest wyświetlany użytkownikowi **dokładnie raz**, w momencie tworzenia. Nie można go odzyskać później; zgubiony klucz musi zostać unieważniony i zastąpiony nowym.

### Cykl życia klucza {#key-lifecycle}

1. **Tworzenie** — użytkownik tworzy klucz z opisem `Name` oraz opcjonalnym terminem ważności. Odpowiedź wyświetla klucz w postaci jawnej tylko raz.
2. **Używanie** — przesyłanie klucza jako bearer token. Każde zaakceptowane żądanie odświeża `LastUsedAt` klucza (zapisy są ograniczane do maksymalnie raz na ~5 minut, aby uniknąć zapisu przy każdym wywołaniu).
3. **Wygasanie** — jeśli `ExpiresAt` jest ustawione i data ta już minęła, klucz zostaje odrzucony.
4. **Unieważnianie** — właściciel klucza lub administrator unieważnia klucz (ustawiono `RevokedAt`); klucz jest natychmiast i trwale odrzucany.

Klucz jest **aktywny**, gdy nie został unieważniony, nie wygasł **oraz** jego właściciel jest nadal aktywny. Klucz nieaktywny z jakiegokolwiek z tych powodów jest odrzucany z błędem `401`.

### Model uprawnień {#permission-model}

Klucz API dziedziczy dokładnie te same uprawnienia, które posiada jego właściciel. Uwierzytelniony podmiot posiada te same roszczenia (claims), co przy normalnym logowaniu przez przeglądarkę, więc cała istniejąca logika autoryzacji działa bez zmian:

- **Administratorzy** (`IsAdmin`) widzą wszystkie konta pocztowe i wszystkie wiadomości.
- **Zwykli użytkownicy** widzą tylko te konta pocztowe, które zostały im przypisane (`UserMailAccount`). Konta i wiadomości spoza tego zestawu są traktowane jako **nieistniejące**: żądania dotyczące nich zwracają `404`, a nie `403`, dzięki czemu API nie ujawnia istnienia kont, których wywołujący nie może widzieć.

### Uzasadnienie bezpieczeństwa {#security-rationale}

- **Ominięcie 2FA.** Klucze API z założenia omijają uwierzytelnianie dwuskładnikowe — jest to cecha charakterystyczna dla tokenów nieinteraktywnych (podobnie jak w przypadku tokenów dostępu osobistego w GitHub/GitLab). Jest to łagodzone przez: klucze są **domyślnie wyłączone**, można je unieważnić, mogą opcjonalnie wygasać, są ograniczone do uprawnień użytkownika, a każdy dostęp jest rejestrowany w [access log](#access-logging).
- **Brak ciasteczek na `/api`.** Uwierzytelnianie za pomocą ciasteczek nie jest akceptowane na trasach `/api`, co eliminuje obawy dotyczące CSRF dla API.
- **Transport.** W środowisku produkcyjnym API zawsze należy obsługiwać przez HTTPS, aby tokeny typu bearer nie były narażone na przechwycenie w sieci.

## Konwencje {#conventions}

### Base URL i wersjonowanie {#base-url-and-versioning}

Wszystkie punkty końcowe znajdują się pod `/api/v1`. Wersja jest częścią ścieżki, dzięki czemu przyszłe zmiany wprowadzające przerwy w kompatybilności mogą być udostępniane jako `/api/v2` bez zakłócania pracy istniejących klientów.

Odpowiedzi są `application/json`, chyba że zaznaczono inaczej (pobieranie załączników zwraca własny typ zawartości załącznika). Wszystkie znaczniki czasu są w formacie UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Stronicowanie {#pagination}

Punkty końcowe list zwracają obiekt opakowujący stronowanie:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` zaczyna się od 1. Strony spoza zakresu zwracają pustą tablicę `items` (nie jest to błąd).
- `pageSize` domyślnie wynosi `Api:DefaultPageSize` i jest ograniczony do `[1, Api:MaxPageSize]`.

### Błędy {#errors}

Błędy używają [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | Kiedy |
| --- | --- |
| `400 Bad Request` | Nieprawidłowe parametry (np. nieparsowalna data, nieprawidłowy `sortBy`). |
| `401 Unauthorized` | Brakujący, błędny, wygasły, cofnięty lub w inny sposób nieprawidłowy klucz. Zawiera `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Pobieranie załączników żądane podczas `AllowAttachmentDownloads=false` lub pobieranie wiadomości podczas `AllowMessageDownloads=false`. |
| `404 Not Found` | Zasób nie istnieje **lub** znajduje się poza dozwolonymi kontami wywołującego. Zwracane również dla każdej trasy `/api/*`, gdy `Api:Enabled=false`. |
| `429 Too Many Requests` | Przekroczono limit zapytań. |
| `500 Internal Server Error` | Nieoczekiwany błąd serwera (problem+json, nigdy strona błędu HTML). |

### Ograniczanie liczby żądań {#rate-limiting}

Zapytania są ograniczone polityką stałego okna `Api:RateLimitPerMinute` żądań na minutę (domyślnie `120`), partycjonowaną według prefiksu klucza API (z powrotem do adresu IP klienta, gdy nie ma klucza). Przekroczenie budżetu zwraca `429`.

## Punkty końcowe {#endpoints}

> Wszystkie przykłady zakładają, że API jest włączone, a `$KEY` zawiera ważny klucz.

### Wyświetl konta {#list-accounts}

```
GET /api/v1/accounts
```

Zwraca konta pocztowe widoczne dla wywołującego. Pola poświadczeń
(IMAP/OAuth username, password, client secret, …) **nigdy** nie są dołączane.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### Listuj foldery {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Zwraca drzewo folderów dla jednego konta w postaci struktury zagnieżdżonej. Zwraca `404`, jeśli
konto nie istnieje lub nie jest widoczne dla wywołującego.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Wyszukiwanie wiadomości {#search-messages}

```
GET /api/v1/emails
```

Wyszukuje zarchiwizowane wiadomości z takimi samymi możliwościami jak interfejs webowy.

| Parametr zapytania | Typ | Opis |
| --- | --- | --- |
| `q` | string | Zapytanie pełnotekstowe po temat, body, from, to, cc, bcc. Obsługuje składnię pól, np. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Najwcześniejsza data wysłania (włącznie), `YYYY-MM-DD`. |
| `to` | date | Najpóźniejsza data wysłania (włącznie), `YYYY-MM-DD`. |
| `accountId` | int | Ogranicz do jednego konta (musi być widoczne dla wywołującego). |
| `folder` | string | Ogranicz do jednego folderu według pełnej ścieżki. |
| `direction` | string | `incoming` lub `outgoing`. |
| `page` | int | Numer strony zaczynający od 1 (domyślnie `1`). |
| `pageSize` | int | Wyniki na stronę (domyślnie `Api:DefaultPageSize`, ograniczony do `Api:MaxPageSize`). |
| `sortBy` | string | Jeden z `sentDate` (domyślnie), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` lub `desc` (domyślnie `desc`). |

Zwraca [stronicowany kontener](#pagination) dla [`EmailSummaryDto`](#emailsummarydto).
Loguje wpis w [dzienniku dostępu](#access-logging) typu `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Pobierz wiadomość {#get-message}

```
GET /api/v1/emails/{id}
```

Zwraca pełną wiadomość wraz z treściami. Treść wykorzystuje ten sam łańcuch awaryjny co interfejs webowy: `OriginalBodyHtml` (surowe bajty) → `BodyUntruncatedHtml` →
`HtmlBody`, oraz odpowiednik dla treści tekstowej. Zwraca `404`, jeśli wiadomość
nie istnieje lub należy do konta spoza uprawnień wywołującego. Rejestruje wpis w dzienniku dostępu typu `Open`.

Zwraca [`EmailDetailDto`](#emaildetaildto).

### Pobierz załącznik {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Przesyła bajty załącznika wraz z jego zapisanym typem zawartości i nazwą pliku. Zwraca
`404`, jeśli wiadomość lub załącznik nie istnieje lub nie jest widoczny dla
wywołującego. Zwraca `403` (problem+json), gdy `Api:AllowAttachmentDownloads=false`.
Rejestruje wpis w dzienniku audytu typu `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Pobierz wiadomość (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Zwraca wiadomość jako `message/rfc822`, odbudowaną z pól archiwalnych:
adresów, przechowywanych surowych nagłówków (`Received`, `Authentication-Results`,
`X-Spam-*`, …), treści oraz załączników. Działa dla każdej zarchiwizowanej wiadomości e-mail. Jest to ta sama rekonstrukcja, którą generuje eksport konta w interfejsie web UI, więc **nie jest** ona identyczna bajt po bajcie z tym, co zostało odebrane: podpisy DKIM nie zostaną zweryfikowane. Do tego celu użyj [`/raw`](#download-original-message).

Zwraca `404`, jeśli wiadomość nie istnieje lub nie jest widoczna dla wywołującego, oraz
`403` (problem+json), gdy `Api:AllowMessageDownloads=false`. Rejestruje wpis w dzienniku dostępu typu `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Pobierz oryginalną wiadomość {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Zwraca oryginalną wiadomość w postaci otrzymanej (`message/rfc822`), do analizy spamu,
weryfikacji DKIM lub kontroli śledczych. Oryginał jest przechowywany tylko wtedy, gdy
w momencie archiwizacji wiadomości e-mail ustawiono `RawMessages:Enabled`; patrz
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Informacja o tym, czy wiadomość e-mail posiada oryginał, jest wyświetlana przez `hasRawMessage` w
[`EmailDetailDto`](#emaildetaildto).

| Nagłówek odpowiedzi | Znaczenie |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 zwróconych bajtów, zarejestrowany w momencie przechowywania wiadomości |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (dokładne kopie) lub `serialized` (nadpisane przez parser, zakończenia linii znormalizowane do CRLF) |
| `X-Raw-Message-Storage` | `postgres` lub `filesystem` — skąd odczytano bajty (patrz [Storage backend](RawMessages.md#storage-backend)) |

Bajty mogą znajdować się w bazie danych lub w pliku, w zależności od
`RawMessages:Storage` w momencie archiwizacji wiadomości; punkt końcowy
rozwiązuje to dla każdej wiadomości, więc oba rodzaje są obsługiwane w ten sam sposób. Przy
`RawMessages:FileSystem:VerifyChecksumOnRead` (domyślne `true`) plik jest
ponownie hashowany przed wysłaniem, a wiadomość, której zawartość nie zgadza się już z
zarejestrowaną sumą kontrolną, **nie** jest zwracana.

Zwraca `404`, jeśli wiadomość nie istnieje lub nie jest widoczna dla wywołującego,
`404` z tytułem problemu `No original message is stored for this email.`
gdy istnieje bez oryginału, oraz `500` z tytułem
`Stored original message failed its checksum.`, gdy przechowywana zawartość nie zgadza się już z
sumą kontrolną zarejestrowaną w czasie archiwizacji. Zwraca `403` (problem+json), gdy
`Api:AllowMessageDownloads=false`. Rejestruje wpis w dzienniku dostępu typu `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Statystyki {#stats}

```
GET /api/v1/stats
```

Zwraca zagregowane statystyki Archiwum widoczne dla wywołującego: całkowitą liczbę zarchiwizowanych
wiadomości e-mail, widoczne konta pocztowe, załączniki do tych wiadomości oraz całkowity
rozmiar bazy danych PostgreSQL w megabajtach. Dla użytkowników bez uprawnień administratora liczby są ograniczone do kont pocztowych przypisanych do nich (tak samo jak w przypadku pulpitu nawigacyjnego web); administratorzy widzą sumy globalne.

Dla kluczy API bez uprawnień administratora, `databaseSizeInMB` jest pustym ciągiem znaków: rozmiar bazy
danych opisuje całą instancję, a nie zakres klucza. Wiadomości e-mail, Konta i Załączniki są zawsze ograniczone do kont, do których klucz może mieć dostęp.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `emails` | int | Zarchiwizowane wiadomości widoczne dla wywołującego |
| `accounts` | int | Konta pocztowe widoczne dla wywołującego |
| `attachments` | int | Załączniki w widocznych wiadomościach |
| `databaseSizeInMB` | string | Całkowity rozmiar bazy danych PostgreSQL w MB; puste dla kluczy niebędących administratorami |

## Typy danych {#data-types}

### MailAccountDto {#mailaccountdto}

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Żadne pola poświadczeń nie są serializowane.

### FolderNodeDto {#foldernodedto}

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `name` | string | Nazwa liścia |
| `fullPath` | string | Pełna ścieżka |
| `totalCount` | int | Wiadomości w folderze |
| `level` | int | Głębokość zagnieżdżenia (0 = root) |
| `children` | FolderNodeDto[] | Podfoldery |

### EmailSummaryDto {#emailsummarydto}

| Pole | Typ |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Wszystkie z [`EmailSummaryDto`](#emailsummarydto) plus:

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Rozwiązane przez łańcuch fallbacku body |
| `htmlBody` | string | Rozwiązane przez łańcuch fallbacku body |
| `attachments` | AttachmentDto[] | Tylko metadane |
| `hasRawMessage` | bool | `true` gdy oryginalna wiadomość jest przechowywana i [`/raw`](#download-original-message) ją zwraca |

### AttachmentDto {#attachmentdto}

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bajty |

Bajty załącznika są uzyskiwane z [download endpoint](#download-attachment),
a nie z tego DTO.

## Logowanie dostępu {#access-logging}

Każdy dostęp do danych za pośrednictwem API jest rejestrowany w tym samym dzienniku dostępu, z którego korzysta interfejs webowy (widocznym w sekcji **Logs**), i jest przypisany do użytkownika będącego właścicielem klucza:

| Endpoint | Typ logowania |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI i Swagger UI {#openapi-and-swagger-ui}

Gdy API jest włączone i `Api:EnableSwaggerUi=true`:

- Dokument OpenAPI v1 jest udostępniany pod adresem **`/apidocs/spec/v1.json`**.
- Swagger UI jest udostępniany pod adresem **`/apidocs`**.

Obie ścieżki znajdują się poza `/api/` i są zatem chronione przez standardową uwierzytelnianie webowe (cookie) — do ich wyświetlenia wymagana jest zalogowana sesja przeglądarki i nie można się do nich dostać za pomocą klucza API.

### Referencja punktów końcowych na tej stronie {#endpoint-reference-on-this-site}

Punkty końcowe oraz typy danych są również publikowane tutaj jako wygenerowana strona:
**[API Reference](API-Reference.md)**. Jest ona renderowana z tego samego dokumentu OpenAPI — kopia jest przechowywana w `doc/assets/openapi-v1.json` — przez `tools/gen_api_reference.py`. CI kończy się niepowodzeniem, gdy strona i specyfikacja przestają być w Synchronizacja, więc referencja zawsze opisuje API, które jest faktycznie dostarczane.

!!! note "Regenerowanie"
    Strona jest wyjściem wygenerowanym. Zmień API (lub generator), a następnie uruchom
    `python3 tools/gen_api_reference.py` i zatwierdź wynik.

    Odświeżenie samej dostarczonej specyfikacji oznacza ponowne uruchomienie generowania
    dokumentacji w czasie budowania, opisanego w `tools/README.md`.
