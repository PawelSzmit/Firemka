# Review fazy 7 — obliczenia i zamknięcie miesiąca

Data: 2026-09-12  
Status: **WYMAGA POPRAWEK PRZED FAZĄ 8**

## Znaleziska

### P2 — nieobsługiwany profil VAT lub ZUS nie blokuje zamknięcia

Projekt fazy 7 ogranicza kalkulator do skali podatkowej, miesięcznego czynnego VAT oraz profilu ZUS „tylko zdrowotna z powodu etatu”. `MonthClosingService.BuildAsync` sprawdza jednak wyłącznie formę PIT. Nie wczytuje okresów VAT/ZUS i nie dodaje blokady, gdy właściciel zmieni profil na `VatExempt` albo `SocialAndHealthContributions`. W takim stanie aplikacja może zamknąć miesiąc przy użyciu wzorów przeznaczonych dla innego profilu.

**Wymagana naprawa:** wczytać okresowe profile firmy, dodać stabilne blokady dla nieobsługiwanego VAT i ZUS, objąć oba przypadki testami oraz pokazać użytkownikowi drogę do ustawień.

### P2 — konflikt źródła KSeF w zaksięgowanym dokumencie nie blokuje zamknięcia

Faza 4 celowo zachowuje flagę `HasSourceConflict` niezależnie od końcowego statusu dokumentu. Faza 7 wybiera do blokad wyłącznie dokumenty, których status nie jest `Booked` ani `UnrelatedToBusiness`. Zaksięgowany dokument z wykrytym później konfliktem KSeF zostaje więc pominięty i miesiąc może być zamknięty mimo nierozwiązanego problemu integralności źródła. Taki konflikt nie uczestniczy też w odcisku zamknięcia, więc wykrycie go po zamknięciu nie uruchamia korekty.

**Wymagana naprawa:** traktować zaksięgowany dokument z `HasSourceConflict` jako nierozwiązany, uwzględnić go w odcisku wejścia i dodać test zarówno przed zamknięciem, jak i po zamknięciu.

### P2 — dwie różne równoczesne korekty mogą wspólnie obniżyć sumę poniżej zera

Limit korekt jest sprawdzany przed zapisem bez transakcji o izolacji szeregowej. Dwa żądania mogą równocześnie odczytać ten sam stan, każde osobno przejść sprawdzenie, a następnie zapisać różne korekty. Unikalny odcisk chroni tylko przed identycznym powtórzeniem, nie przed dwiema różnymi kwotami. W efekcie łączny przychód, koszt albo VAT może stać się ujemny, mimo że pojedyncza ścieżka tego zabrania.

**Wymagana naprawa:** wykonać walidację i zapis korekty w transakcji szeregowej, rozpoznać konflikt równoległy i zwrócić czytelny komunikat. Dodać wymuszony test wyścigu na PostgreSQL, który potwierdzi, że przechodzi najwyżej jedna z dwóch konkurencyjnych korekt.

### P2 — dowolny rodzaj korekty może pozornie rozwiązać spóźnioną fakturę sprzedaży

Blokada spóźnionej faktury znika po znalezieniu jakiejkolwiek korekty z jej identyfikatorem. Nie jest sprawdzane, czy korekta dotyczy rozpoznania przychodu. Można więc powiązać z fakturą np. korektę dochodu zdrowotnego albo VAT naliczonego i zamknąć miesiąc, choć decyzja o przychodzie nadal nie istnieje. Obecny test używa dodatkowego `1 zł`, które zarazem sztucznie zmienia przychód już ujęty w pełnej kwocie.

**Wymagana naprawa:** dodać jawny, bezkwotowy typ potwierdzenia okresu rozpoznania sprzedaży, wymagający powiązanej faktury i dowodu. Tylko taki wpis ma usuwać blokadę i nie może zmieniać PIT, VAT ani zdrowotnej. Inne korekty z identyfikatorem faktury nie mogą udawać tej decyzji.

### P3 — zapytanie o nierozwiązane dokumenty pobiera całą historię właściciela

`BuildAsync` pobiera wszystkie nierozwiązane dokumenty właściciela, a dopiero potem filtruje miesiąc w pamięci. Z czasem każdy widok miesiąca będzie czytał coraz większą historię, mimo że potrzebny jest tylko jeden okres.

**Wymagana naprawa:** ograniczyć zapytanie w bazie do dat dokumentów danego miesiąca, z prawidłowym przedziałem UTC dla rekordów bez daty dokumentu.

## Bezpieczeństwo i izolacja danych

- Wszystkie strony fazy 7 wymagają zalogowania, a serwis sprawdza właściciela firmy przed zapisem.
- Formularze Razor Pages korzystają z ochrony anty-CSRF; komunikaty i treści dowodów są kodowane przy wyświetlaniu.
- Nie znaleziono drogi do odczytu lub zmiany danych innego właściciela.
- Znaleziony konflikt źródła jest problemem integralności księgowej, a nie wyciekiem danych.

## Architektura, scenariusze i dowody

- Niezmienne wersje deklaracji, kalkulacji i zamknięć są rozdzielone prawidłowo; korekta nie nadpisuje poprzedniego zamknięcia.
- Zamknięcie kalkulacji i wersji miesiąca działa w jednej transakcji, a kontrolowana awaria wycofuje całość.
- Wcześniejszy pełny gate przechodził 214 testów, kompilację Release, formatowanie, kontrolę modelu EF, skan podatności, walidację czterech XML oraz izolowany Compose i odbiór przeglądarkowy.
- Review wykryło luki scenariuszowe, których istniejące testy nie obejmowały: zmianę profilu VAT/ZUS, konflikt KSeF po zaksięgowaniu, dwie różne równoczesne korekty oraz błędny typ wpisu przypięty do spóźnionej sprzedaży.

## Decyzja bramki

Wynik: **0× P1, 4× P2, 1× P3**. Faza 8 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do powyższych pięciu napraw, następnie ponowne `dev-docs-review` fazy 7. Nie wykonano commita, pushu ani wdrożenia.
