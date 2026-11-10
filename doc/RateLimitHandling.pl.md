# 📊 Obsługa limitów przepustowości {#rate-limit-handling}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Wielu dostawców poczty e-mail nakłada limity przepustowości na dostęp przez IMAP. Limity te zazwyczaj wynoszą od kilkuset megabajtów do kilku gigabajtów w ciągu 24-godzinnego okresu. Podczas Synchronizacja dużych skrzynek pocztowych, które przekraczają te limity, proces synchronizacji normalnie zakończyłby się niepowodzeniem lub byłby niekompletny.

Mail Archiver zawiera wbudowany system obsługi limitów przepustowości, który automatycznie zarządza tymi ograniczeniami, zapewniając niezawodną synchronizację nawet dla dużych skrzynek pocztowych.

## ⚙️ Jak to działa {#how-it-works}

### Automatyczne śledzenie przepustowości {#automatic-bandwidth-tracking}

System stale monitoruje ilość danych pobieranych podczas Synchronizacja IMAP. Gdy osiągnięty zostanie skonfigurowany limit przepustowości:

1. **Synchronizacja zostaje automatycznie wstrzymana**: Bieżąca operacja synchronizacji zostaje zatrzymana w sposób uporządkowany.
2. **Postęp jest zapisywany**: Punkty kontrolne przechowują dokładną pozycję, w której przerwano synchronizację.
3. **Aktualizacje statusu**: Konto wyświetla status "Rate-Limited" w panelu sterowania.
4. **Automatyczne wznowienie**: Gdy limit zostanie zresetowany (zazwyczaj po 24 godzinach), synchronizacja wznawia się od zapisanego punktu kontrolnego.

### Przykład: Skrzynka pocztowa 14 GB z dziennym limitem 2,5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Konfiguracja {#configuration}

### Włączanie śledzenia przepustowości {#enabling-bandwidth-tracking}

