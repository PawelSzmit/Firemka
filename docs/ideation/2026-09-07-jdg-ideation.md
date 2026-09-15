# Firemka — minimalna aplikacja do własnej JDG

Data: 7 września 2026. Etap: selekcja pomysłów, nie zatwierdzona specyfikacja ani plan implementacji.

## Wniosek

Projekt jest wykonalny jako prywatna strona dla jednej firmy i jednej osoby, działająca na komputerze i telefonie. Największa praca dotyczy poprawności rozliczeń i aktualizowania przepisów, a nie liczby ekranów czy faktur. Rekomendacja: ograniczyć obsługiwane przypadki do rzeczywistej działalności właściciela i oprzeć obsługę na zamknięciu miesiąca.

Właściciel doprecyzował cel: oszczędność, samodzielność i satysfakcja z używania oraz rozwijania własnej aplikacji. Koszt pozostaje istotny, ale nie jest jedynym kryterium. Przyjmujemy budowę własnego rozwiązania jako wybrany kierunek; ograniczamy zakres i późniejszą pracę utrzymaniową. Nie wyceniamy oszczędności ani czasu budowy przed doprecyzowaniem pozostałych danych.

## Stan projektu i dowody

- Katalog `/Users/pawelszmit/Desktop/Coding/Firemka` był pusty przed tą analizą. Nie ma kodu, README, lokalnego AGENTS.md ani dokumentów wcześniejszych analiz.
- Brak repozytorium Git, historii zmian, TODO i FIXME. Nie ma istniejących modułów, które można rozbudować lub ocenić.
- Obowiązują instrukcje AGENTS.md przekazane w rozmowie: samodzielna praca, pytania przy wątpliwościach, proste objaśnienia.
- Dowodem potrzeby każdego pomysłu jest zakres podany przez właściciela; wykonalność integracji wspierają źródła urzędowe poniżej. Nie przypisujemy nieistniejącemu kodowi możliwości.

## Potwierdzony profil firmy — uzupełnienie z rozmowy

- Obecnie skala podatkowa. Przy otwieraniu nowego roku wymagany wybór formy opodatkowania; możliwy przyszły ryczałt, rozważane 8,5% lub 12%, bez przesądzenia prawidłowej stawki dla usług.
- Etat z wynagrodzeniem powyżej minimalnego; według deklaracji właściciela z działalności opłacana będzie tylko składka zdrowotna. To początkowy profil ZUS; zmianę sytuacji należy zapisywać z datą, niezależnie od ustawień roku podatkowego.
- Czynny podatnik VAT, rozliczenie miesięczne.
- Jedno źródło przychodu: abonament dostępowy do własnej aplikacji SaaS; jedna faktura sprzedaży dla jednej polskiej firmy.
- Rozliczenia od założenia działalności, prawdopodobnie od października 2026. Dokładna data pozostaje do potwierdzenia i będzie ustawieniem firmy. Nie jest wymagane przejęcie księgowości istniejącej działalności.
- Koszty: wynajem/leasing samochodu, abonamenty AI, VPS, ewentualnie internet.

### Konsekwencje dla zakresu

1. Ustawienia PIT przypisujemy do roku. Otwarcie kolejnego roku nie zmienia historycznych ksiąg i wyliczeń. Korekta starszego roku korzysta z jego formy opodatkowania i właściwych przepisów. Zmiana ustawienia w aplikacji nie zastępuje wymaganego zgłoszenia wyboru formy opodatkowania.
2. Skala oznacza KPiR; przyszły ryczałt wymaga ewidencji przychodów, właściwego wyliczenia zdrowotnej, odliczeń i innych dokumentów rocznych (PIT-28, ewentualnie JPK_EWP). To więcej niż przełączenie procentu podatku. Obsługę przejścia projektujemy teraz; pełne obliczenia ryczałtowe muszą być wdrożone i sprawdzone przed otwarciem roku na ryczałcie. Na ryczałcie dokumenty zakupów nadal zostają, m.in. dla VAT i archiwum, mimo że zwykłe koszty nie pomniejszają podstawy ryczałtu.
3. 8,5% i 12% są wariantami podanymi przez użytkownika, nie potwierdzoną kwalifikacją SaaS. Stawkę trzeba ustalić według rzeczywistej usługi i właściwej klasyfikacji; nie wybierać jej wyłącznie po nazwie „abonament SaaS”. [8]
4. Samochód wchodzi do pierwszej wersji. Osobno rozpatrujemy leasing/najem, eksploatację i ewentualne ubezpieczenie. Potrzebne są dane umowy, wartość pojazdu, napęd/emisja i użytek prywatny lub wyłącznie firmowy. Nie stosować jednego procentu do każdej faktury samochodowej; uwzględnić przepisy właściwe dla okresu. [9]
5. Zagraniczne AI/VPS mogą wymagać importu usług i przeliczeń walut. Dokumenty spoza KSeF są częścią podstawowego zakresu. Dostawcę i sposób rozliczenia ustalamy na podstawie konkretnej faktury, nie marki ani samej waluty. Obowiązki VAT zależą również od statusu firmy. [10]
6. Roczny PIT na skali wymaga danych z etatu. Wyliczenie zaliczki firmowej oddzielamy od prognozy całorocznego podatku łączącej etat i firmę; nie wolno przedstawiać zaliczek jako gwarancji braku dopłaty rocznej. Pełne zeznanie wymaga danych PIT-11 i innych właściwych informacji.

