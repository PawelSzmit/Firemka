# Firemka — wykonanie aplikacji do własnej JDG

Stan: aktywne zadanie  
Ostatnia aktualizacja: 2026-09-15  
Gałąź: `main` — pierwszy lokalny commit kodu `42dc5f4d437e`; bez pushu i wdrożenia

## Cel

Zbudować prywatną, responsywną aplikację Firemka zgodnie z uzgodnionymi wymaganiami oraz planem technicznym. Praca ma przebiegać sekwencyjnie: wykonanie jednej fazy, niezależne review, usunięcie wykrytych błędów, a dopiero potem następna faza.

## Zakres

Pierwsza wersja obejmuje jedną JDG, skalę podatkową, miesięczny VAT, początkowy profil ZUS, KSeF, dokumenty spoza KSeF, reguły kosztów, obsługę samochodu i ładowania, zamknięcie miesiąca i roku, eksport dokumentów US/ZUS, ręczne płatności, powiadomienia, 2FA oraz zaszyfrowane kopie na MacBooku.

Poza pierwszą wersją pozostają ryczałt, pełne roczne PIT, częściowe płatności, import wyciągów bankowych i automatyczne wysyłanie dokumentów do US/ZUS. Granice dla tych funkcji zostaną przygotowane, lecz funkcje nie zostaną włączone.

## Zasada realizacji

1. `dev-docs-execute` wykonuje dokładnie jedną fazę.
2. `dev-docs-review` zapisuje wynik review tej fazy.
3. Jeśli review ma błąd P1 lub P2 w zakresie bieżącej fazy, kolejny `dev-docs-execute` wykonuje wyłącznie jego naprawę, po czym następuje kolejne review tej samej fazy.
4. Faza następna może zacząć się tylko po bramce review bez P1/P2, z jednym wyjątkiem: zewnętrzna bramka wyraźnie odroczona przez plan źródłowy nie blokuje niezależnego, bezpiecznego zakresu technicznego. Pozostaje widoczna i blokuje dokładnie te jednostki oraz działania produkcyjne, dla których plan jej wymaga.
5. Kod jest tworzony test-first: najpierw test, obserwacja jego oczekiwanej porażki, minimalna implementacja, potem test zielony.
6. Właściciel zezwolił na samodzielne lokalne commity po zielonej weryfikacji logicznego etapu oraz potrzebne pushe do `https://github.com/PawelSzmit/Firemka`. Wdrożenie nadal wymaga osobnego polecenia. Rzeczywiste działania z KSeF, ZUS, US, SMTP i VPS będą wykonywane wyłącznie w warunkach opisanych w poszczególnych bramkach.

## Fazy

| Faza | Nazwa | Główna bramka przed przejściem dalej |
|---|---|---|
| 0 | Fakty, źródła i przykłady referencyjne | potwierdzone źródła oraz dane referencyjne; bez tego nie ma produkcyjnych reguł podatkowych, lecz można zbudować bezpieczny szkielet techniczny |
| 1 | Szkielet, środowisko i logowanie | lokalna aplikacja, 2FA i testy bezpieczeństwa |
| 2 | Dane, pliki, historia i zadania | wersjonowanie, idempotencja, prywatny magazyn plików |
| 3 | Profil firmy i Mój miesiąc | okresowe ustawienia oraz responsywna skorupa aplikacji |
| 4 | Dokumenty, KSeF przychodzący i OCR | import bez duplikatów, ręczne potwierdzanie danych |
| 5 | Abonament i KSeF wychodzący | jedna faktura na okres i bezpieczne ponowienia |
| 6 | Reguły kosztów, KPiR/VAT i samochód | automatyzacja tylko potwierdzonych reguł |
| 7 | Obliczenia i zamknięcie miesiąca | zgodność z niezależnymi przykładami referencyjnymi |
| 8 | Dokumenty US/ZUS i korekty | walidacja XSD oraz ręczny import testowy |
| 9 | Zamknięcie roku i PDF | wersjonowany rok bez pełnego PIT |
| 10 | Płatności, e-mail i dashboard | przypomnienia bez duplikatów i ręczne pełne płatności |
| 11 | Kopie na MacBooku i odtworzenie | pełny, udokumentowany test odtworzenia |
| 12 | Utwardzenie i kontrolowany pilotaż | kompletna checklista odbioru, bez automatycznego wystawiania |

