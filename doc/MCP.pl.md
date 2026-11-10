# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver dostarcza opcjonalny **serwer MCP (Model Context Protocol)**, który
udostępnia zarchiwizowaną pocztę agentom AI za pośrednictwem transportu Streamable HTTP.
Odzwierciedla on [REST API](API.md) w trybie tylko do odczytu: te same dane, ten sam model
uprawnień na użytkownika, to samo logowanie dostępu — tyle że spakowane jako narzędzia MCP
zamiast punktów końcowych REST, dzięki czemu agent LLM może je bezpośrednio odkrywać i wywoływać.

Celem jest umożliwienie agentowi AI odczytywania zarchiwizowanej poczty **bez konieczności
udostępniania danych uwierzytelniających skrzynki pocztowej lub komunikowania się z dostawcą poczty.**
Zakresowy klucz API na użytkownika to uprawnienie do *odczytu Archiwum*, a nie dane uwierzytelniające
pozwalające *być skrzynką pocztową* — dziedziczy on jedynie uprawnienia do skrzynki pocztowej swojego właściciela,
nie zapewnia ścieżki zapisu i może zostać natychmiast cofnięty bez wpływu na powiązaną usługę Konto.

> ⚠️ **Serwer MCP jest domyślnie wyłączony.** Musi zostać jawnie włączony w
> konfiguracji (patrz [Enabling the MCP server](#enabling-the-mcp-server)). Gdy jest
> wyłączony, punkt końcowy `/mcp` zwraca `404 Not Found`.

Serwer MCP jest **z założenia przeznaczony tylko do odczytu**: udostępnia narzędzia do listowania kont,
listowania folderów, przeszukiwania wiadomości, odczytywania wiadomości oraz pobierania załączników.
Nigdy nie tworzy, nie modyfikuje ani nie usuwa danych.

## Spis treści {#table-of-contents}

- [Włączanie serwera MCP](#enabling-the-mcp-server)
- [Uwierzytelnianie](#authentication)
- [Transport i punkt końcowy](#transport-and-endpoint)
- [Narzędzia](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Ograniczanie liczby żądań](#rate-limiting)
- [Logowanie dostępu](#access-logging)
- [Łączenie agenta](#connecting-an-agent)
- [Wskazówki dotyczące bezpieczeństwa przy udostępnianiu w internecie](#security-guidance-for-internet-exposure)

## Włączanie serwera MCP {#enabling-the-mcp-server}

Serwer MCP jest konfigurowany w sekcji `Mcp` `appsettings.json` (lub za pomocą odpowiednich zmiennych środowiskowych, np. `Mcp__Enabled=true`):

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Główny przełącznik. Gdy `false`, punkt końcowy `/mcp` zwraca `404`. |
| `AllowAttachmentDownloads` | `true` | Gdy `false`, `get_attachment` zwraca błąd zamiast bajtów załącznika. Metadane dotyczące załączników są nadal zwracane przez `get_email`. |
| `DefaultPageSize` | `20` | Rozmiar strony używany przez `search_emails`, gdy wywołujący pomija `pageSize`. |
| `MaxResults` | `100` | Górna granica rozmiaru strony `search_emails`; większe wartości są ograniczane. |
| `MaxAttachmentBytes` | `10000000` | Maksymalny rozmiar załącznika (w bajtach), który `get_attachment` zwróci wewnątrz jako base64; większe załączniki zostaną odrzucone. |
| `AllowMessageDownloads` | `true` | Gdy `false`, `get_email_eml` i `get_email_raw` zwracają błąd zamiast wiadomości. Kompletna wiadomość zawiera swoje załączniki, więc **zarówno** ten przełącznik, jak i `AllowAttachmentDownloads` muszą być ustawione na `true`; wyłączenie któregokolwiek z nich dezaktywuje oba narzędzia. |
| `MaxMessageBytes` | `10000000` | Maksymalny rozmiar wiadomości (w bajtach), który `get_email_eml` i `get_email_raw` zwrócą wewnątrz; większe wiadomości zostaną odrzucone. |
| `RateLimitPerMinute` | `120` | Budżet żądań w oknie stałym na klucz API na minutę (patrz [Rate limiting](#rate-limiting)). |

## Uwierzytelnianie {#authentication}

Serwer MCP uwierzytelnia się przy użyciu **tych samych kluczy API dla każdego użytkownika co REST API**, przesyłanych jako bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

Klucze API są tworzone i zarządzane w interfejsie webowym w sekcji **API Keys**. Klucz dziedziczy dokładnie te uprawnienia, które posiada użytkownik będący jego właścicielem (administrator widzi wszystkie konta; użytkownik z ograniczonym dostępem widzi tylko przypisane mu konta). Klucze mogą zostać cofnięte lub wygasnąć niezależnie od powiązanych skrzynek pocztowych. Aby zapoznać się z pełnym cyklem życia klucza i modelem uprawnień, należy sprawdzić [sekcję uwierzytelniania REST API](API.md#authentication) — klucze MCP nie stanowią oddzielnego magazynu poświadczeń.

W przypadku, gdy serwer MCP jest wyłączony lub nie przedstawiono poprawnego bearer tokena, punkt końcowy zwraca odpowiednio `404` / `401` (nigdy nie następuje przekierowanie do strony logowania).

## Transport i punkt końcowy {#transport-and-endpoint}

Serwer MCP korzysta z transportu **Streamable HTTP** i jest zamontowany w:

```
POST /mcp
```

Działa w trybie **stateless** (jedno żądanie JSON-RPC na jedno żądanie HTTP), co jest
zalecanym trybem dla serwerów, które nie wymagają żądań od serwera do klienta,
takich jak sampling lub elicitation. Narzędzia są wykrywane za pomocą standardowego
żądania MCP `tools/list` i wywoływane za pomocą `tools/call`.

## Narzędzia {#tools}

### list_accounts {#list_accounts}

Wyświetla konta pocztowe, do których dostęp ma aktualny klucz API. Zwraca
`id`, `name`, `emailAddress`, `provider`, `isEnabled` i `lastSync`.

**Parametry:** brak.

**Wynik:** tablica obiektów konta. Użyj zwróconego `id` z `list_folders`
i `search_emails`.

### list_folders {#list_folders}

Wyświetla drzewo folderów pojedynczego konta pocztowego wraz z liczbą wiadomości w każdym folderze.

**Parametry:**

| Nazwa | Typ | Wymagany | Opis |
| --- | --- | --- | --- |
| `accountId` | integer | tak | Identyfikator konta pocztowego (z `list_accounts`). |

**Wynik:** tablica obiektów folderów, z których każdy zawiera `name`, `fullPath`, `totalCount`,
`level` oraz rekurencyjne `children`. Zwraca pustą listę, jeśli konto nie
istnieje lub klucz nie ma do niego uprawnień.

### search_emails {#search_emails}

Przeszukuje zarchiwizowane e-maile za pomocą zapytania tekstowego, zakresu dat, konta, folderu oraz kierunku.

**Parametry:**

| Nazwa | Typ | Wymagane | Opis |
| --- | --- | --- | --- |
| `q` | string | nie | Zapytanie tekstowe (temat, treść, od/do). |
| `from` | string (ISO-8601) | nie | Filtr daty/czasu UTC (wysłano w dniu lub po). |
| `to` | string (ISO-8601) | nie | Filtr daty/czasu UTC (wysłano w dniu lub przed). |
| `accountId` | integer | nie | Ogranicz do pojedynczego identyfikatora konta pocztowego. |
| `folder` | string | nie | Ogranicz do pełnej ścieżki pojedynczego folderu (np. `INBOX`). |
| `direction` | string | nie | `incoming` lub `outgoing`. |
| `page` | integer | nie | Numer strony (podstawowy `1`). |
| `pageSize` | integer | nie | Rozmiar strony; ograniczony do `[1, MaxResults]`. `0` używa `DefaultPageSize`. |
| `sortBy` | string | nie | `sentdate` (domyślny), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | nie | `asc` lub `desc` (domyślny `desc`). |

**Wynik:** obiekt stronnicowany z `items` (tablica podsumowań e-maili), `page`,
`pageSize`, `totalItems`, `totalPages`. Każde podsumowanie zawiera `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` oraz
`folderName`. Przekaż zwrócony `id` do `get_email`, aby uzyskać pełną treść i listę załączników.

### get_email {#get_email}

Pobiera pełną zawartość pojedynczej zarchiwizowanej wiadomości e-mail.

**Parametry:**

| Nazwa | Typ | Wymagane | Opis |
| --- | --- | --- | --- |
| `id` | integer | tak | Identyfikator zarchiwizowanej wiadomości e-mail (z `search_emails`). |

**Wynik:** pojedynczy obiekt e-mail ze wszystkimi polami podsumowania plus `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` i `attachments` (tablica
`{ id, fileName, contentType, size }`) oraz `hasRawMessage` (czy
`get_email_raw` może zwrócić oryginał). Zwraca `null`, jeśli wiadomość e-mail nie
istnieje lub klucz nie ma uprawnień dla danego konta.

### get_attachment {#get_attachment}

Pobiera pojedynczy załącznik e-mail jako blob base64.

**Parametry:**

| Nazwa | Typ | Wymagany | Opis |
| --- | --- | --- | --- |
| `id` | integer | tak | Identyfikator zarchiwizowanego e-maila, do którego należy załącznik. |
| `attachmentId` | integer | tak | Identyfikator załącznika (z `attachments` `get_email`). |

**Wynik:** obiekt z `id`, `emailId`, `fileName`, `contentType`, `size`
i `base64Data` (surowe bajty załącznika, zakodowane w base64). Narzędzie zwraca
błąd, gdy `Mcp:AllowAttachmentDownloads=false`, gdy załącznik przekracza
`Mcp:MaxAttachmentBytes` lub gdy klucz nie ma uprawnień do konta
e-mail.

### get_email_eml {#get_email_eml}

Zwraca e-mail jako pełną wiadomość RFC 5322 (`.eml`), odbudowaną z
zarchiwizowanych pól: zapisanych nagłówków (`Received`, `Authentication-Results`,
`X-Spam-*`, …), treści oraz załączników. Działa dla każdego zarchiwizowanego e-maila, ale
wynik nie jest identyczny bajtowo z tym, co zostało odebrane, więc podpisy DKIM nie zostaną zweryfikowane — w tym celu użyj [`get_email_raw`](#get_email_raw). Odzwierciedla `GET /api/v1/emails/{id}/eml`.

**Parametry:**

| Nazwa | Typ | Wymagane | Opis |
| --- | --- | --- | --- |
| `id` | integer | tak | Identyfikator zarchiwizowanego e-maila (z `search_emails`). |

**Wynik:** obiekt z `emailId`, `kind` (`eml`), `source` (`null`),
`sha256`, `size`, `encoding` i `content`. `content` to tekst wiadomości, gdy
`encoding` wynosi `utf-8`, a wiadomość jest zakodowana w formacie base64, gdy `encoding` wynosi
`base64` (wiadomości, które nie są poprawne w formacie UTF-8, więc żaden bajt nie zostaje zmieniony). Narzędzie
zwraca błąd, gdy `Mcp:AllowMessageDownloads=false`, gdy wiadomość
przekracza `Mcp:MaxMessageBytes` lub gdy klucz nie ma uprawnień do
konta e-mail.

### get_email_raw {#get_email_raw}

Zwraca oryginalną wiadomość dokładnie w takiej formie, w jakiej została odebrana, do analizy spamu,
kontroli DKIM/SPF/ARC lub informatyki śledczej. Dostępne tylko wtedy, gdy oryginał został zapisany w czasie
archiwizacji (`RawMessages:Enabled`, patrz
[Original Message Storage Settings](Setup.md#-original-message-storage-settings));
`get_email` raportuje to jako `hasRawMessage`. Odzwierciedla
`GET /api/v1/emails/{id}/raw`.

**Parametry:**

| Nazwa | Typ | Wymagane | Opis |
| --- | --- | --- | --- |
| `id` | integer | tak | Zarchiwizowany identyfikator e-mail (z `search_emails`). |

**Wynik:** ten sam obiekt co `get_email_eml` z `kind` `raw`, `source` ustawionym
tak, jak został przechwycony oryginał (`imap`, `graph`, `eml-file` — dokładne kopie —
lub `serialized`, przepisany przez parser z normalizacją końcówek linii do CRLF)
oraz `sha256` sumą kontrolną zapisaną w czasie archiwizacji. Zwraca te same błędy co
`get_email_eml`, a w przypadku braku zapisanego oryginału wiadomości e-mail zwraca błąd.

Bajty są odczytywane z miejsca, w którym zostały zapisane — z bazy danych lub pliku,
w zależności od `RawMessages:Storage` w czasie archiwizacji (patrz
[Storage backend](RawMessages.md#storage-backend)). Przy
`RawMessages:FileSystem:VerifyChecksumOnRead` (domyślnie `true`) plik jest
ponownie hashowany przed zwróceniem; wiadomość, której zawartość nie zgadza się już z
zapisaną sumą kontrolną, jest odrzucana z błędem zamiast zostać zwrócona.

## Rate limiting {#rate-limiting}

Serwer MCP posiada własną politykę ograniczania liczby żądań typu fixed-window (`"Mcp"`), partycjonowaną
według niejawnego prefiksu klucza API (pierwsze 11 znaków tokenu `ma_...`),
z opcją powrotu do adresu IP klienta w przypadku braku tokenu bearer. Budżet jest
konfigurowany za pomocą `Mcp:RateLimitPerMinute` (domyślnie `120` na minutę). W przypadku
przekroczenia limitu, punkt końcowy zwraca `429 Too Many Requests` z nagłówkiem `Retry-After`.
Jest to niezależne od polityki ograniczania liczby żądań REST API.

## Logowanie dostępu {#access-logging}

Każde wywołanie narzędzia jest logowane za pomocą tego samego `IAccessLogService` co REST API,
z przypisaną nazwą użytkownika oraz odpowiadającym typem dostępu (`Search`, `Open`,
`Download`). Aktywność MCP jest zatem widoczna na stronie **Logs** obok
aktywności REST API i web-UI, co zapewnia pojedynczy dziennik audytu.

## Łączenie agenta {#connecting-an-agent}

Serwer MCP komunikuje się za pomocą standardowego transportu MCP Streamable HTTP, więc może połączyć się każdy klient MCP obsługujący HTTP. Adres URL to podstawowy adres URL aplikacji powiększony o `/mcp`, np. `http://localhost:5179/mcp`, a klucz API jest dostarczany za pomocą nagłówka `Authorization: Bearer ma_...`.

W przypadku agentów skonfigurowanych za pomocą standardowego pliku JSON klienta MCP, wpis wygląda następująco:

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Wskazówki dotyczące bezpieczeństwa przy wystawianiu na świat zewnętrzny {#security-guidance-for-internet-exposure}

Serwer MCP został zaprojektowany w taki sposób, że przy zachowaniu ustawień domyślnych oraz poniższych metod utwardzania, zarchiwizowana poczta nie może opuścić systemu bez ważnego klucza API przypisanego do konkretnego użytkownika, który został mu jawnie wydany. Poniższe punkty są **obowiązkowe do przeczytania przed wystawieniem `/mcp` na świat zewnętrzny**.

### Obowiązkowe (blokowanie eksploatacji / wycieku danych) {#mandatory-block-exploitation-data-exfiltration}

1. **Tylko HTTPS.** Terminuj TLS na brzegu sieci (reverse proxy). Klucz API typu bearer przesyłany przez zwykły protokół HTTP może zostać przechwycony i użyty ponownie.
2. **Zachowaj `Mcp:Enabled=false`, chyba że faktycznie go używasz.** Gdy jest wyłączony, `/mcp` zwraca `404` i żadna ścieżka kodu nie jest osiągalna; jest to bezpieczne ustawienie domyślne i bezpieczny stan dla wdrożeń, które nie wymagają dostępu agenta AI.
3. **Ograniczanie liczby żądań (rate limiting) jest wymuszane na punkcie końcowym.** Polityka `"Mcp"` (`Mcp:RateLimitPerMinute`, domyślnie `120`) jest stosowana do zmapowanego punktu końcowego `/mcp`. Jest ona partycjonowana według niejawnego prefiksu klucza API (pierwsze 11 znaków tokena `ma_...`) lub adresu IP klienta, gdy nie jest obecny token bearer. **Zmniejsz `RateLimitPerMinute`** przy wystawianiu na świat zewnętrzny (np. `30`–`60`) — wywołania narzędzi MCP są bardziej obciążające niż wywołania REST i punkt końcowy nie powinien mieć wysokiego budżetu, gdy mogą do niego dotrzeć niezaufani klienci.
4. **Reverse-proxy `X-Forwarded-For` musi być uważnie zaufany.** Ogranicznik liczby żądań przechodzi w tryb awaryjny do `RemoteIpAddress`, co w przypadku proxy jest adresem IP proxy, chyba że do `UseForwardedHeaders` zostanie przekazany rzeczywisty adres IP klienta. Upewnij się, że Twoje proxy poprawnie ustawia `X-Forwarded-For` i że tylko Twoje proxy może to robić; w przeciwnym razie atakujący może podszyć się pod partycję rate-limit.
5. **Nie umieszczaj `/mcp` za pamięcią podręczną CDN.** Odpowiedzi MCP to pojedyncze żądania JSON-RPC i nigdy nie mogą być buforowane — buforowanie naruszyłoby protokół i spowodowałoby wyciek wyników jednego użytkownika do drugiego. Oznacz `/mcp` jako niebuforowalne na brzegu sieci (`Cache-Control: no-store`).

### Higiena kluczy API (blokowanie nieautoryzowanego dostępu) {#api-key-hygiene-block-unauthorized-access}

6. **Wydawaj dedykowane, krótkotrwałe klucze dla agentów MCP.** Pojedynczy klucz `ma_...` jest ważny zarówno dla `/api`, jak i `/mcp`; obecnie nie ma zakresu (scope) na poszczególne protokoły. Dlatego:
   - Twórz **osobny klucz dla każdego agenta** (nie używaj klucza skryptu raportującego dla zewnętrznego agenta LLM).
   - Zawsze ustawiaj krótki czas `ExpiresAt` i rotuj go; cofaj klucz natychmiast po wycofaniu agenta z eksploatacji.
   - Preferuj klucze należące do **użytkownika o ograniczonych uprawnieniach (nie-admina)**, którego jedynymi przypisanymi kontami są te, których potrzebuje agent. Klucz administratora daje wgląd we *wszystkie* konta pocztowe; klucz o ograniczonych uprawnieniach ogranicza zasięg ewentualnej szkody.
7. **Traktuj klucz API jak hasło.** Jest to poświadczenie typu bearer. Nigdy nie umieszczaj go w kodzie po stronie klienta, nigdy go nie loguj i nigdy nie przesyłaj do repozytorium. Umieść go w magazynie sekretów klienta MCP i rotuj w przypadku podejrzenia kompromitacji.
8. **Audytuj na stronie Logs.** Każde wywołanie narzędzia jest rejestrowane w `AccessLogs` (`Search`, `Open`, `Download`) pod użytkownikiem właścicielskim. Okresowo przeglądaj aktywność MCP pod kątem nieoczekiwanych wzorców (skoki wolumenu, odczyty poza godzinami pracy, masowe pobierania).

### Model zaufania dla zwracanych danych (blokowanie szkód następczych) {#trust-model-for-returned-data-block-downstream-harm}

9. **Treści wiadomości e-mail i załączniki to niezaufane treści**, dokładnie takie same, jakimi były w momencie trafienia do skrzynki pocztowej. Mogą zawierać złośliwe oprogramowanie, linki phishingowe lub aktywny kod HTML/JavaScript.
   - `get_email` zwraca `htmlBody` w formie **surowej i niesanitowanej**. **Nie** renderuj jej dla człowieka bez sanitacji (DOMPurify / `HtmlSanitizer` / piaskownica). Renderowanie surowego kodu HTML z archiwum jest wektorem ataku typu stored-XSS.
   - `get_attachment` zwraca surowe bajty z oryginalnym `contentType`. **Nie** otwieraj ani nie wykonuj automatycznie załączników; traktuj pole `contentType` jako niepewne metadane, a nie jako bezpieczną instrukcję.
   - `get_email_eml` i `get_email_raw` zwracają pełną wiadomość wraz z każdym załącznikiem. Te same zasady dotyczą każdej z jej części.
10. **Nie pozwól agentowi podejmować działań zapisu na podstawie treści e-mail bez potwierdzenia.** Złośliwa wiadomość e-mail może zawierać tekst typu prompt-injection wymierzony w konsumujący ją model LLM. Wymuszaj uprawnienia i potwierdzenia w logice samego agenta — systemowy prompt nie jest barierą bezpieczeństwa.

### Rekomendacje dotyczące obrony wielowarstwowej (Defense-in-depth) {#defense-in-depth-recommendations}

11. **Ogranicz `/mcp` na warstwie sieciowej**, jeśli to możliwe (np. zezwól tylko na adres IP / zakres VPN hosta agenta w reverse proxy), jako uzupełnienie uwierzytelniania kluczem API. Jest to najskuteczniejsza pojedyncza kontrola przy wystawianiu na świat zewnętrzny.
12. **Obniż `Mcp:AllowAttachmentDownloads` do `false`**, jeśli Twoi agenci nie potrzebują bajtów załączników — wyciek załączników to ścieżka danych o największym wolumenie. Metadane (nazwa pliku, typ zawartości, rozmiar) są nadal zwracane poprzez `get_email`.
13. **Obniż `Mcp:MaxAttachmentBytes`** do najmniejszej wartości, której Twoi agenci faktycznie potrzebują (domyślnie `10000000` ≈ 10 MB). To ogranicza największą pojedynczą odpowiedź, jaką klucz API może pobrać w jednym żądaniu. To samo dotyczy `Mcp:MaxMessageBytes`, lub ustaw `Mcp:AllowMessageDownloads` na `false`, gdy agenci nie potrzebują pełnych wiadomości.
14. **Obniż `Mcp:MaxResults` i `Mcp:DefaultPageSize`**, aby ograniczyć największy wynik wyszukiwania, jaki klucz API może pobrać w jednym żądaniu (domyślnie `100` / `20`).
15. **Monitoruj `AccessLogs` oraz odrzucenia z powodu limitu żądań** (`429`) w ramach operacyjnych. Nagły skok dla jednego prefiksu klucza jest sygnałem do cofnięcia tego klucza.
