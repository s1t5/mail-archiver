# ⚙️ Zadania w tle {#background-jobs}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Większość prac w Mail Archiver nie odbywa się w czasie oczekiwania użytkownika. Synchronizacja skrzynki pocztowej, importowanie pliku MBOX, przywracanie tysięcy e-maili lub eksportowanie Archiwum może zająć od minut do wielu godzin. Zadania te działają jako **zadania w tle** — uruchamiasz je, a one kontynuują pracę nawet po zamknięciu przeglądarki.

Strona **Zadania w tle** pokazuje, co jest obecnie uruchomione, co zostało zakończone, a co zakończyło się błędem. Jest to miejsce, do którego należy zajrzeć, gdy coś trwa dłużej niż oczekiwano lub gdy synchronizacja kończy się błędami.

> **ℹ️ Strona administratora.** Zadania w tle wymagają uprawnień administratora i są dostępne z menu głównego. Zwykli użytkownicy ich nie widzą — mogą nadal uruchamiać zadania (na przykład masowe przywracanie), ale monitorowanie odbywa się tutaj.

## 🗂️ Typy zadań {#job-types}

Strona grupuje zadania według typu. Każda sekcja pokazuje do **20 ostatnich** zadań, przy czym zadania uruchomione i oczekujące są sortowane na górze — dzięki temu stare zadanie nie zostanie przesunięte poza widok przez nowsze.

| Sekcja | Co obejmuje | Uruchomiono z |
|---|---|---|
| **Zadania Synchronizacji** | Pobieranie nowej poczty z serwera pocztowego | Harmonogram automatyczny lub *Sync now* na Koncie |
| **Zadania Importu MBox** | Importowanie plików `.mbox` | Konta pocztowe → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **Zadania Importu EML** | Importowanie plików `.eml` / archiwów ZIP | Konta pocztowe → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Zadania Masowego Przywracania** | Kopiowanie wybranych e-maili z powrotem do skrzynki pocztowej | Archiwum → wybierz e-maile → Batch Restore |
| **Zadania Eksportu Konta** | Eksportowanie całego konta (mbox / EML) | Konta pocztowe → [Export](Export-Account.md) |
| **Zadania Eksportu Wybranych E-maili** | Eksportowanie wybranego zestawu e-maili | Archiwum → wybierz e-maile → Export |
| **Zadania Usuwania E-maili** | Usuwanie e-maili z serwera lub archiwum, w tym masowe usuwanie oparte na filtrach | Polityka Przechowywania, ręczne usuwanie lub Archiwum → *Delete all matches* |
| **Zadania Usuwania Konta** | Usuwanie całego konta pocztowego | Konta pocztowe → Delete |
| **Offload** | Przenoszenie poczty z określonego przedziału dat do innego konta | Konta pocztowe → Offload |

## 📊 Odczytywanie wiersza zadania {#reading-a-job-row}

Każdy wiersz zawiera ten sam zestaw kolumn:

| Kolumna | Znaczenie |
|---|---|
| **Job ID** | Krótki identyfikator — podaj go podczas zgłaszania problemu |
| **Account** | Skrzynka pocztowa, na której pracuje zadanie |
| **Status** | Aktualny stan, patrz tabela poniżej |
| **Progress** | Jak daleko zaszło zadanie, z informacją o przetworzonych/całkowitych elementach, jeśli są znane |
| **Created** | Kiedy zadanie zostało dodane do kolejki |
| **Started** | Kiedy faktycznie rozpoczęło działanie |
| **Completed** | Kiedy zakończyło działanie (puste, gdy nadal trwa) |
| **Duration** | Czas trwania |

Niektóre typy zadań dodają liczniki do kolumny postępu:

- **Success** / **Failed** — e-maile przetworzone pomyślnie i niepomyślnie
- **Skipped (duplicates)** — e-maile już obecne w Archiwum, niezaimportowane ponownie
- **Recovered** — wiadomości ponownie pobrane po tym, jak serwer zgłosił ich brak
- **Malformed (skipped)** — wiadomości, których nie można było sparsować i zostały pominięte

### Wartości statusu {#status-values}

