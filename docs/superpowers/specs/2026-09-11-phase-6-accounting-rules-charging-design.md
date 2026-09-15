# Faza 6 — reguły kosztowe, księgi, samochód i ładowanie

Data: 2026-09-11  
Status: projekt wariantu A zatwierdzony przez właściciela 2026-09-11; gotowy do implementacji

## Cel i granice

Faza 6 ma połączyć potwierdzone dokumenty kosztowe z kontrolowanym procesem księgowania. Pierwszy dokument nowego rodzaju zawsze czeka na decyzję właściciela. Dopiero jawnie zapisana reguła może automatycznie obsłużyć następne pełne dopasowania.

Ta faza nie ustanawia przepisów podatkowych. Aplikacja nie otrzyma domyślnych procentów KPiR, odliczenia VAT ani kosztów samochodu. Właściciel podaje wynik decyzji na podstawie zweryfikowanej zasady, a aplikacja zapisuje jej wersję, źródło i wyjaśnienie. Brak potwierdzenia blokuje automatyczne księgowanie.

Domowe ładowanie tworzy techniczne zestawienie energii i kosztu. Nie tworzy wpisu KPiR ani VAT bez odrębnie potwierdzonej podstawy podatkowej. Prywatna faktura za energię domową nie jest księgowana jako koszt.

## Architektura

Zakres zostanie podzielony na trzy niezależne moduły domenowe:

1. `Accounting` — odcisk dokumentu, wersjonowane reguły, decyzje, księgowanie oraz osobne wpisy KPiR i VAT.
2. `Vehicles` — osobne polityki dla najmu/leasingu, eksploatacji, ubezpieczenia i publicznego ładowania. Polityka bez dowodu i jawnych parametrów pozostaje nieaktywna.
3. `Charging` — profil formatu CSV, wersjonowana stawka energii, idempotentny import sesji oraz miesięczne zestawienie.

Warstwa aplikacyjna udostępni osobne przypadki użycia dla przeglądu kosztu, zatwierdzenia pojedynczego dokumentu, zapisania reguły na przyszłość, odczytu ksiąg i importu ładowania. Warstwa WWW nie będzie modyfikować encji bezpośrednio.

## Model reguły kosztowej

Reguła rozpoznaje dokładnie pięć cech wpływających na automatyzację:

- podmiot wystawcy — preferowany identyfikator podatkowy, a nazwa wyłącznie jako jawny plan awaryjny;
- kraj wystawcy jako dwuliterowy kod;
- walutę jako kod trzyliterowy;
- profil VAT oraz — gdy ma zastosowanie — stawkę liczbową;
- rodzaj usługi lub kosztu wybrany z kontrolowanej listy: AI, VPS, internet, najem/leasing auta, eksploatacja auta, ubezpieczenie auta, publiczne ładowanie albo inny jawnie nazwany koszt.

Kwota oraz zwykła zmiana daty dokumentu nie należą do odcisku. Ich zmiana nie zatrzymuje istniejącej reguły. Zmiana dowolnej z pięciu cech daje stan `Wymaga sprawdzenia` i pokazuje konkretne różnice.

Każda reguła zawiera:

- właściciela i firmę;
- numer wersji oraz opcjonalne wskazanie poprzedniej wersji;
- odcisk dopasowania;
- kategorię KPiR;
- jawne parametry obliczenia podane przez właściciela: procent odliczenia podatku naliczonego oraz procent pozostałej kwoty stanowiący koszt KPiR;
- osobną politykę okresu KPiR i VAT: miesiąc dokumentu, następny miesiąc albo zawsze ręczny wybór;
- opis wpływu na KPiR, VAT, PIT i — informacyjnie — przyszłe wyliczenie zdrowotnej;
- źródło decyzji lub notatkę uzasadniającą;
- datę obowiązywania i status aktywności.

Zmiana reguły tworzy nową wersję. Poprzednia nie jest nadpisywana i nadal wyjaśnia historyczne księgowania.

Dla dokumentu z kwotą brutto `B` i podatkiem naliczonym `V` obliczenie reguły jest jawne i powtarzalne:

`VAT do odliczenia = V × procent odliczenia VAT`

`kwota po odliczeniu VAT = B − VAT do odliczenia`

`koszt KPiR = kwota po odliczeniu VAT × procent kosztu KPiR`

Wyniki są zaokrąglane do groszy dopiero na końcu każdego działania. Dane wejściowe muszą spełniać `0 ≤ V ≤ B`, a oba procenty muszą mieścić się od 0 do 100. Są to parametry decyzji właściciela, nie wartości wybierane przez aplikację. Reguła z okresem `zawsze ręczny` może podpowiedzieć kwoty, ale nie księguje automatycznie.

