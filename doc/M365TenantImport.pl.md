# Import skrzynek pocztowych w dzierżawcy Microsoft 365 {#microsoft-365-tenant-mailbox-import}

[← Powrót do indeksu dokumentacji](index.md)

## Przegląd {#overview}

Mail Archiver może tworzyć konta pocztowe Microsoft 365 dla więcej niż jednej skrzynki pocztowej w tym samym dzierżawcy za pomocą pojedynczego formularza tworzenia. Jest to przydatne podczas wdrażania klienta lub organizacji, w której należy zarchiwizować wszystkie lub wybrany podzbiór skrzynek pocztowych Microsoft 365.

Import dzierżawcy wykorzystuje poświadczenia aplikacji Microsoft Graph skonfigurowane w formularzu i tworzy jedno konto pocztowe Mail Archiver na każdą zaimportowaną skrzynkę pocztową.

## Wymagania wstępne {#prerequisites}

Przed użyciem importu dzierżawcy należy ukończyć konfigurację rejestracji aplikacji Microsoft 365 opisaną w [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

Wymagane są następujące wartości z Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

Rejestracja aplikacji musi posiadać **uprawnienia aplikacji** Microsoft Graph, które pozwalają Mail Archiver na odczyt skrzynek pocztowych w dzierżawcy. Oprócz uprawnień wymaganych do standardowego archiwizowania kont M365, do importu dzierżawcy wymagane jest następujące uprawnienie:

- **User.Read.All** – wymagane do wymienienia użytkowników dzierżawcy i ich adresów skrzynek pocztowych.

Import dzierżawcy jest wybierany jako dostawca **Microsoft 365 (tenant)** w formularzu tworzenia.

### Filtrowanie {#filtering}

Import dzierżawcy oraz Zarządzanie dzierżawą (Tenant Management) automatycznie wykluczają:
- **Konta gości** (`userType == "Guest"`) — zewnętrznych zaproszonych użytkowników bez własnych skrzynek pocztowych w dzierżawcy.
- **Użytkowników bez licencji Exchange Online** — wymieniani są tylko użytkownicy z aktywnym planem usługi Exchange w swoim `assignedPlans`. Pozwala to uniknąć importowania kont, których nie można archiwizować.

## Tryby importu {#import-modes}

Import dzierżawcy obsługuje dwa tryby:

### Importuj wszystkie wymienione skrzynki pocztowe {#import-all-listed-mailboxes}

Użyj tego trybu, gdy każda skrzynka pocztowa zwrócona przez Microsoft Graph powinna zostać utworzona jako konto Mail Archiver.

- Mail Archiver ładuje listę skrzynek pocztowych dzierżawcy z Microsoft Graph.
- Już istniejące konta pocztowe M365 są pomijane.
- Jeśli opcja **Skip disabled mailboxes** jest włączona, użytkownicy wyłączeni nie są uwzględniani na liście skrzynek pocztowych.

### Importuj tylko wybrane skrzynki pocztowe {#import-selected-mailboxes-only}

Użyj tego trybu, gdy należy zarchiwizować tylko określone skrzynki pocztowe w dzierżawcy.

- Najpierw załaduj listę skrzynek pocztowych dzierżawcy.
- Wyłącz opcję **Import all listed mailboxes**.
- Wybierz skrzynki pocztowe, które mają zostać zaimportowane.
- Prześlij formularz, aby utworzyć konta tylko dla wybranych skrzynek pocztowych.

## Instrukcja krok po kroku {#step-by-step-usage}

1. Zaloguj się do Mail Archiver jako użytkownik, który może tworzyć konta pocztowe.
2. Otwórz **Mail Accounts** > **Create**.
3. Wybierz **Microsoft 365 (tenant)** jako dostawcę.
4. Wprowadź poświadczenia Microsoft 365:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Wprowadź prefiks **Account name**, który ma być używany dla generowanych kont.
6. Wybierz, czy wyłączone skrzynki pocztowe mają być pomijane.
7. Kliknij **Load mailboxes**, aby wyświetlić listę skrzynek pocztowych dzierżawcy.
8. Wybierz pożądane zachowanie importu:
   - Pozostaw włączone **Import all listed mailboxes**, aby zaimportować wszystkie wymienione skrzynki pocztowe.
   - Wyłącz tę opcję i wybierz poszczególne skrzynki pocztowe, aby zaimportować tylko konkretne skrzynki.
9. Kliknij **Save**.

Po utworzeniu Mail Archiver przekieruje z powrotem do listy kont pocztowych i pokaże, ile kont dzierżawcy zostało zaimportowanych, a ile istniejących kont zostało pominiętych.

## Nazewnictwo kont {#account-naming}

Podczas importu dzierżawcy Mail Archiver używa wartości wprowadzonej w polu **Account name** jako prefiksu dla każdego utworzonego konta skrzynki pocztowej.

Przykład:

- Wprowadzona nazwa konta: `Test GmbH`
- Skrzynka pocztowa dzierżawcy: `mailbox@example.com`
- Utworzona nazwa konta: `Test GmbH - <mailbox@example.com>`

Ułatwia to identyfikację zaimportowanych kont należących do tego samego dzierżawcy lub klienta.

## Istniejące konta {#existing-accounts}

Jeśli skrzynka pocztowa istnieje już jako konto Microsoft 365 w Mail Archiver, import dzierżawcy pomija ją zamiast tworzyć duplikat. Wykrywanie istniejących kont opiera się na adresie e-mail skrzynki pocztowej oraz dostawcy M365.

Lista skrzynek pocztowych może również pokazywać, czy skrzynka pocztowa już istnieje, przed przesłaniem importu.

## Wyłączone skrzynki pocztowe {#disabled-mailboxes}

Pole wyboru **Skip disabled mailboxes** kontroluje, czy wyłączeni użytkownicy dzierżawcy mają być wykluczeni z listy skrzynek pocztowych.

- Włączone: wymieniani i importowani są tylko aktywni użytkownicy dzierżawy.
- Wyłączone: wyłączeni użytkownicy dzierżawy mogą pojawić się na liście i mogą zostać zaimportowani, jeśli zostaną wybrani lub jeśli wszystkie wymienione skrzynki pocztowe zostaną zaimportowane.

## Zarządzanie dzierżawą (Tenant Management) {#tenant-management}

Oprócz importu dzierżawcy z formularza tworzenia, Mail Archiver udostępnia dedykowaną stronę **Tenant Management** dla administratorów. Jest ona dostępna z indeksu Mail Accounts za pomocą przycisku **Tenant Management** (ikona budynku) obok dowolnego konta Microsoft 365.

Zarządzanie dzierżawą jest przydatne do dodawania skrzynek pocztowych, które nie zostały zaimportowane początkowo — na przykład, gdy po wstępnym wdrażaniu w dzierżawcy tworzone są nowe skrzynki pocztowe lub gdy konta zostały pierwotnie pominięte.

### Czym różni się od importu dzierżawy z formularza tworzenia {#how-it-differs-from-the-create-form-tenant-import}

- Poświadczenia Microsoft 365 (Client ID, Client Secret, Tenant ID) **nie są wprowadzane ponownie**. Są one pobierane z konta źródłowego, przy którym kliknięto przycisk.
- Lista skrzynek pocztowych dzierżawcy jest ładowana automatycznie po otwarciu strony (brak przycisku "Load mailboxes").
- Już zaimportowane skrzynki pocztowe są wyświetlane z etykietą "already exists" i nie można ich ponownie wybrać.
- Tylko administratorzy mają dostęp do Tenant Management.

### Instrukcja krok po kroku {#step-by-step-usage}

1. Zaloguj się do Mail Archiver jako administrator.
2. Otwórz **Mail Accounts**.
3. Znajdź konto Microsoft 365, którego dzierżawą chcesz zarządzać, i kliknij przycisk **Tenant Management** obok niego.
4. Strona ładuje się i wyświetla wszystkie skrzynki pocztowe zwrócone przez Microsoft Graph dla dzierżawy konta źródłowego.
   - Skrzynki pocztowe, które istnieją już jako konta M365 w Mail Archiver, są oznaczone etykietą "already exists" i nie można ich wybrać.
   - Wyłączeni użytkownicy dzierżawy nie są wymieniani; Tenant Management zawsze pomija wyłączone skrzynki pocztowe.
5. Wybierz skrzynki pocztowe, które chcesz dodać.
6. Wprowadź prefiks **Account name**, który ma być używany dla nowych kont (schemat nazewnictwa to `{prefix} - <{email}>`, identyczny jak przy imporcie dzierżawy z formularza tworzenia).
7. Opcjonalnie skonfiguruj **Delete After Days** oraz **Local Retention Days** dla nowych kont.
8. Kliknij **Add selected mailboxes**.

Po utworzeniu Mail Archiver przekieruje do listy kont pocztowych i pokaże, ile skrzynek pocztowych zostało dodanych.

### Wyszukiwanie skrzynek pocztowych {#searching-mailboxes}

Dla dużych dzierżaw z kilkoma tysiącami skrzynek pocztowych, pole wyszukiwania nad listą skrzynek pozwala filtrować po nazwie wyświetlanej lub adresie e-mail. Filtr nie rozróżnia wielkości liter i działa natychmiast podczas wpisywania. Opcje **Select all** i **Clear selection** działają tylko na obecnie widocznych (przefiltrowanych) skrzynkach pocztowych, dzięki czemu filtrowanie jest bezpiecznym sposobem na wybranie konkretnego podzbioru bez wpływu na ukryte wpisy.

### Zmiana nazw istniejących kont {#renaming-existing-accounts}

Pole wyboru **Rename existing accounts** stosuje schemat nazewnictwa `{prefix} - <{email}>` do wszystkich kont M365, które współdzielą tę samą rejestrację aplikacji (Client ID i Tenant ID) co konto źródłowe. Jest to przydatne do normalizacji nazw kont po początkowym imporcie, w którym nazwy były niespójne.

Po zaznaczeniu, zapisanie formularza zmieni nazwę każdego pasującego konta — w tym konta źródłowego — niezależnie od tego, czy wybrano nowe skrzynki pocztowe do importu. Możesz użyć tej opcji samodzielnie (bez wybierania żadnych nowych skrzynek pocztowych), aby znormalizować nazwy bez dodawania kont.

### Limit skrzynek pocztowych {#mailbox-limit}

Pojedyncza operacja Tenant Management może dodać maksymalnie `TenantManagement__MaxSelectedMailboxes` skrzynek pocztowych naraz (domyślnie `1000`). Chroni to przed przypadkowymi masowymi importami i nadmiernym obciążeniem Microsoft Graph / bazy danych. Jeśli musisz zaimportować więcej skrzynek pocztowych, uruchom operację wielokrotnie. Aby dowiedzieć się, jak dostosować ten limit, sprawdź [Setup Guide](Setup.md#-tenant-management-settings).

### Wymagane uprawnienia {#permissions-required}

Tenant Management korzysta z tych samych uprawnień aplikacji Microsoft Graph, co import dzierżawy z formularza tworzenia. Rejestracja aplikacji konta źródłowego musi posiadać:

- Uprawnienia wymagane do standardowego archiwizowania kont M365 **oraz**
- **User.Read.All** — wymagane do wymienienia użytkowników dzierżawy i ich adresów skrzynek pocztowych.

Aby dowiedzieć się więcej o wstępnej konfiguracji, sprawdź [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

### Dziennik audytu {#audit-logging}

Każde wyświetlenie strony Tenant Management oraz każda operacja dodawania jest rejestrowana w dzienniku dostępu, w tym liczba wymienionych lub dodanych skrzynek pocztowych oraz użyte konto źródłowe.