## Kryteria akceptacji

- Każda funkcja ma test automatyczny lub udokumentowaną, ręczną próbę o odpowiednim zasięgu.
- Każde obliczenie podatkowe ma wersję reguł, źródło i przykład referencyjny.
- Historia faktur, księgowań, zamknięć i potwierdzeń nie jest nadpisywana.
- Żaden dokument lub e-mail nie może powstać podwójnie po ponowieniu zadania.
- Każde źródło zewnętrzne ma testową ścieżkę, zanim zostanie użyte na rzeczywistych danych.
- Zaszyfrowana kopia odtwarza kompletną aplikację na czystym środowisku.
- Przejście do produkcji wymaga osobnej bramki pilotażu oraz danych i potwierdzeń właściciela wskazanych w planie źródłowym.

## Projekt wykonawczy fazy 8

Faza 8 pozostaje eksportem ręcznym. Żadne pobranie, zatwierdzenie ani zapis potwierdzenia nie wywołuje połączenia wysyłkowego z US lub ZUS.

1. `Domain/Filings` otrzymuje potwierdzony profil urzędowy właściciela, niezmienne wersje artefaktów oraz historię ręcznego losu pliku: przygotowany, zatwierdzony do eksportu, oznaczony jako wysłany, przyjęty albo odrzucony.
2. Profil urzędowy zawiera dane wymagane przez schematy osoby fizycznej, kod urzędu oraz referencję dowodu sprawdzenia. Brak profilu lub brak jego potwierdzenia blokuje generowanie czytelnym komunikatem.
3. Zamknięta wersja miesiąca jest jedynym źródłem artefaktu. Odcisk wejścia, identyfikator kalkulacji, wersja schematu i SHA-256 gotowego XML pozostają zapisane przy wersji artefaktu.
4. Osobne generatory tworzą `JPK_V7M(3)`, techniczny narastający `JPK_PKPIR(3)` oraz właściwy dla obsługiwanego profilu `ZUS DRA` w KEDU 2.27. JPK_PKPIR można oznaczyć jako wysłany dopiero w przepływie zamknięcia roku z fazy 9.
5. Każdy generator waliduje XML lokalnie przeciw dokładnie zapisanej strukturze XSD. Niepoprawny XML nie jest zapisywany ani udostępniany.
6. Plik jest przechowywany prywatnie. Pobranie następuje przez istniejącą kontrolę właściciela. Zatwierdzenie i oznaczenie wysyłki są osobnymi świadomymi czynnościami.
7. Potwierdzenie odbioru jest osobnym prywatnym plikiem przypiętym do dokładnej wersji artefaktu. Status `Przyjęty` albo `Odrzucony`, data oraz krótka referencja są dopisywane, nie nadpisywane.
8. Zamknięta korekta miesiąca tworzy nową wersję artefaktu wskazującą poprzednią. Wymaga nowego zatwierdzenia i nowego potwierdzenia; wcześniejszy plik oraz UPO pozostają dostępne.
9. `ISubmissionGateway` jest wyłącznie nieaktywną granicą przyszłej funkcji. Nie ma implementacji produkcyjnej ani rejestracji wywołującej wysyłkę.
10. Automatyczna bramka obejmuje XSD, deterministyczność, idempotencję, izolację właściciela, niezmienność wersji, odrzucenie, korektę i równoległe żądania na PostgreSQL. Ręczny import w Kliencie JPK WEB oraz Płatniku/ePłatniku pozostaje zewnętrznym P1 przed rzeczywistym użyciem, ale nie blokuje niezależnego zakresu technicznego ani fazy 9.