### Doprecyzowany zakres pierwszej wersji

- Miesięczny rejestr VAT i JPK_V7M; obsługa kwartalnego VAT oraz sprzedaży konsumenckiej i zagranicznej nie jest potrzebna w pierwszej wersji.
- KPiR i rozliczenia skali od daty rozpoczęcia, z przygotowaniem rocznego zamknięcia i otwarcia następnego roku. Według harmonogramu MF opisanego w źródle [3], przy tym profilu uwzględnić również JPK_PKPIR za pierwszy rok.
- Początkowy scenariusz do sprawdzenia: rozpoczęcie w październiku 2026, rozliczenia października–grudnia, zamknięcie 2026 i otwarcie 2027 z zachowaniem albo zmianą formy opodatkowania. To scenariusz roboczy, a nie potwierdzona data uruchomienia.
- Brak migracji poprzednich ksiąg firmy upraszcza start. Nadal potrzebne mogą być dane początkowe, np. wniesiony majątek lub wydatki sprzed rozpoczęcia; nie zakładamy ich istnienia ani automatycznie zerowych wartości.
- Obsługa zakupów zagranicznych pozostaje w zakresie pomimo wyłącznie krajowej sprzedaży.

### Pozostałe dane do szczegółowych zasad księgowania

1. Samochód i umowa: napęd, wartość, data, użytek mieszany czy wyłącznie firmowy.
2. Dostawcy AI/VPS, kraj podmiotu wystawiającego fakturę i waluta dokumentu.
3. Dokładna data rozpoczęcia oraz ewentualny majątek lub dokumenty początkowe.

Te dane doprecyzujemy przy projektowaniu właściwych kategorii kosztów. Nie blokują zamknięcia obecnej analizy pomysłów.

## Pula przed oceną — 15 pomysłów

1. Profil podatkowy jednej firmy z datami obowiązywania ustawień.
2. Kreator zamknięcia miesiąca.
3. Wystawianie i pobieranie faktur przez KSeF.
4. Ręczne dodawanie dokumentów spoza KSeF.
5. Reguły sugerujące sposób księgowania powtarzalnych kosztów.
6. Samodzielne księgowanie i interpretacja wydatków przez AI.
7. Wspólne źródło zapisów KPiR i rejestrów VAT.
8. Wyjaśnione wyliczenia PIT i ZUS, z uwzględnieniem wpłat.
9. Paczka dokumentów urzędowych, korekt i potwierdzeń.
10. Bezpośrednia wysyłka wszystkich deklaracji do US i ZUS.
11. Historia zmian, blokada zamkniętego okresu i kontrolowana korekta.
12. Kopie zapasowe i pełny eksport danych.
13. Automatyczne pobieranie transakcji bankowych.
14. Wiele firm, pracownicy, magazyn, CRM i udostępnianie klientom.
15. Zastąpienie części własnych integracji istniejącymi narzędziami MF i ZUS.

## Selekcja — pięć kierunków

Ocena jakościowa: wartość dla podanego celu, siła dowodów, trudność, zależności i późniejsze utrzymanie. Nie stosujemy pozornie precyzyjnej punktacji do pustego projektu.

