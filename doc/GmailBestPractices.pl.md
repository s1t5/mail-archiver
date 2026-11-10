# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Podczas korzystania z kont Gmail z aplikacją Mail Archiver należy przestrzegać kilku specyficznych konfiguracji i dobrych praktyk, aby zapewnić optymalną wydajność i uniknąć potencjalnych problemów.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Konta Gmail mogą zostać zintegrowane z aplikacją Mail Archiver przy użyciu IMAP z hasłami aplikacji (App Passwords):

1. Włącz uwierzytelnianie dwuskładnikowe na swoim koncie Gmail
2. Wygeneruj hasło aplikacji specjalnie dla aplikacji Mail Archiver
3. Użyj hasła aplikacji zamiast swojego zwykłego hasła Gmail podczas konfiguracji konta w Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

Zdecydowanie zaleca się dostosowanie ustawień etykiet Gmail, aby zapobiec udostępnianiu etykiety „Wszystkie e-maile” (All Mail) poprzez IMAP:

1. Przejdź do interfejsu webowego Gmail i otwórz ustawienia (ikona małej zębatki na górze) → Zobacz wszystkie ustawienia → Karta Etykiety
2. Znajdź etykietę „Wszystkie e-maile” na liście
3. Odznacz opcję „Pokaż w IMAP” dla „Wszystkie e-maile”

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

System etykiet Gmail działa inaczej niż u tradycyjnych dostawców poczty e-mail. W Gmail:
- Wszystkie e-maile znajdują się w pojedynczym repozytorium „Wszystkie e-maile”
- Etykiety działają jak tagi, a nie foldery
- Etykieta „Wszystkie e-maile” zawiera każdy e-mail na Twoim koncie
- Gdy e-mail zostanie oznaczony etykietą, pojawia się zarówno w oryginalnym folderze, jak i w sekcji z etykietą. Zatem wszystkie wiadomości w Twojej odebranej również pojawiają się w etykiecie „Wszystkie e-maile”.

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Gdy etykieta „Wszystkie e-maile” jest udostępniana przez IMAP, może to powodować kilka problemów:

1. **Duplicate Processing**: Archiwizator może przetwarzać ten sam e-mail wielokrotnie, ponieważ pojawia się on w wielu folderach/etykietach
2. **Performance Impact**: Pobieranie „Wszystkie e-maile” może być niezwykle wolne dla dużych kont z tysiącami wiadomości e-mail
4. **Synchronization Issues**: Mogą pojawić się konflikty podczas próby ustalenia, do którego folderu e-mail faktycznie należy

Wyłączając dostęp IMAP do „Wszystkie e-maile”, zapewniasz, że e-maile są pobierane tylko raz z ich pierwotnej lokalizacji, co poprawia zarówno wydajność, jak i dokładność.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Jak wspomniano w głównym pliku README, konta Gmail wymagają szczególnej uwagi w zakresie polityk przechowywania:

- Funkcja Auto-Expunge w Gmail musi zostać wyłączona w ustawieniach Gmail w karcie „Przekazywanie i POP/IMAP”
- Ustawienie to jest kluczowe dla poprawnego działania polityk przechowywania na kontach Gmail

Aby skonfigurować to ustawienie:
1. Przejdź do Ustawienia Gmail → Zobacz wszystkie ustawienia → Karta Przekazywanie i POP/IMAP
2. W sekcji „Dostęp IMAP” wybierz „Auto-Expunge wyłączone - Czekaj na aktualizację serwera przez klienta” oraz „Przenieś wiadomość do Kosza”

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail nakłada dzienny limit przepustowości w wysokości 2500 MB dla pobierania przez IMAP. Podczas archiwizowania dużych ilości wiadomości e-mail należy pamiętać, że możesz osiągnąć ten limit, co może tymczasowo wstrzymać pobieranie wiadomości do następnego dnia.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver zapewnia wbudowaną obsługę limitów przepustowości, aby zarządzać limitami pasma IMAP w Gmail:

- **Automatic bandwidth tracking**: Monitoruje pobrane dane na każde konto dziennie
- **Configurable limits**: Ustaw `BandwidthTracking__DailyLimitMb=2500` dla Gmail
- **Automatic pause**: Synchronizacja zostaje wstrzymana po osiągnięciu limitu i wznawia się po jego zresetowaniu
- **Checkpoint system**: Postęp synchronizacji jest zapisywany i wznawiany od ostatniej pozycji

Aby uzyskać szczegółowe informacje na temat konfiguracji i użytkowania, zobacz **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

W przypadku dużych skrzynek pocztowych Gmail (np. 14 GB), początkowa synchronizacja potrwa kilka dni:

| Rozmiar Skrzynki pocztowej | Dni do zakończenia (przy 2500 MB/dzień) |
|---------------------------|-------------------------------------------|
| 5 GB                      | ~2 dni                                    |
| 10 GB                     | ~4 dni                                    |
| 14 GB                     | ~6 dni                                    |
| 20 GB                     | ~8 dni                                    |

**Wskazówka**: Włącz śledzenie pasma **zanim** dodasz duże konta Gmail, aby zapewnić prawidłową obsługę limitów od samego początku.

## ✅ Best Practices Summary {#best-practices-summary}

Stosowanie tych dobrych praktyk pomoże zapewnić płynne działanie Mail Archiver z Twoimi kontami Gmail, zachowując jednocześnie integralność danych i optymalną wydajność.
