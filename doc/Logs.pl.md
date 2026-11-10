# 📋 Dziennik dostępu {#access-log}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia funkcjonalność dziennika dostępu w aplikacji Mail Archiver, która umożliwia logowanie i przeglądanie aktywności użytkowników w systemie. Funkcjonalność ta jest kluczowa w celach monitorowania, bezpieczeństwa oraz debugowania.

## 📊 Co jest logowane {#what-is-logged}

Aplikacja loguje różne rodzaje aktywności użytkowników, w tym:

1. **Login i Logout** - Logowanie operacji logowania i wylogowania
2. **Operacje wyszukiwania** - Logowanie żądań wyszukiwania e-maili w systemie
3. **Otwieranie e-maili** - Logowanie, gdy użytkownicy otwierają treść e-maili
4. **Pobieranie e-maili** - Logowanie, gdy użytkownicy pobierają e-maile
5. **Przywracanie e-maili** - Logowanie, gdy użytkownicy przywracają zarchiwizowane e-maile
6. **Zarządzanie kontem** - Logowanie działań związanych z kontem
7. **Operacje usuwania** - Logowanie działań usuwania
8. **Eksport danych audytowych** - Logowanie uruchomień eksportu danych audytowych (początek i wynik, patrz [Audit Data Export](AuditExport.md))

## 📝 Typy działań i opisy {#action-types-and-descriptions}

Logowane działania są podzielone na następujące typy:

### Login {#login}
- **Opis**: Loguje, gdy użytkownik loguje się do systemu
- **Szczegóły**: Nazwa użytkownika, znacznik czasu

### Logout {#logout}
- **Opis**: Loguje, gdy użytkownik wylogowuje się z systemu
- **Szczegóły**: Nazwa użytkownika, znacznik czasu

### Search {#search}
- **Opis**: Loguje żądania wyszukiwania w systemie e-mail
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, parametry wyszukiwania

### Open {#open}
- **Opis**: Loguje, gdy użytkownik otwiera e-mail
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, email ID, nadawca, temat

### Download {#download}
- **Opis**: Loguje, gdy użytkownicy pobierają e-maile
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, email ID, nadawca, temat

### Restore {#restore}
- **Opis**: Loguje, gdy użytkownicy przywracają zarchiwizowane e-maile
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, email ID, nadawca, temat

### Account {#account}
- **Opis**: Loguje działania związane z kontem (tworzenie, modyfikacja itp.)
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, account ID

### Deletion {#deletion}
- **Opis**: Loguje operacje usuwania e-maili lub innych zasobów
- **Szczegóły**: Nazwa użytkownika, znacznik czasu, dotknięty zasób
- **Uwaga dotycząca zgodności**: Operacje usuwania są logowane w celach audytu zgodności

## 🔍 Opcje filtrowania {#filter-options}

Strona dziennika oferuje następujące opcje filtrowania:

1. **Data** - Filtrowanie według zakresu dat (Od-Do)
2. **Nazwa użytkownika** - Filtrowanie według konkretnego użytkownika (tylko dla Admin)
3. **Typ działania** - Filtrowanie według konkretnego typu działania

## 🖥️ Wyświetlanie dziennika {#log-display}

Logi są wyświetlane w formacie tabeli z następującymi kolumnami:

- **Timestamp**: Data i godzina działania
- **Username**: Nazwa zaangażowanego użytkownika
- **Type**: Typ działania z kodowaniem kolorystycznym
- **Information**: Dodatkowe szczegóły dotyczące działania

Dla urządzeń mobilnych logi są wyświetlane w formacie kart.

## 🔐 Uprawnienia dostępu {#access-permissions}

- **Użytkownicy bez uprawnień admina**: Mogą przeglądać tylko własne logi
- **Użytkownicy admin**: Mogą przeglądać logi wszystkich użytkowników i filtrować według użytkownika

## 📤 Eksport danych audytowych {#audit-data-export}

Administratorzy mogą otworzyć dedykowaną stronę [Audit Data Export](AuditExport.md) bezpośrednio ze strony Logów. Tworzy ona tabelaryczne pakiety masowych danych (INDEX.XML + CSV) z Archiwum dla zewnętrznych narzędzi audytowych. Każde uruchomienie eksportu zapisuje dwa wpisy w dzienniku dostępu typu **Audit Data Export** (początek i wynik), które są wymienione i mogą być filtrowane na stronie Logów, podobnie jak wszystkie inne wpisy.

Szczegóły znajdują się w [przewodniku Audit Data Export](AuditExport.md).
