# 👥 Zarządzanie użytkownikami i uprawnienia do Skrzynki pocztowej {#user-management-and-mailbox-permissions}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące tworzenia nowych kont użytkowników oraz przydzielania uprawnień do Skrzynki pocztowej w aplikacji Mail Archiver. Obejmuje również procedurę zmiany własnego hasła oraz wymuszoną zmianę hasła na nowo zainstalowanej instancji.


## 🛠️ Wymagania wstępne {#prerequisites}

- Dostęp administracyjny do aplikacji Mail Archiver
- Istniejące konta e-mail skonfigurowane w systemie

## 🧑‍💻 Tworzenie nowego konta użytkownika {#creating-a-new-user-account}

1. Zaloguj się do aplikacji Mail Archiver przy użyciu konta z uprawnieniami administracyjnymi
2. Przejdź do sekcji "Users" w menu głównym
3. Kliknij przycisk "Create New User"
4. Wypełnij wymagane informacje użytkownika:
   - **Username**: Wprowadź unikalną nazwę użytkownika dla nowego użytkownika
   - **Password**: Wprowadź bezpieczne hasło dla użytkownika
   - **Email**: Adres e-mail użytkownika
   - **Admin**: Zaznacz to pole, jeśli użytkownik ma mieć uprawnienia administracyjne
   - **Self Manager**: Zaznacz to pole, jeśli użytkownik ma mieć możliwość zarządzania własnymi kontami pocztowymi (edytowanie istniejących oraz dodawanie nowych, a także usuwanie kont)
   - **active**: Powinno być zaznaczone, aby umożliwić logowanie użytkownikowi
5. Kliknij "Create User", aby zapisać nowe konto użytkownika

## 🔐 Przydzielanie uprawnień do Skrzynki pocztowej użytkownikom {#assigning-mailbox-permissions-to-users}

1. Po utworzeniu użytkownika lub podczas edycji istniejącego użytkownika, przejdź do sekcji "Users" w menu głównym
2. Znajdź użytkownika na liście, któremu chcesz przydzielić konta pocztowe, i kliknij przycisk "Assign" dla tego użytkownika
3. Na stronie "Assign Mail Accounts" zobaczysz wszystkie dostępne konta pocztowe oraz pola wyboru wskazujące, czy są one przypisane do użytkownika
5. Aby przydzielić nowe konto e-mail, zaznacz odpowiednie pole dla konkretnego konta
6. Aby usunąć konto e-mail użytkownikowi, odznacz pole
7. Kliknij "Save Assignments", aby zastosować zmiany

## 👤 Uprawnienia konta użytkownika {#user-account-permissions}

### Użytkownicy Admin {#admin-users}
Użytkownicy Admin mają pełny dostęp do aplikacji i mogą:
- Zarządzać wszystkimi kontami użytkowników i ich uprawnieniami
- Uzyskiwać dostęp do wszystkich kont e-mail w systemie
- Przeglądać wszystkie dzienniki dostępu
- Wykonywać wszystkie zadania administracyjne

### Użytkownicy Self Manager {#self-manager-users}
Użytkownicy Self manager mogą:
- Dodawać nowe konta e-mail do własnego konta
- Edytować istniejące przypisane im konta e-mail
- Usuwać przypisane im konta e-mail
- Uzyskiwać dostęp i zarządzać e-mailami z przypisanych kont (w tym usuwanie)
- Przeglądać własne dzienniki dostępu

### Użytkownicy Standard {#standard-users}
Użytkownicy Standard mają ograniczony dostęp i mogą:
- Przeglądać zarchiwizowane e-maile z przypisanych kont e-mail
- Przeszukiwać przypisane im konta e-mail
- Eksportować e-maile z przypisanych kont
- Przywracać e-maile z przypisanych kont
- Uzyskiwać dostęp do Załączników e-mail z przypisanych kont

## 🔑 Zmiana własnego hasła {#changing-your-own-password}

Każdy użytkownik z lokalnym hasłem może je zmienić bez administratora:

1. Kliknij swoją **nazwę użytkownika** w prawym górnym rogu, aby otworzyć menu użytkownika.
2. Wybierz **Change Password**.
3. Wprowadź swoje **obecne hasło** oraz **nowe hasło** dwukrotnie, a następnie zatwierdź.

Nowe hasło musi spełniać poniższe wymagania i musi być **inne niż obecne**.

> ℹ️ **Użytkownicy OIDC nie mogą zmieniać tutaj swojego hasła.** Strona odpowiada komunikatem:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Zmień hasło u swojego dostawcy tożsamości —
> zobacz [OIDC / SSO](OIDC_Implementation.md).

### Wymagania dotyczące hasła {#password-requirements}

- co najmniej **10 znaków**
- co najmniej jedna wielka litera (**uppercase**)
- co najmniej jedna mała litera (**lowercase**)
- co najmniej jedna cyfra (**number**)
- co najmniej jeden znak specjalny (**special character**)

Formularz wyświetla te wymagania jako listę kontrolną na żywo podczas wpisywania. Te same zasady obowiązują, gdy administrator tworzy użytkownika lub resetuje hasło.

## ⚠️ Wymuszona zmiana hasła (Konfiguracja wstępna) {#forced-password-change-initial-setup}

Świeżo zainstalowana instancja rozpoczyna pracę z poświadczeniami administratora z konfiguracji (`Authentication__Username` / `Authentication__Password`). Dopóki to domyślne hasło jest w użyciu **i nie istnieje jeszcze żadne konto pocztowe**, pierwsze logowanie wymusza zmianę:

