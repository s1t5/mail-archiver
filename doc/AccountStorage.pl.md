# 💾 Per-Account Storage Display {#per-account-storage-display}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Tabela „Account Overview” w Dashboardzie oraz tabela „Show All” w MailAccounts zawierają kolumnę **Storage**. Kolumna ta pokazuje, ile miejsca w bazie danych zajmuje każde Konto — wliczając **wszystkie pola każdej zarchiwizowanej wiadomości** oraz **wszystkie Załączniki**.

## ⚙️ Jak to działa {#how-it-works}

Wartości Storage są **buforowane** w tabeli bazy danych `AccountStorageCache`. Oznacza to, że strony internetowe odczytują wstępnie obliczone wartości zamiast uruchamiać kosztowne zapytanie za każdym razem, gdy otwierasz Dashboard lub listę kont. Bufor jest aktualizowany przez niezależną usługę działającą w tle (`AccountStorageRefreshService`).

Dla każdego konta usługa prosi PostgreSQL o zsumowanie `pg_column_size` dla każdej kolumny wierszy konta w tabeli `ArchivedEmails`. Obejmuje to **wszystkie pola wiadomości** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash itp.), w tym kompresję PostgreSQL i przechowywanie poza wierszem (TOAST). Rozmiar wartości przechowywanej poza wierszem jest odczytywany z jej wskaźnika TOAST, dzięki czemu obliczenie skanuje tylko samą tabelę i nigdy nie ładuje treści wiadomości. Część stanowiąca Załączniki jest dodawana oddzielnie jako logiczna suma `EmailAttachment.Size`.

Usługa odświeżania aktualizuje bufor w trzech sytuacjach:

1. **Startup backfill** — Po nowej instalacji lub po aktualizacji do wersji wprowadzającej tę funkcję, każde Konto zaczyna bez buforowanej wartości. Usługa przechodzi przez wszystkie konta jedno po drugim i oblicza pierwszą wartość.
2. **Daily refresh** — Raz dziennie o skonfigurowanej godzinie (domyślnie `02:30`) usługa ponownie oblicza wszystkie konta jako zabezpieczenie.
3. **Immediate refresh** — Po Synchronizacji wiadomości, imporcie EML, imporcie MBOX lub usunięciu danych w ramach Przechowywania, dotknięte Konto jest przeliczane natychmiast, aby wyświetlana wartość była aktualna.

## 🤔 Dlaczego na początku wyświetlana jest wartość „0 B” {#why-0-b-is-shown-at-first}

Kiedy funkcja zostaje po raz pierwszy włączona lub gdy zostanie utworzone nowe Konto, wiersz bufora dla tego konta jeszcze nie istnieje. Usługa odświeżania działa w tle i oblicza konta **jedno po drugim**, z niewielkim opóźnieniem między każdym kontem, aby bardzo duże Archiwa nie przeciążyły bazy danych.
Dopóki usługa nie przetworzy konta, interfejs użytkownika wyświetla **„0 B”** jako zastępczą wartość. Jest to całkowicie normalne. Gdy tylko usługa działająca w tle zakończy przetwarzanie tego konta, prawdziwa wartość pojawi się przy następnym załadowaniu lub odświeżeniu strony.
W przypadku dużych istniejących Archiwów proces backfill może zająć kilka minut lub dłużej. Usługa śledzi postęp w dedykowanej tabeli (`AccountStorageBackfillState`), więc jeśli aplikacja zostanie uruchomiona ponownie w trakcie procesu, po prostu kontynuuje od miejsca, w którym przerwała — żadne Konto nie jest obliczane dwukrotnie.

## 🔧 Konfiguracja {#configuration}

Funkcję można dostroić w `appsettings.json` lub za pomocą zmiennych środowiskowych. Aby zapoznać się ze wszystkimi dostępnymi opcjami, w tym:

- `AccountStorage__Enabled`: Włączenie lub wyłączenie usługi odświeżania (domyślnie `true`).
- `AccountStorage__DailyExecutionTime`: Czas na codzienne pełne odświeżanie (domyślnie `02:30`).
- `AccountStorage__BackfillDelayMs`: Pauza między kontami podczas startup backfill (domyślnie `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Pauza między kontami podczas codziennego odświeżania (domyślnie `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Timeout polecenia bazy danych dla obliczeń storage na konto (domyślnie `300` sekund). Zwiększ tę wartość dla bardzo dużych Archiwów, gdzie skanowanie `pg_column_size` trwa dłużej.

> 💡 **Wskazówka**: Nie musisz włączać `DatabaseMaintenance__Enabled`, aby ta funkcja działała. Usługa odświeżania storage działa niezależnie.

Zobacz sekcję **Account Storage Settings** w [Setup.md](Setup.md), aby poznać wszystkie dostępne opcje.