## Projekt wykonawczy fazy 9

Faza 9 zamyka wyłącznie firmową część roku. PDF jest czytelnym zestawieniem do przepisania do zewnętrznej aplikacji PIT, a nie zeznaniem PIT-36/PIT-B i nie jest wysyłany automatycznie.

1. `Domain/AnnualClosing` przechowuje wersjonowaną deklarację roczną, niezmienne zamknięcie, listę dokładnych wersji zamkniętych miesięcy oraz identyfikatory prywatnych plików PDF i JPK_PKPIR.
2. Zakres wymaganych miesięcy zaczyna się w miesiącu rozpoczęcia działalności dla pierwszego roku, a w kolejnych latach obejmuje styczeń-grudzień. Rok nie może zostać zamknięty przed końcem 31 grudnia ani przy brakującym, otwartym lub rozjechanym zamknięciu miesiąca.
3. Przed zamknięciem właściciel potwierdza wartości spisu z natury na początek i koniec roku, faktycznie wpłacone zaliczki PIT i składki zdrowotne oraz referencję dowodu niezależnego sprawdzenia. Brak potwierdzenia jest widoczną blokadą, bez wartości domyślnych.
4. Podsumowanie sumuje źródła miesięczne i pokazuje osobno: przychód, koszty przed spisem, różnicę remanentową, koszty po spisie, składki społeczne, korekty podstawy, dochód albo stratę, należne i wpłacone zaliczki PIT, dochód zdrowotny, roczną minimalną podstawę, roczną składkę należną, składki wpłacone oraz różnicę do dopłaty albo zwrotu. Wzór zdrowotnej pozostaje oznaczony jako wymagający niezależnego sprawdzenia dla konkretnego profilu.
5. Roczny JPK_PKPIR(3) powstaje z pełnych zapisów roku i potwierdzonych pól `P_1`/`P_2` spisu z natury. Przechodzi lokalną walidację XSD i zostaje powiązany z dokładną wersją zamknięcia roku; techniczny plik narastający z fazy 8 nie staje się po cichu plikiem rocznym.
6. Generator PDF używa lokalnego silnika PDFsharp/MigraDoc na licencji MIT i czcionki Unicode dostępnej na macOS oraz w obrazie serwera. Raport zawiera nazwę firmy, NIP, rok, numer wersji, datę utworzenia, wszystkie potwierdzone kwoty, źródła, ostrzeżenie o granicach oraz numerację stron.
7. PDF i XML są zapisywane w prywatnym magazynie plików, dostępne wyłącznie właścicielowi i możliwe do ponownego pobrania. Odciski SHA-256 i wersja generatora wchodzą do rocznego snapshotu. Roczny JPK ma osobny ciąg: zatwierdzenie dokładnej wersji, ręczna wysyłka, wynik przyjęty albo odrzucony oraz prywatne UPO/potwierdzenie; nic nie jest wysyłane automatycznie.
8. Zwykła zmiana danych rozliczeniowych zamkniętego roku jest blokowana. `Rozpocznij korektę` wymaga powodu i otwiera nową wersję roku; dopiero wtedy można rozpocząć korektę miesiąca. Ponowne zamknięcie tworzy nowe PDF i JPK, zachowując całą poprzednią wersję.
9. Zamknięcie roku wystawia zlecenie trwałego archiwum rocznego przez port `IAnnualArchiveRequestQueue`. Faza 9 zapisuje zlecenie, lecz rzeczywisty zaszyfrowany plik i klient Mac należą do fazy 11.
10. Automatyczna bramka obejmuje domenę, sumy roczne, brakujące miesiące, rok rozpoczęty w trakcie roku, czas polski, izolację właściciela, idempotencję, korektę, niezmienność poprzedniego roku, XSD, treść PDF i współbieżność PostgreSQL. Najnowszy PDF jest dodatkowo renderowany do obrazów i sprawdzany wizualnie.