| Status | Znaczenie |
|---|---|
| **Queued** | Oczekiwanie na start — inne zadanie jest uruchomione lub worker jest zajęty |
| **Running** | Obecnie wykonywane |
| **Completed** | Zakończone pomyślnie |
| **CompletedWithErrors** | Zakończone, ale niektóre elementy zakończyły się niepowodzeniem (importy MBox) |
| **Failed** | Zakończone błędem |
| **Cancelled** | Przerwane przez użytkownika |
| **Rate Limited** | *Synchronizacja wstrzymana z powodu limitu przepustowości. Wznowienie nastąpi automatycznie.* |
| **Timed Out** | Synchronizacja przekroczyła skonfigurowany limit czasu i została zatrzymana |

> **ℹ️ "Rate Limited" i "Timed Out" to stany specyficzne tylko dla synchronizacji.** Wyglądają jak błędy, ale nimi nie są: synchronizacja z limitem przepustowości wznawia się sama, gdy limit na to pozwoli, a synchronizacja po przekroczeniu czasu kontynuuje pracę od punktu kontrolnego przy następnym uruchomieniu. Zobacz [Rate Limit Handling](RateLimitHandling.md) oraz [Mail Synchronization](Synchronization.md).

### Dlaczego postęp może wynosić 0% {#why-progress-can-sit-at-0}

Postęp jest mierzony liczbą **ukończonych folderów**, a nie poszczególnych e-maili:

> *Postęp jest mierzony liczbą ukończonych folderów. Duży folder (np. INBOX) utrzymuje pasek na poziomie 0%, dopóki nie zostanie ukończony.*

W skrzynce pocztowej, gdzie INBOX zawiera większość poczty, pasek może przez długi czas pozostawać na poziomie 0%, a następnie gwałtownie wzrosnąć. Jest to oczekiwane — zadanie pracuje, po prostu nie zakończyło jeszcze swojego pierwszego dużego folderu.

## 🔄 Strona odświeża się automatycznie {#the-page-refreshes-itself}

Gdy strona jest otwarta, przeładowuje się co **30 sekund**, dzięki czemu można ją zostawić w karcie i obserwować postęp bez klikania czegokolwiek. Etykieta pokazuje, kiedy dane zostały ostatnio zaktualizowane.

Przeładowanie jest anulowane w momencie kliknięcia w link lub przycisk — w przeciwnym razie kliknięcie trafiłoby na stronę, która jest właśnie zastępowana.