| Priorytet | Kierunek | Wartość i dowody | Trudność | Decyzja i następny krok |
|---|---|---|---|---|
| 1 | Profil firmy i zamknięcie miesiąca | Łączy wszystkie cztery potrzeby użytkownika. Profil rozstrzyga, jakie obliczenia i formularze są potrzebne. Brak gotowego procesu w repo. | Średnia dla interfejsu; duża dla pełnego rozliczenia | Rozwinąć teraz: opisać jeden rzeczywisty miesiąc z przykładowymi kosztami i wynikiem. |
| 2 | Skrzynka dokumentów z KSeF i spoza KSeF | Bezpośrednio realizuje pkt 1. Oficjalne API umożliwia wystawianie i odbiór [1]. Dokumenty spoza KSeF są potrzebne, jeśli wystąpią takie wydatki. | Duża | Rozwinąć teraz po profilu: ustalić rodzaje faktur, korekty, uprawnienia i scenariusz testowy. |
| 3 | KPiR i VAT z zatwierdzanych reguł | Realizuje pkt 2–3, ogranicza powtarzalne decyzje przy kilku stałych kosztach. JPK wymaga określonych danych [2–3]. | Duża | Rozwinąć teraz: sporządzić katalog faktycznych wydatków i zasad ich rozliczenia. |
| 4 | PIT i ZUS z widocznym sposobem obliczenia | Realizuje pkt 4. ZUS rozróżnia podstawy i okresy oraz rozliczenie roczne [6–7]. | Duża | Rozwinąć teraz dla jednego ustalonego profilu; przygotować przykłady referencyjne. |
| 5 | Paczka do US/ZUS i archiwum rozliczenia | Realizuje wymóg dokumentów gotowych do wysyłki. MF ma Klient JPK WEB, ZUS obsługuje import KEDU [4–5]. | Średnia–duża | Eksport rozwinąć teraz; bezpośrednią wysyłkę odłożyć. Zweryfikować pliki w narzędziach odbiorcy. |

Pomysły 4–5 z puli weszły do kierunków 2–3. Historia zmian, kopie i eksport stanowią podstawę tych pięciu kierunków, a nie dodatkowe moduły biznesowe. Pierwsze dwa kierunki należy doprecyzować przed wyborem technologii.

### 1. Profil firmy i zamknięcie miesiąca

Ekran prowadzi przez: brakujące dokumenty → zatwierdzenie zapisów → podatki i składki → pobranie plików → zapis potwierdzeń oraz wpłat. Wyliczona kwota, dokument przygotowany, dokument wysłany, dokument przyjęty i zobowiązanie zapłacone mają osobne stany.

Kandydat na pierwszy krok: przykład pełnego miesiąca dla tej konkretnej działalności. Ujawni brakujące dane bez kosztownej implementacji wszystkich wariantów podatkowych.

### 2. Dokumenty i KSeF

Jedna faktura sprzedaży z możliwością wykorzystania danych poprzedniej oraz lista zakupów. Przechowywanie oryginalnego XML, numeru KSeF, statusu i powiązań korekt. Powtórne pobranie nie może dublować dokumentów; niepewny wynik wysyłki wymaga sprawdzenia statusu przed ponowieniem. Uwzględnić brak połączenia i wygasające uprawnienia. Pełny zakres trybów awaryjnych ustalić przed produkcyjnym wystawianiem.

KSeF dostarcza dokument. Przyjęcie go w KSeF nie jest rozstrzygnięciem, czy wydatek stanowi koszt podatkowy ani ile VAT można odliczyć. Ręczne dodanie dokumentu spoza KSeF powinno umożliwiać dołączenie pliku źródłowego.

### 3. Księgowanie

Reguła może proponować kategorię na podstawie kontrahenta i rodzaju wydatku, ale właściciel widzi kwoty i zatwierdza zapis. Oddzielnie przechowujemy okres kosztu/przychodu i okres VAT. Nie utożsamiamy daty pobrania z datą księgowania. Korekty zachowują historię; zamkniętych zapisów nie nadpisujemy bez śladu.

Nieobsługiwany dokument wymaga wyjaśnienia i blokuje finalizację zależnego rozliczenia. Samochód, import usług, amortyzacja czy remanent nie mogą być po cichu pomijane: wchodzą do minimum, jeżeli występują u właściciela.

### 4. Obliczenia

PIT: właściwa metoda, narastające wartości i wcześniejsze zaliczki oraz odliczenia. VAT: należny, odliczany, przeniesienia i korekty. ZUS: składki społeczne i zdrowotna według ustalonego profilu, okresu oraz faktycznych wpłat. Dochód do PIT i podstawa zdrowotnej nie powinny być automatycznie utożsamiane.

