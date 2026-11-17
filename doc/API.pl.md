# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver dostarcza opcjonalne, **tylko do odczytu** REST API do programowego dostępu do zarchiwizowanej poczty. Zostało ono zaprojektowane do przypadków użycia związanych z automatyzacją i integracją (raportowanie, eksport e-discovery, monitorowanie) bez konieczności korzystania z interfejsu webowego.

Celem jest umożliwienie skryptowi lub agentowi AI odczytu zarchiwizowanej poczty **bez narażania danych uwierzytelniających skrzynki pocztowej lub komunikowania się z dostawcą poczty.** Zakresowy klucz API dla użytkownika to funkcja umożliwiająca *odczyt Archiwum*, a nie dane uwierzytelniające pozwalające *być Skrzynką pocztową* — dziedziczy on jedynie uprawnienia Skrzynki pocztowej swojego właściciela, nie zapewnia ścieżki zapisu i może zostać natychmiast cofnięty bez wpływu na powiązaną Skrzynkę pocztową.

> ⚠️ **API jest domyślnie wyłączone.** Musi zostać jawnie włączone w konfiguracji (patrz [Enabling the API](#enabling-the-api)). Gdy jest wyłączone, każda trasa `/api/*` zwraca `404 Not Found`.

API jest **z założenia tylko do odczytu**: udostępnia konta, foldery, wyszukiwanie wiadomości, szczegóły wiadomości oraz pobieranie załączników. Nigdy nie tworzy, nie modyfikuje ani nie usuwa danych.

## Spis treści {#table-of-contents}

- [Włączanie API](#enabling-the-api)
- [Uwierzytelnianie](#authentication)
  - [Format klucza API](#api-key-format)
  - [Cykl życia klucza](#key-lifecycle)
  - [Model uprawnień](#permission-model)
  - [Uzasadnienie bezpieczeństwa](#security-rationale)
- [Konwencje](#conventions)
  - [Base URL i wersjonowanie](#base-url-and-versioning)
  - [Stronicowanie](#pagination)
  - [Błędy](#errors)
  - [Ograniczanie liczby żądań](#rate-limiting)
- [Punkty końcowe](#endpoints)
  - [Listuj konta](#list-accounts)
  - [Listuj foldery](#list-folders)
  - [Wyszukaj wiadomości](#search-messages)
  - [Pobierz wiadomość](#get-message)
  - [Pobierz załącznik](#download-attachment)
  - [Pobierz wiadomość (.eml)](#download-message-eml)
  - [Pobierz oryginalną wiadomość](#download-original-message)
  - [Statystyki](#stats)
  - [Health](#health)
  - [Metryki](#metrics)
- [Typy danych](#data-types)
- [Logowanie dostępu](#access-logging)
- [OpenAPI i Swagger UI](#openapi-and-swagger-ui)
- [Referencja API](API-Reference.md) — wygenerowana referencja punktów końcowych i typów danych

## Włączanie API {#enabling-the-api}

API jest konfigurowane w sekcji `Api` `appsettings.json` (lub za pomocą odpowiednich zmiennych środowiskowych, np. `Api__Enabled=true`):

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

1. **Utwórz** — użytkownik tworzy klucz z opisowym `Name` oraz opcjonalną datą wygaśnięcia. Odpowiedź wyświetla klucz w formie tekstowej tylko raz.
2. **Użyj** — wyślij klucz jako bearer token. Każde zaakceptowane żądanie odświeża `LastUsedAt` klucza (zapisy są ograniczane do maksymalnie raz na około 5 minut, aby uniknąć zapisu przy każdym wywołaniu).
3. **Wygasł** — jeśli `ExpiresAt` jest ustawione i data ta już minęła, klucz zostanie odrzucony.
4. **Cofnij** — użytkownik właściciel lub administrator cofa klucz (ustawiany jest `RevokedAt`); klucz jest natychmiast i trwale odrzucany.

Klucz jest **aktywny**, gdy nie został cofnięty, nie wygasł **i** jego użytkownik właściciel jest nadal aktywny. Klucz nieaktywny z któregokolwiek z tych powodów jest odrzucany z błędem `401`.

### Model uprawnień {#permission-model}

Klucz API dziedziczy dokładnie te same uprawnienia, które posiada jego właściciel. Uwierzytelniony podmiot posiada te same roszczenia (claims), co przy normalnym logowaniu przez przeglądarkę, więc wszystkie istniejące uprawnienia...

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Główny przełącznik. Gdy `false`, wszystkie trasy `/api/*` zwracają `404`, a dokument OpenAPI / Swagger UI nie są mapowane. |
| `AllowAttachmentDownloads` | `true` | Gdy `false`, punkt końcowy pobierania załączników zwraca `403`. Metadane dotyczące załączników są nadal zwracane przez punkt końcowy szczegółów wiadomości. |
| `AllowMessageDownloads` | `true` | Gdy `false`, punkty końcowe kompletnych wiadomości (`/eml` i `/raw`) zwracają `403`. Kompletna wiadomość zawiera swoje załączniki, więc **zarówno** ten przełącznik, jak i `AllowAttachmentDownloads` muszą być ustawione na `true`; wyłączenie któregokolwiek z nich dezaktywuje oba punkty końcowe. |
| `EnableSwaggerUi` | `true` | Gdy `true` (a `Enabled` jest `true`), montuje Swagger UI pod `/apidocs` oraz dokument OpenAPI pod `/apidocs/spec/v1.json`. Oba wymagają uwierzytelnionej **sesji webowej (cookie)** — nie są one częścią `/api/` i nie można ich uzyskać za pomocą klucza API. |
| `DefaultPageSize` | `20` | Rozmiar strony używany, gdy żądanie pomija `pageSize`. |
| `MaxPageSize` | `100` | Górna granica; większe wartości `pageSize` są ograniczane do tej wartości. |
| `RateLimitPerMinute` | `120` | Budżet żądań w oknie stałym na klucz API na minutę (patrz [Rate limiting](#rate-limiting)). |

## Uwierzytelnianie {#authentication}

API uwierzytelnia się za pomocą **kluczy API dla każdego użytkownika** przesyłanych jako bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` akceptuje **tylko** klucze API. Pliki cookie przeglądarki nie są honorowane, więc API nie posiada powierzchni ataku CSRF. Błędy uwierzytelniania zwracają `401` z nagłówkiem `WWW-Authenticate: Bearer` i ciałem w formacie [problem+json](#errors) — API **nigdy** nie przekierowuje do strony logowania.

Klucze są zarządzane w modelu samoobsługowym w interfejsie webowym w sekcji **API Keys** (każdy użytkownik zarządza własnymi kluczami; administratorzy mogą przeglądać i unieważniać klucze dowolnego użytkownika).

### Format klucza API {#api-key-format}

Klucz to ciąg znaków `ma_`, po którym następuje 43 znaki base64 bezpieczne dla URL, kodujące 32 losowe bajty (256 bitów entropii):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Przechowywanie:

- **`KeyPrefix`** — pierwsze ~11 znaków (np. `ma_8Kf3pQ2`), przechowywane w postaci jawnej. Służy do identyfikacji klucza w interfejsie UI i w logach. Nie jest to sekret.
- **`KeyHash`** — zakodowany w formacie hex skrót SHA-256 pełnego klucza, przechowywany pod **unikalnym indeksem**. Klucz jawny nigdy nie jest przechowywany.

Pełny klucz jest wyświetlany użytkownikowi **dokładnie raz**, w momencie tworzenia. Nie można go odzyskać później; zgubiony klucz musi zostać unieważniony i zastąpiony nowym.

### Cykl życia klucza {#key-lifecycle}

1. **Utwórz** — użytkownik tworzy klucz z opisem `Name` i opcjonalną datą wygaśnięcia. Odpowiedź wyświetla klucz jawny tylko raz.
2. **Użyj** — wyślij klucz jako bearer token. Każde zaakceptowane żądanie odświeża `LastUsedAt` klucza (zapisy są ograniczane do maksymalnie raz na ~5 minut, aby uniknąć zapisu przy każdym wywołaniu).
3. **Wygasnij** — jeśli `ExpiresAt` jest ustawione i data ta już minęła, klucz zostaje odrzucony.
4. **Unieważnij** — właściciel klucza lub administrator unieważnia klucz (zostaje ustawione `RevokedAt`); klucz jest natychmiast i trwale odrzucany.

Klucz jest **aktywny**, gdy nie został unieważniony, nie wygasł **i** jego właściciel jest nadal aktywny. Klucz nieaktywny z któregokolwiek z tych powodów jest odrzucany z kodem `401`.

### Model uprawnień {#permission-model}

Klucz API dziedziczy dokładnie te same uprawnienia, które posiada jego właściciel. Uwierzytelniony podmiot posiada te same roszczenia (claims), co przy normalnym logowaniu przez przeglądarkę, więc cała istniejąca logika autoryzacji działa bez zmian:

- **Administratorzy** (`IsAdmin`) widzą wszystkie konta pocztowe i wszystkie wiadomości.
- **Zwykli użytkownicy** widzą tylko przypisane do nich konta pocztowe (`UserMailAccount`). Konta i wiadomości spoza tego zestawu są traktowane jako **nieistniejące**: żądania dotyczące nich zwracają `404`, a nie `403`, dzięki czemu API nie ujawnia istnienia kont, których wywołujący nie może zobaczyć.

### Uzasadnienie bezpieczeństwa {#security-rationale}

- **Ominięcie 2FA.** Klucze API z założenia omijają uwierzytelnianie dwuskładnikowe — jest to cecha charakterystyczna tokenów nieinteraktywnych (podobnie jak w przypadku tokenów dostępu osobistego w GitHub/GitLab). Łagodzone jest to poprzez: klucze **wyłączone domyślnie**, możliwość unieważnienia, opcjonalne wygasanie, ograniczenie do uprawnień użytkownika oraz rejestrowanie każdego dostępu w [access log](#access-logging).
- **Brak plików cookie na `/api`.** Uwierzytelnianie za pomocą plików cookie nie jest akceptowane na trasach `/api`, co eliminuje obawy dotyczące CSRF w przypadku API.
- **Transport.** W środowisku produkcyjnym API zawsze należy obsługiwać przez HTTPS, aby tokeny typu bearer nie były narażone na przechwycenie w sieci.

## Konwencje {#conventions}

### Base URL i wersjonowanie {#base-url-and-versioning}

Wszystkie punkty końcowe znajdują się pod `/api/v1`. Wersja znajduje się w ścieżce, dzięki czemu przyszłe zmiany wprowadzające przełamanie wstecznej kompatybilności mogą być wydawane jako `/api/v2` bez zakłócania pracy istniejących klientów.

Odpowiedzi mają format `application/json`, chyba że zaznaczono inaczej (pobieranie załączników zwraca własny typ zawartości załącznika). Wszystkie znaczniki czasu są w formacie UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Stronicowanie {#pagination}

Punkty końcowe list zwracają opakowanie stronowania:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` zaczyna się od 1. Strony poza zakresem zwracają pustą tablicę `items` (nie jest to błąd).
- `pageSize` domyślnie wynosi `Api:DefaultPageSize` i jest ograniczony do `[1, Api:MaxPageSize]`.

### Błędy {#errors}

Błędy korzystają z [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
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
| `400 Bad Request` | Nieprawidłowe parametry (np. niepoprawna data, nieprawidłowy `sortBy`). |
| `401 Unauthorized` | Brakujący, błędny, wygasły, cofnięty lub w inny sposób nieprawidłowy klucz. Obejmuje `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Żądanie pobrania załącznika podczas `AllowAttachmentDownloads=false` lub pobrania wiadomości podczas `AllowMessageDownloads=false`. |
| `404 Not Found` | Zasób nie istnieje **lub** znajduje się poza kontami, do których wywołujący ma uprawnienia. Zwracany również dla każdej trasy `/api/*`, gdy `Api:Enabled=false`. |
| `429 Too Many Requests` | Przekroczono limit żądań. |
| `500 Internal Server Error` | Nieoczekiwany błąd serwera (problem+json, nigdy strona błędu HTML). |

### Ograniczanie liczby żądań {#rate-limiting}

Żądania są ograniczone przez politykę stałego okna `Api:RateLimitPerMinute`
żądań na minutę (domyślnie `120`), podzieloną według prefiksu klucza API (w przypadku braku klucza następuje przełączenie na adres IP klienta). Przekroczenie limitu zwraca `429`.

## Punkty końcowe {#endpoints}

> Wszystkie przykłady zakładają, że API jest włączone, a `$KEY` zawiera ważny klucz.

### Lista kont {#list-accounts}

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

### Lista folderów {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Zwraca drzewo folderów dla jednego konta jako strukturę zagnieżdżoną. Zwraca `404`, jeśli
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
| `q` | string | Zapytanie pełnotekstowe w polach subject, body, from, to, cc, bcc. Obsługuje składnię pól, np. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Najwcześniejsza data wysłania (włącznie), `YYYY-MM-DD`. |
| `to` | date | Najpóźniejsza data wysłania (włącznie), `YYYY-MM-DD`. |
| `accountId` | int | Ogranicz do jednego konta (musi być widoczne dla wywołującego). |
| `folder` | string | Ogranicz do jednego folderu według pełnej ścieżki. |
| `direction` | string | `incoming` lub `outgoing`. |
| `page` | int | Numer strony (podstawowy `1`). |
| `pageSize` | int | Wyniki na stronę (domyślnie `Api:DefaultPageSize`, ograniczone do `Api:MaxPageSize`). |
| `sortBy` | string | Jedna z `sentDate` (domyślnie), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` lub `desc` (domyślnie `desc`). |

Zwraca [paged envelope](#pagination) zawierający [`EmailSummaryDto`](#emailsummarydto).
Loguje wpis w [access log](#access-logging) typu `Search`.

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

Zwraca pełną wiadomość wraz z treściami. Treść korzysta z tego samego łańcucha zastępczego co interfejs webowy: `OriginalBodyHtml` (surowe bajty) → `BodyUntruncatedHtml` →
`HtmlBody`, oraz odpowiedniego dla treści tekstowej. Zwraca `404`, jeśli wiadomość
nie istnieje lub należy do konta spoza uprawnień wywołującego. Rejestruje wpis w dzienniku audytu typu `Open`.

Zwraca [`EmailDetailDto`](#emaildetaildto).

### Pobierz załącznik {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Przesyła bajty załącznika wraz z jego zapisanym typem zawartości i nazwą pliku. Zwraca
`404`, jeśli wiadomość lub załącznik nie istnieją lub nie są widoczne dla
wywołującego. Zwraca `403` (problem+json), gdy `Api:AllowAttachmentDownloads=false`.
Loguje wpis w dzienniku audytu typu `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Pobierz wiadomość (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Zwraca wiadomość jako `message/rfc822`, odbudowaną z zarchiwizowanych pól:
adresów, zapisanych surowych nagłówków (`Received`, `Authentication-Results`,
`X-Spam-*`, …), treści i załączników. Działa dla każdej zarchiwizowanej wiadomości e-mail. Jest to
ta sama rekonstrukcja, którą generuje eksport konta w interfejsie webowym, więc **nie jest**
identyczna bajtowo z tym, co zostało odebrane: sygnatury DKIM nie zostaną zweryfikowane względem niej.
W tym celu użyj [`/raw`](#download-original-message).

Zwraca `404`, jeśli wiadomość nie istnieje lub nie jest widoczna dla wywołującego, oraz
`403` (problem+json), gdy `Api:AllowMessageDownloads=false`. Tworzy wpis w dzienniku audytu
o typie `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Pobierz oryginalną wiadomość {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Zwraca oryginalną wiadomość w formie, w jakiej została odebrana (`message/rfc822`), w celu analizy spamu,
weryfikacji DKIM lub kontroli śledczych. Oryginał jest przechowywany tylko wtedy, gdy
w momencie archiwizacji wiadomości e-mail ustawiono `RawMessages:Enabled`; zobacz
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Informacja o tym, czy wiadomość e-mail posiada oryginał, jest wyświetlana przez `hasRawMessage` w
[`EmailDetailDto`](#emaildetaildto).

| Nagłówek odpowiedzi | Znaczenie |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 zwróconych bajtów, zarejestrowany w momencie zapisania wiadomości |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (dokładne kopie) lub `serialized` (przepisane przez parser, zakończenia linii znormalizowane do CRLF) |
| `X-Raw-Message-Storage` | `postgres` lub `filesystem` — skąd odczytano bajty (zobacz [Storage backend](RawMessages.md#storage-backend)) |

Bajty mogą znajdować się w bazie danych lub w pliku, w zależności od
`RawMessages:Storage` w momencie, gdy wiadomość została zarchiwizowana; punkt końcowy
rozwiązuje to dla każdej wiadomości z osobna, więc oba rodzaje są obsługiwane w ten sam sposób. Przy
`RawMessages:FileSystem:VerifyChecksumOnRead` (domyślnie `true`) plik jest
ponownie haszowany przed wysłaniem, a wiadomość, której zawartość nie zgadza się już z
zarejestrowaną sumą kontrolną, **nie** jest zwracana.

Zwraca `404`, jeśli wiadomość nie istnieje lub nie jest widoczna dla wywołującego,
`404` z tytułem problemu `No original message is stored for this email.`,
gdy wiadomość istnieje, ale nie posiada oryginału, oraz `500` z tytułem
`Stored original message failed its checksum.`, gdy przechowywana zawartość nie zgadza się już z
sumą kontrolną zarejestrowaną w czasie archiwizacji. Zwraca `403` (problem+json), gdy
`Api:AllowMessageDownloads=false`. Rejestruje wpis w dzienniku audytu typu `Download`.

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
wiadomości e-mail, widoczne konta pocztowe, załączniki w tych wiadomościach oraz całkowity
rozmiar bazy danych PostgreSQL w megabajtach. Dla użytkowników bez uprawnień administratora liczby są
ograniczone do kont pocztowych przypisanych do nich (tak samo jak w panelu webowym); administratorzy widzą sumy globalne.

Dla kluczy API bez uprawnień administratora, `databaseSizeInMB` jest pustym ciągiem znaków: rozmiar bazy danych
opisuje całą instancję, a nie zakres klucza. E-maile, Konta i Załączniki są zawsze ograniczone do kont, do których klucz ma dostęp.

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
| `attachments` | int | Załączniki w tych widocznych wiadomościach |
| `databaseSizeInMB` | string | Całkowity rozmiar bazy danych PostgreSQL w MB; puste dla kluczy niebędących administratorami |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

Wszystkie trzy wymagają klucza API administratora. `health/live` zwraca 200 bez dotykania jakiejkolwiek
zależności. `health/ready` sprawdza PostgreSQL oraz schemat Archiwum i zwraca 503, gdy
którykolwiek z nich jest niedostępny. `health` wykonuje również sprawdzenie świeżości Synchronizacji i zwraca `Degraded`
(HTTP 200), gdy aktywne Konto nie przeprowadziło Synchronizacji w ciągu `Health:SyncStaleMinutes`.

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

Pełny opis, w tym zachowanie 401/403/404 oraz Docker health check, znajduje się w
**[Health and Metrics](Monitoring.md)**.

### Metryki {#metrics}

```
GET /api/v1/metrics
```

Zwraca dane w formacie Prometheus text exposition format. Wymaga klucza API administratora oraz `Metrics:Enabled=true`; w przeciwnym razie punkt końcowy zwraca błąd 404. Nazwy metryk, etykiety oraz przykłady alertów znajdują się w sekcji **[Health and Metrics](Monitoring.md)**.

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

Wszystkie pola z [`EmailSummaryDto`](#emailsummarydto) plus:

| Pole | Typ | Uwagi |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Rozwiązane przez łańcuch fallback dla body |
| `htmlBody` | string | Rozwiązane przez łańcuch fallback dla body |
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

Każdy dostęp do danych za pośrednictwem API jest rejestrowany w tym samym dzienniku dostępu, którego używa interfejs webowy (widocznym w sekcji **Logs**), i jest przypisany do użytkownika będącego właścicielem klucza:

| Endpoint | Typ logowania |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI i Swagger UI {#openapi-and-swagger-ui}

Gdy API jest włączone i `Api:EnableSwaggerUi=true`:

- Dokument OpenAPI v1 jest serwowany pod adresem **`/apidocs/spec/v1.json`**.
- Swagger UI jest serwowany pod adresem **`/apidocs`**.

Obie ścieżki znajdują się poza `/api/` i są zatem chronione standardową uwierzytelnianiem webowym (cookie) — do ich wyświetlenia wymagana jest zalogowana sesja przeglądarki, a nie można się do nich dostać za pomocą klucza API.

### Referencja punktów końcowych na tej stronie {#endpoint-reference-on-this-site}

Punkty końcowe i typy danych są również publikowane tutaj jako wygenerowana strona:
**[API Reference](API-Reference.md)**. Jest ona renderowana z tego samego dokumentu OpenAPI — kopia jest przechowywana w `doc/assets/openapi-v1.json` — przez `tools/gen_api_reference.py`. Proces CI kończy się błędem, gdy strona i specyfikacja przestają być w Synchronizacja, dzięki czemu referencja zawsze opisuje API, które faktycznie jest dostarczane.

!!! note "Regenerowanie"
    Strona jest wyjściem wygenerowanym. Zmień API (lub generator), a następnie uruchom
    `python3 tools/gen_api_reference.py` i zatwierdź wynik.

    Odświeżenie samej dołączonej specyfikacji oznacza ponowne uruchomienie generowania
    dokumentacji w czasie budowania, opisanego w `tools/README.md`.