Dodaj następującą sekcję do swojego `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Opcje konfiguracji {#configuration-options}

| Opcja | Opis | Domyślna | Przykład |
|--------|-------------|---------|---------|
| `Enabled` | Włącz/wyłącz śledzenie przepustowości | `false` | `true` |
| `DailyLimitMb` | Dzienny limit pobierania w megabajtach | `2500` | `2500` |
| `WarningThresholdPercent` | Procent limitu wyzwalający ostrzeżenia w logach | `80` | `80` |
| `PauseHoursOnLimit` | Liczba godzin wstrzymania po osiągnięciu limitu | `24` | `24` |

### Ustawianie poprawnego limitu {#setting-the-correct-limit}

Aby określić odpowiednią wartość `DailyLimitMb`:

1. Sprawdź dokumentację lub warunki świadczenia usług swojego dostawcy poczty e-mail.
2. Poszukaj limitów przepustowości IMAP lub kwot pobierania danych.
3. Skontaktuj się z administratorem poczty, jeśli korzystasz z korporacyjnego systemu pocztowego.
4. Zacznij od ostrożnej wartości, jeśli nie masz pewności (np. 2000 MB).
5. Monitoruj logi pod kątem ostrzeżeń o limitach podczas początkowych procesów synchronizacji.

## 👤 Doświadczenie użytkownika {#user-experience}

### Wskaźniki w panelu sterowania {#dashboard-indicators}

Gdy limit przepustowości jest aktywny, Konto wyświetla:

- **Status**: "Rate-Limited" 
- **Baner informacyjny**: Wyjaśnia sytuację i szacowany czas resetu.
- **Postęp synchronizacji**: Pokazuje e-maile przetworzone przed osiągnięciem limitu.

### Automatyczne zachowanie {#automatic-behavior}

1. **Brak konieczności ręcznej interwencji**: System obsługuje wszystko automatycznie.
2. **Restart aplikacji**: Jeśli aplikacja zostanie uruchomiona ponownie, stan limitu przepustowości jest zachowany w bazie danych.
3. **Wiele kont**: Każde Konto śledzi swoją przepustowość niezależnie.

### Co jest zapisywane {#what-gets-saved}

Gdy wystąpi limit przepustowości, system zapisuje:

- **Punkty kontrolne synchronizacji**: Dokładna pozycja w każdym Folderze, w którym przerwano synchronizację.
- **Użycie przepustowości**: Łączna liczba bajtów pobranych w danym dniu.
- **Stan limitu**: Czy limit został osiągnięty i kiedy zostanie zresetowany.

## 🗄️ Konserwacja bazy danych {#database-maintenance}

`DatabaseMaintenanceService` automatycznie czyści stare dane:

- **Rejestry przepustowości**: Usuwane po 7 dniach.
- **Punkty kontrolne synchronizacji**: Zakończone punkty kontrolne są usuwane po 30 dniach.
- **Niekompletne punkty kontrolne**: Zachowywane do momentu zakończenia synchronizacji.

## ✅ Najlepsze praktyki {#best-practices}

### Dla dużych skrzynek pocztowych {#for-large-mailboxes}

1. **Włącz śledzenie przepustowości**: Niezbędne dla skrzynek większych niż dzienny limit.
2. **Zaplanuj początkową synchronizację**: Pełna Synchronizacja dużej skrzynki może potrwać kilka dni.
3. **Monitoruj postęp**: Sprawdzaj panel sterowania pod kątem statusu limitu przepustowości.
4. **Nie przerywaj**: Pozwól automatycznemu wznawianiu obsłużyć kontynuację procesu.

### Dla administratorów {#for-administrators}

1. **Ustaw odpowiednie limity**: Dopasuj `DailyLimitMb` do rzeczywistych limitów Twojego dostawcy poczty.
2. **Monitoruj logi**: Komunikaty ostrzegawcze pojawiają się przy 80% limitu.
3. **Miejsce w bazie danych**: Śledzenie przepustowości zużywa minimalną dodatkową przestrzeń.
4. **Kopia zapasowa przed zmianami**: Zawsze wykonaj Kopia zapasowa przed modyfikacją ustawień limitów przepustowości.

### Uwagi dotyczące wydajności {#performance-considerations}

- ✅ **Wpływ na pamięć**: Pomijalny – śledzone są tylko bajty, a nie przechowywane e-maile.
- ✅ **Wpływ na bazę danych**: Minimalny – małe rekordy są automatycznie usuwane.
- ✅ **Prędkość synchronizacji**: Brak wpływu na wydajność podczas aktywnej synchronizacji.

## 🛡️ Przejściowe błędy FETCH / Throttling po stronie serwera {#transient-fetch-errors-server-side-throttling}

Oprócz opisanego powyżej śledzenia przepustowości opartego na bajtach, Mail Archiver
wykrywa również i reaguje na **throttling na poziomie pojedynczej wiadomości**, sygnalizowany przez sam serwer IMAP. Niektórzy dostawcy IMAP stosują throttling na poziomie konta lub adresu IP, który jest **niezależny od skonfigurowanych przerw `BatchOperation`**, ponieważ throttling jest wymuszany po stronie serwera na podstawie ogólnej objętości żądań podczas bieżącej sesji.

### Objawy {#symptoms}

Serwer zwraca odpowiedź `NO` (lub `BAD`) na pojedyncze polecenie `FETCH` dotyczące treści wiadomości, z komunikatami takimi jak:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- odpowiedzi zawierające `throttle`, `rate limit` lub podobne znaczniki

Uwierzytelnianie, listowanie Folderów i samo wyszukiwanie wiadomości zazwyczaj nadal przebiegają pomyślnie — zawodzi jedynie faktyczne pobieranie pełnej wiadomości.

### Automatyczna obsługa {#automatic-handling}

Wykryty w ten sposób błąd przejściowy powoduje, że Mail Archiver stosuje strategię ponawiania prób dla pojedynczej wiadomości:

1. **Ponawianie z wykładniczym opóźnieniem (exponential backoff)**: Do **3 prób** dla tej samej wiadomości, z opóźnieniami **5 s → 15 s → 60 s**.
2. **Odzyskiwanie połączenia**: Przed każdą próbą stan połączenia, uwierzytelniania i wyboru Folderu jest walidowany i w razie potrzeby przywracany.
3. **Reset po sukcesie**: Pomyślne wykonanie FETCH resetuje licznik kolejnych niepowodzeń.
4. **Uporządkowane wstrzymanie synchronizacji**: Jeśli po wszystkich próbach **10 kolejnych wiadomości** nadal kończy się błędem przejściowym, bieżący proces synchronizacji zostaje wstrzymany w sposób uporządkowany — przy użyciu tego samego mechanizmu, co wstrzymanie z powodu limitu przepustowości:
   - Znacznik czasu `LastSync` **nie** jest aktualizowany.
   - Jeśli `BandwidthTracking` jest włączone, punkty kontrolne są zachowywane, aby kolejny proces synchronizacji wznowił pracę od ostatniej pomyślnie przetworzonej wiadomości.
   - Zadanie zostaje oznaczone jako ograniczone limitem (rate-limited).
   - Następna zaplanowana Synchronizacja rozpocznie się automatycznie.

### Łagodzenie skutków {#mitigation}

Jeśli regularnie napotykasz throttling po stronie serwera, zmniejsz częstotliwość żądań, dostosowując ustawienia `BatchOperation`:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Zauważ, że niektórzy dostawcy stosują throttling na podstawie **całkowitej liczby bajtów** lub **całkowitej liczby operacji FETCH w oknie czasowym**, a nie na podstawie liczby żądań na sekundę. W takich przypadkach:

- Włącz `BandwidthTracking` (patrz powyżej) i skonfiguruj `DailyLimitMb` tak, aby odpowiadało udokumentowanemu limitowi dostawcy. Synchronizacja zostanie wtedy czysto wstrzymana po wyczerpaniu dziennego budżetu i automatycznie wznowiona następnego dnia.
- Początkowe procesy synchronizacji bardzo dużych skrzynek pocztowych mogą trwać kilka dni, co jest zamierzonym zachowaniem.

### Co jest traktowane jako błąd przejściowy {#what-counts-as-a-transient-error}

Tylko odpowiedzi IMAP `NO`/`BAD` zawierające tekst związany z throttlingiem lub kody odpowiedzi są traktowane jako przejściowe. Inne błędy (niepoprawne wiadomości, problemy z dekodowaniem UTF-8 itp.) są natychmiast traktowane jako `FailedEmails` i **nie wyzwalają** logiki ponawiania prób/wstrzymania.

## 📚 Powiązana dokumentacja {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procedury czyszczenia bazy danych
- [GmailBestPractices.md](GmailBestPractices.md) - Konfiguracja specyficzna dla Gmail