Każdy wynik pokazuje, skąd wzięły się liczby. Reguły i parametry mają daty obowiązywania, aby aktualizacja nie zmieniała historycznych rozliczeń. Roczne rozliczenie zdrowotnej oraz roczny PIT wymagają osobnej ścieżki. Pełny PIT może potrzebować danych spoza firmy, np. innych dochodów i ulg — sama baza faktur nie wystarczy.

### 5. Dokumenty i archiwum

- JPK_V7M lub JPK_V7K odpowiednio do profilu, plik XML i czytelne zestawienie.
- JPK_PKPIR oraz ewentualny JPK_ST zgodnie z obowiązkiem i posiadanym majątkiem.
- ZUS DRA w aktualnej strukturze KEDU do importu w ePłatniku; dodatkowe formularze tylko jeśli sytuacja tego wymaga.
- Odpowiedni PIT roczny z wymaganymi załącznikami i danymi uzupełniającymi. Format i ścieżkę wysyłki należy potwierdzić dla wybranego formularza; sam PDF nie oznacza dokumentu gotowego do elektronicznego złożenia.
- KPiR i rejestry w postaci czytelnej do pobrania; przechowanie oryginałów, wersji deklaracji i potwierdzeń odbioru.

Pierwsza wersja generuje pliki, a właściciel podpisuje i wysyła je w narzędziu urzędowym. Później zapisuje potwierdzenie przyjęcia. To realizuje prośbę o przygotowanie dokumentów bez budowania od razu całej obsługi podpisów i wysyłki.

## Odrzucone i odłożone propozycje

- Samodzielne decyzje podatkowe AI: odrzucone; przy kilku fakturach zatwierdzane reguły są łatwiejsze do sprawdzenia i utrzymania.
- OCR wszystkich faktur: odłożony, ponieważ KSeF udostępnia dane strukturalne; przy kilku pozostałych dokumentach wystarczy formularz.
- Pełne integracje wysyłkowe US/ZUS: odłożone; najpierw eksport sprawdzony przez urzędowe narzędzia.
- Integracja bankowa: odłożona; początkowo wystarczy wpisanie dat i kwot płatności potrzebnych do rozliczeń.
- Wiele firm, pracownicy, CRM i magazyn: odrzucone jako niezgodne z zakresem.
- Uniwersalny program dla wszystkich form opodatkowania: odrzucony; skala oraz przygotowanie zmiany na ryczałt przy nowym roku należą jednak do uzgodnionego kierunku. Podatek liniowy nie jest obecnie wymagany.
- Samodzielna aplikacja mobilna: odrzucona; strona dostosowana do telefonu wystarczy.
- Całość oparta wyłącznie na narzędziach urzędowych: zachowana jako wariant porównawczy kosztów; nie potwierdzono pokrycia całego oczekiwanego procesu.

## Proponowana forma WWW

Cztery główne miejsca: „Mój miesiąc”, „Faktury”, „Księgi”, „Rozliczenia”; ustawienia firmy osobno. Telefon: lista kart, podgląd dokumentu i proste zatwierdzanie; komputer: również wygodne tabele. Pobieranie dokumentów i kontrola rozliczenia mają być dostępne na obu urządzeniach.

Wstępna rekomendacja techniczna: jedna aplikacja, jedna baza danych i proces synchronizacji KSeF na istniejącym VPS. Dobór języka i bibliotek po ustaleniu zakresu oraz sprawdzeniu serwera. Prywatne konto, HTTPS, dodatkowe potwierdzenie logowania, chronione dane dostępowe KSeF i kopia poza VPS są częścią podstawy. Potrzebna będzie próba odtworzenia danych. VPS nie był sprawdzany i niczego na nim nie zmieniano.

## Warunki zaufania do pierwszej wersji

Przed użyciem do rzeczywistych rozliczeń: porównać wyniki na znanych przykładach z niezależnie sprawdzonym wyliczeniem; objąć zwykły miesiąc, brak przychodu, stratę, korektę i zmianę roku. Dodać przypadki faktycznie występujące w firmie. Sprawdzić zgodność XML ze schematem oraz import w narzędziach urzędowych. Sama zgodność formatu nie dowodzi prawidłowości podatku.