## Projekt wykonawczy fazy 10

Faza 10 zapisuje wyłącznie pełne płatności i wysyła oszczędne przypomnienia. Nie importuje banku, nie wykonuje przelewów i nie uruchamia prawdziwego SMTP bez jawnie włączonej konfiguracji.

1. `Domain/Payments` przechowuje pełną płatność jednej konkretnej wersji należności: faktury klienta albo zamknięcia miesiąca dla PIT, VAT lub ZUS. Zapis zawiera źródło `Manual`/`BankImport`, zewnętrzną referencję, datę i dokładną kwotę należną. Kwota inna niż pełna jest odrzucana, ponieważ płatności częściowe pozostają poza pierwszą wersją.
2. Należności nie są kopiowane do osobnej, podatnej na rozjazd tabeli. `PaymentService` wylicza je z wystawionej faktury oraz najnowszej zamkniętej wersji miesiąca i przypina płatność do ich identyfikatora i numeru wersji. Korekta tworzy nowy cel, a historia starej płatności pozostaje niezmienna.
3. Terminy PIT i ZUS przypadają na 20. dzień następnego miesiąca, a VAT na 25. dzień. Jeśli termin wypada w sobotę, niedzielę lub polskie święto ustawowe, kalendarz przesuwa go na następny dzień roboczy. Faktura klienta korzysta z zapisanej daty płatności.
4. `Domain/Notifications` przechowuje jedną próbę konkretnego przypomnienia z unikalnym kluczem obejmującym sprawę i lokalny dzień. Terminy podatkowe przypominają 7 dni, 2 dni i w dniu terminu, faktura klienta dzień po terminie, a bieżąca faktura sprzedaży od pierwszego dnia miesiąca, gdy nadal wymaga działania.
5. Nowy błąd jest kwalifikowany od razu. Ta sama nierozwiązana sprawa może utworzyć najwyżej jedno przypomnienie na dzień czasu `Europe/Warsaw`; rozwiązanie kończy serię, a ponowne pojawienie się ma nowy identyfikator sprawy.
6. Powiadomienie trafia przez outbox do trwałego zadania. Unikalność bazy i klucza zadania chronią przed duplikatem po restarcie workera. Awaria SMTP pozostawia próbę do kontrolowanego ponowienia i jest widoczna; nie jest oznaczana jako sukces.
7. Treść e-maila nie zawiera załączników, kwot ani danych dokumentów. Zawiera tylko krótki rodzaj czynności i bezpieczny link do strony po zalogowaniu. Adres odbiorcy jest konfigurowany; domyślna konfiguracja lokalna nie wysyła wiadomości.
8. `Mój miesiąc` staje się projekcją źródeł: pokazuje najbliższą czynność, dokumenty i błędy, szacunki podatków, terminy i stan pełnych płatności, status miesiąca oraz stan kopii. Do czasu Fazy 11 stan kopii jest jawnie opisany jako nieskonfigurowany, nie jako poprawny.
9. Strona `Płatności` pozwala oznaczyć wyłącznie pełną należność z datą i referencją. Formularz ponownie odczytuje kwotę ze źródła po stronie serwera, wymaga właściciela i chroni przed ponowieniem oraz równoległym zapisem.
10. Bramka automatyczna obejmuje kwotę pełną i błędną, korektę miesiąca, izolację właściciela, terminy i święta, czas polski, 7/2/0 dni, przeterminowaną fakturę, nowy i trwający błąd, restart workera, awarię SMTP oraz współbieżność PostgreSQL. Rzeczywista skrzynka odbiorcza pozostaje zewnętrzną bramką przed włączeniem e-maili.

## Projekt wykonawczy fazy 11

Faza 11 tworzy kopię poza VPS i uznaje ją za poprawną dopiero po pobraniu, lokalnym zaszyfrowaniu i pełnej weryfikacji na Macu. Ponieważ docelowy folder i hasło odzyskiwania nie zostały jeszcze wskazane, implementacja używa konfigurowalnej wartości domyślnej, a prawdziwa instalacja pozostaje wyłączona. Próby techniczne korzystają wyłącznie z folderów tymczasowych i sztucznych danych.

