# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Samodzielne hostowanie archiwizacji e-mail — Archiwum, wyszukiwanie i eksport poczty od dowolnego dostawcy.**
Skonfiguruj raz, zachowaj każdą wiadomość i znajdź ją ponownie po latach.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Rozpoczęcie pracy__

    ---

    Zainstaluj Mail Archiver za pomocą Docker Compose i połącz swoją pierwszą Skrzynkę pocztową.

    [:octicons-arrow-right-24: Konfiguracja i parametry](Setup.md)

-   :material-magnify:{ .lg .middle } __Znajdź cokolwiek__

    ---

    Wyszukiwanie pełnotekstowe we wszystkich Kontach, z filtrami, sortowaniem i działaniami masowymi.

    [:octicons-arrow-right-24: Wyszukiwanie poczty](Search.md)

-   :material-api:{ .lg .middle } __Automatyzacja__

    ---

    Odczytuj swoje Archiwum za pomocą skryptów i agentów AI — klucze API tylko do odczytu, o ograniczonym zakresie.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Zabezpieczanie__

    ---

    Uwierzytelnianie dwuskładnikowe, SSO, logowanie dostępu i polityki Przechowywania.

    [:octicons-arrow-right-24: Uwierzytelnianie dwuskładnikowe](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalacja i utrzymanie {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Konfiguracja i parametry__

    ---

    Każda zmienna środowiskowa, Kestrel HTTPS i zarządzanie sekretami.

    [:octicons-arrow-right-24: Czytaj](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Umieść Mail Archiver za nginx lub Caddy z terminacją TLS.

    [:octicons-arrow-right-24: Czytaj](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Aktualizacja PostgreSQL__

    ---

    Przejdź do nowej głównej wersji PostgreSQL bez utraty danych.

    [:octicons-arrow-right-24: Czytaj](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Utrzymanie bazy danych__

    ---

    VACUUM, ANALYZE i utrzymywanie responsywności Archiwum w czasie.

    [:octicons-arrow-right-24: Czytaj](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Kopia zapasowa i przywracanie__

    ---

    Wykonaj Kopię zapasową bazy danych i ją przywróć — w tym konfiguracje Proxmox.

    [:octicons-arrow-right-24: Czytaj](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokalne środowisko testowe__

    ---

    Uruchom tymczasową instancję lokalnie przed dotknięciem produkcji.

    [:octicons-arrow-right-24: Czytaj](LocalTestEnvironment.md)

</div>

## 📥 Importowanie poczty {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Import Konta__

    ---

    Dodaj wiele Kont jednocześnie z pliku CSV.

    [:octicons-arrow-right-24: Czytaj](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronizacja__

    ---

    Szybka vs. pełna Synchronizacja, punkty kontrolne i to, co dzieje się, gdy Synchronizacja zawiedzie.

    [:octicons-arrow-right-24: Czytaj](Synchronization.md)

-   :material-console:{ .lg .middle } __Polecenia CLI__

    ---

    Importuj istniejące Archiwa z linii komend i przenoś przechowywane oryginalne wiadomości między bazą danych a systemem plików.

    [:octicons-arrow-right-24: Czytaj](CLI.md)

-   :material-upload:{ .lg .middle } __Import przez WWW (EML / MBox)__

    ---

    Prześlij plik MBox lub plik ZIP z plikami EML przez przeglądarkę.

    [:octicons-arrow-right-24: Czytaj](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Obsługa limitów prędkości__

    ---

    Śledzenie przepustowości i to, jak wstrzymane Synchronizacje wznawiają się automatycznie.

    [:octicons-arrow-right-24: Czytaj](RateLimitHandling.md)

</div>

## 🔍 Praca z Archiwum {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Wyszukiwanie poczty__

    ---

    Składnia zapytań, filtry, sortowanie, stronicowanie i działania masowe.

    [:octicons-arrow-right-24: Czytaj](Search.md)

-   :material-progress-clock:{ .lg .middle } __Zadania w tle__

    ---

<|"|>Obserwuj Synchronizacje, importy, Przywracanie i eksporty — i bezpiecznie je anuluj.

    [:octicons-arrow-right-24: Czytaj](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migracja Skrzynki pocztowej__

    ---

    Kopiuj pocztę między Skrzynkami pocztowymi, zachowując strukturę Folderów.

    [:octicons-arrow-right-24: Czytaj](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Eksport Konta (EML / MBox)__

    ---

    Wyeksportuj całe Konto jako przenośne Archiwum EML lub MBox.

    [:octicons-arrow-right-24: Czytaj](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Przechowywanie oryginalnych wiadomości__

    ---

    Zachowaj wiadomość w formie otrzymanej, aby DKIM nadal działał poprawnie, a eksporty były identyczne co do bajtu.

    [:octicons-arrow-right-24: Czytaj](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Deduplikacja Załączników__

    ---

    Jak identyczne Załączniki są przechowywane raz zamiast wielokrotnie.

    [:octicons-arrow-right-24: Czytaj](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Przenoszenie danych w oknach czasowych__

    ---

    Przenoś starszą pocztę do innego Konta, okno po oknie.

    [:octicons-arrow-right-24: Czytaj](Offload.md)

-   :material-harddisk:{ .lg .middle } __Przechowywanie na każde Konto__

    ---

    Zobacz, ile miejsca zajmuje każda Skrzynka pocztowa.

    [:octicons-arrow-right-24: Czytaj](AccountStorage.md)

</div>

## 🔐 Administracja i bezpieczeństwo {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Zarządzanie użytkownikami__

    ---

    Twórz użytkowników, nadawaj uprawnienia administratora, przypisuj Uprawnienia do Skrzynki pocztowej i zmieniaj własne hasło.

    [:octicons-arrow-right-24: Czytaj](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Uwierzytelnianie dwuskładnikowe__

    ---

    Konfiguracja TOTP, kody zapasowe i co zrobić w przypadku utraty urządzenia.

    [:octicons-arrow-right-24: Czytaj](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Zaloguj się przez Entra ID, Authelia lub innego dostawcę tożsamości.

    [:octicons-arrow-right-24: Czytaj](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Logowanie dostępu__

    ---

    Kto otworzył, przeszukiwał, wyeksportował lub usunął co.

    [:octicons-arrow-right-24: Czytaj](Logs.md)

-   :material-file-export:{ .lg .middle } __Eksport danych audytowych__

    ---

    Eksportuj ścieżkę audytu na potrzeby przeglądów zgodności.

    [:octicons-arrow-right-24: Czytaj](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Awaryjne odzyskiwanie Konta__

    ---

    Odzyskaj dostęp, gdy Konto zostanie zablokowane.

    [:octicons-arrow-right-24: Czytaj](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Polityki Przechowywania__

    ---

    Automatyczne usuwanie z serwera pocztowego lub lokalnego Archiwum.

    [:octicons-arrow-right-24: Czytaj](RetentionPolicies.md)

</div>

## ☁️ Przewodniki dostawców {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Rejestracja aplikacji Azure (M365)__

    ---

    Zarejestruj aplikację i skonfiguruj Przechowywanie dla Microsoft 365.

    [:octicons-arrow-right-24: Czytaj](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Import Skrzynki pocztowej z dzierżawy M365__

    ---

    Masowy import każdej Skrzynki pocztowej z dzierżawy za pomocą jednego formularza.

    [:octicons-arrow-right-24: Czytaj](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Instancje Mailcow__

    ---

    Wybieraj Skrzynki pocztowe centralnie i przydzielaj dedykowane hasła aplikacji IMAP.

    [:octicons-arrow-right-24: Czytaj](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Osobiste Konta Microsoft__

    ---

    Outlook.com, Hotmail i M365 Family przez device code flow.

    [:octicons-arrow-right-24: Czytaj](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Dobre praktyki Gmail__

    ---

    Hasła aplikacji, etykiety, expunge i limity prędkości Gmail.

    [:octicons-arrow-right-24: Czytaj](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Ograniczenia Yahoo mail__

    ---

    Czego nie zrobią punkty końcowe IMAP i eksportu Yahoo.

    [:octicons-arrow-right-24: Czytaj](YahooMailExportLimitations.md)

</div>

## 🔌 Dostęp programowy {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Dostęp tylko do odczytu do Archiwum z kluczami API o ograniczonym zakresie dla każdego użytkownika.

    [:octicons-arrow-right-24: Czytaj](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Udostępnij Archiwum agentom AI jako wykrywalne narzędzia.

    [:octicons-arrow-right-24: Czytaj](MCP.md)

</div>

## 🧰 Operacje i rozwój {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Język i lokalizacja__

    ---

    Zmień język interfejsu i dowiedz się, jak dodać tłumaczenie.

    [:octicons-arrow-right-24: Czytaj](Localization.md)

-   :material-docker:{ .lg .middle } __Logi Docker Compose__

    ---

    Czytaj logi kontenerów i zmieniaj poziom logowania.

    [:octicons-arrow-right-24: Czytaj](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Wersje rozwojowe__

    ---

    Przełącz się na tag dev, aby przetestować nadchodzące zmiany.

    [:octicons-arrow-right-24: Czytaj](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ta dokumentacja jest stale rozbudowywana.**
Jeśli zauważysz błąd lub masz sugestię, prosimy o
[otwarcie zgłoszenia](https://github.com/s1t5/mail-archiver/issues) lub przesłanie pull requesta.
</div>
