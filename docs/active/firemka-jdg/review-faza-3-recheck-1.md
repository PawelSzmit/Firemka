# Ponowne review fazy 3 — profil firmy, okresy i „Mój miesiąc”

Data: 2026-09-10  
Status: **GOTOWE DO KONTYNUACJI**

## Znaleziska

Brak znalezisk P1, P2 i P3 w zakresie fazy 3. Wszystkie cztery P2 z `review-faza-3.md` są zamknięte.

## Weryfikacja wcześniejszych P2

### Pełny profil firmy

Adres firmy i opis usługi są wymagane przy rejestracji, należą do agregatu firmy i projekcji profilu oraz mają ograniczenia długości w modelu PostgreSQL (`src/Firemka.Domain/Companies/Company.cs`, wiersze 42–43 i 54–114; `src/Firemka.Application/Onboarding/CompanyProfileContracts.cs`, wiersze 6–37). Kreator pokazuje oba pola, a test WWW odczytuje je po trwałym zapisie (`tests/Firemka.Web.Tests/CompanyOnboardingTests.cs`, wiersze 29–73). Test PostgreSQL potwierdza ponowny odczyt z osobnego kontekstu.

### Okresy VAT, ZUS i pojazdu

Każdy z pięciu rodzajów ustawień ma osobną, typowaną historię i odczyt wartości właściwej dla miesiąca. Nowy okres musi zaczynać się pierwszego dnia miesiąca i być późniejszy od ostatniego (`src/Firemka.Domain/Companies/Company.cs`, wiersze 117–184 i 216–233). Usługi i formularze dopisują nowe rekordy zamiast nadpisywać stare; ekran pokazuje historię VAT, ZUS i samochodu. Test domeny sprawdza poprzednią i nową wartość dla wszystkich trzech kategorii (`tests/Firemka.Domain.Tests/CompanyProfileTests.cs`, wiersze 101–140), a test WWW przechodzi przez trzy formularze i potwierdza po dwa okresy (`tests/Firemka.Web.Tests/CompanyOnboardingTests.cs`, wiersze 191–244).

### Kolejność lat podatkowych

Otwarcie roku przyjmuje wyłącznie rok bezpośrednio po najpóźniejszym zapisanym roku i nadal używa wyłącznie skali podatkowej (`src/Firemka.Domain/Companies/Company.cs`, wiersze 187–200). Testy obejmują poprawny rok 2027, rok wcześniejszy 2025 i pominięty 2028; formularz pokazuje użytkownikowi poprawny kolejny rok i nie zapisuje błędnej wartości.

### Polska data biznesowa i podpowiedzi okresów

`PolishBusinessTime` jawnie używa `Europe/Warsaw`, korzysta z wstrzykiwanego `TimeProvider` i ma test granicy miesiąca 30 września o 22:30 UTC (`src/Firemka.Application/Time/PolishBusinessTime.cs`, wiersze 3–25). „Mój miesiąc” oraz ustawienia korzystają z tego samego źródła. Podpowiadany okres jest późniejszy zarówno od bieżącego miesiąca w Polsce, jak i od najpóźniejszego zapisu; przypadki ostatniego okresu w przeszłości, przyszłym miesiącu i dalszej przyszłości są pokryte testem.

## Cztery perspektywy review

- **Bezpieczeństwo i dane:** kreator oraz ustawienia pozostają za logowaniem, operacje wyznaczają właściciela z uwierzytelnionej sesji i filtrują firmę po jego identyfikatorze. Formularze korzystają z ochrony anty-CSRF, a adresy, NIP-y i opis usługi nie są logowane. Migracja lokalna zachowała konto i dane TOTP; nie wprowadzono prawdziwych danych firmy.
- **Wydajność i odporność:** odczyt profilu używa rozdzielonych zapytań dla kolekcji, co unika iloczynu rekordów, a unikalne indeksy chronią jeden okres danego typu i miesiąca. Dla jednej JDG liczba okresów jest mała i zapytania są proporcjonalne. Migracja przeszła w przód, cofnięcie do fazy 2 oraz ponowne nałożenie na prawdziwym PostgreSQL.
- **Architektura i typy:** reguły okresów oraz kolejności lat są w domenie, czas biznesowy w warstwie aplikacyjnej, zapis w infrastrukturze, a WWW tylko zbiera dane i pokazuje błędy. Enumy eliminują sprzeczne kombinacje flag VAT/ZUS. Model EF jest zgodny z migracją.
- **Scenariusze i interfejs:** pokryto pełny abonament przy starcie w środku miesiąca, brak ryczałtu, wymagane pola, zły dzień okresu, próbę dopisania wstecz, wszystkie trzy nowe historie, wcześniejszy i pominięty rok oraz granicę UTC/Polska. Ręczny odbiór na sztucznym koncie przeszedł przy 390 px i 1440 px; zapis VAT zachował październik, dopisał listopad i zaproponował grudzień. Po usunięciu pustego znacznika walidacji nie ma artefaktów ani błędów konsoli.

## Dowody końcowe

- kompilacja całego rozwiązania: 0 ostrzeżeń, 0 błędów;
- 55 testów niezależnych od zewnętrznej bazy: 55 zielonych; osobny, 56. test na prawdziwym PostgreSQL: zielony;
- EF Core: brak zmian modelu bez migracji;
- formatowanie: bez zmian;
- skan NuGet: brak znanych podatności w zależnościach bezpośrednich i przechodnich;
- składnia Compose: poprawna;
- lokalne Compose: WWW zdrowe, `https://localhost/health` zwraca `200`, migracja `20260910124702_CompanyProfilesAndPeriods` zastosowana;
- po aktualizacji lokalnej bazy: jedno konto, zachowane wpisy konfiguracji uwierzytelniania, zero firm;
- odizolowane konto, baza i kontener użyte do odbioru zostały usunięte.

## Decyzja bramki

Wynik: **0× P1, 0× P2, 0× P3**. Faza 3 spełnia zakres Jednostki 3 i bramka pozwala rozpocząć Fazę 4. Nadal obowiązuje osobna bramka z Fazy 0: bez rzeczywistych, zanonimizowanych przykładów i niezależnego potwierdzenia nie wolno uruchamiać produkcyjnych reguł podatkowych ani automatycznego księgowania. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
