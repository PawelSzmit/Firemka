# Review fazy 6 — reguły kosztów, samochód i ładowanie

Data: 2026-09-12  
Status: **GOTOWE DO KONTYNUACJI W BEZPIECZNYM ZAKRESIE TECHNICZNYM**

## Znaleziska

Nie znaleziono nowych P1 ani P2 w zaimplementowanym zakresie fazy 6.

Obserwacja P3: na stronie ładowania skrypt walidacji formularzy próbuje ukryć pustą listę podsumowania przez styl inline, który jest blokowany przez CSP. Nie zmienia to walidacji serwerowej, importu ani poprawności danych; pozostaje do ewentualnego uporządkowania przy wspólnym porządkowaniu walidacji stron.

Pozostają otwarte dane zewnętrzne: niezależnie potwierdzone zasady podatkowe, dokumenty samochodu oraz podstawa księgowania domowego ładowania. Aplikacja nie włącza na tej podstawie żadnych domyślnych stawek ani automatycznego wpisu podatkowego.

## Zakres i zgodność z planem

- Reguła kosztu powstaje dopiero po decyzji właściciela. Dopasowanie wymaga zgodności sprzedawcy, kraju, waluty, profilu i stawki VAT oraz rodzaju usługi; kwota i data nie zmieniają odcisku dokumentu.
- Decyzja „tylko ten dokument” jest oddzielona od „zastosuj także do kolejnych”. Wersje reguł, wpisów KPiR i VAT oraz okresy pozostają jawne i niezmienne po zapisie.
- Cztery kategorie kosztów samochodu mają osobne, wersjonowane polityki. Aktywacja wymaga obu procentów oraz referencji do dowodu; brak danych pozostawia politykę oczekującą.
- Profil ładowarki wymaga osobnego potwierdzenia jednostki Wh. Domyślny profil odwzorowuje przekazany eksport: separator `,`, `started_at`, `energy [Wh]`, format `yyyy-MM-dd HH:mm:ss`, strefę `Europe/Warsaw` i `session_id` jako identyfikator.
- Parser sprawdza cały plik przed zapisem, zachowuje dodatkowe kolumny jako dopuszczone dane wejściowe, odrzuca błędne daty/energię i nie wykonuje treści komórkowych. Import pliku, powtórzenia i częściowe nakładanie wierszy są idempotentne.
- Zestawienie ładowania jest niezmiennym snapshotem miesiąca i stawki. Nie tworzy `CostBooking`, KPiR ani VAT i pokazuje ostrzeżenie `Tylko zestawienie — podstawa podatkowa niepotwierdzona`.

## Dowody

- Niezależny przebieg testów zakresu fazy 6: 22 testy domeny, 25 testów infrastruktury i 4 testy stron WWW — wszystkie zielone.
- Pełny wcześniejszy gate rozwiązania: 169 zielonych testów, kompilacja Release bez ostrzeżeń i błędów, formatowanie bez zmian, brak oczekujących zmian modelu EF, brak zgłoszonych podatności zależności oraz poprawna walidacja czterech kontrolnych XML.
- Osobny test PostgreSQL fazy 6 przeszedł dla migracji, równoczesnych przygotowań i potwierdzeń kosztu, polityk samochodu, równoczesnych importów tego samego pliku, importów częściowo nakładających się oraz równoczesnych raportów.
- Przekazany plik Numbers został rozpakowany wyłącznie lokalnie do analizy odpowiadającego mu CSV. Układ zawiera 8 sesji, w tym 7 `COMPLETED` i 1 `ABORTED`; suma to `159624 Wh` (`159.624 kWh`). Do repozytorium trafiła tylko syntetyczna kopia testowa tego formatu.
- Odbiór Playwright na 390 px i 1440 px nie wykazał poziomego przewijania dla ekranów polityk pojazdu i ładowania. Zrzuty są w `output/playwright/phase6-vehicle-mobile.png`, `phase6-charging-mobile-defaults.png` i `phase6-charging-desktop.png`.

## Decyzja bramki

Wynik techniczny: **0× P1, 0× P2, 1× odroczona obserwacja P3**. Faza 6 jest gotowa do rozpoczęcia Fazy 7. Otwarta bramka podatkowa i brak rzeczywistych dokumentów samochodu/ładowania nadal ograniczają użycie produkcyjne do ręcznej decyzji właściciela i niezależnego potwierdzenia. Nie wykonano commita, pushu ani wdrożenia.
