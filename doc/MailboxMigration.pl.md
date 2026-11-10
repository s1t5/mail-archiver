# 📥 Przewodnik po migracji Skrzynki pocztowej {#mailbox-migration-guide}

[← Powrót do Indeksu Dokumentacji](index.md)

Niniejszy przewodnik wyjaśnia, jak przenieść Skrzynkę pocztową z jednego dostawcy poczty e-mail do drugiego za pomocą aplikacji Mail Archiver.

## 📋 Przegląd {#overview}

Aplikacja Mail Archiver zapewnia funkcjonalność umożliwiającą migrację wiadomości e-mail ze źródłowej Skrzynki pocztowej do docelowej Skrzynki pocztowej. Jest to szczególnie przydatne podczas zmiany dostawcy poczty e-mail przy jednoczesnym zachowaniu historii wiadomości. Struktura Folderów źródłowej Skrzynki pocztowej jest również zachowana podczas migracji.

> **Migrujesz tylko niedawne wiadomości?** Opisany poniżej proces kopiuje *całą* zarchiwizowaną Skrzynkę pocztową, a ponowne jego uruchomienie spowoduje dodanie wszystkiego po raz drugi. Jeśli nowa Skrzynka pocztowa ma zawierać tylko wiadomości nowsze niż określony termin, podczas gdy pełna historia pozostaje w Archiwum, użyj zamiast tego [date-windowed offload](Offload.md). Przyjmuje on przedział dat, może wykluczać i zmieniać nazwy Folderów, oferuje tryb testowy (dry run) i jest bezpieczny do powtarzania, ponieważ pomija to, co docelowa Skrzynka pocztowa już zawiera. Można go również uruchamiać z linii komend, co pozwala na automatyzację obsługi floty Skrzynek pocztowych.

## 🚀 Proces migracji {#migration-process}

Wykonaj poniższe kroki, aby przeprowadzić migrację Skrzynki pocztowej:

1. **Dodaj źródłowe Konto**
   - Przejdź do sekcji "Email Accounts"
   - Kliknij "Add Account"
   - Wprowadź szczegóły i dane uwierzytelniające swojego źródłowego Konta
   - Zapisz Konto

2. **Zsynchronizuj źródłowe Konto**
   - Pozwól aplikacji na przeprowadzenie Synchronizacji wszystkich wiadomości e-mail ze źródłowego Konta
   - Poczekaj na zakończenie procesu Synchronizacji

3. **Dodaj docelowe Konto**
   - W sekcji "Email Accounts" kliknij "Add Account"
   - Wprowadź szczegóły i dane uwierzytelniające swojego docelowego Konta
   - Zapisz Konto

4. **Zsynchronizuj docelowe Konto**
   - Jeśli docelowe Konto jest nowe, przeprowadź Synchronizację, aby upewnić się, że zostało poprawnie skonfigurowane
   - Poczekaj na zakończenie procesu Synchronizacji

5. **Uruchom migrację**
   - W sekcji "Email Accounts" zidentyfikuj źródłowe Konto
   - Przejdź do szczegółów Konta
   - Wybierz opcję "Copy All Emails to Another Mailbox"
   - Wybierz docelowe Konto oraz docelowy Folder
   - Włącz opcję **"Preserve Folder Structure"** – wybrany docelowy Folder będzie wówczas służył jako główny katalog, w którym cała źródłowa struktura Folderów zostanie odtworzona w sposób rekurencyjny
   - Jeśli ta opcja nie zostanie włączona, wszystkie wiadomości e-mail zostaną umieszczone bezpośrednio w wybranym docelowym Folderze (płasko, bez podfolderów)
   - Rozpocznij proces migracji

6. **Monitoruj migrację**
   - Jeśli liczba wiadomości e-mail jest duża, migracja będzie realizowana jako zadanie działające w tle
   - Status i postęp migracji można sprawdzić w zakładce "Jobs"
   - Poczekaj na pomyślne zakończenie migracji

7. **Wyłącz źródłowe Konto**
   - Po pomyślnym transferze wróć do źródłowego Konta
   - Ustaw źródłowe Konto na "Disabled" w ustawieniach Konta
   - Zapewni to, że w przyszłości nie będzie ono poddawane procesowi Archiwizacji

## ⚠️ Ważne uwagi {#important-notes}

- Ten proces nie jest powtarzalny: ponowne jego uruchomienie spowoduje dodanie każdej wiadomości po raz drugi. Powtarzalną alternatywą jest [date-windowed offload](Offload.md).
- Upewnij się, że źródłowe Konto posiada wystarczające Uprawnienia do odczytu wszystkich wiadomości e-mail
- Upewnij się, że docelowe Konto posiada wystarczającą przestrzeń dyskową na zmigrowane wiadomości e-mail
- Proces migracji może zająć trochę czasu, w zależności od liczby wiadomości e-mail i prędkości połączenia
- Zaleca się przeprowadzanie migracji w godzinach mniejszego obciążenia, aby zminimalizować wpływ na usługę pocztową
- Regularnie sprawdzaj status migracji w zakładce "Jobs", aby upewnić się, że proces postępuje
- Po migracji zweryfikuj, czy wszystkie wiadomości e-mail zostały pomyślnie przeniesione do docelowego Konta

## ✅ Dobre praktyki {#best-practices}

- Zawsze testuj proces migracji na małym podzbiorze wiadomości e-mail przed migracją całej Skrzynki pocztowej
- Zachowaj Kopię zapasową ważnych wiadomości e-mail przed rozpoczęciem migracji
- Zaplanuj migrację na czas, w którym korzystanie z poczty e-mail jest minimalne
- Ściśle monitoruj postęp migracji i bądź przygotowany na rozwiązanie wszelkich problemów, które mogą wystąpić
- Po zakończeniu migracji zweryfikuj, czy wszystkie wiadomości e-mail zostały poprawnie przeniesione i są dostępne