1. Serwer przechowuje osobny, możliwy do unieważnienia token kopii wyłącznie jako SHA-256. Token daje dostęp tylko do planu kopii, pobrania strumienia i zgłoszenia wyniku; nie loguje do panelu i nie zmienia danych księgowych.
2. Spójny pakiet ZIP zawiera logiczny zrzut PostgreSQL w formacie `custom`, wszystkie prywatne pliki, klucze ochrony danych, wersję aplikacji oraz manifest z rozmiarami i SHA-256. Pliki są niemutowalne, a rekord bazy powstaje dopiero po ich bezpiecznym zapisie, dlatego migawka bazy nie może wskazać niedokończonego pliku.
3. Klient Mac zapisuje pobrany strumień do pliku tymczasowego i szyfruje go porcjami AES-256-GCM. Klucz jest wyprowadzany z hasła odzyskiwania przez PBKDF2-SHA256 z losową solą; każda porcja ma osobny nonce i znacznik autentyczności, a nagłówek formatu jest uwierzytelniony.
4. Hasło odzyskiwania i token są odczytywane z Pęku kluczy macOS. Nie trafiają do pliku konfiguracyjnego, argumentów procesu, logów, nazwy pliku ani archiwum. Adres serwera i folder kopii mogą być zapisane w zwykłej konfiguracji użytkownika.
5. Po zaszyfrowaniu klient odszyfrowuje nowy plik do tymczasowego strumienia, sprawdza manifest, rozmiar i skrót każdego wpisu i dopiero wtedy atomowo zmienia nazwę `.partial` na `.fmbak` oraz zgłasza sukces.
6. Pięć najnowszych poprawnych kopii dziennych jest zachowywanych. Rotacja następuje wyłącznie po weryfikacji nowej kopii. Pliki roczne trafiają do `Archives` i nigdy nie podlegają tej rotacji.
7. `launchd` uruchamia klienta po zalogowaniu i co godzinę. Serwerowy plan zleca najwyżej jedną kopię dzienną oraz zaległe archiwa roczne, więc Mac automatycznie nadrabia po ponownym uruchomieniu.
8. Panel `Kopie` pozwala wygenerować/unieważnić token, zlecić kopię teraz i pokazuje ostatni zweryfikowany sukces albo błąd. Dashboard oraz oszczędne powiadomienie ostrzegają dopiero po skonfigurowaniu tokenu i przekroczeniu 36 godzin bez poprawnej kopii.
9. Odtworzenie najpierw tylko odszyfrowuje i weryfikuje pakiet. Skrypt odmawia pracy, gdy docelowa baza, folder plików albo folder kluczy nie są puste; przywraca do czystej instancji, uruchamia migracje do przodu i porównuje liczby rekordów oraz skróty plików.
10. Bramka automatyczna obejmuje zły sekret, uszkodzony fragment, przerwane pobranie, błąd zapisu, rotację 5→5, zaległy Mac, archiwum roczne, token i izolację właściciela, migrację PostgreSQL oraz pełne odtworzenie sztucznej bazy i plików. Prawdziwa instalacja `launchd`, docelowy folder, hasło offline i odtworzenie na drugim urządzeniu pozostają zewnętrznym P1 przed produkcją.

## Projekt wykonawczy fazy 12

Faza 12 nie jest automatycznym wdrożeniem. Przygotowuje powtarzalny, sprawdzalny proces wydania i pilotażu, a wszystkie działania na prawdziwym VPS, telefonie, Macu, KSeF, SMTP oraz danych firmy pozostawia jako jawne kroki właściciela. Dopóki zewnętrzne bramki nie są podpisane, KSeF produkcyjny, SMTP i automatyczne wystawianie pozostają wyłączone.

