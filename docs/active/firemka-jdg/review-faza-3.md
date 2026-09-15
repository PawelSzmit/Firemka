# Review fazy 3 — profil firmy, okresy i „Mój miesiąc”

Data: 2026-09-10  
Status: **WYMAGA NAPRAWY P2 PRZED FAZĄ 4**

## Znaleziska

### P2 — profil nie zapisuje adresu firmy ani opisu usługi abonamentowej

Plan wymaga przed Jednostką 3 dokładnych danych firmy i klienta oraz opisu usługi (`docs/plans/2026-09-09-jdg-application-plan.md`, wiersz 644). Komenda, model domenowy, migrowana tabela i kreator zapisują nazwę i NIP firmy, lecz nie jej adres; zapisują też kwotę abonamentu, ale nie opis usługi (`src/Firemka.Application/Onboarding/CompanyProfileContracts.cs`, wiersze 6–18; `src/Firemka.Web/Pages/Setup/Company.cshtml.cs`, wiersze 42–55). Następna faza faktury sprzedaży musiałaby więc ponownie zmieniać fundament profilu i nie miałaby kompletnych danych sprzedawcy ani pozycji faktury.

**Wymagana naprawa:** dodać wymagany adres firmy i opis usługi do domeny, komendy, migrowanego modelu, kreatora, podsumowania ustawień oraz testów trwałego zapisu. Używać nadal wyłącznie danych sztucznych.

### P2 — trzy z pięciu ustawień okresowych nie mają ścieżki zmiany

Firma tworzy osobne kolekcje okresów VAT, ZUS i pojazdu, ale udostępnia zmianę tylko abonamentu i energii (`src/Firemka.Domain/Companies/Company.cs`, wiersze 97–125). Kontrakt aplikacyjny oraz ekran ustawień również pomijają przyszłe okresy VAT, ZUS i pojazdu (`src/Firemka.Application/Onboarding/CompanyProfileContracts.cs`, wiersze 20–70). To nie realizuje wzorca danych obowiązujących w czasie dla wszystkich pięciu kategorii, a w szczególności narusza uzgodnione wymaganie, że zmianę sytuacji ZUS zapisuje się z datą.

**Wymagana naprawa:** dodać typowane wartości i operacje dopisujące okres VAT, ZUS oraz pojazdu, pokazać ich historię i formularze w ustawieniach oraz przetestować, że zmiana przyszła nie zmienia poprzedniego miesiąca.

### P2 — można „otworzyć” dowolny dawny lub pominięty rok

`Company.OpenTaxYear` odrzuca wyłącznie duplikat i następnie przyjmuje każdy rok od 2000 do 2200 (`src/Firemka.Domain/Companies/Company.cs`, wiersze 127–135; `src/Firemka.Domain/TaxYears/TaxYear.cs`, wiersze 37–59). Zmienione żądanie formularza może więc dopisać rok sprzed rozpoczęcia działalności albo pominąć rok pośredni. Jest to sprzeczne z operacją otwarcia **nowego** roku i może stworzyć niespójną historię.

**Wymagana naprawa:** pozwolić otworzyć wyłącznie rok bezpośrednio po najpóźniejszym zapisanym roku, zwrócić czytelny błąd w interfejsie i dodać testy roku wcześniejszego, pominiętego oraz poprawnego kolejnego.

### P2 — domyślny miesiąc zależy od UTC, a nie od daty właściciela w Polsce

„Mój miesiąc” i domyślne daty nowych okresów używają `DateTime.UtcNow` (`src/Firemka.Web/Pages/Month/Index.cshtml.cs`, wiersze 14–18; `src/Firemka.Web/Pages/Settings/Index.cshtml.cs`, wiersze 159–161). W pierwszych godzinach pierwszego dnia miesiąca w Polsce serwer nadal widzi poprzedni miesiąc UTC. Aplikacja może wtedy otworzyć niewłaściwy miesiąc i podpowiedzieć złą datę obowiązywania ustawienia.

**Wymagana naprawa:** wprowadzić jedno testowalne źródło czasu i jawnie wyznaczać dzień w strefie `Europe/Warsaw`; pokryć testem granicę miesiąca.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** strony wymagają zalogowania i tokenu anty-CSRF; NIP nie trafia do logów. CSP blokuje skrypty inline, a po korekcie pozwala tylko na lokalne skrypty i obrazy `data:` potrzebne ikonom. Nie znaleziono P1 ani wycieku danych.
- **Wydajność i odporność:** dashboard wykonuje jedno zapytanie firmy i jedno grupowanie stanów dokumentów. Dla jednej JDG jest to proporcjonalne; unikalne indeksy chronią przed dwoma okresami tego samego typu od tej samej daty.
- **Architektura i typy:** granice warstw są zachowane, projekcja dashboardu pochodzi z danych źródłowych, a migracja ma komplet ograniczeń. Brakuje jednak pełnego profilu oraz operacji dla trzech typów okresów.
- **Scenariusze i testy:** pokryto środek miesiąca, niezmienność historii abonamentu i energii, brak ryczałtu, zły dzień początku okresu, migrację w obie strony oraz widoki 390/1440 px. Brakuje scenariuszy trzech pozostałych okresów, niekolejnego roku i polskiej granicy miesiąca.

## Dowody wykonane podczas review

- pełna kompilacja rozwiązania: 0 ostrzeżeń, 0 błędów;
- testy zakresu: 9 domeny, 2 aplikacji, 17 WWW i 16 infrastruktury — łącznie 44/44 zielone;
- PostgreSQL: migracja w przód, cofnięcie, ponowne nałożenie i trwała historia stawek;
- EF Core: brak zmian modelu bez migracji;
- formatowanie: bez zmian;
- ręczny odbiór: kreator 390 px, dashboard 390 px i 1440 px, bez błędów CSP w poprawionej wersji.

## Decyzja bramki

Wynik: **0× P1, 4× P2, 0× P3**. Zgodnie z regułą tego zadania Faza 4 pozostaje zablokowana do naprawienia wszystkich czterech P2 i ponownego review Fazy 3. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
