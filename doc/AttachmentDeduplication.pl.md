# 🧬 Deduplikacja Załączników {#attachment-deduplication}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Załączniki e-mail są przechowywane w bazie danych jako ładunki binarne. **Ten sam plik** bardzo często jest dołączany do wielu wiadomości e-mail (logo firm w podpisach, przekazywane pliki PDF, biuletyny, umowy, ...). Bez deduplikacji każdy załącznik przechowuje własną prywatną kopię tych bajtów, co marnuje dużą ilość miejsca w bazie danych.

**Deduplikacja załączników** przechowuje każdy *unikalny* ładunek dokładnie raz w tabeli adresowanej treścią (kluczem jest skrót SHA-256 bajtów) i pozwala wszystkim identycznym załącznikom odwoływać się do tej jednej, współdzielonej kopii. Zazwyczaj znacznie zmniejsza to rozmiar Archiwum z dużą liczbą załączników.

## ⚙️ Jak to działa {#how-it-works}

### Nowe załączniki {#new-attachments}
Za każdym razem, gdy nowa wiadomość e-mail jest archiwizowana (IMAP, Microsoft 365 / Graph, import EML, import MBOX, ...) ładunek załącznika jest haszowany i przechowywany raz. Jeśli ten sam ładunek już istnieje, nowy załącznik po prostu odwołuje się do istniejącego wiersza z treścią. Dzieje się to w sposób przejrzysty na warstwie danych, dzięki czemu każda ścieżka archiwizacji jest automatycznie objęta tą funkcją.

### Istniejące dane (migracja) {#existing-data-migration}
W przypadku instalacji, które zawierają załączniki zarchiwizowane **przed** wprowadzeniem tej funkcji, **solidna, wznawialna migracja w tle** przekształca istniejące ładunki wbudowane w wiersze współdzielonej treści:

- Przetwarza załączniki **partiami (batch by batch)**, gdzie każda partia jest własną transakcją.
- Postęp jest zapisywany w dedykowanej tabeli stanu, dzięki czemu migracja **przetrwa restart aplikacji** i po prostu kontynuuje pracę od miejsca, w którym została przerwana.
- Jest **idempotentna** – załączniki, które zostały już zmigrowane, nigdy nie są dotykane ponownie, nawet po awarii lub restarcie.
- Działa w tle i nie blokuje normalnego użytkowania aplikacji.

Po zakończeniu migracji w logach pojawia się komunikat o ukończeniu, np.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Przy następnym uruchomieniu system wykrywa, że nie ma już nic do zrobienia i pozostaje w trybie bezczynności. Jeśli jakiekolwiek niezdeduplikowane załączniki pozostałyby w systemie (na przykład po przywracaniu starszej kopii zapasowej), system automatycznie wznawia pracę i migruje je — system zawsze dąży do stanu pełnej deduplikacji.

### Garbage collection (zawsze włączone) {#garbage-collection-always-on}
Gdy wiadomości e-mail (a zatem i załączniki) są usuwane, współdzielony ładunek może stać się nieużywany (bez odniesień). Wbudowany mechanizm garbage collection usuwa te osierocone wiersze treści.

> 🧹 **Czyszczenie osieroconych danych ZAWSZE działa**, niezależnie od funkcji
> `DatabaseMaintenance`. Uruchamia się raz bezpośrednio po migracji, a następnie
> okresowo (co `OrphanCleanupIntervalHours`, domyślnie co 12 h). Gdy
> `DatabaseMaintenance__Enabled=true`, to samo czyszczenie uruchamia się dodatkowo przed
> codziennym `VACUUM`, aby zwolnione miejsce zostało fizycznie odzyskane.

### Zużycie dysku po migracji (ważne) {#disk-usage-after-the-migration-important}