Sprawdzić też ponowny import dokumentu, przerwaną synchronizację, korektę zamkniętego miesiąca, odtworzenie kopii i użycie na telefonie. Rozważyć jednorazową kontrolę zasad oraz pierwszego rozliczenia przez księgowego lub doradcę jako koszt uruchomienia, bez założenia stałego abonamentu.

## Ustalenia urzędowe i źródła

Sprawdzone 7 września 2026; daty i struktury ponownie zweryfikować przed wdrożeniem.

1. [MF — API KSeF 2.0 i FA(3)](https://ksef.podatki.gov.pl/wyjasnienia/publikacja-dokumentacji-api-ksef-20-oraz-struktury-logicznej-fa-3-30062025/): oficjalna dokumentacja integracji wystawiania i odbierania faktur. Podstawa do własnej integracji.
2. [MF — JPK_VAT od lutego 2026](https://www.podatki.gov.pl/media/wgbkrejs/broszura-jpk_vat-z-deklaracj%C4%85-od-1-lutego-2026-r.pdf): JPK_V7M i JPK_V7K w wersji (3) dla okresów od lutego 2026. Nie mylić wersji z dawnym JPK_VAT(3).
3. [MF — JPK_PD, aktualizacja 17 lipca 2026](https://podatki.gov.pl/podatki-firmowe/jednolity-plik-kontrolny/jpk_pd/jpk_pd): dla PIT objętego miesięcznym JPK_VAT obowiązek obejmuje lata rozpoczynające się po 31 grudnia 2025; pozostali, w tym kwartalny VAT, po 31 grudnia 2026. MF udostępnia JPK_PKPIR(3) i JPK_ST(1). Zakres i termin dla właściciela zależą od profilu; aplikacja nie może ograniczyć się do samego JPK VAT.
4. [MF — bezpłatne narzędzia JPK](https://www.podatki.gov.pl/podatki-firmowe/jednolity-plik-kontrolny/jpk_vat-z-deklaracja/bezplatne-narzedzia): Klient JPK WEB obsługuje podpisywanie i wysyłkę; dostępne jest sprawdzanie statusu i pobranie UPO. Uzasadnia wariant eksportu zamiast własnej wysyłki.
5. [ZUS — import KEDU](https://www.zus.pl/portal/pomoc/epl0017.html) i [ePłatnik](https://www.zus.pl/portal/pomoc/epl0000.html): import dokumentów XML z zewnętrznych programów, weryfikacja i wysyłka. Wersję eksportu dopasować do aktualnej dokumentacji.
6. [ZUS — kalkulator zdrowotnej](https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej): zależności od formy opodatkowania i okresu; może służyć do porównania wybranych wyliczeń.
7. [ZUS — roczne rozliczenie zdrowotnej za 2025](https://www.zus.pl/en/-/roczne-rozliczenie-sk%C5%82adki-zdrowotnej-za-2025-r.): potwierdza odrębny obowiązek rocznego rozliczenia w DRA/RCA. Dla przyszłego roku sprawdzić właściwy termin i wzór.

8. [MF — stawki i limity PIT](https://www.podatki.gov.pl/podatki-firmowe/pit/stawki-i-limity): stawka ryczałtu dla usług zależy co do zasady od PKWiU; część usług informatycznych objęto stawką 12%. Nie rozstrzyga klasyfikacji konkretnego SaaS.
9. [MF — Podatki 2026, przewodnik](https://www.podatki.gov.pl/media/rr5pgg25/podatki-2026-przewodnik-dla-inwestorow.pdf): zmiany limitów kosztów samochodów od 2026, uzależnione od napędu i emisji. Szczegółowe rozliczenie konkretnej umowy wymaga jej danych.
10. [MF — VAT, informacje podstawowe](https://www.podatki.gov.pl/podatki-firmowe/vat/informacje-podstawowe) i [miejsce opodatkowania usług](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/miejsce-swiadczenia-opodatkowania): podstawa do ustalenia obowiązków przy usługach kupowanych za granicą. Rodzaj deklaracji ustalić po potwierdzeniu statusu VAT.

## Rekomendowane przekazanie do następnego etapu

Najpierw `dev-brainstorm`: profil firmy i jeden kompletny miesiąc od faktury do zapłaty i przyjętych dokumentów. Na jego podstawie wybrać dokładny zakres pierwszej wersji i przykłady poprawnych wyników. Dopiero potem `dev-plan`: pliki, technologia, kolejność budowy i weryfikacja. Ta analiza nie upoważnia do implementacji, commitów, publikacji ani wdrożenia.
