# ☁️ Przewodnik po rejestracji aplikacji Azure i polityce Przechowywania dla Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące konfiguracji rejestracji aplikacji Azure w celu pobierania poczty e-mail oraz konfiguracji polityk Przechowywania w aplikacji Mail Archiver.

## 📚 Spis treści {#table-of-contents}

1. [Przegląd](#overview)
2. [Rejestracja aplikacji Azure dla pobierania poczty e-mail](#azure-app-registration-for-email-retrieval)
   - [Wymagania wstępne](#prerequisites)
   - [Utworzenie rejestracji aplikacji](#create-app-registration)
   - [Ustawienie wymaganych uprawnień API](#set-required-api-permissions)
   - [Wygenerowanie Client Secret](#generate-client-secret)
3. [Konfiguracja Konta M365 w Mail Archiver](#configure-mail-archiver-m365-account)
4. [Konfiguracja polityki Przechowywania](#retention-policy-setup)
   - [Konfiguracja polityki Przechowywania](#configure-retention-policy)

## 🌐 Przegląd {#overview}

Aplikacja Mail Archiver obsługuje Konta Microsoft 365 (M365) przy użyciu przepływu poświadczeń klienta OAuth2 do pobierania poczty e-mail. Dodatkowo zapewnia funkcjonalność polityki Przechowywania, aby automatycznie usuwać e-maile z serwera pocztowego po określonym czasie, co pomaga zarządzać przestrzenią dyskową przy jednoczesnym zachowaniu kompletnego Archiwum.

## ☁️ Rejestracja aplikacji Azure dla pobierania poczty e-mail {#azure-app-registration-for-email-retrieval}

### 🛠️ Wymagania wstępne {#prerequisites}

- Dostęp administracyjny do Microsoft Entra ID (Azure AD)
- Tenant Microsoft 365 z licencjami Exchange Online

### 🚀 Utworzenie rejestracji aplikacji {#create-app-registration}

1. Przejdź do [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Zaloguj się przy użyciu swojego konta administratora
3. W lewym panelu nawigacyjnym wybierz **App registrations**
4. Kliknij **+ New registration** na górze strony App registrations
5. Wypełnij następujące dane:
   - **Name**: Wprowadź opisową nazwę (np. "Mail Archiver M365 Provider")
   - **Supported account types**: Wybierz **Accounts in this organizational directory only** (Single tenant)
     - Jest to wymagane dla przepływu poświadczeń klienta używanego przez Mail Archiver
   - **Redirect URI**: Zostaw to pole puste (nie jest potrzebne dla przepływu poświadczeń klienta)
6. Kliknij **Register**

**Ważne**: Zapisz następujące wartości ze strony **Overview**:
- **Application (client) ID** - Będziesz potrzebować tego jako `ClientId` w Mail Archiver
- **Directory (tenant) ID** - Będziesz potrzebować tego jako `TenantId` w Mail Archiver

### 🔐 Ustawienie wymaganych uprawnień API {#set-required-api-permissions}

1. Przejdź do **API permissions** w lewym menu
2. Kliknij **+ Add a permission**
3. Wybierz **Microsoft Graph**
4. Wybierz **Application permissions** (wymagane dla przepływu poświadczeń klienta Mail Archiver)
5. Dodaj następujące uprawnienia:
   - **Mail.Read** - Odczyt poczty we wszystkich Skrzynkach pocztowych
   - **Mail.ReadWrite** - Odczyt i zapis poczty we wszystkich Skrzynkach pocztowych (również dla funkcji przywracania oraz usuwania)
   - **User.Read.All** - Odczyt pełnych profili wszystkich użytkowników (wymagane do importu Skrzynki pocztowej w locie Microsoft 365; zapewnia również `assignedPlans` dla filtrowania licencji Exchange)

6. Kliknij **Add permissions**
7. **KRYTYCZNE**: Kliknij **Grant admin consent for [Your Organization]**
8. Potwierdź, klikając **Yes**

**Uwaga**: Uprawnienia aplikacji (Application permissions) są wymagane, ponieważ Mail Archiver używa przepływu poświadczeń klienta do dostępu do Skrynek pocztowych bez interakcji użytkownika.

### 🔑 Wygenerowanie Client Secret {#generate-client-secret}

1. Przejdź do **Certificates & secrets** w lewym menu
2. W sekcji **Client secrets** kliknij **+ New client secret**
3. Podaj opis (np. "Mail Archer Secret")
4. Wybierz okres wygaśnięcia
5. Kliknij **Add**
6. **Ważne**: Natychast skopiuj **Value** i przechowuj je w bezpieczny sposób. Ten sekret nie zostanie wyświetlony ponownie.

## 📧 Konfiguracja Konta M365 w Mail Archiver {#configure-mail-archiver-m365-account}

Po zakończeniu rejestracji aplikacji należy skonfigurować konto poczty M365 w Mail Archiver, używając następujących wartości:

### 📋 Wymagane wartości z rejestracji aplikacji: {#required-values-from-app-registration}

1. **Client ID**: Skopiuj **Application (client) ID** ze strony **Overview** rejestracji aplikacji
2. **Client Secret**: Skopiuj **Value**, które zapisałeś podczas tworzenia Client Secret
3. **Tenant ID**: Skopiuj **Directory (tenant) ID** ze strony **Overview** rejestracji aplikacji

### 🚀 Tworzenie Konta M365 w Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Zaloguj się do swojej aplikacji Mail Archiver
2. Przejdź do **Mail Accounts** > **Create**
3. Wypełnij następujące pola:
   - **Name**: Opisowa nazwa dla konta (np. "Sales Team M365")
   - **Email Address**: Docelowy adres e-mail Skrzynki pocztowej do archiwizacji
   - **Provider**: Wybierz **M365**
   - **Client ID**: Wprowadź Application (client) ID ze swojej rejestracji aplikacji
   - **Client Secret**: Wprowadź zapisaną wartość Client Secret
   - **Tenant ID**: Wprowadź Directory (tenant) ID ze swojej rejestracji aplikacji

4. Kliknij **Create**

### ⚠️ Ważne uwagi: {#important-notes}

- Pole **Email Address** musi zawierać właściwy adres e-mail Skrzynki pocztowej, którą chcesz archiwizować, podczas tworzenia pojedynczego konta M365.
- Rejestracja aplikacji musi posiadać uprawnienia do dostępu do określonej Skrzynki pocztowej.
- Aby utworzyć konta dla wszystkich lub wybranych Skrynek pocztowych w tenant jednocześnie, skorzystaj z przewodnika [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Konfiguracja polityki Przechowywania {#retention-policy-setup}

Aplikacja Mail Archiver zapewnia funkcjonalność polityki Przechowywania, aby automatycznie usuwać e-maile z serwera IMAP po ich pomyślnej archiwizacji, co pomaga zarządzać przestrzenią dyskową na serwerze IMAP przy jednoczesnym zachowaniu kompletnego Archiwum.

### 🛠️ Konfiguracja polityki Przechowywania {#configure-retention-policy}

1. Przejdź do **Mail Accounts** w aplikacji Mail Archiver
2. Utwórz nowe konto M365 lub edytuj istniejące
3. W formularzu konfiguracji konta znajdź pole **Delete After Days**
4. Wprowadź liczbę dni, po których archiwizowane e-maile powinny zostać usunięte z serwera:
   - Na przykład wpisz `30`, aby usunąć e-maile po 30 dniach
   - Zostaw puste, aby wyłączyć automatyczne usuwanie
5. Zapisz konfigurację konta


## ℹ️ Informacje dotyczące Migrowanych Skrynek pocztowych {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Foldery niewidoczne po migracji {#folders-not-visible-after-migration}

Jeśli przeprowadziłeś migrację Skrzynki pocztowej z innego dostawcy poczty do Microsoft 365, niektóre foldery mogą nie pojawiać się na liście folderów.

#### 🔍 Tło problemu {#background}

Mail Archiver pobiera informacje o folderach ze Skrynek pocztowych M365 za pośrednictem Microsoft Graph API. Aplikacja poprawnie żąda wszystkich dostępnych folderów (w tym folderów ukrytych) i przechodzi przez pełną hierarchię folderów.

Jednak Microsoft Graph API zwraca tylko foldery, których `PR_CONTAINER_CLASS` jest ustawione na `IPF.Note`. Podczas migracji Skrynek pocztowych z innych systemów e-mail, foldery mogą otrzymać inną klasę kontenera, taką jak `IPF.Imap`. Foldery te nie są udostępniane poprzez punkty końcowe listy folderów w Graph API.

To zachowanie dotyczy wszystkich aplikacji używających tego API do pobierania folderów.

#### 🔧 Opcje obejścia problemu {#workaround-options}

**Opcja 1: Użycie narzędzia MFCMAPI (Zalecane)**

1. Pobierz [MFCMAPI](https://github.com/Microsoft/mfcmapi/) z oficjalnego repozytorium Microsoft
2. Uruchom MFCMAPI i połącz się ze swoją Skrzynką pocztową
3. Przejdź do każdego brakującego folderu
4. Zmień właściwość `PR_CONTAINER_CLASS` z `IPF.Imap` na `IPF.Note`
5. Uruchom ponownie Outlook i przeładuj foldery w Mail Archiver

**Opcja 2: Ponowne utworzenie folderów w Outlook**

1. Utwórz nowe foldery bezpośrednio w Outlook (będą one miały poprawną klasę `IPF.Note`)
2. Przenieś wszystkie e-maile z migrowanych folderów do nowych folderów
3. Usuń stare, migrowane foldery, jeśli to konieczne

**Opcja 3: Skrypt PowerShell (Dla zmian masowych)**

Dla Skrynek pocztowych z wieloma migrowanymi folderami można użyć PowerShell z modułem Exchange Online, aby zmienić klasę kontenera dla wielu folderów jednocześnie. Skontaktuj się z administratorem Exchange, aby uzyskać pomoc w tym podejściu.


---

**Uwaga**: Ten przewodnik jest aktualny na rok 2025. Microsoft regularnie aktualizuje swoje usługi, więc niektóre elementy interfejsu użytkownika mogą się różnić. Zawsze sprawdzaj najnowszą dokumentację Microsoft, aby uzyskać najbardziej aktualne informacje.