## Przepływ dokumentu kosztowego

1. Dokument ma potwierdzone podstawowe dane z fazy 4.
2. Właściciel uzupełnia kraj, profil i stawkę VAT, rodzaj usługi, kwotę brutto oraz podatek naliczony potrzebne do decyzji księgowej.
3. Aplikacja szuka jednej aktywnej, pełnej reguły.
4. Brak reguły lub różnica odcisku tworzy propozycję i niczego nie księguje.
5. Właściciel wybiera jedną z dwóch odrębnych czynności:
   - `Zastosuj tylko do tego dokumentu` — zapisuje decyzję i wpisy, ale nie zmienia reguły;
   - `Zastosuj także do kolejnych` — zapisuje decyzję, tworzy nową wersję reguły i dopiero potem wpisy.
6. Następny dokument z pełnym dopasowaniem może zostać zaksięgowany automatycznie. Zapis wskazuje dokładną wersję reguły.

Jeden dokument może mieć najwyżej jedną aktywną decyzję księgową. Ponowione żądanie zwraca istniejący wynik. Korekty zamkniętych okresów pozostają zakresem fazy 7.

## KPiR i VAT

Jedna decyzja księgowa tworzy dwa oddzielne, powiązane rekordy:

- wpis KPiR z okresem, kategorią, kwotą i wersją reguły;
- wpis VAT z okresem, kwotą podatku naliczonego, kwotą odliczaną oraz wersją reguły.

Brak zastosowania w jednej księdze jest zapisany jawnie jako decyzja `Nie ujmuj`, a nie przez brak danych. Suma dokumentu, podatek naliczony, kwota KPiR i kwota VAT muszą przejść walidację spójności. Okres obu ksiąg wynika z jawnie zatwierdzonej polityki reguły; `zawsze ręczny` zatrzymuje automatyzację. Aplikacja nie wylicza jeszcze miesięcznego PIT ani zdrowotnej; pokazuje wyłącznie tekstowy wpływ, który późniejszy silnik fazy 7 wykorzysta jako dane wejściowe.

## Samochód

Rodzaje kosztów samochodu są rozdzielone na:

- najem lub leasing;
- eksploatację;
- ubezpieczenie;
- publiczne ładowanie.

Każdy rodzaj ma własną politykę i wersję. Nie istnieje wspólny domyślny procent. Aktywacja wymaga wskazania rodzaju umowy lub kosztu, okresu obowiązywania, jawnych parametrów KPiR/VAT oraz referencji do dokumentu lub potwierdzonej notatki. Bez tych danych system pozwala oznaczyć koszt jako samochodowy, ale pozostawia go w stanie `Wymaga ustalenia polityki`.

Ponieważ właściciel nie przekazał jeszcze umowy lub oferty, faza 6 dostarczy blokady i model, ale nie utworzy aktywnej polityki samochodu.

## Domowe ładowanie

Profil importu jest konfigurowany na podstawie rzeczywistego pliku i zawiera:

- separator pól;
- nazwę kolumny czasu;
- nazwę kolumny energii;
- format daty i strefę czasu;
- potwierdzoną jednostkę `Wh`;
- sposób identyfikacji wiersza.

Profil nie może zostać aktywowany bez jawnego potwierdzenia jednostki. Parser obsługuje nagłówek, pola cytowane i czytelne błędy numeru wiersza. Cały plik jest sprawdzany przed zapisem; błąd jednego wiersza nie pozostawia częściowego importu.

Idempotencję zapewniają skrót całego pliku oraz deterministyczny skrót znormalizowanego wiersza. Ponowny import tego samego pliku zwraca poprzedni wynik, a plik częściowo pokrywający się zapisuje tylko nowe wiersze.

Stawka brutto energii jest wersjonowana i obowiązuje od pierwszego dnia wybranego miesiąca. Wygenerowany raport jest osobnym snapshotem wskazującym wersję stawki oraz dokładny zbiór sesji, dlatego późniejsza stawka nie zmienia historii. Raport liczy:

`energia kWh = suma Wh / 1000`

`koszt brutto = energia kWh × stawka brutto za kWh`

Zmiana stawki nie przelicza zamkniętych ani historycznie zapisanych raportów. Dla wartości referencyjnej `10 000 Wh` i `0,91 zł/kWh` wynik wynosi `10 kWh` oraz `9,10 zł`.

