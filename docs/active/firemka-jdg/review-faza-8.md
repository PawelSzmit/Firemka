# Review fazy 8 — dokumenty US/ZUS, potwierdzenia i korekty

Data: 2026-09-13  
Status: **WYMAGA POPRAWEK PRZED FAZĄ 9**

## Znaleziska

### P1 — JPK_V7M deklaruje niezamówiony zwrot i gubi przeniesienie nadwyżki

Generator poprawnie wylicza nadwyżkę VAT w zmiennej `carry`, ale zapisuje ją tylko w `P_53`. Następnie zawsze dodaje `P_54 = 0` wraz z `P_540 = 1`, co według urzędowego XSD oznacza wybór zwrotu na rachunek w terminie 15 dni, mimo że właściciel nie podjął takiej decyzji. Jednocześnie `P_62`, czyli kwota do przeniesienia na następny okres, jest zawsze zerem. Także nadwyżka z poprzedniej deklaracji trafia do sumy `P_48`, ale `P_39` pozostaje zerem. XML przechodzi XSD, lecz jego treść deklaracyjna jest błędna.

**Wymagana naprawa:** zapisać przeniesienie z poprzedniej deklaracji w `P_39`, całkowity podatek naliczony w `P_48`, bieżącą nadwyżkę w `P_53` i — przy braku świadomej decyzji o zwrocie — tę samą kwotę w `P_62`. Nie dodawać `P_54` ani żadnego pola wyboru zwrotu. Dodać test przypadku nadwyżki i poprzedniego przeniesienia, który sprawdza znaczenie pól, a nie tylko XSD.

### P2 — artefakt nie wskazuje wersji profilu, z której został utworzony

XML zależy od imienia, nazwiska, daty urodzenia, PESEL, urzędu, kodu ZUS i e-maila z wersjonowanego profilu. `FilingArtifact` zapisuje zamknięcie, kalkulację i odcisk miesiąca, ale nie identyfikator `FilingProfileVersion`. Ponadto idempotencja uznaje za istniejący każdy artefakt danego rodzaju dla zamknięcia. Po poprawieniu błędnego profilu ponowne „Przygotuj” zwraca więc stary plik, a baza nie potrafi później wykazać, która wersja danych osobowych była jego źródłem.

**Wymagana naprawa:** dodać wymagane powiązanie artefaktu z dokładną wersją profilu i uwzględnić profil oraz wersję generatora/schematu w odcisku wejścia. Powtórzenie z identycznym profilem ma zwracać ten sam plik, a zapis nowej wersji profilu ma tworzyć nową, niezatwierdzoną wersję artefaktu powiązaną z poprzednią. Migracja, test przepływu i test PostgreSQL mają objąć ten przypadek.

### P1 — ZUS DRA ma błędny termin i zawsze identyfikator pierwszego złożenia

W bloku I generator wpisuje `1` jako kod terminu przekazania deklaracji. Oficjalny poradnik ZUS wskazuje dla pozostałych płatników, w tym JDG rozliczanej do 20. dnia, kod `6`. Zagnieżdżony identyfikator jest zawsze `01`, także dla nowej wersji po korekcie. ZUS wymaga kolejnych identyfikatorów `02`, `03` itd. XSD kontroluje tu tylko długość i cyfry, więc nie wykrywa błędnego znaczenia.

**Wymagana naprawa:** dla obsługiwanego profilu JDG zapisywać kod terminu `6`, a dwucyfrowy identyfikator kompletu wyznaczać z liczby rzeczywiście oznaczonych wysyłek: `01` przed pierwszą wysyłką, potem `02`, `03` itd. Ta sama reguła ma sterować celem złożenia JPK, aby poprawa wersji roboczej przed wysyłką nie udawała korekty. Zablokować numer spoza zakresu `01`–`99` oraz dodać test wersji przed pierwszą wysyłką i po niej.

### P2 — dwa równoczesne zapisy wyniku mogą zakończyć jeden formularz błędem serwera

`RecordOutcomeAsync` poprawnie wycofuje transakcję i usuwa fizyczny plik przegranego żądania, ale każdy wyjątek ponownie wyrzuca bez zamiany konfliktu współbieżności na wynik idempotentny albo czytelny błąd domenowy. Strona nie przechwytuje `DbUpdateConcurrencyException`. Podwójne kliknięcie lub dwie karty zapisujące to samo UPO mogą więc pokazać błąd serwera, mimo że pierwszy zapis został przyjęty.

**Wymagana naprawa:** po konflikcie wyczyścić śledzenie i odczytać zwycięski artefakt. Identyczny wynik oraz identyczny skrót potwierdzenia mają być idempotentne; różny wynik lub plik ma zwrócić czytelny komunikat o odświeżeniu bez osieroconego pliku. Wymusić oba warianty na PostgreSQL.

### P3 — walidacja profilu może zwrócić techniczny wyjątek dla pustych kodów

Fabryka profilu wywołuje `.Length` na PESEL i kodach bez wcześniejszego sprawdzenia `null`. Formularz zwykle zatrzyma pustą wartość, lecz publiczny serwis aplikacyjny może otrzymać ją z innego klienta lub testu i zwrócić `NullReferenceException` zamiast kontrolowanego komunikatu.

**Wymagana naprawa:** jawnie sprawdzić wymagane teksty przed długością i pokryć to testem domenowym.

## Bezpieczeństwo i izolacja danych

- Strony wymagają zalogowania, formularze mają ochronę anty-CSRF, a czynności na artefakcie filtrują właściciela.
- Wygenerowany XML i UPO są prywatnymi plikami; pliku deklaracji nie można pobrać przed zatwierdzeniem konkretnej wersji.
- `ISubmissionGateway` nie ma implementacji ani rejestracji. Review nie znalazło automatycznego połączenia z US lub ZUS.
- Obsługa kraju dostawcy i urzędowej wartości `BRAK` została poprawiona przed review i odpowiada aktualnej broszurze JPK_VAT od 1 lutego 2026.

## Architektura, scenariusze i dowody

- Trzy generatory walidują XML względem zapisanych JPK_V7M(3), JPK_PKPIR(3) i KEDU 2.27. Korekta miesiąca zachowuje poprzednie artefakty i ponownie wymaga zatwierdzenia.
- Pełny zestaw przeszedł 232 testy, a test migracji i wymuszonej równoległości Fazy 8 przeszedł osobno na PostgreSQL. Kompilacja Release, formatowanie, zgodność modelu EF, cztery próbki XSD, Compose i skan zależności były czyste.
- Te dowody nie wykryły znaczenia pól deklaracji VAT, braku źródłowej wersji profilu ani wyścigu dwóch wyników. Dlatego bramka pozostaje zamknięta mimo zielonej walidacji struktury.
- Ręczny import w Kliencie JPK WEB i Płatniku/ePłatniku nadal jest zewnętrznym P1 przed użyciem rzeczywistym; nie zastępuje napraw technicznych znalezionych powyżej.

## Decyzja bramki

Wynik: **2× P1, 2× P2, 1× P3**. Faza 9 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do pięciu napraw, a następnie ponowne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.
