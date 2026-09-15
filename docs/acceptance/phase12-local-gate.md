# Dowód lokalnej bramki Fazy 12

Data: 2026-09-15  
Zakres: wyłącznie dane syntetyczne i odizolowane kontenery lokalne  
Wynik techniczny: **ZALICZONY**  
Zgoda na pilotaż/produkcję: **NIE — wymaga osobno podpisanej checklisty**

## Co zostało sprawdzone

- Przywrócenie zależności w trybie blokowanym, formatowanie, kompilacja Release z ostrzeżeniami traktowanymi jak błędy oraz zgodność modelu bazy z migracjami.
- Pełny zestaw 311 testów bez pominięć: 6 kontraktowych, 90 domenowych, 5 aplikacyjnych, 161 infrastruktury, 3 pełnego przepływu i 46 WWW.
- Wszystkie 12 scenariuszy PostgreSQL, w tym migracje, równoległość, kopia i odtworzenie, na osobnym PostgreSQL 16 zgodnym z lokalnym narzędziem `pg_dump`.
- Cztery zapisane kontrakty XML, składnia skryptów, szablon `launchd`, konfiguracja Compose oraz brak podatnych pakietów zgłaszanych przez skan NuGet.
- Budowa obrazów WWW, workera, Caddy i PostgreSQL z przypiętych obrazów bazowych. Docker Scout zgłosił dla każdego obrazu `0C / 0H`, czyli zero podatności krytycznych i wysokich.
- Obrazy działają bez konta administratora. WWW, worker i Caddy mają system plików tylko do odczytu, kontrolowaną przestrzeń tymczasową, ograniczenia procesów/pamięci/CPU, usunięte zbędne uprawnienia i zakaz uzyskiwania nowych przywilejów.
- Pełna produkcyjna konfiguracja uruchomiła PostgreSQL 18.6, migrację zakończoną kodem 0, zdrowe WWW i worker. Bezpośredni `/health` zwrócił `Healthy`.
- Caddy 2.11.4 uruchomił się w tych samych ograniczeniach, wystawił lokalne HTTPS i przekazał `/health` do WWW z wynikiem `Healthy`. Próba używała wyłącznie tymczasowych portów lokalnych.
- Syntetyczny pełny przepływ tworzy konto, konfiguruje TOTP, kończy pierwszą sesję, loguje się ponownie hasłem i TOTP, a następnie przechodzi przez firmę, fakturę, koszt, zamknięcie miesiąca, trzy pliki urzędowe, płatność i potwierdzoną kopię.
- Osobna świeża próba przykładu ładowania potwierdziła przeliczenie 10 000 Wh na 10 kWh i 9,10 zł przy stawce 0,91 zł/kWh. Wynik pozostaje oznaczony jako niepotwierdzona podstawa podatkowa, a błędny import nie tworzy wpisu KPiR, VAT ani kosztu.
- Osobna świeża próba reguł kosztowych potwierdziła, że pierwszy dokument wymaga decyzji, kolejny dokładnie zgodny przypadek księguje się raz, a zmiana kraju lub innego pola podatkowego zatrzymuje automat i pokazuje różnicę do sprawdzenia.
- Osobna świeża próba zamknięcia miesiąca potwierdziła trzy stany: braki blokują bez częściowego zapisu, komplet danych tworzy jedno zamknięcie również po ponowieniu, a późniejsza zmiana wymaga nowej wersji korekty z zachowaniem poprzedniej.

## Identyfikatory obrazów bramki

| Obraz | Identyfikator |
|---|---|
| WWW | `sha256:1e4434e9cf28a00f544e4b04f95c1b446413876ca32a7cdf85648e64f9b14c30` |
| Worker | `sha256:e0ed2b49d505bb286913a863e91970f55058da5c026e5294cf7319224d9d0387` |
| Caddy | `sha256:5f2f5d2f6e1e35bbe6adf0f4904c4dd818b8dd368e51e39e8dd836602f544b23` |
| PostgreSQL | `sha256:b9567f0c59fcdd7423dedc0ca57dde170862dd4f3e7a75e0884dff5d03d95e7e` |

Te identyfikatory dokumentują wyłącznie lokalną próbę. Wydanie na VPS musi otrzymać własny, unikalny numer wersji i ponownie zapisać rzeczywiście uruchomione identyfikatory.

## Ważne ograniczenie narzędzi testowych

Lokalne `pg_dump` ma wersję 16, dlatego pełne testy kopii uruchomiono na PostgreSQL 16. Produkcyjne obrazy WWW i workera zawierają `pg_dump` 18.6, a osobna próba całego zestawu potwierdziła ich współpracę z docelowym PostgreSQL 18.6. To rozdzielenie zapobiega fałszywej porażce wynikającej wyłącznie z zakazu użycia starszego `pg_dump` wobec nowszego serwera.

## Czego ten dowód nie potwierdza

Nie użyto prawdziwego VPS, publicznej domeny, rzeczywistego telefonu/Maca, dostępu KSeF, skrzynki SMTP, dokumentów firmy ani narzędzi urzędowych. Nie wykonano pełnego odtworzenia na drugim urządzeniu. Te próby pozostają puste w `phase12-pilot-checklist.md` i blokują zgodę na pilotaż oraz produkcję.
