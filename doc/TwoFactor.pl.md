# 🔐 Uwierzytelnianie dwuskładnikowe {#two-factor-authentication}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Uwierzytelnianie dwuskładnikowe (2FA) dodaje drugi krok do procesu logowania: po
podaniu hasła, Mail Archiver prosi o **6-cyfrowy kod** wygenerowany przez
aplikację uwiercielniającą w telefonie. Sam skradziony kod hasła nie jest już
wystarczający, aby uzyskać dostęp do Archiwum.

Mail Archiver implementuje 2FA przy użyciu standardu **TOTP** (Time-based One-Time Password, RFC
6238) — tego samego standardu, którego używają Google Authenticator, Microsoft
Authenticator, Authy, 1Password, Bitwarden i inne. Nie jest angażowana żadna zewnętrzna usługa;
tajny klucz nigdy nie opuszcza Twojej instancji.

**2FA jest opcjonalne dla każdego użytkownika.** Jest ono konfigurowane przez każdego
użytkownika dla jego własnego Konta, a nie globalnie przez administratora.

## 🛠️ Wymagania wstępne {#prerequisites}

- Aplikacja uwiercielniająca obsługująca TOTP w telefonie lub na komputerze
- Dostęp do własnego Konta (musisz być zalogowany)
- **Lokalne (hasłowe) Konto** — patrz nota o OIDC poniżej

