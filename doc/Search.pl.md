Strona: 🔍 Mail Search

# 🔍 Mail Search {#mail-search}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Wyszukiwanie stanowi serce Archiwum poczty e-mail. Mail Archiver przeszukuje **pełny tekst**
Twojego zarchiwizowanego maila — temat, treść, nadawca i odbiorcy — we wszystkich
kontach, do których masz dostęp, i zwraca wyniki w milisekundach, nawet w
archiwach liczących miliony wiadomości.

Niniejszy przewodnik obejmuje interfejs wyszukiwania, składnię zapytań, filtry, sortowanie,
stronicowanie oraz działania, jakie należy podjąć, gdy trzeba zareagować na zestaw wyników.

> **ℹ️ Wyszukiwanie widzi tylko to, co Ty możesz widzieć.** Wyniki są filtrowane przez
> uprawnienia Twojego konta. Użytkownik niebędący administratorem znajduje tylko
> maile z przypisanych mu skrzynek pocztowych — ograniczenie jest stosowane w samym
> zapytaniu, a nie w interfejsie.

## 🚀 Dostęp do wyszukiwania {#accessing-search}

1. Zaloguj się i wybierz **Archive** w menu głównym
2. Pasek wyszukiwania znajduje się nad listą wyników

Otwarcie Archiwum bez terminu wyszukiwania wyświetla Twoje najnowsze e-maile, od najnowszych.
Możesz tam przeglądać wiadomości lub zawęzić wyniki za pomocą filtrów.

## 🎯 Interfejs wyszukiwania {#the-search-interface}

