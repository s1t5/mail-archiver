# ☁️ Konfiguracja osobistego konta Microsoft (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia, jak wykonać Archiwum wiadomości e-mail z **osobistego konta Microsoft** (Outlook.com, Hotmail, live.com lub Microsoft 365 Family) w Mail Archiver.

W przeciwieństwie do organizacyjnych kont Microsoft 365 (które używają przepływu client-credentials — zobacz [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), konta osobiste używają **OAuth2 z Device Code Flow (RFC 8628)**. Microsoft wycofał uwierzytelnianie podstawowe (Basic Auth) oraz hasła aplikacji dla kont osobistych, dlatego wymagane jest OAuth2.

Mail Archiver jest dostarczany z **wstępnie zarejestrowanym współdzielonym Client ID**, dzięki czemu większość użytkowników **nie musi** tworzyć własnej rejestracji Azure App. Zaaweni użytkownicy nadal mogą zarejestrować własną aplikację (patrz Opcja B poniżej).

## 📚 Spis treści {#table-of-contents}

1. [Opcja A: Standardowa konfiguracja (Zalecana)](#option-a-standard-setup-recommended)
2. [Opcja B: Własna rejestracja aplikacji (Zaawani użytkownicy)](#option-b-custom-app-registration-power-users)
3. [Odświeżanie i unieważnianie tokenów](#token-refresh--revocation)
4. [Ważne uwagi](#important-notes)
5. [Rozwiązywanie problemów](#troubleshooting)

---

## 🚀 Opcja A: Standardowa konfiguracja (Zalecana) {#option-a-standard-setup-recommended}

Użyj tej opcji, jeśli Twoja instancja Mail Archiver ma skonfigurowany domyślny Client ID (domyślny dla oficjalnych obrazów Docker). Nie jest wymagany dostęp do Azure Portal.

### 🛠️ Wymagania wstępne {#prerequisites}

- Osobiste konto Microsoft (Outlook.com, live.com lub M365 Family)
- Urządzenie z przeglądarką (smartfon, laptop lub tablet) w celu ukończenia autoryzacji

### 📝 Kroki {#steps}

1. Zaloguj się do swojej aplikacji Mail Archiver.
2. Przejdź do **Mail Accounts** > **Create**.
3. Wypełnij następujące pola:
   - **Name**: Opisowa nazwa konta (np. "Personal Outlook")
   - **Email Address**: Twój osobisty adres e-mail Microsoft (np. `you@outlook.com`)
   - **Provider**: Wybierz **Microsoft Personal**
4. Pola Client ID ani Client Secret nie są wyświetlane — Mail Archiver automatycznie używa wbudowanej domyślnej rejestracji.
5. Kliknij **Create**.
6. Strona autoryzacji za pomocą kodu urządzenia otworzy się automatycznie. Wyświetlony zostanie krótki kod (np. `ABCD-EFGH`) wraz z linkiem do `https://microsoft.com/devicelogin` (lub wyświetlonym adresem URL weryfikacji).
7. Na **dowolnym urządzeniu** z przeglądarką otwórz ten adres URL i zaloguj się na swoje osobiste konto Microsoft.
8. Wprowadź kod z kroku 6.
9. Pojawi się ekran zgody, pokazujący **"Mail-Archiver"** proszący o dostęp IMAP. Przejrzyj i zatwierdź.
10. Strona Mail Archiver automatycznie wykryje sukces i przekieruje do widoku edycji konta.
11. Gotowe — Synchronizacja rozpoczyna się automatycznie zgodnie z ustawieniami konta.

> ⚠️ **Ostrzeżenie o nieweryfikowanym wydawcy**: Ekran zgody może wyświetlić ostrzeżenie, że "Mail-Archiver" jest nieweryfikowanym wydawcą. Jest to zjawisko oczekiwane — weryfikacja wydawcy wymaga tożsamości biznesowej (konta Microsoft Partner Network) i nie jest wymagana do działania przepływu. Aplikacja działa normalnie bez niej.

---

## 🔧 Opcja B: Własna rejestracja aplikacji (Zaawani użytkownicy) {#option-b-custom-app-registration-power-users}

Użyj tej opcji, jeśli nie chcesz używać współdzielonego domyślnego Client ID (np. ze względów zgodności lub jeśli chcesz mieć pełną kontrolę nad rejestracją aplikacji).

### 🛠️ Wymagania wstępne {#prerequisites}

- Konto Microsoft (osobiste lub służbowe/szkolne), które ma dostęp do Azure Portal
- Podstawowa znajomość portalu Azure/Entra

### 🚀 Utwórz rejestrację aplikacji {#create-app-registration}

1. Przejdź do [Microsoft Entra Admin Center](https://entra.microsoft.com) lub [Azure Portal](https://portal.azure.com).
2. Zaloguj się na swoje konto Microsoft.
3. W lewym panelu nawigacyjnym wybierz **App registrations** (w sekcji Identity > Applications).
4. Kliknij **+ New registration**.
5. Wypełnij następujące szczegóły:
   - **Name**: Wprowadź opisową nazwę (np. "Mail Archiver MSA")
   - **Supported account types**: Wybierz **Accounts in any organizational directory and personal Microsoft accounts**
     - Jest to opcja multi-tenant, która obsługuje zarówno konta osobiste, jak i organizacyjne.
   - **Redirect URI**: Zostaw to pole puste (nie jest potrzebne dla Device Code Flow).
6. Kliknij **Register**.
7. Zanotuj **Application (client) ID** ze strony **Overview**.

### 🔐 Włącz przepływy Public Client {#enable-public-client-flows}

1. W rejestracji swojej aplikacji przejdź do sekcji **Authentication** w lewym menu.
2. Na dole znajdź **Allow public client flows** i ustaw na **Yes**.
3. Kliknij **Save**.

> ℹ️ Nie trzeba ręcznie konfigurować Client Secret, Redirect URI ani uprawnień API — Device Code Flow używa uprawnień delegowanych, które są żądane w czasie rzeczywistym za pomocą zakresu (scope) `IMAP.AccessAsUser.All`.

### 📧 Skonfiguruj Mail Archiver {#configure-mail-archiver}

Nadpisz globalną wartość domyślną (wpływa na wszystkie konta MSA)

Ustaw Client ID w swojej zmiennej środowiskowej:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Odświeżanie i unieważnianie tokenów {#token-refresh-revocation}

### Automatyczne odświeżanie tokenów {#automatic-token-refresh}

Po początkowej autoryzacji Mail Archiver otrzymuje:
- **access token** (krótkotrwały, ok. 1 godziny)
- **refresh token** (długotrwały, używany do automatycznego uzyskiwania nowych tokenów dostępu)

Mail Archiver automatycznie odświeża token dostępu przed każdym uruchomieniem synchronizacji. Nie musisz ponownie autoryzować konta, chyba że token odświeżania wygaśnie lub zostanie unieważniony.

### Unieważnianie dostępu {#revoking-access}

Aby unieważnić dostęp Mail Archiver do swojego osobistego konta Microsoft:

1. Odwiedź [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Zaloguj się na swoje osobiste konto Microsoft.
3. Znajdź **"Mail-Archiver"** (lub nazwę swojej własnej aplikacji) na liście.
4. Kliknij **Remove**, aby unieważnić dostęp.

Po unieważnieniu Mail Archiver nie będzie mógł uzyskać dostępu do konta, dopóki nie dokonasz ponownej autoryzacji.

---

## ⚠️ Ważne uwagi {#important-notes}
- **Współdzielony Client ID**: Podczas korzystania z domyślnego Client ID, wszyscy użytkownicy Mail Archiver na wszystkich instancjach współdzielą tę samą rejestrację Azure App. Administrator Mail Archiver jest odpowiedzialny za utrzymanie rejestracji w działaniu. Jeśli Microsoft wyłączy współdzieloną rejestrację, wszyscy użytkownicy będą musieli przełączyć się na własną rejestrację (Opcja B), dopóki nowa wersja nie zostanie wydana z zaktualizowanym Client ID.


- **Konta organizacyjne**: Dostawca Microsoft Personal używa punktu końcowego autoryzacji `/common` i obsługuje zarówno konta osobiste, jak i organizacyjne (służbowe/szkolne). Dla kont organizacyjnych wymagających dostępu client-credentials (tylko aplikacja, bez logowania użytkownika), użyj zamiast tego dostawcy **M365**.

---

## 🔍 Rozwiązywanie problemów {#troubleshooting}

### Foldery są widoczne na outlook.com, ale brakuje ich w Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Objaw**: Niektóre foldery istnieją w Skrzynce pocztowej (są widoczne na outlook.com lub w Outlook), ale nigdy nie pojawiają się w Mail Archiver — ani na liście folderów na stronie ustawień konta, ani podczas synchronizacji. Nie jest logowany żaden błąd.

**Przyczyna (po stronie serwera)**: Foldery utworzone przez **zewnętrzne klienty za pomocą EWS** — co konfiguruje konta outlook.com jako konta Exchange zamiast IMAP — mogą posiadać błędną właściwość `PR_CONTAINER_CLASS` (np. `IPF.Imap` zamiast `IPF.Note`). Outlook.com następnie **całkowicie filtruje te foldery z odpowiedzi IMAP `LIST`**, mimo że są one w pełni widoczne w interfejsie webowym. Jest to zachowanie po stronie Microsoft, a nie błąd Mail Archiver — to samo zjawisko jest dokumentowane dla Microsoft Graph API, gdzie foldery z klasą kontenera inną niż `IPF.Note` są niewidoczne w listach `mailFolders`, dopóki właściwość nie zostanie poprawiona:

Mail Archiver odpytuje listę folderów z trzech źródeł (rekurencyjne `LIST`, przechodzenie przez dzieci każdego folderu poziom po poziomie oraz `LSUB`) i łączy wyniki. Gdy serwer nie zgłasza folderu przez żadne z nich, żaden klient IMAP nie ma możliwości go zobaczyć.

**Rozwiązanie tymczasowe**: "Dotknij" dotknięte foldery raz poprzez interfejs webowy outlook.com, aby Microsoft skorygował klasę kontenera:

1. Otwórz <https://outlook.com> i zlokalizuj brakujący folder.
2. Zmień jego nazwę (na dowolną tymczasową), a następnie przywróć poprzednią nazwę — lub utwórz nowy folder i przenieś do niego wiadomości.
3. Powtórz to dla każdego dotkniętego folderu.
4. Uruchom **Full Resync** konta ze strony Account Details w Mail Archiver. Pełna synchronizacja jest wymagana, ponieważ synchronizacje przyrostowe pobierają tylko wiadomości nowsze niż data ostatniej synchronizacji konta — w przeciwnym razie wiadomości w folderach, które nigdy nie zostały wykryte, pozostałyby niezaarchiwizowane.


*Niniejszy przewodnik jest aktualny na rok 2026. Microsoft regularnie aktualizuje swoje usługi i interfejs użytkownika, więc niektóre kroki mogą się różnić. Więcej szczegółów znajdziesz w [dokumentacji Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/).*