> **⚠️ Niedostępne dla użytkowników OIDC.** Jeśli logujesz się przez zewnętrznego
> dostawcę tożsamości (Entra ID, Authelia, Keycloak, …), 2FA jest zarządzane przez tego
> dostawcę, a strona konfiguracji jest blokowana komunikatem: *"OIDC users cannot
> enable 2FA. Authentication is managed by your OIDC provider."* Zamiast tego
> włącz uwierzytelnianie wieloskładnikowe u swojego dostawcy tożsamości — patrz
> [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Włączanie uwierzytelniania dwuskładnikowego {#enabling-two-factor-authentication}

### Krok 1 — Otwórz stronę konfiguracji {#step-1-open-the-setup-page}

1. Zaloguj się do Mail Archiver
2. Otwórz menu swojego Konta i wybierz **Two-Factor Authentication**
3. Zostaniesz przeniesiony na stronę **Two-Factor Setup**

### Krok 2 — Zeskanuj kod QR {#step-2-scan-the-qr-code}

Na stronie wyświetlany jest kod QR. Otwórz aplikację uwiercielniającą, wybierz *Add account* →
*Scan QR code* i skieruj na niego aparat.

**Nie możesz zeskanować kodu?** Użyj zamiast tego opcji **Manual Entry**: strona wyświetla
tajny klucz jako tekst (`If you can't scan the QR code, enter this secret manually in
your authenticator app`). Wpisz go do swojej aplikacji jako konto oparte na czasie/TOTP.

> **ℹ️ Tajny klucz nie jest jeszcze zapisany.** Jest on przechowywany tylko przez czas trwania
> konfiguracji w Twojej sesji przeglądarki. 2FA staje się aktywne dopiero po pomyślnym
> zakończeniu kroku 3 — jeśli przerwiesz konfigurację, nic nie zmieni się na Twoim Koncie.

### Krok 3 — Potwierdź kodem {#step-3-confirm-with-a-code}

Wpisz 6-cyfrowy kod, który wyświetla teraz Twoja aplikacja, i kliknij **Enable Two-Factor
Authentication**.

Jeśli kod zostanie odrzucony, strona konfiguracji zostanie wyświetlona ponownie z nowym kodem QR oraz
komunikatem *"Invalid authentication code. Please try again."*. Sprawdź, czy zegar w Twoim
telefonie jest ustawiony automatycznie — TOTP zależy od dokładnego czasu.

### Krok 4 — Zapisz swoje kody zapasowe {#step-4-save-your-backup-codes}

Po pomyślnym zakończeniu zostaniesz przeniesiony na stronę **Backup Codes**. Wyświetla ona **10 kodów**,
z których każdy ma 16 znaków.

> **⚠️ To jedyny moment, w którym kody są wyświetlane.** Są one przechowywane
> w formie skrótu (hash) — ani Ty, ani administrator nie możecie ich wyświetlić ponownie w przyszłości.
> Użyj **Download Backup Codes**, aby zapisać je w pliku, lub skopiuj je do swojego
> menedżera haseł przed opuszczeniem strony.

Pobrany plik zawiera to przypomnienie:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Przechowuj te kody zapasowe
> w bezpiecznym miejscu. Możesz ich użyć do logowania, jeśli stracisz dostęp do swojej
> aplikacji uwiercielniającej. Każdy kod zapasowy może być użyty tylko raz. Po użyciu
> kodu zostanie on unieważniony.*
> *Ważne: Przechowuj te kody bezpiecznie i nie udostępniaj ich nikomu.*

## 🔑 Logowanie z 2FA {#logging-in-with-2fa}

Po wpisaniu hasła zostaniesz przeniesiony do **Two-Factor Verification** i zostaniesz poproszony o kod:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character
> backup code*

Oba rodzaje kodów działają w tym samym polu — Mail Archiver rozróżnia je po długości:

| Wprowadzony element | Długość | Zachowanie |
|---|---|---|
| Kod z aplikacji uwiercielniającej | 6 cyfr | Weryfikowany względem bieżącego okna czasowego |
| Kod zapasowy | 16 znaków | Weryfikowany względem zapisanych kodów, a następnie **unieważniany** |

Aby uwzględnić niewielkie różnice w czasie, akceptowane są również kody z bezpośrednio poprzedniego i
następnego kroku czasowego.

Opcja **"Remember me"** jest przenoszona z formularza logowania: jeśli ją zaznaczyłeś, pozostaniesz
zalogowany po ponownym uruchomieniu przeglądarki po przejściu kroku 2FA. Wybór jest utrzymywany w sesji po stronie serwera, dopóki krok 2FA nie zostanie zakończony, więc przetrwa on tylko wtedy, gdy wpiszesz kod w ciągu **60-minutowego** czasu bezczynności sesji. Szczegóły i limity (7-dniowy bezwzględny czas życia, brak wpływu na użytkowników OIDC) znajdują się w
[User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Używanie kodu zapasowego {#using-a-backup-code}

1. Wpisz jeden ze swoich zapisanych 16-znakowych kodów w tym samym polu
2. Kod zostaje zaakceptowany i **natychmiast unieważniony** — nie można go użyć ponownie
3. Udane logowania za pomocą kodu zapasowego są rejestrowane w dzienniku dostępu z
   notatką `(2FA Backup Code)`, dzięki czemu możesz później wykryć nietypowe użycie

### Ograniczanie liczby prób (Rate limiting) {#rate-limiting}

Aby spowolnić próby brute-force, weryfikacja 2FA jest ograniczona do **5 prób na 15 minut**, liczonych na adres IP i nazwę użytkownika. Po tym czasie zobaczysz:

> **Too Many Attempts** — *You have made too many 2FA verification attempts.
> Please wait and try again later.*

Odczekaj do końca okna czasowego i spróbuj ponownie. Limit dotyczy zarówno nieudanych, jak i udanych
prób w danym oknie.

## 🚫 Wyłączanie uwierzytelniania dwuskładnikowego {#disabling-two-factor-authentication}

1. Otwórz **Two-Factor Authentication** w menu swojego Konta
2. Strona poprosi o potwierdzenie: *"Are you sure you want to disable
   two-factor authentication? This will reduce the security of your account."*
3. Wpisz swoje **hasło** — ten krok wymaga aktualnego hasła, a nie kodu 2FA
4. Kliknij **Disable Two-Factor Authentication**

Wyłączenie usuwa tajny klucz i unieważnia wszystkie pozostałe kody zapasowe. Twoje
następne logowanie będzie wymagało tylko hasła.

## 🔄 Straciłeś aplikację uwiercielniającą? Dwa sposoby powrotu {#lost-your-authenticator-two-ways-back-in}

### Jeśli nadal masz kody zapasowe {#if-you-still-have-backup-codes}

Zaloguj się za pomocą kodu zapasowego, jak opisano powyżej, a następnie wyłącz i ponownie włącz 2FA
za pomocą nowego urządzenia. Pamiętaj, aby zapisać nowe kody zapasowe.

### Jeśli nie masz już żadnych kodów zapasowych {#if-you-have-no-backup-codes-left}

**Administrator** musi zresetować 2FA dla Twojego Konta:

1. Administrator otwiera **Users**
2. Edytuje odpowiedniego użytkownika
3. Wybiera **Reset Two-Factor Authentication** i potwierdza:
   *"Are you sure you want to reset two-factor authentication for this user?
   They will need to set it up again."*
4. Użytkownik może następnie zalogować się tylko za pomocą hasła i ponownie skonfigurować 2FA

Reset usuwa tajny klucz oraz kody zapasowe. Komunikat potwierdzający
*"Two-factor authentication has been successfully reset for user …"* pojawia się w
liście Users.

> **ℹ️ Zakres resetowania.** Reset dotyczy tylko 2FA. **Nie jest** to reset hasła — patrz [Emergency Account Recovery](EmergencyAccountRecovery.md), jeśli
> problemem jest hasło lub jeśli Konto jest całkowicie zablokowane.

## 👁️ Co widzą administratorzy {#what-administrators-can-see}

Przegląd **Users** zawiera kolumnę 2FA:

- *"Two-factor authentication is currently enabled for this user."* — 2FA jest aktywne
- W przeciwnym razie 2FA jest wyłączone dla tego użytkownika

Administratorzy mogą resetować 2FA (powyżej), ale **nie mogą** odczytać tajnego klucza ani
kodów zapasowych — oba są przechowywane w formie skrótu. Użytkownik, który straci urządzenie bez
kodów zapasowych, nie ma innej drogi powrotu poza resetem przez administratora.

## 🔍 Ścieżka audytu {#audit-trail}

Aktywność 2FA jest zapisywana w logach, a w przypadku udanych logowań również w dzienniku dostępu:

| Zdarzenie | Gdzie | Wpis |
|---|---|---|
| Logowanie przez aplikację uwiercielniającą | Access log | `Login` z notatką `IP: <address> (2FA)` |
| Logowanie przez kod zapasowy | Access log | `Login` z notatką `IP: <address> (2FA Backup Code)` |
| Nieudana próba 2FA | Application log | Warning z nazwą użytkownika, IP i zamaskowanym tokenem |
| Nieprawidłowy kod zapasowy | Application log | Warning z nazwą użytkownika i IP |

Wpis w dzienniku dostępu sprawia, że użycie kodu zapasowego jest widoczne po fakcie — co jest przydatne do
wykrycia, że kod został użyty przez kogoś, kto nie powinien go posiadać. Szczegóły dotyczące czytania tych wpisów znajdziesz w [Access Logging](Logs.md).

## ⚙️ Jak to działa pod maską {#how-it-works-under-the-hood}

Dla administratorów i recenzentów:

- **Standard**: TOTP (RFC 6238), 20-bajtowy losowy klucz, zakodowany w base32
- **Okno weryfikacji**: ±1 krok czasowy, aby tolerować dryft zegara
- **Kody zapasowe**: 10 kodów, każdy po 16 znaków, generowanych z kryptograficznego
  generatora liczb losowych (RNG), przechowywanych w formie **skrótu**, unieważnianych indywidualnie przy użyciu
- **Przechowywanie klucza**: w rekordzie użytkownika; nigdy nie jest zwracany do przeglądarki po konfiguracji
- **Obsługa sesji**: sesja jest regenerowana przed zapisaniem tożsamości 2FA, dzięki czemu ciasteczko sesyjne utworzone przed logowaniem nie może zostać przeniesione do
  uwierzytelnionej sesji
- **Konta OIDC**: zarządzanie 2FA jest zablokowane; dostawca jest podmiotem nadrzędnym
- **Klucze API**: z założenia omijają 2FA, ponieważ tokeny nieinteraktywne nie mogą odpowiedzieć na
  wyzwanie (challenge) — patrz uzasadnienie bezpieczeństwa w [REST API guide](API.md)

## ❓ Rozwiązywanie problemów {#troubleshooting}

| Objaw | Przyczyna | Rozwiązanie |
|---|---|---|
| *"Invalid authentication code"* | Zły czas w telefonie lub błędne konto w aplikacji | Włącz automatyczną Synchronizację czasu; sprawdź, czy aplikacja pokazuje wpis Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | Strona konfiguracji była otwarta zbyt długo, sesja wygasła | Rozpocznij konfigurację ponownie z menu Konta |
| *"Your session has expired. Please log in again."* | Zbyt dużo czasu upłynęło na stronie weryfikacji | Zaloguj się ponownie |
| *"Two-factor authentication is not enabled for this account."* | 2FA zostało zresetowane lub nigdy nie zostało ukończone | Skonfiguruj je ponownie lub poproś administratora |
| Kod zapasowy odrzucony | Kod został już użyty lub znak został błędnie odczytany | Kody zapasowe działają **tylko raz**; użyj innego lub zresetuj 2FA |
| *"Too Many Attempts"* | Więcej niż 5 prób weryfikacji w ciągu 15 minut | Odczekaj do końca okna czasowego |
| Brak strony konfiguracji w menu Konta | Logujesz się przez OIDC | Włącz MFA u swojego dostawcy tożsamości |
| Brak kolumny 2FA w liście Users | Nie jesteś administratorem | Poproś administratora |

## 📚 Powiązana dokumentacja {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — tworzenie kont, uprawnienia administratora
- [OIDC / SSO Authentication](OIDC_Implementation.md) — logowanie przez zewnętrznych dostawców
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — zablokowane Konta
- [Access Logging](Logs.md) — czytanie ścieżki audytu
- [REST API](API.md) — dlaczego klucze API omijają 2FA