| Pole | Co robi |
|---|---|
| **Search Term** | Zapytanie pełnotekstowe — zobacz [Query Syntax](#-query-syntax) poniżej |
| **From** | Tylko e-maile wysłane w tym dniu lub później |
| **To** | Tylko e-maile wysłane w tym dniu lub wcześniej |
| **Direction** | `All`, `Incoming` lub `Outgoing` |
| **Account** | Ogranicz do jednego konta pocztowego |
| **Folders** | Ogranicz do jednego folderu za pomocą drzewa folderów |
| **Search** | Uruchom zapytanie |
| **Reset** | Wyczyść wszystkie filtry |

Termin wyszukiwania dopasowuje **temat, treść, nadawcę i odbiorców**
(`From`, `To`, `Cc`, `Bcc`).

### Drzewo folderów {#the-folder-tree}

Foldery są wyświetlane jako **hierarchiczne drzewo** po lewej stronie, odzwierciedlające
strukturę folderów źródłowej skrzynki pocztowej — w tym foldery zagnieżdżone. Kliknij folder,
aby ograniczyć do niego wyszukiwanie, i użyj kontrolki zwijania/rozwijania, aby schować
drzewo, gdy potrzebujesz miejsca na ekranie.

Wybór folderu nie zmienia terminu wyszukiwania; oba elementy łączą się ze sobą.

## 🧩 Składnia zapytań {#query-syntax}

Składnia jest celowo uproszczona. Poniżej znajduje się wszystko, co aplikacja
faktycznie rozumie.

### Zwykłe słowa — i dlaczego prefiksy działają {#plain-words-and-why-prefixes-work}

Wpisz jedno lub więcej słów; wszystkie muszą być obecne (wyszukiwanie **AND**):

```
invoice january
```

Znajduje e-maile zawierające zarówno `invoice`, jak i `january` w przeszukiwanych polach.

> **💡 Częściowe słowa są dopasowywane automatycznie.** Każde słowo jest traktowane jako
> **prefiks**, więc wyszukiwanie `isenb` znajduje również `isenboeck` i `isenböck`, a
> `rech` znajduje zarówno `Rechnung`, jak i `Rechnungen`. Nie musisz wpisywać znaku wieloznacznego
> — nie ma go w składni. Oznacza to również, że bardzo krótki termin może
> zwrócić bardzo wiele wyników; zobacz [Performance](#-performance-and-large-archives).

Wyszukiwanie **nie uwzględnia wielkości liter**. `Invoice`, `invoice` i `INVOICE` to
to samo zapytanie.

### Dokładne frazy — cudzysłowy {#exact-phrases-quotes}

Umieść tekst w podwójnych cudzysłowach, aby wymagać słów **obok siebie** w tej
kolejności:

```
"important meeting"
```

Bez cudzysłowu, `important meeting` dopasowałoby również e-mail, w którym dwa
słowa występują w niezwiązanych ze sobą zdaniach. Z cudzysłowem dopasowana zostanie
tylko sąsiadująca fraza.

### Wyszukiwanie w konkretnych polach {#field-specific-search}

Dodaj nazwę pola i dwukropek przed terminem, aby przeszukiwać **tylko** to pole:

| Składnia | Przeszukuje w |
|---|---|
| `subject:invoice` | Tylko temat (Subject) |
| `from:john` | Tylko nadawca (Sender) |
| `to:example.com` | Tylko odbiorcy (Recipients) |
| `body:important` | Tylko treść (Body) |

Nazwy pól nie uwzględniają wielkości liter (`Subject:` również działa), a każde z nich można
połączyć z cudzysłowem dla frazy:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Obsługiwane są cztery pola:** `subject`, `body`, `from`, `to`. Wpisanie
> `cc:someone` lub `bcc:someone` **nie jest** wyszukiwaniem pola — termin jest traktowany
> jako zwykły tekst i przeszukiwany we wszystkich polach. Aby znaleźć odbiorcę, który
> był tylko w Cc, wyszukaj adres lub nazwisko bez prefiksu.

### Łączenie wszystkiego {#combining-everything}

Filtry i terminy sumują się. To zapytanie znajduje e-maile od dowolnego ubezpieczyciela,
z frazą "car insurance" w treści oraz "invoice" w
temacie:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Praktyczne kombinacje:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Znaki, które są ignorowane {#characters-that-are-ignored}

Parser zapytań usuwa znaki `& | ! ( ) : *` z poszczególnych terminów,
ponieważ mają one znaczenie w silniku wyszukiwania pełnotekstowego. Jeśli Twoje wyszukiwanie
nie zwraca niczego oczekiwanego, sprawdź, czy powodem nie jest znak specjalny.

## 📊 Sortowanie i stronicowanie {#sorting-and-paging}

### Sortowanie {#sorting}

Kliknij nagłówek kolumny, aby posortować. Kolumny sortowalne:

| Kolumna | Sortuje według |
|---|---|
| **Subject** | Linia tematu, alfabetycznie |
| **From** | Nadawca |
| **To** | Odbiorcy |
| **Date** | Data wysłania |

Ponowne kliknięcie tego samego nagłówka odwraca kierunek. Domyślnie jest to **od najnowszych**.

### Rozmiar strony {#page-size}

Wybierz, ile wyników ma być wyświetlanych na stronie:

**20** (domyślnie) · **50** · **75** · **100** · **150**

Większa strona pokazuje więcej kontekstu, ale jej renderowanie trwa dłużej. Użyj
kontrolki **Previous** / **Next**, aby przechodzić przez strony wyników; liczba wyników
informuje Cię, ile e-maili dopasowano łącznie.

> **ℹ️ Twoja strefa czasowa ma zastosowanie do wyświetlanych dat.** Wyświetlane czasy są
> konwertowane z UTC na Twoją skonfigurowaną strefę czasową, więc data, którą widzisz, odpowiada
> Twojemu lokalnemu kalendarzowi — jest to przydatne podczas wyszukiwania w okolicach północy.

## 👁️ Praca z wynikami {#working-with-results}

Każdy wiersz pokazuje konto, nadawcę, temat, datę oraz **📎 ikonę spinacza**,
gdy e-mail ma załączniki. Spinacz jest tylko wskaźnikiem — nie jest
filtrem.

Kliknij **Details**, aby otworzyć e-mail:

- Pełna treść (HTML lub tekst czysty)
- **Attachments**, wymienione z opcją pobrania każdego z nich
- **Download as EML** — kompletna wiadomość, do użycia gdzie indziej
- **Restore** — skopiowanie e-maila z powrotem do aktywnej skrzynki pocztowej

Aby przywrócić e-maile do skrzynki pocztowej, zobacz [Mailbox Migration](MailboxMigration.md).

## ✅ Działanie na wielu wynikach jednocześnie {#acting-on-many-results-at-once}

Wyniki mogą być wybierane i przetwarzane masowo. Włącz **Selection Mode**, aby uzyskać
polami wyboru, a następnie:

- **Select All** / **Deselect All** dla bieżącej strony
- **Select all {n} matching** — każdy wynik bieżącego wyszukiwania we wszystkich
  stronach (wyświetlane, gdy wyniki rozciągają się na więcej niż jedną stronę)
- **Copy Selected** — przywracanie do skrzynki pocztowej
- **Export Selected** — jako MBOX lub EML
- **Delete Selected** — z Archiwum

Licznik pokazuje *"{selected} z {total} wybranych"* podczas pracy.

### Limit wyboru {#the-selection-limit}

> **⚠️ Można wybrać maksymalnie 250 e-maili naraz** (konfigurowalne poprzez
> `Selection:MaxSelectableEmails`). Powyżej tej liczby interfejs wyświetla *"Maximum
> selections reached (250)"* oraz **Too Many — Split Required**.

Limit ten istnieje, ponieważ każda operacja musi trzymać dotknięte wiadomości.
Komunikat *"Split larger operations into multiple batches"* to sugerowane rozwiązanie:
pracuj nad wynikami w grupach po 250 lub mniej.

**Select all {n} matching** przestrzega tego samego limitu. Gdy wyszukiwanie dopasuje więcej
e-maili niż pozwala limit, wybiera pierwsze z bieżącego sortowania
i wyświetla komunikat *"Maximum selections reached"*. Zawęź wyszukiwanie (zakres dat,
folder, konto), aby dotrzeć do reszty.

Próba przekroczenia limitu wyświetla: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Usuwanie wszystkich dopasowań filtra {#deleting-every-match-of-a-filter}

Przycisk **Delete all matches** obok *Reset* usuwa każdy e-mail, który
pasuje do bieżącego wyszukiwania, a nie tylko bieżącą stronę lub wybór. Jest on dostępny dla administratorów i menedżerów i działa jako zadanie w tle, więc
limit 250 e-maili nie ma zastosowania.

Okno potwierdzenia pokazuje, ile e-maili można usunąć, a ile jest zablokowanych przez politykę Przechowywania. Musisz wpisać tę liczbę, aby umożliwić
potwierdzenie. Serwer przelicza ponownie i przerywa działanie, jeśli zestaw wyników zmienił się w międzyczasie, na przykład dlatego, że synchronizacja dodała maila.

Zablokowane e-maile nigdy nie są usuwane przez tę akcję; są zgłaszane jako pominięte w
statusie zadania. `DeletionPolicy:MaxBulkDeleteEmails` (domyślnie 100 000) ogranicza jedno
zadanie. Filtr, który dopasowuje więcej e-maili, zostaje odrzucony z prośbą o jego zawężenie.

Zadanie pojawia się na stronie [Background Jobs](BackgroundJobs.md) i można je tam
anulować.

### Przetwarzanie bezpośrednie vs zadanie w tle {#direct-processing-vs-background-job}

W przypadku **Copy** i **Export** wybierasz, jak przebiega praca:

| Opcja | Zachowanie |
|---|---|
| **Process on this page (wait for completion)** | Przeglądarka czeka; dobre dla kilku e-maili |
| **Process in background (monitor separately)** | Działa jako zadanie; możesz zamknąć przeglądarkę |

Eksporty zawsze działają w tle:

> *"Eksport zostanie uruchomiony w tle, a po zakończeniu będziesz mógł pobrać
> wynik ze strony Jobs."*

Trwające i zakończone zadania znajdziesz na stronie [Background Jobs](BackgroundJobs.md).

> **⚠️ Usuwanie jest trwałe.** Potwierdzenie jest jednoznaczne: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Usunięcia są rejestrowane
> w dzienniku dostępu.

## ⚡ Wydajność i duże Archiwa {#performance-and-large-archives}

Wyszukiwanie opiera się na indeksie pełnotekstowym PostgreSQL, więc różnica między
szybkim a wolnym wyszukiwaniem zazwyczaj zależy od tego, jak **wybiórcze** jest zapytanie.

**Rób:**

- Dodaj **zakres dat**, gdy wiesz mniej więcej, kiedy e-mail dotarł — jest to
  najskuteczniejszy sposób zawężenia wyników
- Ogranicz do **konta** lub **folderu**, gdy wiesz, gdzie znajduje się mail
- Używaj **kilku słów** zamiast jednego bardzo krótkiego
- Używaj wyszukiwania w polach (`subject:`, `from:`), aby wyeliminować nieistotne dopasowania

**Unikaj:**

- Pojedynczego krótkiego prefiksu, takiego jak `re` — jako wyszukiwanie prefiksowe pasuje do ogromnej
  części Twojego archiwum
- Bardzo szerokich zakresów dat połączonych ze wspólnym słowem
- Przeglądania wielu stron, gdy zamiast tego mógłbyś zawęzić wyszukiwanie

> **💡 Jeśli wyszukiwanie wydaje się wolne, rozwiązaniem jest zazwyczaj zakres dat.** Ograniczenie
> do miesiąca często zmienia wielosekundowe wyszukiwanie w natychmiastowe, ponieważ
> indeks może pominąć większość archiwum.

## ❓ Rozwiązywanie problemów {#troubleshooting}

| Objaw | Przyczyna | Co zrobić |
|---|---|---|
| Brak wyników, mimo że ich oczekujesz | Zbyt specyficzny termin lub literówka | Spróbuj mniej słów; sprawdź pisownię |
| `cc:name` nie znajduje nic użytecznego | `cc` nie jest obsługiwanym polem | Wyszukaj nazwę bez prefiksu |
| Znak specjalny psuje zapytanie | `& \| ! ( ) : *` są usuwane | Usuń lub zastąp znak |
| Zbyt wiele wyników | Wspólne słowo lub krótki prefiks | Dodaj zakres dat lub wyszukiwanie w polach |
| *"Too Many — Split Required"* | Wybrano więcej niż 250 e-maili | Zmniejsz wybór lub podziel operację |
| Brakuje eksportu | Działa jako zadanie w tle | Sprawdź [Background Jobs](BackgroundJobs.md) |
| Wyniki wyglądają na brakujące | Uprawnienia konta | Potwierdź, że skrzynka pocztowa jest przypisana do Twojego użytkownika |
| Daty wydają się przesunięte | Konwersja strefy czasowej | Sprawdź ustawienia strefy czasowej |

## 📚 Powiązana dokumentacja {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — przywracanie wyników do skrzynki pocztowej
- [Background Jobs](BackgroundJobs.md) — monitorowanie eksportów i przywracania
- [Access Logging](Logs.md) — ścieżka audytu wyszukiwań i usunięć
- [REST API](API.md) — to samo archiwum przez API
- [MCP Server](MCP.md) — wyszukiwanie za pomocą agenta AI
- [Retention Policies](RetentionPolicies.md) — kiedy e-maile opuszczają archiwum
