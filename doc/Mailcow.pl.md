# Archiwum instancji Mailcow {#mailcow-instance-archiving}

[← Powrót do spisu treści dokumentacji](index.md)

## Przegląd {#overview}

Mail Archiver może centralnie archiwizować Skrzynki pocztowe instancji [Mailcow](https://mailcow.email), bez zbierania osobistych haseł właścicieli skrzynek.

Administrator łączy instancję tylko raz za pomocą swojego klucza API. Mail Archiver wymienia następnie Skrzynki pocztowe i dla każdej wybranej do archiwizacji tworzy **dedykowane hasło aplikacji** ograniczone do `imap_access`. Hasło to jest używane przez istniejącego dostawcę IMAP do archiwizacji skrzynki i może zostać cofnięte w dowolnym momencie.

Administratorzy przechodzą do menedżera instancji z poziomu **Add Account → Mailcow** lub z menu akcji konta, które jest już zarządzane przez Mailcow. Menedżer jest dostępny wyłącznie dla administratorów, ponieważ zapisany klucz API może zarządzać poświadczeniami dla każdej Skrzynki pocztowej w instancji.

## Tworzenie kont z Mailcow {#creating-accounts-from-mailcow}

W sekcji **Mail Accounts → Add Account** administratorzy mogą wybrać **Mailcow** jako dostawcę. Formularz wyświetli wtedy połączone instancje oraz akcję **Manage mailboxes**. Wybranie skrzynek w tym miejscu tworzy zwykłe konta archiwum IMAP z dedykowanymi hasłami aplikacji; nie jest zapisywane żadne oddzielne konto Mailcow. Jeśli żadna instancja nie istnieje, przycisk **Manage Mailcow instances** otwiera menedżera instancji, gdzie można nawiązać połączenie.

## Łączenie istniejącego konta {#linking-an-existing-account}

W widoku edycji konta IMAP administratorzy mogą zmienić dostawcę na **Mailcow** i wybrać instancję. Konto zachowuje dostawcę IMAP, ale zostaje przejęte przez zarządzanie Mailcow: Mail Archiver tworzy dedykowane hasło aplikacji, weryfikuje je przez IMAP i zastępuje zapisane hasło konta. Konto pojawia się następnie z akcją Mailcow na liście kont.

Aby zrezygnować z zarządzania przez Mailcow, należy wybrać innego dostawcę i zapisać zmiany. Hasło aplikacji zostaje cofnięte, a lokalne hasło jest czyszczone. Przy powrocie do IMAP należy wprowadzić nowe hasło IMAP; przełączenie na Microsoft 365 lub konto tylko do importu dostarczy własne poświadczenia. Osoby niebędące administratorami mogą nadal edytować ustawienia niepowiązane z poświadczeniami konta zarządzanego przez Mailcow, ale nie mogą go łączyć ani odłączać.

## Wymagania wstępne {#prerequisites}

### Szyfrowanie poświadczeń musi być włączone {#credential-encryption-must-be-enabled}

Mail Archiver przechowuje klucz API Mailcow oraz każde wygenerowane hasło aplikacji w formie zaszyfrowanej przy użyciu istniejącej funkcji szyfrowania poświadczeń. Ustaw

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

przed połączeniem instancji. Tworzenie instancji oraz aktywacja skrzynek zostaną odrzucone, jeśli szyfrowanie jest wyłączone, dzięki czemu poświadczenia nigdy nie zostaną zapisane jako tekst jawny za pomocą tej funkcji. Przechowuj klucz poza bazą danych i dołącz go do swojej bezpiecznej Kopii zapasowej — bez niego zaszyfrowane hasła aplikacji nie mogą zostać odszyfrowane, a Skrzynki pocztowe nie mogą zostać skonfigurowane. Szczegóły znajdują się w sekcji [Setup](Setup.md#-security-settings).

### Dostęp do API Mailcow {#mailcow-api-access}

Utwórz klucz API w Mailcow w sekcji **Configuration → Access → API**. Klucz musi mieć uprawnienia do:

- wymieniania skrzynek pocztowych (`get/mailbox/all`),
- wymieniania, tworzenia i usuwania haseł aplikacji (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Ogranicz dostęp do API tylko do adresu IP hosta Mail Archiver w ustawieniach API Mailcow. Punkt końcowy haseł aplikacji w Mailcow wymaga, aby funkcja **App Passwords** była włączona dla danej instancji.

### Dostęp sieciowy {#network-access}

- **HTTPS** do źródła Mailcow (na przykład `https://mail.example.com`).
- **IMAP over TLS** do hosta IMAP, zazwyczaj port `993`. IMAP w tekście jawnym nie jest obsługiwany podczas konfiguracji.

## Instrukcja krok po kroku {#step-by-step-usage}

### Łączenie instancji {#connecting-an-instance}

1. Zaloguj się do Mail Archiver jako administrator.
2. Otwórz **Mail Accounts** i kliknij **Mailcow instances**.
3. Kliknij **Connect Mailcow instance**.
4. Wprowadź szczegóły połączenia:
   - **Account name** — nazwa wyświetlana instancji w Mail Archiver.
   - **Mailcow URL** — wyłącznie źródło HTTPS, na przykład `https://mail.example.com`. Ścieżki, ciągi zapytań i osadzone poświadczenia zostaną odrzucone.
   - **API key** — klucz utworzony w Mailcow.
   - **IMAP host** — nazwa hosta używana do archiwizacji, na przykład `mail.example.com`. Wprowadź samą nazwę hosta, bez schematu lub portu.
   - **IMAP TLS port** — domyślnie `993`.
5. Kliknij **Connect**. Mail Archiver weryfikuje klucz API poprzez wymienienie skrzynek pocztowych przed zapisaniem instancji. W przypadku sukcesu otworzy wybór skrzynek.

Klucz API jest przechowywany w formie zaszyfrowanej i nigdy nie jest ponownie wyświetlany w formularzu ani w komunikacie o błędzie. Przekierowania HTTP są wyłączone dla wywołań API, aby klucz nie mógł zostać przekazany do innego źródła.

### Aktywacja skrzynek pocztowych {#activating-mailboxes}

1. Na stronie skrzynek pocztowych opcjonalnie przefiltruj listę za pomocą pola wyszukiwania. Filtr dopasowuje adres i nazwę wyświetlaną, nie rozróżniając wielkości liter.
2. Wybierz skrzynki do archiwizacji. Opcje **Select all** i **Clear selection** dotyczą tylko obecnie widocznych (przefiltrowanych) skrzynek, więc filtrowanie jest bezpiecznym sposobem na wybranie podzbioru.
3. Kliknij **Activate archiving**.

Dla każdej wybranej skrzynki Mail Archiver:

1. ponownie weryfikuje adres względem aktualnej listy skrzynek Mailcow i ignoruje wyłączone skrzynki,
2. tworzy konto archiwum o nazwie `{instance name} – {mailbox display name}`,
3. generuje losowe hasło aplikacji o nazwie `MailArchiver-<unique id>` ograniczone do `imap_access`,
4. weryfikuje poświadczenia poprzez logowanie IMAP przez TLS,
5. i dopiero wtedy uruchamia Synchronizację.

Jeśli weryfikacja IMAP nie powiedzie się, konto pozostaje wyłączone i można podjąć ponowną próbę.

### Etykiety statusu skrzynki pocztowej {#mailbox-status-badges}

Strona wyboru etykietuje każdą skrzynkę, aby było jasne, dlaczego pole wyboru jest dostępne lub niedostępne:

| Etykieta | Znaczenie | Możliwe do wybrania |
| --- | --- | --- |
| **Available** | Wymieniona w Mailcow, jeszcze nie skonfigurowana | tak |
| **Active** | Skonfigurowana i synchronizująca się | nie |
| **Pending** | Konfiguracja rozpoczęta, ale nie zakończona (można powtórzyć) | tak |
| **Revoked** | Hasło aplikacji zostało cofnięte; można je ponownie aktywować | tak |
| **Existing** | Niezależne konto IMAP z tym adresem już istnieje | nie |
| **Disabled** | Skrzynka pocztowa jest wyłączona w Mailcow | nie |

### Ponawianie próby dla oczekującej skrzynki {#retrying-a-pending-mailbox}

Wygenerowane poświadczenia oraz unikalna nazwa hasła aplikacji są zapisywane **zanim** zostanie wywołane API Mailcow. Jeśli żądanie zakończy się niejednoznacznie — na przykład przekroczeniem czasu oczekiwania po utworzeniu hasła aplikacji — skrzynka pozostaje widoczna jako **Pending**. Odśwież stronę i wybierz ją ponownie: Mail Archiver najpierw sprawdza nazwę aplikacji i tworzy nowe hasło aplikacji tylko wtedy, gdy żadne nie istnieje. Ponawianie nie powoduje kumulowania duplikujących się poświadczeń.

Konfiguracja i cofanie uprawnień dla jednej instancji są szeregowane przy użyciu blokad doradczych PostgreSQL, dzięki czemu dwie jednoczesne operacje nie mogą skonfigurować tej samej skrzynki dwukrotnie.

### Cofanie dostępu {#revoking-access}

Kliknij **Revoke app password** obok skrzynki pocztowej. Powoduje to:

- wyłączenie przyszłej Synchronizacji,
- usunięcie zarządzanego hasła aplikacji w Mailcow,
- i wyczyszczenie lokalnego hasła.

**Zarchiwizowane maile są zachowane.** Usuwane jest tylko zarządzane hasło aplikacji — inne hasła aplikacji należące do skrzynki nie są dotykane. Trwające operacje Synchronizacji mogą zakończyć się przy użyciu istniejącego połączenia.

Jeśli żądanie zdalne zawiedzie, konto pozostanie wyłączone i można ponowić próbę cofnięcia uprawnień.

Usunięcie konta archiwum w **Mail Accounts** nie usuwa po cichu zdalnych poświadczeń. Powiązanie pozostaje wymienione w sekcji **Remaining app passwords** na stronie instancji, aby można było je wyraźnie cofnąć.

### Edycja instancji {#editing-an-instance}

Otwórz **Mailcow instances** i kliknij **Edit**. Pole klucza API jest zawsze puste — zapisany klucz nigdy nie jest wyświetlany. Zostaw je puste, aby zachować obecny klucz, lub wprowadź nowy, aby go zastąpić. Zmienione szczegóły połączenia są weryfikowane względem Mailcow przed zapisaniem.

### Usuwanie instancji {#deleting-an-instance}

Otwórz **Mailcow instances** i kliknij **Delete**.

Usunięcie instancji usuwa jej zaszyfrowany klucz API oraz powiązania ze skrzynkami. **Konta archiwum i ich zarchiwizowane maile zostają zachowane**, ale konta są wyłączone.

Usunięcie zostanie **odrzucone, jeśli jakiekolwiek zarządzane hasło aplikacji jest nadal aktywne** w Mailcow: usunięcie instancji spowodowałoby usunięcie klucza API wymaganego do jego cofnięcia. Najpierw cofnij wszystkie zarządzane hasła aplikacji na stronie skrzynki pocztowej.

## Dziennik audytu {#audit-logging}

Każda operacja administracyjna jest zapisywana w dzienniku dostępu wraz z użytkownikiem wykonującym czynność:

- łączenie instancji,
- aktualizacja instancji,
- usuwanie instancji,
- aktywacja skrzynek pocztowych (z liczbą skonfigurowanych i nieudanych),
- cofanie hasła aplikacji.

## Limit skrzynek pocztowych {#mailbox-limit}

Pojedyncze uruchomienie aktywacji może skonfigurować maksymalnie `Mailcow__MaxSelectedMailboxes` skrzynek pocztowych (domyślnie `1000`). Chroni to przed przypadkową masową konfiguracją oraz nadmiernym obciążeniem API Mailcow i IMAP. W przypadku większych instancji należy uruchomić operację kilka razy.

Każda skrzynka wymaga kilku cykli API oraz weryfikacji IMAP, a są one przetwarzane sekwencyjnie. Konfiguracja wielu setek skrzynek w jednym żądaniu może przekroczyć limity czasu oczekiwania (timeout) serwera proxy lub przeglądarki; podziel wybór na partie.

## Uwagi dotyczące bezpieczeństwa {#security-notes}

- Klucz API pozostaje po stronie serwera. Jest wysyłany jako nagłówek `X-API-Key`, jest usuwany z dzienników i nigdy nie jest zwracany do przeglądarki.
- Odpowiedzi haseł aplikacji Mailcow mogą zawierać hasła w tekście jawnym w polach `log`. Mail Archiver celowo nie loguje treści odpowiedzi i nie wyświetla błędów API w przeglądarce; błędy są raportowane jako lokalne komunikaty.
- Odpowiedź HTTP 200 nie jest traktowana jako sukces: zmiany wymagają `type: success` od Mailcow dla każdego wyniku.
- Uprawnienie IMAP nie jest z natury trybem tylko do odczytu. Skonfigurowane konta nie mają ustawionej polityki usuwania po stronie serwera, a administratorzy zachowują kontrolę nad ustawieniami konta. Ta funkcja archiwizuje wiadomości, a nie konfigurację lub Kopie zapasowe Mailcow.

### Nazwy hostów i sieci prywatne {#hostnames-and-private-networks}

Adres URL Mailcow i host IMAP są wprowadzane przez administratora i mogą wskazywać na prywatną przestrzeń adresową. Jest to zamierzone i wspierane: Mail Archiver jest często wdrażany obok serwera pocztowego, więc `mail.example.lan`, `192.168.10.20` lub `10.0.0.5` są normalnymi wartościami. **Żaden prywatny, pętlowy ani lokalny adres nie jest blokowany.**

Granicą zaufania jest rola administratora, a nie adres sieciowy. `Mail Accounts` jest dostępny dla samodzielnych menedżerów, którzy mogą już konfigurować dowolny serwer IMAP dla własnych kont; sekcja Mailcow jest ograniczona do administratorów, a administrator ma już nieograniczony dostęp do każdego konta archiwum w instalacji. Ograniczenie hosta IMAP nie zamknęłoby zatem granicy uprawnień, a przerwałoby zamierzony model wdrożenia on-premise.

Nadal obowiązują dwa ograniczenia:

- Adres URL Mailcow musi być czystym źródłem HTTPS. Ścieżki, ciągi zapytań, informacje o użytkowniku i przekierowania są odrzucane, więc klucz API nie może zostać przekazany do innego źródła.
- Host IMAP musi być nazwą hosta bez schematu lub portu, a konfiguracja zawsze łączy się przez TLS (`SslOnConnect`).

Jeśli musisz ograniczyć, do jakich hostów może łączyć się Mail Archiver, wymuś to na warstwie sieciowej — za pomocą reguł zapory sieciowej (egress firewall) lub polityki DNS dla kontenera — zamiast polegać na walidacji danych wejściowych w aplikacji.

## Rozwiązywanie problemów {#troubleshooting}

| Objaw | Przyczyna i rozwiązanie |
| --- | --- |
| *Encryption required* podczas łączenia lub aktywacji | `Security__CredentialEncryptionKey` nie jest ustawiony lub jest nieprawidłowy. Skonfiguruj klucz 32-bajtowy zakodowany w base64 i zrestartuj. |
| *HTTPS origin required* | Adres URL zawiera ścieżkę, ciąg zapytań lub osadzone poświadczenia. Wprowadź czyste źródło, np. `https://mail.example.com`. |
| *IMAP host invalid* | Host IMAP musi być nazwą hosta bez schematu lub portu. |
| *Instance already connected* | Istnieje inna instancja o tym samym adresie URL. Edytuj tę instancję. |
| *API request failed (401/403)* | Klucz API nie ma wymaganych uprawnień lub ograniczenie IP nie obejmuje hosta Mail Archiver. |
| *API request rejected* | Mailcow zwrócił wiersz `error`/`danger`/`warning`. Sprawdź dzienniki Mailcow; Mail Archiver nie przekazuje treści odpowiedzi. |
| *IMAP verification failed* | Błędny host lub port IMAP, niewiarygodny certyfikat lub funkcja haseł aplikacji jest wyłączona w Mailcow. |
| *IMAP account exists* | Niezależne konto IMAP już archiwizuje ten adres. Użyj tego konta zamiast tworzyć drugie archiwum. |
| *Revoke previous password* | Konto archiwum dla tej skrzynki zostało usunięte. Najpierw cofnij pozostałe hasło aplikacji, a następnie aktywuj ponownie. |
| Skrzynka pozostaje w stanie **Pending** | Nastąpiło przekroczenie czasu oczekiwania API Mailcow. Odśwież stronę i ponownie aktywuj skrzynkę; nie zostanie utworzone duplikujące się hasło aplikacji. |
| Usuwanie jest odrzucane | Zarządzane hasła aplikacji są nadal aktywne. Najpierj cofnij je na stronie skrzynki pocztowej. |

## Używane punkty końcowe API {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Zmiany w bazie danych {#database-changes}

Migracja dodaje tylko tabele `MailcowInstances` i `MailcowMailboxes` wraz z ich indeksami i kluczami obcymi. Istniejące tabele skrzynek pocztowych i zarchiwizowane dane zostają zachowane. Usunięcie konta archiwum ustawia `MailAccountId` powiązania na `NULL`, aby zdalne poświadczenia można było później cofnąć.