- Baner ostrzegawczy wyjaśnia przyczynę: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Nie można opuścić tej strony: *"You must change your password before
  you can continue using the system."*
- Nowe hasło musi być inne niż obecne.
- Po zapisaniu flaga zostaje wyczyszczona, a użytkownik zostaje przeniesiony do pulpitu nawigacyjnego.

Po zmianie domyślnego hasła, wymuszona zmiana nigdy więcej się nie pojawi.

## 🕒 Pozostanie zalogowanym — pole wyboru "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

Formularz logowania posiada pole wyboru o nazwie **Remember Me** (*angemeldet bleiben* w niemieckim interfejsie). Wykonuje ono dokładnie jedną rzecz: decyduje, czy ciasteczko uwierzytelniające jest ciasteczkiem sesyjnym, czy trwałym.

| | Pole wyboru odznaczone | Pole wyboru zaznaczone |
|---|---|---|
| Czas życia ciasteczka | `Authentication__SessionTimeoutMinutes` (domyślnie **60 minut**) | **7 dni** |
| Typ ciasteczka | Ciasteczko sesyjne — usuwane po zamknięciu przeglądarki | Ciasteczko trwałe — przechowywane na dysku |
| Restart przeglądarki | Musisz zalogować się ponownie | Pozostajesz zalogowany |
| Timeout bezczynności | Przedłużany przy każdym żądaniu (`SlidingExpiration`) | Przedłużany przy każdym żądaniu, aż do bezwzględnego limitu 7 dni |
| Wylogowanie | Kończy sesję natychmiast | Kończy sesję natychmiast |

Zatem pole wyboru zmienia status "zalogowany na tę sesję przeglądarki, maksymalnie 60 minut bezczynności" na "zalogowany do 7 dni, wliczając restarty przeglądarki i komputera".

### Czego pole wyboru nie robi {#things-the-checkbox-does-not-do}

- **Nie zapamiętuje Twojego hasła ani nazwy użytkownika.** Formularz logowania nie jest wypełniony przy kolejnej wizycie; zachowywane jest tylko ciasteczko sesyjne.
- **7 dni to sztywny limit, którego nie można skonfigurować.** Wartość jest stała w kodzie (`Auth/Services/CookieAuthenticationService.cs`). Tylko czas życia nieutrwalony podąża za `Authentication__SessionTimeoutMinutes`.
- **Nie jest to licznik bezczynności.** 7 dni liczonych jest od momentu logowania; po ich upływie wymagane jest ponowne zalogowanie, nawet jeśli korzystano z archiwum minutę temu.
- **Nie przetrwa wylogowania.** Kliknięcie *Logout* usuwa ciasteczko niezależnie od zaznaczenia pola.

### Interakcja z 2FA {#interaction-with-2fa}

Przy włączonej uwierzytelnianiu dwuskładnikowym (2FA), pole wyboru jest zapamiętywane podczas kroku 2FA i stosowane dopiero po pomyślnym przejściu drugiego etapu. Przekazanie 2FA odbywa się w sesji po stronie serwera, która ma własny **60-minutowy timeout bezczynności**: jeśli kod nie zostanie wprowadzony w tym oknie, logowanie zostaje przerwane, a wybór pola zostaje utracony.

### Interakcja z sesją aplikacji {#interaction-with-the-application-session}

Mail Archiver utrzymuje drugą, oddzielną sesję po stronie serwera dla krótkotrwałych stanów — przekazania 2FA, wyboru przy przywracaniu wsadowym oraz ostatniego stanu wyszukiwania. Sesja ta ma stały **60-minutowy timeout bezczynności** i *nie jest* trwała. Po restarcie przeglądarki z ważnym ciasteczkiem "Remember Me" jesteś zalogowany, ale każda oczekująca operacja trzymana w tej sesji zaczyna się od nowa.

### Interakcja z OIDC / SSO {#interaction-with-oidc-sso}

Użytkownicy logujący się przez zewnętrznego dostawcę tożsamości nigdy nie otrzymują ciasteczka trwałego: callback OIDC zawsze rozpoczyna sesję z `persistAuthentication: false`. Dla tych kont pole wyboru **nie ma wpływu** — to, jak długo trwa sesja, jest decydowane przez ustawienia sesji dostawcy tożsamości.

> ⚠️ **Uwaga dotycząca bezpieczeństwa.** Na współdzielonym lub niezabezpieczonym komputerze, pozostaw pole wyboru odznaczone — trwałe ciasteczko sprawia, że archiwum jest dostępne przez 7 dni bez hasła. W środowiskach, gdzie jest to niedopuszczalne, skróć `Authentication__SessionTimeoutMinutes` i wymagaj od użytkowników niekorzystania z pola wyboru lub użyj OIDC, gdzie dostawca wymusza własną politykę sesji.

## 🔒 Rozważania dotyczące bezpieczeństwa {#security-considerations}

1. Używaj silnych haseł dla wszystkich kont użytkowników
2. Zmień domyślne hasło administratora natychmiast po instalacji
3. Przydzielaj Uprawnienie Admin tylko użytkownikom, którzy go potrzebują
4. Przydzielaj Uprawnienie Self Manager tylko użytkownikom, którzy muszą zarządzać własnymi kontami
5. Regularnie przeglądaj uprawnienia użytkowników, aby upewnić się, że dostęp jest odpowiedni
6. Usuwaj dostęp użytkownika, gdy nie jest już potrzebny
7. Stosuj zasadę najmniejszych uprawnień — przydzielaj dostęp tylko do tych kont e-mail, których użytkownicy faktycznie potrzebują