1. Obrazy bazowe aplikacji, PostgreSQL i Caddy są przypięte do konkretnych skrótów. Kontrolny test wykrywa powrót do zmiennego tagu. Każde podniesienie wersji wymaga ponownego builda, skanu zależności i testu wycofania.
2. Usługi aplikacyjne otrzymują ograniczenia uprawnień i zasobów, brak nowych przywilejów, kontrolowaną przestrzeń tymczasową oraz rotację logów. Publicznie wystawione pozostają wyłącznie porty 80 i 443; baza, WWW i worker działają wyłącznie w sieci Compose.
3. Read-only skrypt audytu VPS sprawdza system, Docker, zaporę, publiczne porty, wolne miejsce, stan kontenerów, HTTPS, ważność certyfikatu, nagłówki i `/health`. Nie zmienia konfiguracji. Osobny monitor zwraca niezerowy kod przy małej ilości miejsca, kończącym się certyfikacie albo awarii health checku.
4. Procedura wydania wymaga identyfikatora wersji, zielonego builda/testów/skanu, świeżej poprawnej kopii poza VPS, zapisania bieżących skrótów obrazów i jawnego punktu powrotu. Migracje uruchamiają się jako osobna usługa przed przełączeniem WWW.
5. Procedura wycofania nie cofa migracji w ciemno. Najpierw zatrzymuje ruch, zachowuje dowody i kopię, potem wybiera zgodny poprzedni obraz albo pełne odtworzenie na czystej instancji; decyzję podejmuje właściciel na podstawie opisanej tabeli.
6. Automatyczna bramka obejmuje pełny lokalny przepływ właściciela od 2FA przez profil, fakturę, koszt, zamknięcie miesiąca, artefakty, płatność i kopię oraz kontrolowane awarie KSeF, SMTP i workera. Wykorzystuje wyłącznie dane syntetyczne.
7. Macierz odbioru mapuje każde R1–R46 i każde kryterium sukcesu na test automatyczny, próbę ręczną albo jawną otwartą bramkę. Brak dowodu nie może zostać oznaczony jako zaliczenie.
8. Checklista pilotażu zapisuje wersje aplikacji, obrazów i schematów, wynik miesiąca referencyjnego, próbę telefonu i Maca, testową synchronizację KSeF, import urzędowy, testową wiadomość SMTP oraz pełne odtworzenie kopii. Każdy wpis ma wykonawcę, datę, dowód i wynik.
9. Runbook awarii obejmuje KSeF, odrzucony plik urzędowy, brak e-maila, zatrzymany worker, pełny dysk, kończący się certyfikat, utratę telefonu, utratę Maca i nieudaną aktualizację. Dla każdego przypadku podaje bezpieczny pierwszy ruch, zakazane skróty i warunek wznowienia.
10. Lokalny zakres techniczny może zostać zamknięty osobnym review. Cała Faza 12 i projekt pozostają jednak otwarte, dopóki właściciel nie przeprowadzi oraz nie podpisze zewnętrznej checklisty na rzeczywistym środowisku. Nie wykonujemy commita, pushu ani wdrożenia bez osobnego polecenia.

## Ryzyka nadrzędne

- Zmiana przepisów, struktur XML albo API KSeF/ZUS może unieważnić wcześniej poprawny kod.
- Rzeczywiste dokumenty dostawców i umowa samochodu mogą wymagać innych reguł niż założenia ogólne.
- Działanie produkcyjne wymaga danych, dostępów i fizycznych prób na MacBooku/VPS, których nie można zastąpić testem lokalnym.
- Niepełne lub błędne kopie są ryzykiem krytycznym; odtworzenie jest osobną bramką jakości.

## Źródła

- [Wymagania Firemki](../../brainstorms/2026-09-07-jdg-requirements.md)
- [Analiza ideacyjna](../../ideation/2026-09-07-jdg-ideation.md)
- [Pełny plan techniczny](../../plans/2026-09-09-jdg-application-plan.md)
