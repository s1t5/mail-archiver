# 📤 Eksportowanie konta pocztowego (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Eksport konta zapisuje **każdą wiadomość jednego konta** z powrotem do pliku ZIP do pobrania. Jest to odpowiednik [importu przez przeglądarkę](Import-EML-MBox.md): otrzymujesz przenośne Archiwum konta w formacie, który może odczytać dowolny klient pocztowy lub narzędzie do migracji.

Oferowane są dwa formaty:

| Format | Co otrzymujesz | Typowe zastosowanie |
|--------|--------------|-------------|
| **EML** | Pojedyncze pliki `.eml`, zorganizowane w foldery | Ponowny import do klienta pocztowego, przetwarzanie poszczególnych wiadomości, długoterminowa czytelność |
| **MBox** | Tradycyjne pliki Skrzynka pocztowa — **jeden plik na folder** | Narzędzia i klienci obsługujący MBox, masowe przetwarzanie tekstu |

W obu przypadkach wiadomości są zapisywane jako surowe wiadomości RFC-822, więc **załączniki są dołączone** do eksportu.

> 💡 **Wskazówka**: Aby wyeksportować tylko wybrane wiadomości zamiast całego konta, użyj akcji masowych w [Wyszukiwaniu poczty](Search.md#-acting-on-many-results-at-once).

## 🧭 Dostęp {#access}

1. Otwórz **Konta pocztowe** w menu głównym.
2. Kliknij przycisk **⋮** (więcej akcji) w wierszu konta i wybierz **Export**. Ta sama opcja znajduje się na stronie **Szczegóły** konta.

Eksport wymaga roli **admin** lub **self manager** — standardowi użytkownicy widzą tę opcję, ale nie mogą z niej korzystać. Eksport zawsze obejmuje całe konto; nie ma filtra daty ani folderu.

## 🖥️ Korzystanie ze strony eksportu {#using-the-export-page}

Strona pokazuje **całkowitą**, **przychodzącą** i **wychodzącą** liczbę wiadomości konta, dzięki czemu możesz zobaczyć rozmiar zadania przed jego rozpoczęciem. Następnie:

1. Wybierz format — **EML** lub **MBox**.
2. Naciśnij **Start Export**.

Eksport odbywa się **w tle**. Zostaniesz przeniesiony na stronę statusu, która pokazuje identyfikator zadania, wybrany format, znaczniki czasu oraz liczbę przetworzonych/całkowitych wiadomości.

### Statusy zadań {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Gdy zadanie osiągnie status **Completed**, na stronie statusu pojawi się **przycisk pobierania**. To samo zadanie jest również wymienione na stronie [Zadania](BackgroundJobs.md).

> ⚠️ **Pliki eksportu są usuwane automatycznie.** Plik ZIP jest usuwany **po pomyślnym pobraniu**, a w każdym przypadku **po 7 dniach**. Pobierz plik, dopóki jest on oferowany — samo Archiwum nie jest dotknięte, możesz po prostu uruchomić nowy eksport później.

## ⚠️ Na co uważać {#things-to-watch-out-for}

- **Duże konta wymagają czasu.** Duża Skrzynka pocztowa może wymagać kilku minut, a plik ZIP może stać się bardzo duży, w zależności od liczby wiadomości i załączników. Ponieważ zadanie działa w tle, możesz zamknąć przeglądarkę i wrócić do strony statusu później.
- **Miejsce na dysku serwera.** Plik ZIP jest składany na serwerze przed jego pobraniem, więc upewnij się, że obok Archiwum jest wystarczająco dużo wolnego miejsca.
- **"Export file not found"** — zadanie jeszcze się nie zakończyło lub plik został już pobrany lub usunięty przez 7-dniowe czyszczenie. Uruchom nowy eksport.
- **Brak opcji filtrowania** — eksport dotyczy całego konta (wszystko albo nic). Aby wyeksportować podzbiór, użyj eksportu ze strony wyszukiwania.
- **To nie jest eksport zgodny z wymogami audytu (compliance).** W celu przekazania danych do audytu wraz z plikiem indeksu, użyj zamiast tego [Eksportu danych audytowych](AuditExport.md) — generuje on pakiet w tabelarycznym formacie danych masowych, którego oczekują narzędzia audytowe.

## 🔗 Powiązana dokumentacja {#related-documentation}

- [Importowanie poczty w interfejsie WWW](Import-EML-MBox.md) — odwrotny proces
- [Wyszukiwanie poczty](Search.md) — eksportowanie wyboru wiadomości
- [Zadania w tle](BackgroundJobs.md) — lista zadań, postęp i anulowanie
- [Eksport danych audytowych](AuditExport.md) — pakiety eksportu gotowe do audytu
- [Polecenia CLI](CLI.md) — importowanie z zamontowanego katalogu
