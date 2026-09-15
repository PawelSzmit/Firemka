# Ponowne review fazy 7 — po pierwszej naprawie

Data: 2026-09-13  
Status: **WYMAGA JESZCZE JEDNEJ POPRAWKI PRZED FAZĄ 8**

## Wynik sprawdzenia poprzednich znalezisk

Wszystkie pięć punktów z `review-faza-7.md` zostało zamkniętych:

- nieobsługiwane profile VAT i ZUS mają osobne blokady zamknięcia i linki do ustawień;
- zaksięgowany dokument z konfliktem źródła uczestniczy w blokadach i odcisku danych;
- zapis korekty wraz z kontrolą nieujemnej sumy działa w transakcji szeregowej, a wymuszony wyścig na PostgreSQL przepuszcza najwyżej jedną korektę;
- spóźnioną sprzedaż rozwiązuje tylko bezkwotowy wpis `SalesRecognition`;
- dokumenty miesiąca są filtrowane po stronie bazy, z warszawskimi granicami UTC dla rekordów bez daty dokumentu.

Pełny zestaw po tych naprawach ma 218 zielonych testów. Kompilacja Release, formatowanie, zgodność modelu EF z migracjami, skan podatności i cztery próbki XML również są czyste.

## Nowe znalezienie

### P2 — konflikt źródła dokumentu tworzy trwałą blokadę bez ścieżki rozwiązania

Po wykryciu innej treści KSeF pod tym samym numerem dokument otrzymuje `HasSourceConflict = true`. Poprawnie blokuje to zamknięcie miesiąca, również dla dokumentu już zaksięgowanego. Domena, serwis i ekran dokumentu nie mają jednak żadnej czynności pozwalającej właścicielowi zapisać wyjaśnienie i oznaczyć konflikt jako rozwiązany. Flagi nie da się usunąć przez zmianę zwykłego statusu dokumentu. Taki miesiąc pozostaje więc niemożliwy do zamknięcia lub skorygowania na zawsze.

**Wymagana naprawa:** zachować informację, że konflikt wystąpił, ale dodać osobny stan rozwiązania z datą i obowiązkowym wyjaśnieniem. Właściciel musi móc wykonać tę czynność na stronie własnego dokumentu. Nowa różnica źródła po wcześniejszym rozwiązaniu ma ponownie otwierać konflikt. Blokada miesiąca powinna dotyczyć tylko konfliktu nierozwiązanego. Całość wymaga testów domeny, izolacji właściciela, serwisu, strony i migracji PostgreSQL.

## Bezpieczeństwo i historia

- Rozwiązanie nie może kasować flagi ani daty wykrycia konfliktu i nie może nadpisywać pliku źródłowego.
- Wyjaśnienie ma pozostać w dokumencie oraz śladzie audytowym, bez kopiowania jego treści do metadanych audytu.
- Czynność musi być ograniczona do zalogowanego właściciela dokumentu i chroniona standardowym formularzem anty-CSRF.
- Powtórne wykrycie konfliktu ma wyczyścić wyłącznie poprzedni stan rozwiązania i ponownie wymagać decyzji.

## Decyzja bramki

Wynik ponownego review: **0× P1, 1× P2, 0× P3**. Faza 8 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do audytowalnej ścieżki rozwiązania konfliktu, a potem kolejne `dev-docs-review` fazy 7. Nie wykonano commita, pushu ani wdrożenia.