**Konfigurowalne:** `Jobs:RefreshSeconds` w `appsettings.json` (domyślnie `30`).
Ustaw na `0`, aby wyłączyć automatyczne przeładowywanie. Każde przeładowanie odbudowuje stronę ze wszystkich źródeł zadań, więc bardzo krótki interwał przy instalacji z wieloma kontami stanowi realne obciążenie — a obciążenie to mnoży się z każdą otwartą kartą.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Anulowanie zadania {#cancelling-a-job}

Zadania obsługujące anulowanie pokazują przycisk **Cancel Job**, który pyta:

> *Are you sure you want to cancel this job?*

Zadania importu używają sformułowania **Cancel Import** / *Are you sure you want to cancel this import job?*

Anulowanie jest dostępne dla zadań **synchronizacji, masowego przywracania, eksportu wybranych e-maili oraz usuwania e-maili**. Zadanie zatrzymuje się natychmiast po zakończeniu bieżącego elementu; status zmienia się na **Cancelled**.

> **⚠️ Anulowanie nie jest wycofaniem zmian (rollback).** E-maile, które zostały już zaimportowane, przywrócone, wyeksportowane lub usunięte, pozostają w tym stanie. Anulowanie powstrzymuje zadanie przed robieniem *więcej* — nie cofa tego, co już zostało zrobione. W przypadku zadania usuwania oznacza to, że e-maile, które zostały już usunięte, przepadły.

Zadań dotyczących Konta oraz importu MBox nie można anulować z tej strony; należy je zatrzymać na dedykowanej stronie importu danego konta.

## ⚠️ Błędy synchronizacji i "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Gdy synchronizacja kończy się statusem **Failed**, wiersz zadania oferuje opcję **Acknowledge Failures**. Jest to jedyna akcja na stronie, która zmienia przyszłe zachowanie, dlatego warto ją zrozumieć.

Każde uruchomienie synchronizacji zaczyna się tam, gdzie zakończyło się poprzednie, korzystając z **punktu kontrolnego (checkpoint)**. Jeśli synchronizacja zawiedzie dla niektórych e-maili, punkt kontrolny pozostaje za tymi e-mailami, aby kolejne uruchomienie spróbowało ich ponownie. Jest to bezpieczna opcja domyślna — nic nie zostaje pominięte po cichu.

Jeśli jednak błędy są trwałe (uszkodzona wiadomość, folder, którego serwer odmawia przesłania), każda kolejna synchronizacja będzie próbowała ich ponownie, co spowoduje, że synchronizacja będzie ciągle zawodzić w tym samym miejscu.

**Acknowledge Failures** rozwiązuje ten impas, przesuwając punkt kontrolny poza nieudane e-maile. Potwierdzenie wyraźnie informuje o konsekwencjach:

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

W przypadku sukcesu zobaczysz: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Potwierdź dopiero po zapoznaniu się z błędami.** Po potwierdzeniu te e-maile **nie** zostaną powtórzone — będą brakować w Archiwum, chyba że pobierzesz je w inny sposób. Najpierw sprawdź szczegóły błędów, a jeśli przyczyną jest błędna konfiguracja, napraw ją przed potwierdzeniem. Szczegółowe informacje na temat działania punktów kontrolnych znajdziesz w [Mail Synchronization](Synchronization.md).

Potwierdzenie jest rejestrowane w dzienniku dostępu jako *SyncAcknowledgeFailures*, wraz z liczbą nieudanych e-maili i Kontem, dzięki czemu można to później prześledzić.

## 👁️ Czego nie możesz tutaj zrobić {#what-you-cannot-do-here}

- **Brak przycisku ponowienia (retry).** Nieudane zadanie nie jest restartowane z tej strony — uruchom operację ponownie od miejsca, w którym ją zacząłeś (synchronizacja, import, eksport).
- **Brak edycji.** Parametry zadania są ustalane w momencie tworzenia.
- **Tylko ostatnich 20 na typ.** Starsze zadania nie są wymienione. Dla długoterminowych zapisów źródłami są dziennik dostępu oraz dziennik aplikacji — zobacz [Access Logging](Logs.md) i [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Rozwiązywanie problemów {#troubleshooting}

| Objaw | Prawdopodobna przyczyna | Co zrobić |
|---|---|---|
| Zadanie pozostaje w statusie **Queued** | Inne zadanie tego samego workera jest uruchomione | Czekaj; sprawdź inne sekcje pod kątem długotrwałego zadania |
| Postęp zatrzymany na 0% | Jeden duży folder jest wciąż przetwarzany | To normalne — zobacz "Dlaczego postęp może wynosić 0%" |
| Synchronizacja wielokrotnie **Failed** na tym samym koncie | Trwałe błędy w poszczególnych e-mailach | Przejrzyj błędy, napraw przyczynę, a następnie użyj Acknowledge |
| Synchronizacja pokazuje **Rate Limited** | Osiągnięto limit przepustowości | Nic nie rób — wzniesie się automatycznie |
| Synchronizacja pokazuje **Timed Out** | Skrzynka pocztowa większa niż limit czasu | Kontynuuje pracę od punktu kontrolnego przy następnym uruchomieniu; zobacz [Synchronization](Synchronization.md) |
| **CompletedWithErrors** przy imporcie MBox | Niektóre wiadomości są uszkodzone | Liczniki pokazują, ile ich było; reszta została zaimportowana |
| Zadanie znika po przeładowaniu | Wypadło z okna "ostatnich 20" | Sprawdź dziennik aplikacji, aby poznać jego końcowy status |
| Strona przeładowuje się podczas pracy | Automatyczne odświeżanie co 30 s | Ustaw `Jobs:RefreshSeconds` na `0` |

## 📚 Powiązana dokumentacja {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — punkty kontrolne, synchronizacje po przekroczeniu czasu
- [Rate Limit Handling](RateLimitHandling.md) — limity przepustowości i automatyczne wznawianie
- [Access Logging](Logs.md) — kto i co zrobił, w tym akcje zadań
- [Batch Restore](MailboxMigration.md) — przenoszenie e-maili między skrzynkami pocztowymi
- [Date-Windowed Offload](Offload.md) — zadania Offload
- [Docker Compose Logs](DockerComposeLogs.md) — czytanie dziennika aplikacji