> 📈 **Wartość "Storage" na pulpicie nawigacyjnym może na krótko WZROŚNĄĆ tuż po
> migracji** (np. 3,5 GB → 5,3 GB). Jest to zjawisko oczekiwane i nie jest błędem.
>
> Pulpit nawigacyjny raportuje **fizyczny** rozmiar bazy danych (`pg_database_size`). Gdy
> migracja przenosi ładunek do pamięci współdzielonej, stara kopia wbudowana zostaje oznaczona jako `NULL`,
> ale PostgreSQL (MVCC) przechowuje te stare bajty jako *dead tuples* do czasu, aż plik tabeli zostanie
> przepisany — dlatego przez chwilę baza danych zawiera zarówno starą, jak i nową kopię.
>
> Aby naprawić to automatycznie, aplikacja uruchamia **jednorazowy**
> `VACUUM FULL` na tabeli załączników **po zakończeniu migracji i gdy żadne zadania
> synchronizacji nie są w toku**. Operacja ta przepisuje tabelę i zwraca zwolnione miejsce do
> systemu operacyjnego; po tym czasie raportowany rozmiar spada **poniżej** pierwotnego. Operacja ta wykonuje się
> dokładnie raz (śledzone za pomocą znacznika `ReclaimedAt`), jest bezpieczna dla restartów i jest odraczana
> tak długo, jak trwa synchronizacja (system ponawia próbę automatycznie po zakończeniu pracy).

>
> ⚠️ `VACUUM FULL` blokuje tabelę załączników na czas trwania operacji (brak odczytów/zapisów
> załączników w międzyczasie) i wymaga pewnej ilości wolnego miejsca na dysku do przepisania danych. Jeśli chcesz
> uruchomić to ręcznie, wykonaj:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Konfiguracja {#configuration}


Te ustawienia są opcjonalne; wartości domyślne są odpowiednie dla większości instalacji. Zobacz także
[Setup Guide](Setup.md#-attachment-deduplication-settings).

| Ustawienie | Domyślne | Opis |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Liczba istniejących załączników migrowanych w jednej transakcji. Większe wartości przyspieszają migrację, ale zwiększają obciążenie pamięci/bazy danych na partię. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Opcjonalna przerwa (ms) między partiami migracji w celu ograniczenia obciążenia bazy danych w zajętych systemach. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Opóźnienie (s) po starcie aplikacji przed rozpoczęciem migracji (pozwala najpierw zakończyć migrację schematu). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Interwał (h) stale działającego mechanizmu garbage collection dla osieroconych danych. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Timeout komendy bazy danych (w sekundach) dla operacji partii migracyjnych. Domyślnie 5 minut. Jeśli partia nadal przekracza timeout, usługa automatycznie ponawia próbę z połową rozmiaru partii. |

Przykład (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Czy mogę wyłączyć deduplikację?**
Nie. Jest to kluczowa funkcja przechowywania danych i jest zawsze aktywna. Możesz jedynie dostosować rozmiar partii i parametry harmonogramu wymienione powyżej.

**Czy moje dane są bezpieczne podczas migracji?**
Tak. Każda partia to pojedyncza transakcja, a praca jest idempotentna. Awaria lub restart w trakcie migracji wycofuje nieukończoną partię, która zostanie ponownie przetworzona przy następnym uruchomieniu; żadne dane nie zostaną utracone ani powielone.

**Czy to spowalnia archiwizację?**
Narzut związany z haszowaniem każdego załącznika jest pomijalny. Jednorazowa migracja istniejących danych odbywa się w tle i może być ograniczana za pomocą `DelayBetweenBatchesMs` w razie potrzeby.

**Migracja ciągle przekracza timeout i restartuje się w tym samym miejscu. Co mogę zrobić?**
Może się to zdarzyć w bardzo dużych bazach danych (setki tysięcy e-maili), gdzie pojedyncza partia 200 załączników z haszowaniem SHA-256 przekracza domyślny timeout komendy. Usługa automatycznie ponawia próbę z połową rozmiaru partii, gdy wystąpi timeout. Jeśli nadal widzisz timeouty, możesz ręcznie zmniejszyć rozmiar partii i/lub zwiększyć timeout komendy:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Ile miejsca zaoszczędzę?**
Zależy to całkowicie od tego, ile duplikatów załączników zawiera Twoje Archiwum. Najwięcej zyskują Archiwa z wieloma powtarzającymi się podpisami, logotypami lub przekazywanymi plikami.

---