Raport ma stan `Tylko zestawienie — podstawa podatkowa niepotwierdzona`, dopóki właściciel nie zapisze odrębnej, zweryfikowanej decyzji. W obecnym zakresie nie będzie przycisku automatycznie tworzącego KPiR lub VAT z CSV.

## Interfejs

- `Dokumenty → Wymagające reguły` pokaże propozycje, różnice od istniejącej reguły oraz dwa oddzielne przyciski decyzji.
- `Księgi` pokażą osobne tabele KPiR i VAT, źródło dokumentu, tryb ręczny/automatyczny i wersję reguły.
- `Samochód` pokaże cztery oddzielne polityki i wyraźne blokady brakujących danych.
- `Ładowanie domowe` pozwoli zdefiniować profil na podstawie pliku, ustawić stawkę od miesiąca, zaimportować CSV i obejrzeć miesięczne zestawienie.

Komunikaty będą opisywać skutek prostym językiem. Automatyczne zaksięgowanie zawsze będzie widoczne i odwracalne dopiero przez kontrolowaną korektę z późniejszej fazy, nie przez ciche nadpisanie.

## Błędy i bezpieczeństwo

- Wszystkie zapytania i polecenia filtrują po właścicielu.
- Import CSV ma limit rozmiaru i liczby wierszy oraz działa transakcyjnie.
- Dane CSV nie są wykonywane jako formuły ani polecenia; tekst jest traktowany wyłącznie jako dane.
- Niepełny odcisk, wiele pasujących reguł, nieaktywna polityka auta lub brak stawki energii blokują automatyzację.
- Konflikt współbieżności i ponowione żądanie nie tworzą drugich wpisów księgowych.
- Audyt zapisuje decyzję, wersję i skutek, ale nie kopiuje całych dokumentów ani danych uwierzytelniających.

## Testy akceptacyjne

1. Pierwszy koszt tworzy propozycję i nie trafia do ksiąg przed zatwierdzeniem.
2. Pełne dopasowanie kolejnego kosztu księguje go automatycznie dokładnie raz.
3. Zmiana kwoty lub zwykłej daty nadal pasuje; zmiana sprzedawcy, kraju, waluty, VAT albo usługi zatrzymuje dokument i pokazuje różnicę.
4. `Tylko ten dokument` nie zmienia reguły; `Także kolejne` tworzy nową wersję.
5. KPiR i VAT mają osobne wpisy wskazujące to samo źródło i wersję decyzji.
6. Polityka auta bez dowodu lub kompletu parametrów nie może zostać aktywowana.
7. `10 000 Wh` daje `10 kWh` i `9,10 zł` przy stawce `0,91 zł/kWh`.
8. Nowa stawka nie zmienia raportu wcześniejszego miesiąca.
9. Ponowny CSV nie dubluje pliku ani wierszy; częściowo nakładający się plik zapisuje tylko nowe sesje.
10. Błędna jednostka, kolumna lub wiersz wycofuje cały import z czytelnym komunikatem.
11. Raport ładowania nie tworzy KPiR/VAT bez odrębnie potwierdzonej podstawy.
12. PostgreSQL potwierdza unikalność, transakcje, wersje reguł i równoległe ponowienia.
13. Widoki przechodzą próbę telefonu 390 px i komputera 1440 px na sztucznych danych.

## Elementy odroczone i wymagane dane

Do odbioru rzeczywistych reguł potrzebne są:

- zanonimizowane faktury OpenAI, Anthropic — jeśli wystąpi — oraz OVH;
- oferta lub umowa samochodu z rozbiciem opłat;
- przykładowy CSV z aplikacji kabla wraz z potwierdzeniem kolumn i jednostki;
- niezależnie potwierdzone zasady ujęcia każdej kategorii w KPiR/VAT oraz domowego ładowania.

Brak tych danych nie blokuje budowy mechanizmów, lecz blokuje aktywację reguł, polityk samochodu, automatyczne księgowanie rzeczywistych dokumentów oraz uznanie zestawienia ładowania za koszt podatkowy.

## Plan weryfikacji

- testy domenowe dla dopasowania, wersjonowania i blokad;
- testy aplikacyjne dla rozdzielenia poleceń oraz wyjaśnień;
- testy infrastruktury z PostgreSQL dla idempotencji i transakcji;
- testy WWW formularzy i izolacji właściciela;
- odbiór responsywny na sztucznym pliku CSV;
- pełna kompilacja, formatowanie, skan zależności, sprawdzenie migracji i zdrowia Compose;
- osobna, pozostawiona otwarta bramka dla rzeczywistych dokumentów i potwierdzenia podatkowego.
