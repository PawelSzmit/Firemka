# Firemka — kontekst wykonawczy

Ostatnia aktualizacja: 2026-09-15

## Faza 12 — ósme ponowne review: publikacja Git 2026-09-15

- Ponowne sprawdzenie po pierwszym pushu potwierdziło zachowanie obu historii Git bez `force push`; zdalny startowy commit `24483d154b72d5d9c7981da418b82f371b57fc33` pozostaje osiągalny, a stan połączenia historii był `f6bcb44901001519b5c0db9ab1af413da978013b`.
- Lokalny `HEAD`, `origin/main`, `git ls-remote` oraz API GitHuba wskazały ten sam zdalny stan podczas kontroli. Repozytorium jest publiczne, a push obejmował wyłącznie historię Git; nie wysłano obrazów kontenerów i nie wykonano wdrożenia.
- Kontrola całego drzewa nie wykazała sekretów, plików tymczasowych ani przypadkowych kopii lockfile; po przeniesieniu obcego `.git/refs/.DS_Store` do Kosza `git fsck --no-dangling` przechodzi.
- Review `review-faza-12-recheck-8.md` ma wynik **0× P1, 0× P2, 0× P3**. Krok 1 publikacji jest zamknięty, lecz cała Faza 12 nadal czeka na zewnętrzne bramki A3–F6.
- Następny krok to Krok 2: wyłącznie odczytowy audyt docelowego VPS-a po podaniu domeny, potwierdzeniu nazwy połączenia Maca oraz wyborze kanału alarmowego. Sam audyt nie zmienia serwera.

## Faza 12 — siódme ponowne review 2026-09-15

- Po review utworzono commit dowodowy `6317408d16c3ce8d292a98977697b322015898e2`. Zdalny startowy commit z krótkim `README.md` połączono bez `force push`; commit scalający `f6bcb44901001519b5c0db9ab1af413da978013b` zachowuje obie historie i pełniejszą instrukcję lokalną.
- Pierwszy push do publicznego `https://github.com/PawelSzmit/Firemka` zakończył się powodzeniem. Lokalny `HEAD`, `origin/main`, `git ls-remote` i API GitHuba wskazywały ten sam skrót `f6bcb44901001519b5c0db9ab1af413da978013b`; gałąź lokalna śledzi `origin/main`.
- Przed pushem powtórzono kontrolę całego drzewa. Nie znaleziono pliku `.env`, prywatnych kluczy, wysokoprawdopodobnych sekretów, katalogów tymczasowych ani przypadkowych kopii lockfile. Ukryty `.git/refs/.DS_Store`, który zakłócał `git fsck`, przeniesiono do Kosza; ponowne `git fsck --no-dangling` przeszło.
- Push obejmował wyłącznie kod i dokumentację Git. Obrazy kontenerów nie zostały wysłane, a aplikacja nie została wdrożona.
- `review-faza-12-recheck-7.md` ma wynik **0× P1, 0× P2, 0× P3** w zakresie identyfikacji pierwszego wydania. Commit, wersja, cztery pełne identyfikatory obrazów, etykiety OCI i najnowsza migracja są spójne.
- Każdy z czterech obrazów został ponownie przeskanowany. WWW, worker, Caddy i PostgreSQL mają po `0C / 0H`. Chwilowa blokada pamięci skanera przy równoległej próbie WWW zniknęła przy sekwencyjnym powtórzeniu bez zmiany obrazu.
- Protokół `docs/acceptance/phase12-release-20260915-42dc5f4d437e.md` jest zatwierdzony, A1 checklisty jest zaliczone lokalnie, a Krok 1 instrukcji właściciela jest zakończony.
- Granica pozostaje jawna: obrazy są lokalne dla `linux/arm64`; przyszłe skróty prywatnego rejestru i zgodność architektury docelowego VPS-a wymagają osobnego dowodu. W chwili tego review nie wykonano jeszcze pushu; push nastąpił później po osobnej kontroli publikacji. Wdrożenia nadal nie wykonano.
- Następny etap po review publikacji to Krok 2 — wyłącznie odczytowe przygotowanie i wykonanie audytu po wskazaniu domeny, potwierdzeniu zapisanej nazwy połączenia do VPS-a oraz rzeczywistego kanału alarmowego.

## Faza 12 — pierwszy commit i obrazy wydania 2026-09-15

- Po wyraźnej zgodzie właściciela utworzono pierwszy lokalny commit `42dc5f4d437e089d2939102af7a56fa26cde0806` z opisem `feat: implement Firemka MVP with pilot safeguards`. Commit zawiera 570 sprawdzonych plików. Nie wykonano pushu ani wdrożenia.
- Przed commitem pełna lokalna bramka przeszła kompilację Release, 311 testów bez pominięć, 12 dodatkowych prób PostgreSQL, walidację kontraktów, migracji i obrazów oraz skany czterech obrazów bez podatności krytycznych i wysokich.
- Z czystej rewizji utworzono wersję `20260915-42dc5f4d437e`. Obrazy WWW, workera, Caddy i PostgreSQL zapisują pełny skrót commita w etykiecie OCI; ponowne skany każdego obrazu zwróciły `0C / 0H`.
- Pełne identyfikatory czterech lokalnych obrazów i najnowszej migracji zapisano w `docs/acceptance/phase12-release-20260915-42dc5f4d437e.md`. Protokół czeka na `dev-docs-review`; A1 pozostaje niezaznaczone do pozytywnego wyniku.
- Właściciel zezwolił na dalsze lokalne commity wykonywane według potrzeby po zielonej kontroli, a następnie także na potrzebne pushe do `https://github.com/PawelSzmit/Firemka`. Repozytorium GitHub jest publiczne, ma gałąź `main`, pojedynczy własny commit startowy z krótkim `README.md`, a zalogowane konto `PawelSzmit` ma uprawnienie administratora. Zgoda nie obejmuje wdrożenia.
- Następny krok: siódme ponowne `dev-docs-review` Fazy 12 ograniczone do protokołu i powiązania kod → wersja → obrazy.

## Faza 8 — trzecia naprawa i końcowe review 2026-09-14

- Ekran każdej wersji pokazuje teraz osobno datę i dowód zatwierdzenia, datę i referencję ręcznej wysyłki oraz datę i referencję wyniku. Teksty są kodowane przez widok i nie są przepisywane między wersjami.
- Wspólny `PolishBusinessTime` przelicza chwile na `Europe/Warsaw`; widok nie zależy już od strefy systemowej VPS. Test obejmuje przejście z końca miesiąca UTC na pierwszy dzień kolejnego miesiąca w Polsce.
- Trzecie ponowne review w `review-faza-8-recheck-3.md` ma wynik **0× P1, 0× P2, 0× P3** w bezpiecznym zakresie technicznym. Wszystkie znaleziska trzech przeglądów są zamknięte.
- Końcowy dowód: 243 zielone testy i 7 warunkowo pominiętych wcześniejszych prób PostgreSQL, osobny zielony test Fazy 8 na PostgreSQL, Release 0/0, czysty format i model EF, cztery poprawne próbki XSD, poprawny Compose oraz brak znanych podatności w 12 projektach.
- Zewnętrzny import w Kliencie JPK WEB i Płatniku/ePłatniku nadal blokuje rzeczywiste użycie, lecz zgodnie z zatwierdzonym projektem nie blokuje bezpiecznego technicznego zakresu Fazy 9. Nie wykonano commita, pushu ani wdrożenia.

## Drugie ponowne review fazy 8 — 2026-09-14

- Raport `review-faza-8-recheck-2.md` zamyka oba P2 i oba P3 z poprzedniego przeglądu.
- Znaleziono 1 nowy P2: ekran nie pokazuje pełnej zapisanej historii zatwierdzenia, ręcznej wysyłki i wyniku. Znaleziono 1 P3: data utworzenia używa strefy serwera zamiast jawnej strefy Warszawy.
- Faza 9 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do historii i czasu, a potem trzecie ponowne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.

## Faza 8 — druga naprawa po review 2026-09-14

- Generator nie tworzy już częściowego kompletu dla nieobsługiwanego albo historycznie zmienionego profilu VAT. Cała operacja kończy się czytelną blokadą i nie zapisuje żadnego artefaktu; test obejmuje zamknięty kolejny miesiąc oraz zmianę profilu po zamknięciu.
- Fabryka profilu otrzymuje jawną polską datę biznesową. Odrzuca pustą albo przyszłą datę niezależnego potwierdzenia oraz niemożliwą datę urodzenia. Test strony potwierdza, że przyszła data nie zapisuje profilu i pokazuje prosty komunikat.
- Porównanie z istniejącą wersją najpierw rozpoznaje brakujące wymagane teksty, dzięki czemu kontrolowany `ArgumentException` pochodzi z walidacji domenowej zamiast z technicznego `NullReferenceException`.
- Usunięto drugi kontrakt `Application.Filings.ISubmissionGateway`. Pozostał wcześniejszy wspólny port w `Application.ExternalServices`, a test sprawdza zarówno brak duplikatu, jak i brak rejestracji implementacji wysyłkowej.
- Pełna bramka po zmianach ma 242 zielone testy i 7 warunkowo pominiętych wcześniejszych prób PostgreSQL. Test Fazy 8 przeszedł osobno na PostgreSQL. Release ma 0 ostrzeżeń i błędów; format, model EF, cztery próbki XSD i Compose są czyste. Skan wszystkich 12 projektów pozostaje bez zgłoszonych znanych podatności.
- Następny krok: drugie ponowne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.

## Ponowne review fazy 8 — 2026-09-14

- Raport `review-faza-8-recheck-1.md` potwierdza zamknięcie wszystkich pięciu pierwotnych problemów oraz poprawne użycie `BFK` zamiast domyślnego `OFF`.
- Review znalazło 2 nowe P2: historyczna zmiana profilu VAT może po cichu usunąć JPK_V7M z kompletu opartego na wcześniejszym zamknięciu, a profil pozwala zapisać pustą lub przyszłą datę niezależnego sprawdzenia.
- Znaleziono też 2 P3: porównanie istniejącego profilu nadal może wywołać `Trim()` na brakującym tekście oraz pozostały dwa różne kontrakty `ISubmissionGateway`.
- Faza 9 jest zablokowana. Następny krok to `dev-docs-execute` ograniczony do tych czterech napraw, a potem kolejne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.

## Faza 8 — naprawy po review 2026-09-14

- Poprawiono deklarację JPK_V7M: poprzednia nadwyżka trafia do `P_39`, pełny VAT naliczony do `P_48`, a bieżąca nadwyżka do `P_53` i `P_62`. Generator nie wybiera już bez decyzji właściciela zwrotu ani terminu 15 dni.
- Dla zwykłego dokumentu elektronicznego lub papierowego bez numeru KSeF generator stosuje `BFK`, a nie przeznaczone wyłącznie dla trybu awarii `OFF`. Zmiana ma nową wersję generatora, więc wcześniejszy artefakt nie zostanie uznany za aktualny.
- ZUS DRA używa kodu terminu `6`. Identyfikator kompletu ZUS oraz cel złożenia JPK są liczone z rzeczywistych oznaczonych wysyłek: poprawa profilu przed pierwszą wysyłką nadal tworzy `01`/pierwsze złożenie, a dopiero kolejny plik po wysyłce tworzy korektę.
- Każdy artefakt zapisuje dokładny identyfikator wersji profilu i wersję generatora. Odcisk obejmuje zamknięcie, profil, rodzaj, schemat i generator. Poprawiony profil tworzy nową niezatwierdzoną wersję powiązaną z poprzednią.
- Równoczesne generowanie dla firmy i miesiąca jest serializowane transakcyjną blokadą PostgreSQL. Równoczesny identyczny wynik/UPO zwraca zwycięski zapis, a różny wynik daje czytelny konflikt i usuwa przegrany plik. Wymuszony test bazy przeszedł pięć kolejnych uruchomień, w tym ostatnie po zmianie generatora.
- Profil najpierw sprawdza brakujące PESEL i kody, a także odrzuca datę urodzenia sprzed 1900 roku lub niepoprzedzającą utworzenia profilu. Nie zwraca technicznego `NullReferenceException`.
- Po naprawach pełny zestaw ma 238 zielonych testów i 7 warunkowo pominiętych wcześniejszych testów PostgreSQL; test Fazy 8 przeszedł osobno na rzeczywistym PostgreSQL. Kompilacja Release ma 0 ostrzeżeń i błędów, formatowanie jest czyste, model EF odpowiada migracji, cztery próbki przechodzą XSD, Compose z przykładowym środowiskiem jest poprawny, a 12 projektów nie ma zgłoszonych znanych podatności.
- JPK_PKPIR pozostaje zablokowany przed oznaczeniem wysyłki i opisany jako narastający podgląd techniczny. Oficjalne źródło potwierdza roczny termin oraz pola spisu z natury; rzeczywisty plik roczny i wartości remanentu zostaną ustalone świadomie w Fazie 9.
- Następny krok: ponowne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.

## Review fazy 8 — 2026-09-13

- Raport `review-faza-8.md` ma wynik **2× P1, 2× P2 i 1× P3**. Faza 9 pozostaje zablokowana.
- P1: JPK_V7M przechodzi XSD, ale zawsze wybiera 15-dniowy zwrot i zeruje kwotę przenoszoną na następny miesiąc; poprzednia nadwyżka nie trafia do przeznaczonego pola.
- Drugie P1: ZUS DRA używa kodu terminu `1` zamiast właściwego dla obsługiwanej JDG kodu `6` i nie zwiększa identyfikatora kompletu przy korekcie.
- Pierwsze P2: artefakt nie zapisuje wersji profilu, a poprawa danych urzędowych nie tworzy nowej wersji pliku dla tego samego zamknięcia.
- Drugie P2: równoczesny zapis wyniku i UPO sprząta przegrany plik, ale może zwrócić techniczny błąd serwera zamiast rozpoznać identyczny zapis lub czytelnie odrzucić różny.
- P3: walidacja pustych wartości profilu może zakończyć się `NullReferenceException`.
- Następny krok: `dev-docs-execute` ograniczony do tych pięciu napraw, następnie ponowne `dev-docs-review` Fazy 8. Nie wykonano commita, pushu ani wdrożenia.

## Faza 8 — dokumenty US/ZUS i ręczny eksport 2026-09-13

- Dodano wersjonowany profil danych urzędowych oraz trzy osobne generatory: JPK_V7M(3), narastający JPK_PKPIR(3) i ZUS DRA w KEDU 2.27. Każdy XML jest sprawdzany lokalnie względem dokładnej zapisanej wersji XSD przed utworzeniem prywatnego pliku.
- Artefakt wskazuje niezmienną kalkulację zamknięcia, odcisk wejścia, wersję schematu, skrót XML, poprzednią wersję i dokładny prywatny plik. Powtórzenie oraz wymuszony wyścig na PostgreSQL zostawiają jeden komplet trzech artefaktów bez osieroconych plików.
- Pobranie jest zablokowane do świadomego zatwierdzenia. Eksport, ręczne oznaczenie wysyłki oraz przyjęcie lub odrzucenie z prywatnym UPO/potwierdzeniem mają osobny audyt. JPK_PKPIR nie może zostać oznaczony jako wysłany przed przepływem rocznym Fazy 9.
- Korekta zamkniętego miesiąca tworzy nowy komplet wskazujący poprzednie wersje i ponownie wymaga zatwierdzenia. `ISubmissionGateway` pozostaje niezarejestrowaną granicą przyszłej funkcji; żaden ekran ani serwis Fazy 8 nie łączy się z US lub ZUS.
- Generator używa polskiej daty biznesowej na granicy północy, kraju zapisanego przy księgowaniu kosztu i urzędowej reguły `BRAK` przy braku identyfikatora dostawcy. W JPK_PKPIR opcjonalny identyfikator oraz kod kraju są pomijane razem. Adres sprzedawcy jest wymagany przed utworzeniem narastającej KPiR.
- Ekrany profilu i plików przeszły odbiór na danych syntetycznych przy 390 px i 1440 px. Formularz nie pokazuje już pozornej daty urodzenia `01.01.0001`; brak daty pozostaje pustym polem wymagającym świadomego uzupełnienia.
- Pełny zestaw ma 232 zielone testy i 7 warunkowo pominiętych testów wcześniejszych faz. Test PostgreSQL Fazy 8 uruchomiono osobno i przeszedł. Kompilacja Release ma 0 ostrzeżeń i błędów; formatowanie, model EF, cztery próbki XML, składnia Compose oraz skan znanych podatności są czyste.
- Ręczny import każdego rodzaju pliku w aktualnym Kliencie JPK WEB i Płatniku/ePłatniku pozostaje zewnętrzną bramką P1 przed użyciem na rzeczywistych danych. Zgodnie z zatwierdzonym projektem nie blokuje technicznej Fazy 9. Nie wykonano commita, pushu ani wdrożenia.
- Następny krok: `dev-docs-review` Fazy 8. Faza 9 pozostaje zamknięta do wyniku review bez P1/P2 w zaimplementowanym zakresie.

## Ponowne review fazy 7 — po pierwszej naprawie 2026-09-13

- Raport `review-faza-7-recheck-1.md` potwierdził zamknięcie wszystkich pięciu pierwotnych znalezisk: 4× P2 i 1× P3.
- Review wykryło nowy P2. Konflikt źródła poprawnie blokuje miesiąc, lecz obecny model, serwis i ekran nie pozwalają właścicielowi zapisać wyjaśnienia ani oznaczyć konfliktu jako rozwiązany. Zaksięgowany dokument po różnicy KSeF staje się więc trwałą blokadą bez wyjścia.
- Naprawa ma zachować historię wykrycia, dodać datę i obowiązkowe wyjaśnienie rozwiązania, ponownie otwierać konflikt przy kolejnej różnicy oraz ograniczyć czynność do właściciela dokumentu. Blokada miesiąca będzie dotyczyć tylko nierozwiązanego konfliktu.
- Faza 8 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do tego P2, a następnie kolejne `dev-docs-review` fazy 7. Nie wykonano commita, pushu ani wdrożenia.

## Faza 7 — druga naprawa po review 2026-09-13

- Dokument zachowuje flagę i datę wykrycia konfliktu, a osobno zapisuje datę oraz obowiązkowe wyjaśnienie rozwiązania. Wyjaśnienie ma limit 2000 znaków, nie nadpisuje źródła i nie trafia w treści do metadanych audytu.
- Właściciel ma na stronie dokumentu osobny formularz „Zapisz wyjaśnienie i zamknij konflikt”. Formularz wymaga zalogowania, ochrony anty-CSRF i właściciela dokumentu. Po rozwiązaniu lista oraz szczegóły nadal pokazują historię, ale bez czerwonej blokady.
- Zaksięgowany dokument pozostaje zaksięgowany. Dokument będący wcześniej w stanie błędu wraca do sprawdzenia danych. Zamknięcie miesiąca blokuje tylko konflikt nierozwiązany, a test potwierdza, że wyjaśnienie usuwa blokadę i przejściową różnicę odcisku.
- Zapisywany jest skrót konfliktowej treści. Ponowna synchronizacja dokładnie tej samej, już wyjaśnionej różnicy nie otwiera konfliktu; dopiero kolejna inna treść ponownie wymaga decyzji.
- Migracja `20260913102725_Phase7SourceConflictResolution` dodaje trzy pola historii. Jej przejście w dół i ponownie w górę oraz trwałość decyzji przeszły na prawdziwym PostgreSQL.
- Pełny zestaw ma 222 zielone testy: 1 kontraktowy, 71 domenowych, 4 aplikacyjne, 109 infrastruktury, 2 E2E i 35 WWW. Kompilacja Release ma 0 ostrzeżeń i błędów; formatowanie, zgodność modelu EF, cztery próbki XML i skan podatności są czyste.
- Następny krok: drugie ponowne `dev-docs-review` fazy 7. Fazy 8 jeszcze nie rozpoczęto; nie wykonano commita, pushu ani wdrożenia.

## Drugie ponowne review fazy 7 — 2026-09-13

- Raport `review-faza-7-recheck-2.md` potwierdził, że właściciel może wyjaśnić konflikt, a blokada miesiąca znika dopiero po tej decyzji. Izolacja właściciela, anty-CSRF, audyt, PostgreSQL i ponowne otwarcie przy nowym skrócie działają.
- Review wykryło nowy P2: następny konflikt zeruje pola poprzedniej decyzji, więc historia potwierdzeń jest nadpisywana. Synchronizacja zachowuje tylko skrót odmiennego XML, bez prywatnej kopii treści do rzeczywistego porównania.
- Naprawa ma wprowadzić dopisywany wpis dla każdego nowego konfliktu, prywatny plik alternatywnego źródła, widoczną historię decyzji oraz transakcyjne sprzątanie po awarii. Identyczne ponowienie nie może tworzyć kolejnego wpisu ani pliku.
- Faza 8 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do tego P2, a następnie trzecie ponowne `dev-docs-review` fazy 7.

## Trzecie ponowne review fazy 7 — 2026-09-13

- Dopisywana historia, prywatne pliki różnic, brak duplikatów, sprzątanie awarii i migracja danych przeszły review.
- Znaleziono nowy P2 w rzadkim, ale istotnym wyścigu: wyjaśnienie istniejącego konfliktu i zapis nowej różnicy w tej samej chwili mogą pozostawić szybki stan dokumentu niespójny z otwartym wpisem historii.
- Naprawa włącza kontrolę wersji dokumentu, opiera blokadę także na historii, zwraca czytelny komunikat po przegranym zapisie oraz wymusza ten wyścig na PostgreSQL.
- Faza 8 nadal jest zablokowana; nie wykonano commita, pushu ani wdrożenia.

## Faza 7 — naprawa równoległości i końcowe review 2026-09-13

- `StateVersion` dokumentu jest znacznikiem współbieżności. Przegrane równoczesne wyjaśnienie zwraca prośbę o odświeżenie, a przegrany zapis nowej różnicy wycofuje wpis i konfliktowy plik zamiast pozostawić niespójny stan.
- Zamknięcie miesiąca sprawdza otwarte wpisy bezpośrednio w dopisywanej historii. Wymuszony test PostgreSQL ładuje dwie kopie tego samego konfliktu, zapisuje rozwiązanie jedną z nich i potwierdza, że druga nie może dopisać nowego konfliktu na nieaktualnej wersji.
- Migracja `20260913104501_Phase7SourceConflictHistory` tworzy historię oraz przenosi wcześniejsze konflikty. Test migracji przechodzi w dół do fazy 6, przez wersję pośrednią z istniejącym konfliktem i ponownie do najnowszego modelu bez utraty decyzji.
- Raport `review-faza-7-recheck-4.md` ma wynik **0× P1, 0× P2, 0× P3**. Pełny zestaw ma 224 zielone testy; kompilacja Release, formatowanie, model EF, cztery XSD i skan zależności są czyste.
- Faza 7 jest zamknięta technicznie. Można rozpocząć fazę 8 na danych syntetycznych; rzeczywisty KSeF, potwierdzenie zasad podatkowych i produkcja nadal wymagają osobnych bramek.

## Review fazy 7 — 2026-09-12

- Raport `review-faza-7.md` wykazał 0× P1, 4× P2 i 1× P3. Faza 8 pozostaje zablokowana.
- P2 dotyczą braku blokad dla nieobsługiwanych profili VAT/ZUS, pomijania konfliktu źródła w zaksięgowanym dokumencie, wyścigu dwóch różnych korekt oraz możliwości pozornego rozwiązania spóźnionej sprzedaży dowolnym rodzajem korekty.
- P3 dotyczy pobierania całej historii nierozwiązanych dokumentów przed filtrowaniem miesiąca.
- Następny krok: `dev-docs-execute` naprawia wyłącznie te pięć punktów test-first, a potem `dev-docs-review` ponownie ocenia fazę 7. Nie rozpoczęto fazy 8.

## Faza 7 — naprawa po review 2026-09-13

- Dodano osobne blokady nieobsługiwanego profilu VAT i ZUS. Linki prowadzą bezpośrednio do oznaczonych sekcji ustawień, a test sprawdza oba warianty okresowe.
- Zaksięgowany dokument z później wykrytym konfliktem KSeF jest ponownie traktowany jako nierozwiązany. Blokuje zamknięcie, wchodzi do odcisku danych i po wcześniejszym zamknięciu wymusza kontrolowaną korektę.
- Korekta i kontrola nieujemnej sumy są zapisywane w jednej transakcji szeregowej. Test na prawdziwym PostgreSQL był czerwony przed zmianą (zapisywały się obie konkurencyjne korekty), po zmianie przechodzi; dziesięć kolejnych powtórzeń było stabilnie zielonych.
- Dodano bezkwotowy typ `SalesRecognition`: wymaga konkretnej faktury i dowodu, nie zmienia żadnej kwoty i jako jedyny usuwa blokadę spóźnionej sprzedaży. Powiązanie zwykłej korekty z fakturą nie jest już pozorną decyzją o okresie.
- Zapytanie o dokumenty źródłowe filtruje miesiąc po stronie PostgreSQL, uwzględniając datę dokumentu albo warszawskie granice UTC dla rekordu bez tej daty.
- Pełny zestaw po naprawie ma 218 zielonych testów: 1 kontraktowy, 69 domenowych, 4 aplikacyjne, 107 infrastruktury, 2 E2E i 35 WWW. Testy PostgreSQL obejmują trzy scenariusze fazy 7. Kompilacja Release ma 0 ostrzeżeń i 0 błędów; format, model EF, skan podatności oraz cztery próbki XML są czyste.
- Pełna bramka ujawniła także wcześniejszą zależność walidacji kwot od języka procesu na stronach sprzedaży i kosztów. Granice dziesiętne są teraz zawsze odczytywane w stałym formacie, a oba wcześniej czerwone scenariusze WWW wróciły do zieleni.
- Następny krok: ponowne `dev-docs-review` fazy 7. Fazy 8 jeszcze nie rozpoczęto; nie wykonano commita, pushu ani wdrożenia.

## Faza 7 — obliczenia i zamknięcie miesiąca 2026-09-12

- Dodano wersjonowane zestawy reguł, deklaracje danych właściciela, jawne korekty, niezmienne kalkulacje i łańcuch wersji zamknięcia miesiąca. Zamknięcie przelicza dane w jednej transakcji, jest idempotentne przy identycznym równoległym żądaniu i nie nadpisuje wcześniejszej wersji.
- Kalkulator pokazuje osobno PIT, VAT i składkę zdrowotną wraz z liniami źródłowymi i wynikami wzorów. Reguły wbudowane na 2026 rok pozostają materiałem referencyjnym. Zamknięcie wymaga utworzenia nowej wersji po niezależnej weryfikacji, z datą i odniesieniem do dowodu; próba przeglądarkowa używała wyłącznie sztucznego potwierdzenia w usuniętej bazie testowej.
- Ekrany `/Month`, `/Settlements/Month` i `/Settings/Calculations` pokazują stan zaufania, braki, źródła, potwierdzenia, korekty i historię. Potwierdzone zasady z innymi brakami są opisane jako szacunek roboczy, a nie błędnie jako niezatwierdzone zasady.
- Polskie kwoty z przecinkiem działają zarówno po stronie przeglądarki, jak i serwera. Zastąpiono podsumowanie błędów generujące niedozwolony styl inline oraz mobilne menu rozwiązaniami zgodnymi z CSP. Tym samym zamknięto wcześniejszą obserwację P3 z Fazy 6.
- Migracja `20260912154553_Phase7MonthCalculationAndClosing` tworzy sześć tabel Fazy 7. Model EF nie ma oczekujących zmian, a próby PostgreSQL obejmują zapis, wycofanie całej transakcji przy kontrolowanej awarii, powtórzenie oraz konflikty równoległe.
- Pełna bramka przechodzi 214 testów, w tym 104 testy infrastruktury z PostgreSQL, rzeczywistym `pdftotext` i OCR. Kompilacja Release ma 0 ostrzeżeń i 0 błędów; formatowanie, cztery próbki XSD oraz skan podatności NuGet są czyste. Niestabilny test odnowienia dzierżawy został uniezależniony od czasu pierwszego uruchomienia bazy i przeszedł 15 kolejnych powtórzeń.
- Odbiór Playwright na sztucznym koncie i firmie potwierdził 390 px i 1440 px bez przepełnienia strony, działające linki naprawcze, wpisy z przecinkiem, reguły w wersji 2, zamknięcie wersji 1, wykrycie zmiany źródła oraz zamkniętą korektę wersji 2. Nie ma czynności wysyłki US/ZUS, płatności ani zamknięcia roku. Zrzuty: `output/playwright/phase7-month-mobile.png`, `phase7-settlement-mobile.png`, `phase7-settlement-desktop.png`, `phase7-correction-mobile.png`.
- Izolowane kontenery, sieć i woluminy odbioru zostały usunięte po kontroli. Repozytorium nadal nie ma żadnego commita; nie wykonano pushu ani wdrożenia. Następny krok to `dev-docs-review` Fazy 7.

## Faza 6 — wykonanie i dopasowanie eksportu ładowarki 2026-09-12

- Dodano odcisk dokumentu kosztowego, wersjonowane reguły z jawnymi parametrami VAT/KPiR, dwa odrębne tryby zatwierdzenia oraz osobne wpisy KPiR i VAT. Pierwszy koszt pozostaje do decyzji; pełne, późniejsze dopasowanie może zostać automatyczne wyłącznie dla reguły z nie-ręcznymi okresami.
- Dodano cztery niezależne polityki samochodu (najem/leasing, eksploatacja, ubezpieczenie, publiczne ładowanie). Bez procentów i referencji dowodu polityka pozostaje nieaktywna; wersje wcześniejsze są zachowane.
- Dodano import CSV i snapshoty raportów ładowania. Profil nie aktywuje się bez potwierdzenia Wh, raport nie zapisuje KPiR/VAT, a stawka jest pobierana z istniejących okresów energii i zapisywana w raporcie.
- Przeanalizowany eksport z `/Users/pawelszmit/Desktop/Ładowanie samochodu - Lipiec 2026.csv` ma separator przecinek oraz kolumny `session_id`, `device_id`, `status`, `started_at`, `stopped_at`, `time [s]`, `energy [Wh]`, `power [W]`, `tag NFC`, `tag NFC ID`. Kod ma teraz te wartości jako domyślny profil: czas `yyyy-MM-dd HH:mm:ss`, `Europe/Warsaw`, identyfikator `session_id`; dodatkowe kolumny są akceptowane, a energia z wierszy `ABORTED` nie jest cicho odrzucana.
- Dla próbki użytkownika: 9 wierszy danych, 7 `COMPLETED`, 1 `ABORTED`, łącznie `159 624 Wh` (`159,624 kWh`). Przy zapisanej stawce `0,91 zł/kWh` daje to techniczny koszt brutto około `145,26 zł`; nie jest to jeszcze podstawa podatkowa.
- Równoległe zapisy reguł, polityk, profili, raportów i nakładających się plików CSV mają idempotentne ścieżki oraz unikalne ograniczenia PostgreSQL. Dodano syntetyczny fixture formatu w `tests/Fixtures/phase6` bez kopiowania danych użytkownika.
- Weryfikacja: pełny zestaw `Firemka.sln` przechodzi 169 testów w obrazie z `pdftotext` i Tesseract, test PostgreSQL Fazy 6 przechodzi, kompilacja Release ma 0 ostrzeżeń, formatowanie, brak oczekujących zmian modelu EF, walidacja czterech XSD i skan podatności NuGet są czyste. Compose w izolowanym projekcie ma zdrowe PostgreSQL/WWW, zakończoną migrację i działający worker.
- Odbiór Playwright na sztucznym koncie potwierdził widoki Fazy 6 przy 390 px i 1440 px bez poziomego przepełnienia (`scrollWidth == innerWidth`). Zrzuty zapisano w `output/playwright/phase6-vehicle-mobile.png`, `phase6-charging-mobile-defaults.png` i `phase6-charging-desktop.png`.
- Pozostają otwarte bramki zewnętrzne: rzeczywiste zasady podatkowe domowego ładowania i samochodu oraz zanonimizowane faktury dostawców. Nie wykonano commita, pushu ani wdrożenia.

## Aktualny stan

- Katalog jest lokalnym repozytorium Git na gałęzi `main`, nadal bez commitów, pushy i wdrożeń.
- Faza 1 stworzyła rozwiązanie .NET 10 z modułami `Domain`, `Application`, `Infrastructure`, `Web`, `Worker` i `BackupClient`, a także komplet projektów testowych z planu źródłowego.
- Docker Desktop działa na tym MacBooku. Pełne lokalne Compose na danych sztucznych przeszło: PostgreSQL i WWW są `healthy`, migracja zakończyła się powodzeniem, a Caddy i worker działają. Certyfikat lokalnego Caddy jest zaufany w pęku użytkownika macOS, a Safari otwiera bezpieczną stronę konfiguracji.
- Bramka fazy 1 jest zamknięta: właściciel utworzył konto, ustawił TOTP na telefonie oraz potwierdził ponowne logowanie. Nie wprowadzono danych firmy ani sekretów do repozytorium lub rozmowy. Nieblokujące P3 dotyczy dodania graficznego QR obok działającego klucza tekstowego.
- Nie ma jeszcze dostępu do VPS, testowego KSeF, SMTP, przykładowych faktur, CSV ładowania ani umowy/oferty samochodu.

## Uzgodnione decyzje techniczne

- Modułowy monolit C#/.NET 10: `Domain`, `Application`, `Infrastructure`, `Web`, `Worker` i `BackupClient`.
- ASP.NET Core Razor Pages, Identity z TOTP i PostgreSQL 18.
- Oficjalny klient KSeF dla .NET; faktyczne wersje paczek i kontraktów zostaną przypięte po fazie 0.
- Trwałe zadania i outbox w PostgreSQL, bez osobnego Redisa w pierwszej wersji.
- Dokumenty poza KSeF: najpierw tekst PDF, następnie lokalny OCR; właściciel zawsze potwierdza odczyt.
- Kopie MacBooka: osobny klient macOS z `launchd`, pojedynczy szyfrowany plik, manifest, pięć kopii rotacyjnych i archiwum roczne.
- Automatyczne wystawienie abonamentu jest domyślnie wyłączone. Automatyczna wysyłka US/ZUS i import banku są poza pierwszą wersją.

## Reguły pracy

- Każda faza wykonania ma własny wpis w `firemka-jdg-zadania.md` oraz aktualizację tego pliku kontekstu.
- Przegląd zapisuje się jako `review-faza-<numer>.md`.
- P1 blokuje przejście dalej. P2 wymaga naprawy, ponieważ użytkownik wyraźnie zażądał usunięcia wszystkich błędów przed kolejną fazą. P3 jest dokumentowany i oceniany indywidualnie.
- Dane wrażliwe, tokeny, hasła, faktury źródłowe i dane osobowe nie trafiają do dokumentacji ani repozytorium.
- Bez danych referencyjnych nie wolno deklarować poprawności podatkowej lub wysyłać czegokolwiek do urzędu/KSeF.

## Faza 0 — wykonanie 2026-09-09

- Utworzono dokumentację wykonawczą pod `docs/active/firemka-jdg/` oraz osiem materiałów fazy 0 w `docs/research/`, `docs/test-cases/` i `docs/adr/`.
- Pobrano migawki: OpenAPI KSeF test, FA(3), JPK_V7M(3), JPK_PKPIR(3) i KEDU 2.27. Ich SHA-256 zapisano w `docs/research/contracts/SHA256SUMS`.
- Świeża kontrola poprawności przeszła: `shasum -a 256 -c SHA256SUMS` uruchomione z katalogu kontraktów, `xmllint --noout` dla czterech XSD oraz `jq empty` dla OpenAPI.
- Wykryto i naprawiono błąd instrukcji kontroli sum: nazwy w `SHA256SUMS` są względne wobec katalogu kontraktów, dlatego dokumentacja zawiera już dokładne polecenie z `cd`.
- Przygotowano scenariusze zachowania produktu dla zwykłego miesiąca, braku przychodu, straty, korekty, opóźnionej faktury, ładowania, zmiany reguły i blokady zamknięcia. Żaden nie zawiera niepotwierdzonego wyniku podatkowego.
- Bramka pozostaje niezamknięta: brak rzeczywistych dokumentów i niezależnie potwierdzonego miesiąca referencyjnego, brak VPS/Maca oraz danych firmy. Następny krok to review fazy 0, nie faza 1.

## Review fazy 0 — 2026-09-09

- Review zapisano w `review-faza-0.md`; wynik to **blokada przejścia do fazy 1**.
- P1: brakuje danych firmy, zanonimizowanych dokumentów/CSV, umowy lub oferty samochodu (jeśli będzie w zakresie) i niezależnie zatwierdzonych wyników referencyjnych. Kod nie może ich zastąpić założeniami.
- P2: zapisane XSD mają sprawdzoną składnię, lecz brak testowych XML walidowanych względem każdego z nich. To naprawa techniczna pozostająca w zakresie fazy 0.
- Następny krok: `dev-docs-execute` naprawia wyłącznie P2, po czym `dev-docs-review` sprawdza fazę 0 ponownie. Faza 1 pozostaje zamknięta do usunięcia P1.

## Faza 0 — naprawa P2 2026-09-09

- Pobrano lokalne zależności importowane przez urzędowe XSD i zapisano ich SHA-256 w `docs/research/contracts/dependencies/SHA256SUMS`. Oryginalne migawki XSD nie zostały zmienione.
- Dodano `catalog.xml`, który wiąże adresy importów z lokalnymi kopiami, oraz `validate-fixtures.sh`, który używa katalogu i `--nonet`.
- Dodano cztery kontrolne XML: oficjalny przykład JPK_V7M(3), oficjalny testowy DRA z KEDU 2.27 oraz bezpieczne sztuczne przykłady FA(3) i JPK_PKPIR(3). Ich pochodzenie, granice użycia i SHA-256 zapisano w `docs/research/contracts/fixtures/`.
- Świeże wykonanie `bash docs/research/contracts/validate-fixtures.sh` potwierdziło walidację wszystkich czterech XML względem właściwych XSD bez dostępu do sieci. To dowód struktury, nie poprawności podatkowej.
- P2 jest naprawione. Następny krok: ponowne `dev-docs-review` fazy 0; P1 nadal oczekuje na dane i niezależne potwierdzenie właściciela.

## Ponowne review fazy 0 — 2026-09-09

- Raport `review-faza-0-recheck-1.md` potwierdził zamknięcie P2: wszystkie cztery próbki XML przechodzą lokalną walidację względem zapisanych XSD, z pełnym łańcuchem importów i bez sieci.
- P1 nadal blokuje: źródła techniczne nie mogą zastąpić danych firmy oraz niezależnie potwierdzonych wyników referencyjnych.
- Nie rozpoczęto fazy 1. Po bezpiecznym przekazaniu danych i zatwierdzeniu przez księgowego/doradcę trzeba wykonać naprawę P1 i ponownie sprawdzić fazę 0.

## Korekta bramki fazy 0 — 2026-09-09

- Ponownie porównano aktywną checklistę z planem źródłowym. Ten plan wprost pozwala budować techniczny szkielet mimo niezamkniętej bramki danych referencyjnych.
- P1 pozostaje obowiązkową bramką dla obliczeń, automatycznego księgowania i użycia danych rzeczywistej firmy. Nie blokuje fazy 1, która nie będzie zawierać logiki podatkowej, dokumentów firmy, KSeF ani wyjścia produkcyjnego.
- Następny krok: `dev-docs-execute` fazy 1, potem niezależne `dev-docs-review` fazy 1.

## Faza 1 — wykonanie 2026-09-09

- Utworzono `Firemka.sln`, moduły aplikacji oraz projekty testowe `Domain`, `Application`, `Infrastructure`, `Contract`, `Web` i `E2E`. Wersje paczek są centralnie przypięte w `Directory.Packages.props`, a dla wszystkich projektów zapisano `packages.lock.json`.
- Dodano migrację EF Core `InitialIdentity` dla PostgreSQL: tabele Identity, jednorazowy stan kreatora właściciela, zdarzenia audytu logowania i skróty tokenów zaufanych urządzeń. Lokalny manifest `dotnet-tools.json` przypina `dotnet-ef` 10.0.12.
- Aplikacja WWW ma jednorazowy kreator właściciela, obowiązkowy TOTP, jednorazowe kody odzyskiwania, sesje zaufanych urządzeń wygasające po 30 dniach, unieważnienie wszystkich urządzeń, limit prób logowania, ochronę formularzy, nagłówki bezpieczeństwa i audyt bez haseł ani kodów.
- Utworzono prywatną nawigację: „Mój miesiąc”, „Faktury”, „Księgi”, „Rozliczenia”, „Ustawienia”. Ekrany księgowe są wyłącznie pustą skorupą i nie zawierają danych firmy ani obliczeń.
- Dodano `compose.yaml`, `Caddyfile`, `deploy/Dockerfile`, `.env.example`, `.gitignore`, `.dockerignore` i `README.md`. Sekrety są dostarczane przez `.env` lub User Secrets i nie trafiają do repozytorium.
- Test-first: pierwszy test ochrony strony głównej był czerwony, następnie implementacja została doprowadzona do zieleni. Końcowe `dotnet restore Firemka.sln --locked-mode` oraz `dotnet test Firemka.sln --no-restore` przeszły: 11 testów. `dotnet format --verify-no-changes` przeszedł, a skan `dotnet list Firemka.sln package --vulnerable --include-transitive` nie wykazał znanych podatności.
- Wygenerowano idempotentny skrypt migracji PostgreSQL bez łączenia z bazą. `compose.yaml` przechodzi kontrolę składni YAML. Rzeczywiste uruchomienie kontenerów i ręczna próba na MacBooku/telefonie nie były możliwe, ponieważ `docker` nie jest obecny w środowisku.
- Następny krok: `dev-docs-review` fazy 1. Bramka P1 z fazy 0 nadal blokuje wyłącznie produkcyjne obliczenia, księgowanie i użycie danych firmy.

## Review fazy 1 — 2026-09-09

- Raport `review-faza-1.md` stwierdził 1× P1 i 2× P2. Faza 2 nie może się rozpocząć.
- P1 jest zewnętrzny: nie ma działającego Docker Desktop, dlatego nie dowiedziono działania Compose, PostgreSQL, Caddy ani ręcznego przejścia TOTP na telefonie.
- P2 dotyczą pełnego audytu ekranu wznowienia kreatora oraz braku automatycznego testu rzeczywistego limitu logowania.
- Następny krok: `dev-docs-execute` naprawia wyłącznie oba P2, następnie ponowne `dev-docs-review` fazy 1. P1 pozostaje widoczny do ręcznej próby po uruchomieniu Docker Desktop.

## Faza 1 — naprawa P2 2026-09-09

- Ekran wznowienia niedokończonego kreatora zapisuje teraz do audytu zarówno błąd, jak i sukces weryfikacji hasła. Zapis zawiera wyłącznie identyfikator użytkownika (gdy jest znany), wynik, nazwę metody i adres IP — bez hasła ani kodu TOTP.
- Dodano test integracyjny tego przepływu oraz oddzielny test limitu: dziesięć żądań strony logowania z jednego klienta przechodzi, a jedenaste otrzymuje `429`.
- Najpierw nowy test audytu był czerwony (brak zdarzeń), po implementacji oba nowe testy są zielone. Świeże `dotnet restore Firemka.sln --locked-mode`, `dotnet test Firemka.sln --no-restore` i `dotnet format Firemka.sln --verify-no-changes --no-restore` przeszły; pełny zestaw ma 13 testów.
- P2 są zamknięte. Następny krok: niezależne ponowne `dev-docs-review` fazy 1; P1 dotyczące realnego uruchomienia kontenerów i telefonu nadal wymaga Docker Desktop.

## Ponowne review fazy 1 — 2026-09-09

- Raport `review-faza-1-recheck-1.md` potwierdził zamknięcie obu P2: audyt wznowienia kreatora oraz test jedenastego żądania logowania (`429`). Cały zestaw testów ma 13 zielonych testów.
- P1 pozostaje jedyną blokadą. `docker` nadal nie jest dostępny, więc nie udowodniono uruchomienia Compose, migracji PostgreSQL, health checków Caddy i ręcznego TOTP na telefonie.
- Nie rozpoczęto fazy 2. Po uruchomieniu Docker Desktop następny `dev-docs-execute` wykona wyłącznie lokalną próbę kontenerów i telefonu, a następne `dev-docs-review` ponownie sprawdzi fazę 1.

## Faza 1 — próba lokalnego Compose 2026-09-09

- Docker Desktop 29.7.2 jest zainstalowany i jego silnik odpowiada. `docker compose config --quiet` przeszedł z wyłącznie sztucznymi wartościami `firemka_test` oraz `localhost` — nie utworzono pliku `.env` ani nie użyto danych firmy.
- Pierwsze pełne budowanie obrazów zakończyło się powodzeniem. PostgreSQL osiągnął status `healthy`, kontener migracji zakończył się kodem `0`, a WWW osiągnęło status `healthy`; worker także wystartował.
- Caddy nie wystartował. Log Docker Desktop pokazuje zatrzymanie przy `VolumeApprove` dla bind mounta lokalnego `Caddyfile` z katalogu Pulpit. Sam obraz Caddy uruchamia się poprawnie bez montowania plików, więc nie jest to błąd Caddy ani kodu Firemki.
- Bramka P1 pozostaje otwarta. Przed kolejną próbą potrzebna jest decyzja: nadać Docker Desktop dostęp do Pulpitu albo zmienić Compose tak, aby obraz Caddy zawierał wersjonowany `Caddyfile` i nie wymagał montowania pliku z MacBooka. Druga opcja jest rekomendowana, ponieważ ogranicza uprawnienia Docker Desktop i daje powtarzalny obraz.

## Faza 1 — naprawa technicznej części P1 2026-09-09

- Po zatwierdzeniu właściciela dodano `deploy/Caddy.Dockerfile`. Obraz bazuje na `caddy:2.10-alpine` i kopiuje `Caddyfile` podczas budowania; `compose.yaml` nie montuje już pliku z katalogu Pulpit. Zachowano trwałe, nazwane woluminy Caddy dla danych i konfiguracji.
- Najpierw nowy obraz Caddy zbudował się poprawnie. Pełna próba `docker compose -p firemka-phase1-test-caddy-image up --build --wait --wait-timeout 180` przeszła na wyłącznie sztucznych wartościach środowiskowych: PostgreSQL był `healthy`, migracja zakończyła się kodem `0`, WWW było `healthy`, worker uruchomiony, a Caddy wystartował na portach 80/443.
- Sprawdzono trasę Caddy → WWW oraz endpoint `https://localhost/health`; zwrócił `Healthy`. Konfiguracja wewnątrz obrazu przechodzi `caddy validate`, a plik sformatowano zgodnie z `caddy fmt` bez ostrzeżeń. Pełny zestaw testów .NET nadal przechodzi: 13/13; `dotnet format --verify-no-changes` także przeszedł.
- Docker Desktop wymagał jednego dodatkowego restartu po wcześniejszej zawieszonej próbie. Izolowana próba obrazu Caddy bez plików i woluminów po restarcie przeszła, dlatego nie zmieniano kodu aplikacji z powodu tego przejściowego problemu środowiska.
- Caddy wygenerował lokalny publiczny root `Caddy Local Authority - 2026 ECC Root`; wyeksportowano go wyłącznie do `/private/tmp/firemka-caddy-local-root.crt` w celu przyszłego ręcznego zaufania. Nie został zainstalowany w macOS. Ostateczna część P1 nadal wymaga wyraźnej zgody właściciela na tę zmianę systemową, a następnie ręcznego przejścia kreatora z aplikacją TOTP na telefonie.

## Ponowne review fazy 1 — po naprawie Caddy i realnym Compose 2026-09-09

- Raport `review-faza-1-recheck-2.md` potwierdza pełne lokalne uruchomienie Compose na danych sztucznych oraz prawidłowy proxy/HTTPS Caddy. Caddy redaguje próbne ciasteczko w logu, więc nie wykryto wycieku jego wartości.
- P1 nadal wymaga ręcznego zaufania certyfikatowi lokalnego Caddy w macOS i rzeczywistego przejścia kreatora z aplikacją TOTP na telefonie. Tej zmiany systemowej nie wykonano automatycznie.
- Wykryto dwa P2 do poprawy przed fazą 2: ciasteczko anty-CSRF nie otrzymuje flagi `Secure` w odpowiedzi HTTPS oraz pusty worker zapisuje wpis informacyjny co sekundę. Następny krok to `dev-docs-execute` ograniczony wyłącznie do tych dwóch napraw, po nim kolejne `dev-docs-review` fazy 1.

## Faza 1 — naprawa P2 po realnym Compose 2026-09-09

- Dodano konfigurację anty-CSRF dla środowisk innych niż `Development`, która wymusza `CookieSecurePolicy.Always`. Test `Antiforgery_cookie_is_secure_outside_development` został najpierw uruchomiony czerwono (brak `secure`), następnie zielono. Klienci testowi używający ciasteczek działają teraz przez HTTPS, tak jak przeglądarka za Caddy.
- Zmieniono pusty worker z pętli logującej co sekundę na oczekiwanie na anulowanie procesu i pojedynczy wpis startowy. Nie dodano jeszcze żadnego zadania biznesowego ani harmonogramu — to pozostaje poza fazą 1.
- Świeże `dotnet restore Firemka.sln --locked-mode` i `dotnet test Firemka.sln --no-restore` przeszły: 14 zielonych testów. Pełne przebudowanie `firemka-phase1-test-caddy-image` przeszło; `GET https://localhost/Setup` przez Caddy zwrócił ciasteczko anty-CSRF z `secure; samesite=strict; httponly`, a po ponad sześciu sekundach log workera zawierał wyłącznie jeden wpis startowy.
- P2 są przygotowane do ponownego review fazy 1. P1 nie uległo zmianie: właściciel musi jeszcze świadomie zaufać lokalnemu certyfikatowi w macOS i przejść kreator z aplikacją TOTP na telefonie.

## Ponowne review fazy 1 — po naprawie ciasteczka i workera 2026-09-09

- Raport `review-faza-1-recheck-3.md` potwierdził zamknięcie wszystkich P2 fazy 1. Rzeczywisty nagłówek HTTPS ma `Secure` dla ciasteczka anty-CSRF, a worker nie generuje już wpisu co sekundę.
- Pozostaje dokładnie jedna bramka P1: świadome zaufanie do lokalnego certyfikatu Caddy w macOS i ręczne przejście kreatora właściciela z telefonem TOTP. Faza 2 nie może się rozpocząć przed jej zamknięciem.
- Lokalny zestaw nadal zawiera wyłącznie dane sztuczne. Produkcyjny sekret do ochrony kluczy Data Protection i sprawdzenie VPS są osobnym warunkiem przed pilotażem; nie wykonano ani nie zasugerowano wdrożenia.

## Faza 1 — zaufanie lokalnemu HTTPS 2026-09-10

- Po jednoznacznej zgodzie właściciela zweryfikowano odcisk SHA-256 publicznego certyfikatu `Caddy Local Authority - 2026 ECC Root` (`692FF61CC94CB0B67E9775D641FF9DFDB02F573BCED7F6BE07BE6B640D6A3297`) i dodano go wyłącznie do pęku użytkownika `login.keychain-db` macOS.
- Zwykłe połączenie `https://localhost/health`, bez opcji omijającej sprawdzanie certyfikatu, zwróciło `Healthy`. Nie dodano danych firmy, sekretów ani haseł.
- Safari potwierdził bezpieczne `https://localhost/Setup` i pokazał formularz konta właściciela. Chrome oraz przeglądarka wbudowana w Codex nadal odrzucają lokalnego wystawcę; nie użyto obejścia ostrzeżenia certyfikatu.
- P1 nie jest jeszcze zamknięte: właściciel musi samodzielnie przejść kreator konta testowego z telefonem TOTP i potwierdzić pełną ścieżkę, po czym należy wykonać kolejne `dev-docs-review` fazy 1.

## Faza 1 — naprawa blokady kreatora właściciela 2026-09-10

- Ręczna próba w Safari ujawniła, że przycisk „Utwórz konto i ustaw TOTP” pobierał pusty plik `Setup` lub `Login` zamiast przejść do kodu QR. Log Caddy potwierdził odpowiedź `429` na `POST /Setup` z pustą treścią.
- Przyczyną były dwa powiązane błędy konfiguracji: niezalogowana przeglądarka była kierowana do logowania przy pobieraniu plików CSS/JS, a każde zwykłe wejście `GET` na stronę logowania lub konfiguracji zużywało ten sam limit co próba wysłania formularza. Pierwsze załadowanie strony mogło więc wyczerpać limit 10 prób.
- Najpierw dodano trzy testy odtwarzające błąd: wielokrotne wejścia na `/Setup`, dostępność publicznych zasobów strony oraz granicę 10 prób `POST` z czytelną odpowiedzią `429`. Wszystkie trzy testy były czerwone przed zmianą kodu.
- Pliki statyczne są teraz jawnie dostępne przed zalogowaniem, limit dotyczy wyłącznie żądań `POST`, a odpowiedź po przekroczeniu limitu ma typ `text/plain` i polski komunikat zamiast pustego pobrania. Testy naprawy przechodzą 3/3, a pełny projekt WWW 11/11.
- Pełny zestaw rozwiązań ma 15 zielonych testów. Jedyny czerwony test kontraktowy to znana bramka Fazy 0 oczekująca na oficjalny walidator próbek i nie jest związany z logowaniem. `dotnet format Firemka.sln --verify-no-changes --no-restore` przechodzi.
- Przebudowane lokalne Compose jest zdrowe. Baza nadal nie zawiera użytkownika właściciela; wszystkie używane pliki CSS/JS, w tym publikowany pakiet stylów, zwracają `200`, a 11 kolejnych wejść na `/Setup` także zwraca `200`.
- P1 Fazy 1 pozostaje otwarte wyłącznie do ręcznego potwierdzenia pełnej ścieżki: utworzenie właściciela, pokazanie QR, konfiguracja TOTP, zapis kodów odzyskiwania i wejście do części prywatnej. Po tej próbie trzeba wykonać kolejne `dev-docs-review` Fazy 1.

## Ponowne review fazy 1 — po ręcznym TOTP i naprawie limitera 2026-09-10

- Właściciel potwierdził ręcznie utworzenie konta, dodanie klucza do aplikacji uwierzytelniającej, poprawny kod TOTP, wylogowanie i ponowne logowanie. Baza potwierdza jednego użytkownika z włączonym TOTP, zakończony kreator i udane zdarzenia audytu bez odczytywania danych poufnych.
- Raport `review-faza-1-recheck-4.md` ma wynik **0× P1, 0× P2, 1× P3**. Faza 1 jest gotowa do kontynuacji i można rozpocząć fazę 2.
- P3 dotyczy braku graficznego QR. Obecny ekran świadomie obsługuje ręczne dodanie klucza i zadziałał, dlatego poprawka nie blokuje. Ewentualny QR musi być generowany lokalnie, bez ujawniania sekretu zewnętrznej usłudze.
- Znaleziona podczas ręcznej próby blokada `429` została naprawiona test-first: zasoby publiczne nie wymagają logowania, zwykłe wejścia nie zużywają limitu, a odrzucenie ma czytelny komunikat. Testy WWW przechodzą 11/11, formatowanie i skan podatności są czyste, a lokalne Compose pozostaje zdrowe.
- Bramka P1 z fazy 0 nadal obowiązuje osobno przed obliczeniami podatkowymi, rzeczywistymi dokumentami firmy i automatycznym księgowaniem. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.

## Faza 2 — wykonanie 2026-09-10

- Dodano przekrojowy rdzeń modelu z jawnymi stanami dokumentu i kontrolowanymi przejściami oraz dopisywaną historią korekt faktury. Poprzednia wersja pozostaje niezmieniona, a korekta wskazuje ją przez identyfikator. Lista encji z rozdziału 6.3 planu pozostaje mapą modelu docelowego: w tej fazie powstały elementy wspólne dla wielu funkcji, natomiast encje firmowe, podatkowe, OCR, księgowe, zamknięć i wysyłek powstaną w przypisanych im fazach 3–10 razem z właściwymi regułami i testami.
- Dodano prywatny magazyn plików poza katalogiem WWW. Przyjmuje PDF, JPEG i PNG do 20 MB, sprawdza sygnaturę zawartości, zapisuje rozmiar i SHA-256, używa losowego klucza oraz usuwa plik tymczasowy po błędzie. Metadane wiążą plik z właścicielem, a podgląd wymaga zalogowania, sprawdza właściciela i wyłącza pamięć podręczną.
- Dodano `AuditEvent`, transakcyjny outbox oraz trwałe zadania PostgreSQL z kluczem idempotencji, leasingiem, odzyskaniem po wygaśnięciu blokady, opóźnionymi ponowieniami i stanem końcowego błędu. Wykryty w realnym teście wyścig dwóch równoczesnych zapisów naprawiono atomowym `INSERT ... ON CONFLICT`; oba wywołania otrzymują ten sam identyfikator jednego zadania.
- Worker wysyła outbox i wykonuje zadania w osobnych zakresach bazy. Jego konfigurację oddzielono od mechanizmów logowania WWW, dlatego nie tworzy niepotrzebnych kluczy sesji; rutynowe puste zapytania bazy nie zaśmiecają logów informacyjnych.
- Zdefiniowano porty przyszłych integracji: `IKsefGateway`, `IFilingExporter`, `ISubmissionGateway`, `IDocumentExtractor` i `IPaymentMatcher`. Domena nie zależy od PostgreSQL, WWW, OCR ani KSeF.
- Migracja `20260910075958_CoreDataFilesAndJobs` dodaje tabele audytu, zadań, outboxu, dokumentów źródłowych, plików i wersji faktur. Izolowany test na prawdziwym PostgreSQL wykonał migrację w przód, cofnięcie do `InitialIdentity`, ponowne przejście w przód, równoległe dodanie tego samego zadania, równoległy leasing i odzyskanie po kontrolowanej awarii. Każda próba używa losowej bazy i usuwa ją po zakończeniu.
- Pełne lokalne Compose zastosowało migrację do istniejącej bazy bez utraty konta: nadal jest dokładnie jeden właściciel, kreator jest zakończony i TOTP jest włączone. PostgreSQL i WWW są zdrowe, `https://localhost/health` zwraca `Healthy`, prywatny adres pliku bez sesji przekierowuje do logowania, a nazwany wolumin `private-files` jest zapisywalny przez użytkownika aplikacji.
- Świeża weryfikacja: rozwiązanie buduje się bez ostrzeżeń i błędów; 34/34 testy przeszły, w tym test PostgreSQL; `dotnet format --verify-no-changes`, brak oczekujących zmian modelu EF, walidacja zapisanych kontraktów XML, składnia Compose i skan znanych podatności NuGet są czyste. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
- Następny krok to niezależne `dev-docs-review` fazy 2. Faza 3 nie może rozpocząć się przed bramką bez P1/P2.

## Review fazy 2 — 2026-09-10

- Raport `review-faza-2.md` ma wynik **0× P1, 2× P2, 0× P3**. Faza 3 pozostaje zablokowana.
- Pierwsze P2 dotyczy długiego zadania: dwuminutowy leasing wygasa nawet wtedy, gdy worker nadal poprawnie pracuje, ponieważ nie ma okresowego przedłużania. Drugi worker może wtedy przejąć ten sam proces.
- Drugie P2 dotyczy historii pliku: zapis zawiera SHA-256, typ i rozmiar, ale nie wymaga pochodzenia ani wskazania konkretnego rekordu i jego wersji, mimo że plan wymaga pełnej proweniencji.
- Następny `dev-docs-execute` naprawia wyłącznie te dwa P2 test-first, po czym nastąpi ponowne review Fazy 2. Nie rozpoczęto Fazy 3.

## Faza 2 — naprawa P2 2026-09-10

- Kolejka ma teraz atomowe `RenewLeaseAsync`, a osobny `BackgroundJobLeaseRenewer` używa oddzielnego kontekstu bazy, więc nie współdzieli aktywnego kontekstu z długim wykonawcą. Runner odnawia leasing co 30 sekund przy dwuminutowym czasie ważności. Utrata leasingu anuluje wykonawcę i nie oznacza zadania jako zakończone ani błędne przez starego właściciela.
- Test długiego zadania najpierw nie kompilował się z powodu braku mechanizmu odnowienia. Po implementacji potwierdza, że po przekroczeniu pierwotnego czasu blokady drugi worker nadal nie może przejąć aktywnego zadania. Drugi test przejmuje leasing kontrolnie i potwierdza anulowanie starego wykonawcy bez nadpisania nowego właściciela. Test PostgreSQL sprawdza atomowe odnowienie i późniejsze odzyskanie po rzeczywistej awarii.
- Dodano wymagany `StoredFileContext` z typowanym pochodzeniem, rodzajem rekordu, identyfikatorem oraz dodatnim numerem wersji. `StoredFileService` odrzuca pusty lub nieobsługiwany kontekst przed zapisem pliku, a metadane i indeks pozwalają odnaleźć pliki konkretnej wersji.
- Migracja `20260910115743_StoredFileProvenance` dodaje cztery pola i indeks. Ponieważ przed tą fazą nie istniał formularz dokumentów, migracja świadomie wymaga pustej tabeli `StoredFiles`, zamiast przypisywać istniejącym plikom fałszywe pochodzenie. Lokalna tabela była pusta; migracja w przód/cofnięcie przeszła na losowej bazie, a pełne Compose zastosowało ją poprawnie.
- Po naprawie pełne rozwiązanie ma 36/36 zielonych testów, kompilację Release bez ostrzeżeń i błędów, czyste formatowanie, model EF zgodny z migracjami, poprawną składnię Compose, cztery zielone walidacje XSD oraz brak zgłoszonych znanych podatności NuGet. Wszystkie losowe bazy testowe zostały usunięte.
- Lokalne środowisko jest zdrowe, ma trzy migracje, dokładnie jedno konto, zakończony kreator i włączone TOTP. Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny krok to ponowne `dev-docs-review` Fazy 2.

## Ponowne review fazy 2 — 2026-09-10

- Raport `review-faza-2-recheck-1.md` ma wynik **0× P1, 0× P2, 0× P3**. Oba wcześniejsze P2 są zamknięte.
- Długie zadanie odnawia leasing przez oddzielny kontekst bazy, utrata własności anuluje wykonawcę bez nadpisania nowego workera, a testy obejmują także atomowe zachowanie na PostgreSQL.
- Każdy nowy plik wymaga typowanego pochodzenia i powiązania z konkretnym rekordem oraz jego wersją; migracja i test metadanych potwierdzają trwały zapis.
- Bramka pozwala rozpocząć Fazę 3. Nadal nie wolno wdrażać reguł podatkowych ani używać rzeczywistych dokumentów przed zamknięciem oddzielnej bramki danych referencyjnych z Fazy 0.

## Faza 3 — wykonanie 2026-09-10

- Dodano agregat firmy z jednym właścicielem i kontrahentem oraz osobnymi okresami ustawień abonamentu, ceny energii, VAT, ZUS i pojazdu. Pierwszy okres zaczyna się pierwszego dnia miesiąca rozpoczęcia działalności, ale abonament pozostaje należny w pełnej miesięcznej kwocie także przy starcie w środku miesiąca.
- Każda przyszła stawka jest dopisywana jako nowy okres. Rozwiązanie odczytuje ustawienia właściwe dla wskazanego miesiąca i nie nadpisuje poprzednich wartości. Formularz odrzuca okres, który nie zaczyna się pierwszego dnia miesiąca, pokazuje czytelny błąd i zachowuje poprawne wartości pozostałych formularzy.
- Rok podatkowy można otworzyć bez zmiany lat wcześniejszych. Domena i interfejs dopuszczają wyłącznie skalę podatkową; nie ma nieaktywnego przełącznika ryczałtu. Założenia VAT i ZUS są zapisane jako osobne okresy, ale nie uruchomiono żadnych obliczeń podatkowych.
- Dodano uwierzytelniony kreator danych firmy i kontrahenta, ustawienia okresowe oraz ekran „Mój miesiąc”. Dashboard jest projekcją danych źródłowych: pokazuje abonament i liczniki stanów dokumentów. Kwoty podatku, VAT i ZUS są jawnie oznaczone jako jeszcze nieobliczane, więc niepełna prognoza nie jest prezentowana jako wynik końcowy.
- Migracja `20260910124702_CompanyProfilesAndPeriods` tworzy tabele firmy, kontrahenta, lat i pięciu rodzajów okresów wraz z unikalnymi ograniczeniami. Izolowany test na prawdziwym PostgreSQL wykonał migrację w przód, cofnięcie do Fazy 2, ponowne nałożenie i trwały zapis dwóch stawek abonamentu bez zmiany historii.
- W trakcie odbioru przeglądarkowego usunięto pusty skrypt import map blokowany przez CSP i jawnie dopuszczono wyłącznie lokalne skrypty oraz obrazy `data:` używane przez ikony Bootstrapa. Polityka nadal nie zezwala na skrypty inline. Poprawiono też puste wartości kreatora, aby data nie pokazywała technicznego roku `0001`.
- Świeża weryfikacja: całe rozwiązanie buduje się bez ostrzeżeń i błędów; przechodzi 9 testów domeny, 2 aplikacji, 17 WWW i 16 infrastruktury (łącznie 44), w tym rzeczywisty PostgreSQL. Formatowanie jest czyste, a model EF nie ma zmian bez migracji.
- Ręczny odbiór Playwright przeszedł na sztucznym koncie i osobnej bazie: kreator działa na szerokości 390 px, a „Mój miesiąc” na 390 px i 1440 px pokazuje pełne `1 500,00 zł` w październiku oraz ostrzeżenie o braku obliczeń podatkowych. Zrzuty są w `output/playwright/phase3-company-wizard-mobile.png`, `phase3-month-mobile.png` i `phase3-month-desktop.png`.
- Nie użyto prawdziwych danych firmy ani działającego lokalnego konta właściciela. Nie wykonano commita, pushu ani wdrożenia. Następny krok to niezależne `dev-docs-review` Fazy 3; Faza 4 pozostaje zamknięta do review bez P1/P2.

## Review fazy 3 — 2026-09-10

- Raport `review-faza-3.md` ma wynik **0× P1, 4× P2, 0× P3**. Faza 4 pozostaje zablokowana.
- Profil nie zapisuje jeszcze adresu firmy ani opisu usługi abonamentowej wymaganych przed Jednostką 3. VAT, ZUS i pojazd mają osobne tabele okresów, lecz nie mają operacji ani formularzy dopisujących przyszłą zmianę.
- Otwarcie roku odrzuca duplikat, ale pozwala wpisać rok dawny lub pominąć rok pośredni. Domyślny miesiąc dashboardu i ustawień pochodzi z UTC, co na granicy miesiąca może różnić się od dnia w Polsce.
- Bezpieczeństwo, wydajność bieżącej skali, granice warstw, migracja PostgreSQL, formatowanie i responsywne widoki przeszły review bez dodatkowych P1/P2. Następny `dev-docs-execute` naprawia wyłącznie cztery P2 Fazy 3, po czym nastąpi ponowne `dev-docs-review`. Nie rozpoczęto Fazy 4.

## Faza 3 — naprawa P2 2026-09-10

- Profil firmy zapisuje teraz wymagany adres sprzedawcy oraz opis usługi abonamentowej. Pola są obowiązkowe w kreatorze, utrwalane w PostgreSQL i widoczne w podsumowaniu ustawień; test sprawdza ich odczyt po ponownym otwarciu bazy.
- VAT, ZUS i sposób używania samochodu mają typowane wartości, osobne operacje dopisujące przyszły okres, historię oraz formularze ustawień. Nowy okres musi zaczynać się pierwszego dnia miesiąca i być późniejszy od ostatniego; poprzedni miesiąc nadal zwraca poprzednią wartość.
- Nowy rok podatkowy musi być dokładnie rokiem następującym po ostatnio otwartym. Lata wcześniejsze i pominięte są odrzucane z komunikatem wskazującym poprawny kolejny rok.
- Dodano jedno testowalne źródło polskiej daty biznesowej oparte na `Europe/Warsaw`. Dashboard oraz podpowiedzi dat w ustawieniach nie zależą już od daty UTC. Podpowiadany okres jest późniejszy zarówno od bieżącego miesiąca w Polsce, jak i od ostatniego zapisanego okresu.
- Test-first potwierdził wszystkie braki: nowe testy początkowo nie kompilowały się bez pól, operacji i polskiej daty, a test kolejności okresów najpierw wykazał możliwość dopisania okresu wstecz. Po poprawkach pełny zestaw ma 55 zielonych testów automatycznych; test PostgreSQL przechodzi oddzielnie z migracją w przód, cofnięciem do Fazy 2 i ponownym nałożeniem.
- Odbiór w odizolowanej bazie i na wyłącznie sztucznym koncie przeszedł w widoku 390 px i 1440 px. Kreator zawiera oba nowe pola, ustawienia pokazują pięć historii, ręczna zmiana VAT zachowała październik i dopisała listopad, a następna podpowiedź przesunęła się na grudzień. Usunięto też pustą czerwoną kropkę pozostawianą przez niewidoczną listę walidacji. Nie było błędów konsoli.
- Sztuczne konto, baza i kontener odbiorczy zostały usunięte. Lokalne Compose zastosowało migrację, pozostaje zdrowe i zachowało dokładnie jedno istniejące konto z konfiguracją TOTP; tabela firm jest nadal pusta. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
- Następny krok to ponowne `dev-docs-review` Fazy 3. Faza 4 pozostaje zablokowana do raportu bez P1/P2.

## Ponowne review fazy 3 — 2026-09-10

- Raport `review-faza-3-recheck-1.md` ma wynik **0× P1, 0× P2, 0× P3**. Wszystkie cztery wcześniejsze P2 są zamknięte i bramka pozwala rozpocząć Fazę 4.
- Profil zawiera adres firmy i opis usługi; pięć kategorii ustawień zachowuje osobne, dopisywane okresy; lata można otwierać tylko kolejno; bieżący miesiąc oraz podpowiedzi okresów używają polskiej daty biznesowej.
- Pełny zestaw ma 55 zielonych testów bez zewnętrznej bazy, a osobny 56. test PostgreSQL przechodzi z migracją w przód, cofnięciem i ponownym nałożeniem. Kompilacja, formatowanie, model EF, Compose i skan podatności są czyste.
- Ręczny odbiór 390 px i 1440 px przeszedł na usuniętym już sztucznym środowisku. Lokalne Compose jest zdrowe, ma jednego właściciela z zachowaną konfiguracją uwierzytelniania i nadal zero firm.
- Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny dozwolony krok to `dev-docs-execute` Fazy 4; bramka danych referencyjnych z Fazy 0 nadal blokuje dopiero produkcyjne reguły podatkowe i automatyczne księgowanie.

## Faza 4 — wykonanie 2026-09-10

- Dodano prywatną skrzynkę dokumentów przychodzących. Właściciel może wgrać PDF, JPEG lub PNG do 20 MB, zobaczyć dokument obok odczytanych pól, poprawić dane ręcznie albo oznaczyć dokument jako niezwiązany z firmą wraz z powodem. Podgląd wymaga zalogowania i właściciela pliku; specjalna polityka bezpieczeństwa pozwala osadzić go wyłącznie wewnątrz tej samej aplikacji.
- Surowy plik jest zachowywany przed ekstrakcją. Zapis dokumentu, metadanych i zadania OCR jest objęty jedną transakcją bazy, aby nagłe przerwanie nie pozostawiło widocznego dokumentu bez zadania. Worker najpierw próbuje odczytać tekst PDF, a przy skanie używa lokalnego Popplera i Tesseracta z językiem polskim i angielskim; procesy mają limit czasu i rozmiaru wyniku.
- Odczyt OCR jest zawsze propozycją, nie potwierdzonym księgowaniem. Zapisywana jest ogólna pewność oraz pewność poszczególnych pól. Brak numeru, sprzedawcy, daty, dodatniej kwoty albo poprawnej waluty blokuje przejście dalej i pokazuje rozwiązanie. Ręczne potwierdzenie tworzy kolejną rewizję danych i zdarzenie audytu.
- Dodano przychodzącą synchronizację KSeF z oddzielną konfiguracją Test/Demo/Produkcja i dodatkową blokadą użycia produkcji. Adapter korzysta z zachowanego oficjalnego kontraktu OpenAPI: pobiera strony po dacie i kursorze, zapisuje surowy XML, numer KSeF, datę trwałego przechowywania i historię przebiegu. Powtórzenie pomija identyczny dokument, przerwanie wznawia stronę bez duplikatu, a inna treść pod tym samym numerem tworzy konflikt i nie nadpisuje źródła. Import pojedynczej faktury jest transakcyjny.
- Automatyczne testy obejmują powtórzenie synchronizacji, przerwanie w połowie, konflikt zmienionego XML, błędny skrót serwera, wygasłe uprawnienie bez ujawnienia tokenu, nieczytelny skan, ręczne uzupełnienie, walidację pól, izolację pliku, nagłówki podglądu i rzeczywisty formularz WWW. Rzeczywisty PostgreSQL przeszedł migrację w przód, cofnięcie i ponowne nałożenie.
- Utworzono wyłącznie sztuczną fakturę PDF i jej sztuczny obraz PNG w `output/pdf`. PDF został wyrenderowany i obejrzany bez ucięć, a lokalne narzędzia odczytały z obu plików numer i kwotę. To sprawdza rzeczywisty format plików i działanie narzędzi, lecz nie zastępuje zdjęcia wykonanego telefonem ani dokumentu testowego KSeF.
- Świeża weryfikacja: 76/76 zwykłych testów oraz osobny test PostgreSQL są zielone; formatowanie, model EF, cztery kontrakty XML, składnia Compose i skan znanych podatności NuGet są czyste. Przebudowane lokalne Compose jest zdrowe, ma najnowszą migrację, zachowało dokładnie jedno konto z TOTP oraz nadal zero firm i dokumentów. Worker zawiera `pdftotext`, `pdftoppm`, Tesseract oraz języki `pol` i `eng`.
- Bramka zewnętrzna pozostaje jawna: nie otrzymano dostępu testowego KSeF, rzeczywistego PDF ani zdjęcia z telefonu. Oficjalny pakiet klienta KSeF jest publikowany w repozytorium pakietów wymagającym dostępu, dlatego obecna implementacja używa izolowanego adaptera zgodnego z oficjalnym OpenAPI; zgodność tej decyzji z planem oceni osobny review. Synchronizacja pozostaje domyślnie wyłączona i nie użyto żadnego sekretu ani rzeczywistych danych firmy.
- Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny krok to niezależne `dev-docs-review` Fazy 4; Faza 5 nie może rozpocząć się przed wynikiem tego review.

## Review fazy 4 — 2026-09-10

- Raport `review-faza-4.md` ma wynik **1× zewnętrzne P1, 5× P2, 1× P3**. Faza 5 pozostaje zablokowana do naprawy P2 i ponownego review Fazy 4.
- P2 dotyczą: osieroconego pliku po wycofaniu całej operacji, niedomkniętego procesu i próby OCR po anulowaniu, mylącej pewności brakującego pola, niewidocznego konfliktu źródła w stanie końcowym oraz nieograniczonej listy dokumentów.
- Zewnętrzna P1 pozostaje otwarta, ponieważ nie ma testowego dostępu KSeF ani rzeczywistego PDF i zdjęcia z telefonu. Obecny adapter korzysta z oficjalnego OpenAPI zamiast niedostępnego z publicznego NuGet pakietu klienta; przed testem potrzebna jest świadoma decyzja albo dostęp do oficjalnego pakietu.
- Automatyczne testy, PostgreSQL, formatowanie, model EF, XSD, Compose, skan zależności i zdrowie lokalnego środowiska są zielone. Ręczny odbiór Safari nie odbył się, ponieważ Mac był zablokowany.
- Następny `dev-docs-execute` naprawia wyłącznie pięć P2 Fazy 4 test-first. P3 można poprawić razem z testami, lecz nie rozpoczyna to Fazy 5.

## Faza 4 — naprawa P2 i P3 2026-09-11

- Cała operacja dodania dokumentu ma teraz sprzątanie kompensacyjne. Jeśli audyt, kolejka albo końcowy zapis zawiodą po utworzeniu pliku, transakcja wycofuje rekordy, a fizyczna kopia jest usuwana. Test obejmuje ręczny upload, import KSeF oraz rzeczywisty PostgreSQL z kontrolowaną awarią kolejki.
- Anulowanie OCR zawsze zabija całe drzewo procesu zewnętrznego. Próba otrzymuje stan `Interrupted`, datę końca i komunikat o bezpiecznym wznowieniu, a dokument pozostaje w stanie oczekującym. Test uruchamia prawdziwy blokujący proces, anuluje go i potwierdza, że PID już nie działa.
- Pewność jest teraz pokazywana dla konkretnego pola. Brak wyniku w ręcznym OCR daje „brak podpowiedzi”, nawet gdy inne pola mają wysoką ogólną ocenę. Pochodzenie KSeF pozostaje rozróżnione i może użyć pewności danych urzędowego źródła.
- Flaga oraz data konfliktu źródła trafiają do listy i szczegółów. Czerwone ostrzeżenie pozostaje widoczne również wtedy, gdy dokument był wcześniej zaksięgowany lub oznaczony jako niezwiązany z firmą; zachowany plik nadal nie jest nadpisywany.
- Skrzynka pobiera maksymalnie 25 rekordów na stronę, stabilnie sortuje po dacie i identyfikatorze, ogranicza żądany rozmiar strony do 50 oraz pozwala filtrować status. Test 25 sztucznych dokumentów potwierdza brak powtórzeń i pominięć między stronami.
- Sztuczne PDF i PNG zostały dodane do `tests/Fixtures/phase4`. Test odnajduje `pdftotext` i Tesseract przez `PATH`, a ich brak jest jawnym błędem, nie cichym sukcesem. Lokalne kopie w `output/pdf` pozostają artefaktami do oglądania.
- Naprawy mają 85 zielonych zwykłych testów oraz osobny zielony test PostgreSQL. Następny krok to ponowne `dev-docs-review` Fazy 4; Faza 5 nadal nie została rozpoczęta.

## Ponowne review fazy 4 — 2026-09-11

- Raport `review-faza-4-recheck-1.md` ma wynik **1× wcześniej odroczone P1 zewnętrzne, 0× P2, 0× P3**. Wszystkie pięć błędów P2 i P3 są zamknięte.
- Testy potwierdzają pełne sprzątanie po awarii także na PostgreSQL, zakończenie prawdziwego procesu OCR przy anulowaniu, przerwaną historię próby, ostrożne pewności pól, widoczny konflikt po stanie końcowym oraz stabilne stronicowanie i filtr.
- Pełny zestaw ma 85 zielonych testów bez zewnętrznej bazy i osobny zielony test PostgreSQL. Kompilacja Release, migracje, formatowanie, XSD, Compose, skan zależności i lokalne zdrowie są czyste; konto i TOTP zostały zachowane.
- Techniczna bramka pozwala rozpocząć niezależny zakres Fazy 5 na sztucznych danych. Testowe/produkcyjne KSeF oraz rzeczywiste dokumenty pozostają wyłączone do dostarczenia danych i świadomej decyzji o kliencie KSeF.
- Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny dozwolony krok to `dev-docs-execute` Fazy 5 wyłącznie w granicach bezpiecznego zakresu technicznego.

## Faza 5 — wykonanie 2026-09-11

- Dodano jedną miesięczną fakturę abonamentową na firmę i okres, z pełnym miesiącem usługi niezależnym od dnia zatwierdzenia. Szkic powstaje pierwszego dnia miesiąca albo na żądanie, termin płatności wynosi siedem dni od rzeczywistej daty wystawienia, a opóźnione zatwierdzenie poprzedniego miesiąca nie zastępuje faktury miesiąca bieżącego.
- Kwotę pojedynczego szkicu można zmienić bez zmiany stawki przyszłej. Gdy późniejsza zmiana abonamentu trafia na ręcznie zmieniony szkic, aplikacja zachowuje obie wartości i czeka na decyzję właściciela zamiast nadpisywać kwotę. Wystawiona wersja jest niemutowalna.
- Generator tworzy deterministyczny XML FA(3) i sprawdza go lokalnie względem zapisanej oficjalnej struktury wraz z zależnościami. Proces wysyłki zapisuje stan przed kontaktem z KSeF, używa stałego klucza miesiąca, po niepewnym wyniku najpierw sprawdza status i nigdy nie wysyła drugiej kopii w ciemno. Odrzucenie, UPO, numer KSeF, zwrócona treść i różnica między dokumentem wysłanym a odebranym mają osobne stany i historię audytu.
- Automatyczne wystawianie jest wyłączone domyślnie. Wymaga jednocześnie świadomego potwierdzenia właściciela oraz osobnego odblokowania operatora; konfiguracja produkcyjna i automatyczna pozostaje wyłączona. Brak zatwierdzonego rzeczywistego adaptera powoduje twardą blokadę wysyłki, więc sama zmiana zwykłej flagi nie uruchomi połączenia z KSeF.
- Worker ma idempotentne zadania utworzenia szkicu i wystawienia. Równoległe procesy na rzeczywistym PostgreSQL tworzą jeden szkic i wykonują najwyżej jedno wysłanie. Testy obejmują także odrzucenie, przerwanie lub limit czasu po wysłaniu, ręczną i automatyczną ścieżkę tego samego dokumentu oraz porównanie dokumentu zwróconego.
- Dodano responsywne ekrany listy i szczegółów sprzedaży. Odbiór Playwright na osobnym sztucznym koncie i bazie przeszedł dla szerokości 390 px. W trakcie próby wykryto i naprawiono dwa błędy: angielski zapis kwot oraz odczyt `1650,50` jako `165050`. Po poprawce przecinek i kropka dziesiętna są testowane, a ekran pokazuje `1 650,50 zł`. Zrzuty znajdują się w `output/playwright/phase5-sales-mobile.png` i `phase5-sales-list-mobile.png`.
- Migracja `20260911135310_SalesInvoicesAndOutgoingKsef` dodaje faktury i ustawienia automatyzacji. Świeża weryfikacja ma 113 zielonych zwykłych testów; jedyny pominięty w tym przebiegu test PostgreSQL uruchomiono osobno i przeszedł. Kompilacja Release ma 0 ostrzeżeń i błędów, formatowanie jest czyste, model EF odpowiada migracji, cztery próbki XML przechodzą XSD, konfiguracja Compose jest poprawna, a zależności nie mają zgłoszonych znanych podatności.
- Przebudowane lokalne Compose jest zdrowe i zachowało jedno istniejące konto z TOTP. Baza nadal ma zero firm, szkiców sprzedaży i ustawień automatyzacji, więc nie użyto konta właściciela ani rzeczywistych danych. Izolowane sztuczne konto, baza i kontenery odbiorcze zostały usunięte.
- Zewnętrzna bramka pozostaje otwarta: brak bezpiecznie przekazanego dostępu testowego KSeF i zatwierdzonego rzeczywistego adaptera uniemożliwia próbę wysłania, odbioru UPO i porównania treści z prawdziwym środowiskiem testowym. Do tego czasu wysyłka i automatyzacja pozostają twardo wyłączone.
- Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny krok to niezależne `dev-docs-review` Fazy 5; Faza 6 pozostaje zamknięta do wyniku review bez nowych P1/P2 w bezpiecznym zakresie technicznym.

## Review fazy 5 — 2026-09-11

- Raport `review-faza-5.md` ma wynik **1× wcześniej odroczone P1 zewnętrzne, 3× P2, 0× P3**. Faza 6 pozostaje zablokowana.
- Pierwsze P2 dotyczy automatycznego zadania: odpowiedź oczekująca lub niepewna kończy pracę kolejki, więc numer KSeF i UPO mogą już nigdy nie zostać pobrane bez ręcznego kliknięcia.
- Drugie P2 dotyczy odrzucenia: zapisany stan jest trwały, nie ma kontrolowanej poprawy ani nowej próby, a unikalny miesiąc uniemożliwia utworzenie drugiego szkicu.
- Trzecie P2 dotyczy dwóch równoczesnych sprawdzeń statusu: końcowy zapis nie obsługuje przegranego konfliktu współbieżności tak jak pierwsze wysłanie.
- Data, pełny okres, termin płatności, ręczna stawka, generator FA(3), pierwsze wysłanie najwyżej raz, blokady automatyzacji, polski format i separator dziesiętny przeszły review. Automatyzacja, produkcja i rzeczywisty KSeF pozostają wyłączone.
- Następny krok to `dev-docs-execute` ograniczony do trzech P2, po nim ponowne `dev-docs-review` Fazy 5. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.

## Faza 5 — naprawa P2 2026-09-11

- Automatyczny handler nie uznaje już stanu `Sending` ani `DeliveryUncertain` za zakończenie. Zleca kontrolowane ponowienie, a kolejne wykonanie trafia do istniejącej ścieżki sprawdzenia statusu i nie wywołuje ponownie wysyłki. Zadanie wystawienia ma 101 prób zamiast zwykłych pięciu, co przy ograniczonym odstępie ponowień daje czas na asynchroniczne rozstrzygnięcie bez nieskończonej pętli. Test `Pending` → `Accepted` kończy się numerem KSeF i UPO przy dokładnie jednym wysłaniu.
- Odrzucona faktura ma świadomą czynność „Otwórz do poprawy”. Przed powrotem do szkicu pełny stan odrzuconej próby, XML, referencje i powód są utrwalane jako niezmienna wersja. Nowa rewizja czyści wyłącznie dane transmisji, otrzymuje nowy klucz idempotencji i może być poprawiona oraz wysłana ponownie. Test potwierdza jedną fakturę miesiąca, dwie powiązane wersje historii i dwa celowe wysłania dwóch różnych prób.
- Wszystkie zapisy wyniku sprawdzenia statusu obsługują konflikt współbieżności. Przegrany proces usuwa własne niezapisane zmiany i odczytuje zwycięski stan; nie tworzy drugiej wersji ani nie zwraca błędu. Test na rzeczywistym PostgreSQL uruchamia dwa równoczesne `Accepted` i potwierdza jeden stan `Issued`, jedną wersję oraz brak dodatkowego `SendAsync`.
- Pełny zestaw bez zewnętrznej bazy ma 115 zielonych testów i jeden warunkowo pominięty test PostgreSQL. Ten test PostgreSQL uruchomiono osobno i przeszedł. Kompilacja Release ma 0 ostrzeżeń i błędów; formatowanie, model EF, cztery próbki XSD, składnia Compose i skan zależności są czyste.
- Lokalne Compose zostało przebudowane. Stare hasło wyłącznie lokalnej bazy testowej nie odpowiadało obecnej sztucznej konfiguracji, dlatego zsynchronizowano rolę `firemka_test` z dokumentowaną testową wartością bez usuwania wolumenu. Po operacji PostgreSQL i WWW są zdrowe, Caddy i worker działają, migracja pozostaje najnowsza, a baza zachowała jedno konto z TOTP oraz zero firm, faktur i ustawień automatyzacji.
- Rzeczywisty adapter KSeF, testowe wysłanie i produkcja pozostają wyłączone przez wcześniej odroczoną bramkę zewnętrzną P1. Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny krok to ponowne `dev-docs-review` Fazy 5; Faza 6 nadal jest zablokowana.

## Ponowne review fazy 5 — po pierwszej naprawie P2 2026-09-11

- Raport `review-faza-5-recheck-1.md` potwierdza zamknięcie trzech pierwotnych P2: kontrolowane ponawianie statusu bez drugiego wysłania, poprawę odrzuconej próby z łańcuchem wersji oraz bezpieczny równoległy wynik `Accepted` na PostgreSQL.
- Review wykryło 2 nowe P2 w świeżych ścieżkach. Po pierwsze, cofnięcie zgody na przyszłą automatyzację po `SendAsync` blokuje także obowiązkowe sprawdzenie już wysłanej faktury, ponieważ zgoda jest weryfikowana przed odczytem stanu. Po drugie, dwa równoczesne żądania otwarcia odrzucenia mogą zakończyć przegrany proces wyjątkiem współbieżności lub unikalnej wersji.
- Faza 6 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do tych dwóch P2, a następnie drugie ponowne `dev-docs-review` Fazy 5. Zewnętrzna bramka testowego KSeF nadal pozostaje otwarta; produkcja i automatyzacja są wyłączone.

## Faza 5 — druga naprawa P2 2026-09-11

- Rozpoznanie stanu faktury następuje teraz przed sprawdzeniem zgody na nowe automatyczne wystawienie. Dla szkicu nadal są wymagane jednocześnie zgoda właściciela i operatora, lecz faktura już wysłana w stanie `Sending` albo `DeliveryUncertain` może pobrać wyłącznie status po zwykłym sprawdzeniu dostępności adaptera. Test cofa zgodę właściciela oraz blokuje nowe automatyczne wysyłki po `Pending`, po czym odbiera `Accepted`, UPO i numer KSeF przy jednym `SendAsync`.
- Ponowne otwarcie odrzuconej faktury jest idempotentne. Jeśli drugi proces zastanie już otwarty szkic z zachowaną wersją, kończy bez zmian. Jeśli oba procesy odczytają `Rejected`, przegrany konflikt znacznika lub unikalnej wersji czyści własne niezapisane encje, odczytuje zwycięski szkic i również kończy bez błędu. Test na PostgreSQL potwierdza jeden szkic i jedną wersję historii.
- Testy były czerwone przed zmianą: pierwszy zatrzymywał się na komunikacie o wyłączonej automatyzacji, drugi na unikalności numeru wersji. Po poprawce oba są zielone. Pełny zestaw ma 115 zielonych testów i jeden warunkowo pominięty test PostgreSQL; pełny test PostgreSQL przechodzi osobno.
- Kompilacja Release ma 0 ostrzeżeń i błędów; formatowanie, model EF, cztery próbki XSD, składnia Compose i skan zależności są czyste. Przebudowane lokalne Compose jest zdrowe, ma najnowszą migrację, zachowało jedno konto z TOTP oraz nadal zero firm, faktur i ustawień automatyzacji.
- Nie wykonano commita, pushu ani wdrożenia produkcyjnego. Następny krok to drugie ponowne `dev-docs-review` Fazy 5; Faza 6 nadal nie została rozpoczęta.

## Drugie ponowne review fazy 5 — 2026-09-11

- Raport `review-faza-5-recheck-2.md` ma wynik **0× P1, 0× P2, 0× P3** w zaimplementowanym zakresie technicznym. Wszystkie pięć P2 z dwóch wcześniejszych review jest zamkniętych.
- Potwierdzono jedno wysłanie przy kontrolowanych ponowieniach statusu także po cofnięciu zgody na przyszłą automatyzację, pełny łańcuch wersji po odrzuceniu i poprawie, idempotentne równoczesne otwarcie oraz jeden wynik końcowy przy dwóch `Accepted` na PostgreSQL.
- Pełny zestaw ma 115 zielonych testów bez zewnętrznej bazy; osobny test PostgreSQL również jest zielony. Kompilacja, formatowanie, model EF, XSD, Compose, skan zależności i lokalne zdrowie są czyste. Konto oraz TOTP zostały zachowane, a dane firmy nadal nie istnieją.
- Faza 5 jest gotowa do kontynuacji w bezpiecznym zakresie technicznym. Brak rzeczywistego adaptera i testowego dostępu KSeF nadal blokuje wyłącznie realną integrację oraz każde użycie produkcyjne. Nie wykonano commita, pushu ani wdrożenia.

## Wymagania wejściowe od właściciela

| Przed fazą | Potrzebne dane lub działanie |
|---|---|
| 0 / 7 | niezależnie potwierdzony miesiąc referencyjny i tabela zasad podatkowych |
| 1 | uruchomienie Docker Desktop do ręcznej próby lokalnego środowiska i telefonu; nie wymaga danych firmy |
| 3 | data startu firmy, dane firmy/kontrahenta, opis usługi, kwota i VAT abonamentu |
| 4-5 | bezpiecznie przekazany dostęp testowy KSeF i przykładowe dokumenty |
| 6 | CSV ładowania, faktury AI/VPS, umowa lub oferta samochodu |
| 8 | faktyczny zestaw dokumentów ZUS/US oraz test importu w narzędziach urzędowych |
| 10 | dane SMTP albo wskazanie dostawcy e-mail |
| 11 | docelowy folder MacBooka i hasło odzyskiwania przechowywane poza kopią |
| 12 | audytowany VPS, test telefonu/Maca i ręczne zatwierdzenie pilotażu |

## Review fazy 6 — 2026-09-12

- Raport `review-faza-6.md` ma wynik **0× P1, 0× P2 i 1× odroczona obserwacja P3**. Reguły kosztów, cztery polityki samochodu oraz profil/import/raport ładowania są zgodne z planem i mają zielone testy domeny, infrastruktury, PostgreSQL oraz stron.
- Domyślny profil ładowarki jest ustawiony na przekazany format: przecinek, `started_at`, `energy [Wh]`, `yyyy-MM-dd HH:mm:ss`, `Europe/Warsaw`, `session_id`. Rzeczywisty plik nie został zapisany w repozytorium; użyto wyłącznie syntetycznej kopii do testów.
- Jedyna uwaga P3 dotyczy blokowanego przez CSP stylu inline generowanego przez skrypt walidacji pustego podsumowania. Nie blokuje następnej fazy i nie osłabia walidacji serwerowej.
- Techniczna bramka pozwala rozpocząć Fazę 7. Otwarte pozostają niezależne potwierdzenie zasad podatkowych, podstawa księgowania ładowania i dokumenty samochodu. Nie wykonano commita, pushu ani wdrożenia.

## Faza 9 — wykonanie 2026-09-14

- Dodano wersjonowane zamknięcie roku, które wymaga zakończenia 31 grudnia, wszystkich właściwych miesięcy od daty rozpoczęcia firmy, braku zmian po zamknięciu miesięcy, potwierdzonego profilu urzędowego oraz osobno potwierdzonych danych rocznych. Pierwszy rok rozpoczęty w październiku wymaga wyłącznie października–grudnia.
- Podsumowanie zachowuje dokładne wersje miesięcy i pokazuje przychód, koszty przed i po spisie z natury, dochód albo stratę, należne i wpłacone zaliczki PIT oraz roczne wyliczenie zdrowotnej. Różnica roczna wobec sumy miesięcznych składek należnych jest oddzielona od porównania z faktycznymi wpłatami; wynik zdrowotny pozostaje jawnie oznaczony jako roboczy i wymagający niezależnego sprawdzenia.
- Roczny `JPK_PKPIR(3)` powstaje z pełnych zapisów roku, zawiera potwierdzone pola spisu `P_1`/`P_2`, uwzględnia je w `P_3`/`P_4` i przechodzi lokalną walidację XSD. Dokładna wersja ma własny stan zatwierdzenia, ręcznej wysyłki, przyjęcia albo odrzucenia oraz prywatny plik UPO/potwierdzenia; Firemka nie wysyła dokumentu automatycznie.
- Generator PDFsharp/MigraDoc tworzy dwustronicowe zestawienie A4 z polskimi znakami, kwotami, granicą zakresu, źródłami, czasem polskim i numeracją stron. Plik `output/pdf/firemka-zestawienie-roczne-2026-sample.pdf` został ponownie wyrenderowany do dwóch obrazów i sprawdzony wizualnie bez ucięć, nakładania treści ani błędnych znaków.
- Zamknięty rok jest tylko do odczytu. Osobna korekta wymaga powodu, zachowuje poprzednią wersję i dopiero wtedy odblokowuje korektę miesiąca. Każde ponowne zamknięcie tworzy nowe prywatne PDF i XML oraz zapisuje zlecenie archiwum; właściwy zaszyfrowany plik archiwalny pozostaje zakresem Fazy 11.
- Migracja `20260914102849_Phase9AnnualClosing` przeszła na odizolowanym PostgreSQL w przód, cofnięcie do Fazy 8 i ponowne nałożenie. Dwa równoczesne zamknięcia utworzyły jeden rekord, jeden komplet plików i jedno zlecenie archiwum; stan przyjętego JPK i UPO został odczytany z nowego kontekstu bazy. Tymczasowy kontener i baza zostały usunięte.
- Pełny zestaw ma 253 zielone testy i 8 standardowo pominiętych testów PostgreSQL; nowy test PostgreSQL Fazy 9 przeszedł osobno. Kompilacja Release ma zero ostrzeżeń i błędów, formatowanie jest czyste, model EF nie ma zmian bez migracji, cztery próbki XML przechodzą zapisane XSD, konfiguracja Compose jest poprawna z `.env.example`, a skan NuGet nie zgłasza znanych podatności.
- Źródłowy generator widoków Razor SDK 10 błędnie odrzucał poprawne strony po pełnej przebudowie. Projekt WWW korzysta z udokumentowanego alternatywnego trybu kompilacji Razor (`UseRazorSourceGenerator=false`), który tworzy osobny zestaw widoków; zwykła i Release kompilacja całego rozwiązania przechodzą. Nie wykonano commita, pushu ani wdrożenia.
- Następny krok to niezależne `dev-docs-review` Fazy 9. Faza 10 pozostaje zablokowana do review bez P1/P2.

## Faza 9 — naprawy i ponowne review 2026-09-14

- Pierwsze review wykryło 1× P1 i 4× P2: korekty roku nie dało się ponownie zamknąć, operacje roku i miesiąca nie miały wspólnej blokady, historyczny JPK można było zatwierdzić, późna faktura otrzymywała datę spoza roku, a równoległe UPO nie było idempotentne.
- Ponowne zamknięcie jawnie zapisuje nowy komplet migawek miesięcy. PostgreSQL potwierdził po trzy odrębne migawki w wersji 1 i 2 bez przenoszenia ani nadpisywania historii.
- Wspólna blokada transakcyjna serializuje zapis danych rocznych, zamknięcie i korektę roku oraz otwarcie i zamknięcie korekty miesiąca. Wymuszony wyścig kończy się jednym dozwolonym działaniem i nigdy nie pozostawia zamkniętego roku z otwartym miesiącem.
- Tylko najnowszą wersję rocznego JPK można zatwierdzić i oznaczyć jako wysłaną. Wynik nadal można przypiąć do starszej wersji faktycznie wysłanej przed korektą. Identyczne równoległe UPO zwracają ten sam zapis, a różne kończą się jednym zwycięzcą i posprzątanym plikiem przegranego żądania.
- Roczny JPK używa daty wystawienia tylko z zamykanego roku; potwierdzona późna faktura otrzymuje datę swojego miesiąca usługi. Test odczytuje `K_2` z wygenerowanego XML.
- Pełny zestaw ma 256 zielonych testów i 9 standardowo pominiętych testów PostgreSQL. Dwa testy Fazy 9 przeszły osobno na rzeczywistym PostgreSQL. Kompilacja Release, format, model EF, XSD, Compose i skan zależności są czyste.
- Raport `review-faza-9-recheck-1.md` ma wynik **0× P1, 0× P2, 0× P3**. Faza 10 może rozpocząć się wyłącznie w bezpiecznym zakresie technicznym, bez rzeczywistego SMTP. Nie wykonano commita, pushu ani wdrożenia.

## Faza 10 — wykonanie, naprawy i ponowne review 2026-09-14

- Dodano stronę pełnych płatności oraz projekcję należności z wystawionej faktury i najnowszej zamkniętej wersji miesiąca. Płatność zachowuje wersję, datę, dokładną kwotę i unikalną referencję; kwota częściowa albo nadmiarowa jest odrzucana, a korekta tworzy nowy nieopłacony cel bez zmiany historii.
- Terminy PIT i ZUS są liczone na 20. dzień, VAT na 25. dzień następnego miesiąca, z przesunięciem przez weekendy i polskie święta. Dashboard pokazuje terminy i stan płatności oraz jawnie opisuje kopie jako jeszcze nieskonfigurowane.
- Worker tworzy trwałe, dziennie deduplikowane powiadomienia: podatki 7/2/0 dni przed terminem, faktura klienta dzień po terminie, bieżąca faktura wymagająca działania oraz nowe i trwające błędy. Wiadomość jest ogólna, bez kwot, załączników i danych dokumentu; prowadzi do Firemki wymagającej logowania.
- SMTP pozostaje domyślnie wyłączone. Włączenie wymaga hosta, nadawcy i TLS; pusta nazwa użytkownika nie powoduje użycia poświadczeń systemowych. Brak danych prawdziwej skrzynki jest nadal zewnętrzną bramką przed jakąkolwiek wysyłką.
- Pierwsze review znalazło trzy P2: tłumienie ponownego błędu tego samego dnia, przypisywanie ogólnego zadania każdemu kontu oraz możliwość wyłączenia TLS. Wszystkie naprawiono i pokryto testami; raport `review-faza-10-recheck-1.md` ma wynik **0× P1, 0× P2, 0× P3**.
- Pełny zestaw ma 267 zielonych testów i 10 standardowo pominiętych testów PostgreSQL. Osobny test Fazy 10 przeszedł na rzeczywistym PostgreSQL z migracją przód/cofnięcie/przód oraz wymuszonym wyścigiem płatności i powiadomień. Release ma 0 ostrzeżeń i błędów; format, model EF, cztery XSD, Compose i skan 12 projektów są czyste. Tymczasowy kontener został usunięty.
- Nie wykonano commita, pushu ani wdrożenia. Następny krok to `dev-docs-execute` Fazy 11 w bezpiecznym zakresie technicznym: szyfrowanie, token tylko do pobierania, klient Mac, rotacja i próbne odtworzenie na danych syntetycznych.

## Faza 11 — wykonanie 2026-09-15

- Dodano osobny, odwoływalny token kopii przechowywany wyłącznie jako SHA-256. Token obsługuje tylko plan, strumień eksportu i raport wyniku; panel nadal wymaga zwykłego logowania z 2FA. Równoległe tworzenie tokenu i ręcznego zlecenia jest serializowane na PostgreSQL.
- Serwer tworzy strumieniowy ZIP z logicznym zrzutem PostgreSQL, prywatnymi plikami, kluczami ochrony danych oraz manifestem rozmiarów, SHA-256, wersji i dokładnych liczników wszystkich tabel. Obraz WWW zawiera zgodny z bazą klient PostgreSQL 18 i działa jako użytkownik bez praw administratora.
- Klient Mac szyfruje strumień porcjami AES-256-GCM z PBKDF2-SHA256, losową solą, osobnym nonce i znacznikiem każdej porcji oraz uwierzytelnionym końcem. Zły sekret, uszkodzenie i ucięcie są wykrywane. Dopiero ponowne odszyfrowanie i pełna kontrola manifestu pozwalają atomowo utworzyć `.fmbak`.
- Token i hasło są pobierane wyłącznie z Pęku kluczy macOS. Konfiguracja zawiera tylko HTTPS i pełną ścieżkę folderu. `launchd` uruchamia program po zalogowaniu i co godzinę; plan serwera nadrabia brakujący dzień i archiwa roczne.
- Pięć najnowszych poprawnych kopii dziennych jest zachowywanych po udanej weryfikacji. Archiwa roczne trafiają do `Archives`. Jeśli zgłoszenie sukcesu nie dotrze do serwera, lokalny dziennik jest weryfikowany i ponawiany przed kolejnym pobraniem, więc nie powstaje zbędna druga kopia.
- Panel `Kopie` pokazuje token tylko raz, pozwala go unieważnić i zlecić kopię, pokazuje oczekiwanie, ostatni sukces i błąd. Dashboard oraz powiadomienie ostrzegają dopiero po skonfigurowaniu i po 36 godzinach bez sukcesu albo po rzeczywistej awarii.
- Odtworzenie najpierw odszyfrowuje i sprawdza cały pakiet. Skrypt przed zmianą bazy ponownie kontroluje skróty i manifest, odmawia niepustej bazy/folderów, przywraca zrzut, pliki i klucze oraz porównuje liczniki i SHA-256.
- W trakcie rzeczywistej próby HTTP wykryto synchroniczne domknięcie ZIP, które przerywało odpowiedź. Dodano strumień bezpiecznie buforujący wyłącznie metadane ZIP oraz test odpowiedzi dopuszczającej tylko zapis asynchroniczny. Powtórna próba z PostgreSQL 18 pobrała pełny pakiet 181 730 bajtów z 58 tabelami.
- Pełny zestaw ma 286 zielonych testów i 12 standardowo pominiętych testów PostgreSQL. Oba testy Fazy 11 przeszły osobno na rzeczywistym PostgreSQL, w tym pełne szyfrowanie i odtworzenie wszystkich liczników i plików. Release ma 0 ostrzeżeń/błędów; format, model EF, XSD, Compose, skrypty, plist, obraz oraz skan zależności są czyste.
- Protokół techniczny znajduje się w `docs/acceptance/phase11-synthetic-restore-protocol.md`. Prawdziwy folder, hasło offline, instalacja `launchd` i odtworzenie na drugim urządzeniu pozostają zewnętrzną bramką P1 przed produkcją. Nie wykonano commita, pushu ani wdrożenia.
- Następny krok to osobne `dev-docs-review` Fazy 11. Faza 12 pozostaje zablokowana do review bez P1/P2 w bezpiecznym zakresie technicznym.

## Faza 11 — naprawy i ponowne review 2026-09-15

- Skrypt odtworzenia wymaga teraz migratora dokładnie tej wersji WWW, która ma zostać uruchomiona, i wykonuje migracje przed końcową kontrolą skrótów oraz liczników. Obraz WWW zawiera `jq`, klienta PostgreSQL 18 i `/app/Firemka.Web.dll`; instrukcja opisuje bezpieczne przekazanie połączenia przez sekret środowiska.
- Klient odtworzenia śledzi własne pliki i foldery, a po błędzie usuwa wyłącznie je. Czerwony wcześniej test dodaje obcy plik już po rozpoczęciu operacji, anuluje ją i potwierdza zachowanie pliku.
- Raport sukcesu przyjmuje najwyżej jeden rodzaj zlecenia oraz sprawdza nazwę dzienną lub roczną z właściwym rokiem i wersją formatu przed zmianą stanu. Błędne kombinacje nie zamykają żadnego zlecenia.
- Publikacja samodzielnego klienta Mac korzysta z tymczasowych blokad zależności osobnych dla każdego projektu. Próba utworzyła działający program Mach-O arm64 bez zmiany blokad serwera, a późniejszy obraz Linux zbudował się w trybie blokowanym.
- Raport `review-faza-11-recheck-1.md` ma wynik **0× P1, 0× P2, 0× P3**. Pełny zestaw ma 293 zielone testy i 12 warunkowo pominiętych testów PostgreSQL; oba testy Fazy 11 na prawdziwym PostgreSQL są zielone. Release, format, EF, XSD, Compose, skrypty, plist, obraz i skan zależności są czyste.
- Techniczna bramka Fazy 11 jest zamknięta. Rzeczywista instalacja Mac i odtworzenie na drugim urządzeniu/VPS nadal blokują dopiero pilotaż/produkcję. Nie wykonano commita, pushu ani wdrożenia.

## Faza 12 — wykonanie i review techniczne 2026-09-15

- Dodano przypięte skrótami obrazy aplikacji, Caddy i PostgreSQL, produkcyjną nakładkę ograniczeń kontenerów, jednoznaczne nazwy obrazów wydania oraz blokadę `local/latest` w odczytowym audycie VPS. Caddy i PostgreSQL działają jako zwykli użytkownicy; WWW, worker i Caddy mają system plików tylko do odczytu tam, gdzie stan jest przechowywany w jawnych woluminach.
- Lokalna bramka buduje i sprawdza cztery obrazy, wymusza skan NuGet i Docker Scout, sprawdza narzędzia kopii/OCR, składnię konfiguracji, migracje, przypięte kontrakty oraz pełne testy PostgreSQL. Obrazy WWW, workera, Caddy i PostgreSQL mają wynik 0 podatności krytycznych i wysokich.
- Syntetyczny E2E kończy konfigurację TOTP, zamyka sesję, loguje właściciela ponownie hasłem i TOTP, a następnie przechodzi przez firmę, fakturę, koszt, zamknięcie miesiąca, trzy artefakty urzędowe, płatność i potwierdzoną kopię.
- Pełna bramka ma 311 zielonych testów bez pominięć, w tym wszystkie 12 prób PostgreSQL. Release ma 0 ostrzeżeń/błędów; format, model EF, cztery XSD, Compose, skrypty i plist są czyste. Produkcyjny zestaw uruchomił PostgreSQL 18.6, migrację zakończoną kodem 0, zdrowe WWW, worker i Caddy 2.11.4 z lokalnym HTTPS oraz `Healthy` przez reverse proxy.
- Raport `review-faza-12.md` ma wynik **0× P1, 0× P2, 0× P3 w kodzie**. Pięć grup zewnętrznych P1 nadal blokuje całą Fazę 12: docelowy VPS/monitor, miesiąc referencyjny, KSeF/SMTP/narzędzia urzędowe, próby telefonu/Maca oraz prawdziwa kopia z odtworzeniem na drugim urządzeniu.
- Dowód lokalny zapisano w `docs/acceptance/phase12-local-gate.md`, a niewykonane próby pozostają puste w `docs/acceptance/phase12-pilot-checklist.md`. Nie wykonano commita, pushu ani wdrożenia.

## Faza 1 — lokalny kod QR i piąte ponowne review 2026-09-15

- Za zgodą właściciela zamknięto odroczone P3: ekran konfiguracji TOTP generuje obraz PNG kodu QR wyłącznie lokalnie w pamięci procesu. Sekret nie trafia do zewnętrznej usługi, odpowiedź nadal ma zakaz zapisywania w pamięci podręcznej, a klucz tekstowy i adres `otpauth://` pozostają planem awaryjnym.
- Nowy test najpierw odtworzył brak obrazu, a następnie potwierdził osadzony PNG, tekstowy plan awaryjny i nagłówek `no-store`. Ręczna kontrola przeglądarkowa przeszła na Macu w szerokościach 1440 px i 390 px; formularz zachowuje QR także po błędnym kodzie, bez błędów konsoli.
- Pełna bramka po zmianie ma 311 zielonych testów bez pominięć i cztery obrazy z wynikiem `0C / 0H`. Przy okazji usunięto losową porażkę starego testu odnowienia leasingu przez zastosowanie realistycznego marginesu czasu wyłącznie w teście; kod produkcyjny nie został zmieniony.
- Raport `review-faza-1-recheck-5.md` ma wynik **0× P1, 0× P2, 0× P3**. Wszystkie znaleziska Fazy 1 są zamknięte. Zewnętrzne bramki Fazy 12 nie uległy zmianie. Nie wykonano commita, pushu ani wdrożenia.

## Faza 12 — pierwszy potwierdzony punkt checklisty pilotażu 2026-09-15

- Przez `dev-docs-execute` zaznaczono wyłącznie punkt, dla którego istnieje pełny lokalny dowód: obrazy bazowe są przypięte, pełna bramka jest zielona, skan NuGet jest czysty, a obrazy WWW, workera, Caddy i PostgreSQL mają 0 podatności krytycznych i wysokich.
- Świeża ponowna kontrola czterech gotowych obrazów potwierdziła te same identyfikatory co `docs/acceptance/phase12-local-gate.md` i wynik 0C/0H. Ponowny skan NuGet nie znalazł podatnej zależności.
- Punkt identyfikatora wydania pozostaje pusty: gałąź `main` nie ma jeszcze żadnego commita, a plan wymaga skrótu trwałej rewizji kodu. Nie utworzono zastępczego numeru ani pozornego dowodu.
- Raport `review-faza-12-recheck-1.md` ma wynik **0× nowych P1, 0× P2, 0× P3**. Pięć grup zewnętrznych P1 pozostaje bez zmian; nie wykonano commita, pushu ani wdrożenia.

## Faza 12 — potwierdzenie przykładu domowego ładowania 2026-09-15

- Przez kolejny `dev-docs-execute` uruchomiono dokładny przykład checklisty: 10 000 Wh przy 0,91 zł/kWh daje 10 kWh i 9,10 zł. Raport zawsze pozostaje opisany jako niepotwierdzona podstawa podatkowa.
- Zielone są trzy celowane próby: matematyka domenowa, zachowanie stawki zapisanej dla miesiąca oraz brak wpisów kosztu, KPiR i VAT po błędnym imporcie. Dowód dopisano do lokalnej bramki, a tylko ten jeden punkt zaznaczono w checkliście jako syntetyczny.
- Raport `review-faza-12-recheck-2.md` ma wynik **0× nowych P1, 0× P2, 0× P3**. Review potwierdziło, że zaznaczenie nie oznacza podatkowego zatwierdzenia domowego ładowania; osobna decyzja księgowa pozostaje wymagana.

## Faza 12 — potwierdzenie kontrolowanej automatyzacji kosztu 2026-09-15

- Przez `dev-docs-execute` uruchomiono trzy celowane testy. Pierwszy dokument czeka na świadomą decyzję, kolejny dokładnie zgodny przypadek jest księgowany automatycznie i idempotentnie, a zmiana kraju sprzedawcy zatrzymuje regułę z czytelnym wskazaniem różnicy.
- Test domenowy potwierdza, że identyfikator sprzedawcy, kraj, waluta, sposób VAT, stawka VAT i rodzaj usługi są podatkowo istotne; kwota i data nie powodują zbędnego zatrzymania znanej reguły.
- Raport `review-faza-12-recheck-3.md` ma wynik **0× nowych P1, 0× P2, 0× P3**. Punkt checklisty zaznaczono wyłącznie jako zaliczony na danych syntetycznych; odbiór rzeczywistych dokumentów nadal należy do miesiąca referencyjnego.

## Faza 12 — potwierdzenie blokad i wersji zamknięcia miesiąca 2026-09-15

- Przez `dev-docs-execute` uruchomiono trzy celowane scenariusze. Brak deklaracji i niepotwierdzone reguły blokują zamknięcie bez jakiegokolwiek częściowego rekordu. Kompletne wejście tworzy jedno zamknięcie również przy ponowieniu.
- Zmiana źródła po zamknięciu jest wykrywana, zwykłe ponowne zamknięcie zostaje zablokowane, a świadoma korekta tworzy wersję 2 powiązaną z zachowaną wersją 1.
- Raport `review-faza-12-recheck-4.md` ma wynik **0× nowych P1, 0× P2, 0× P3**. Punkt checklisty zaznaczono jako zaliczony na danych syntetycznych; zgodność kwot z niezależnym miesiącem referencyjnym pozostaje otwarta.

## Faza 12 — przygotowanie działań właściciela 2026-09-15

- Przez `dev-docs-execute` utworzono `docs/acceptance/phase12-owner-handoff.md`. Dokument prowadzi prostym językiem przez osiem etapów od pierwszej trwałej wersji kodu do końcowej decyzji pilotażowej i mapuje wszystkie punkty A1–F6.
- Kolejność rozdziela zgodę na lokalny commit, push i wdrożenie. Zabrania przekazywania sekretów w rozmowie, zaczyna VPS od odczytowego audytu, utrzymuje integracje produkcyjne wyłączone i wymaga prawdziwego odtworzenia przed końcową decyzją.
- `.gitignore` pomija teraz wyłącznie katalog roboczy przeglądarki `.playwright-cli/` i folder `tmp/`, aby nie weszły przypadkiem do pierwszego commita. Trwałe, wskazane w dokumentacji zrzuty `output/` pozostają widoczne do świadomej decyzji o zakresie commita.
- `dev-docs-review` wykrył 1× P2: Krok 1 błędnie sugeruje, że skrót pierwszego commita i identyfikatory zbudowanych z niego obrazów można trwale zapisać przed utworzeniem tego commita. Następny `dev-docs-execute` naprawia wyłącznie kolejność wersjonowania i dowodu.

## Faza 12 — naprawa kolejności pierwszego wydania 2026-09-15

- Przez `dev-docs-execute` poprawiono P2 z piątego ponownego review. Instrukcja najpierw wymaga pełnej kontroli i pierwszego commita, dopiero potem tworzy wersję z jego skrótu oraz buduje obrazy z czystej rewizji.
- Identyfikatory i migracja trafiają do późniejszego protokołu. Jego osobny commit dowodowy wymaga następnej zgody właściciela; zgoda na pierwszy commit nadal nie obejmuje pushu ani wdrożenia.
- Raport `review-faza-12-recheck-6.md` ma wynik **0× P1, 0× P2, 0× P3** w poprawianym zakresie. Instrukcja jest gotowa do użycia, a właściciel udzielił zgody wyłącznie na pierwszy lokalny commit.

## Źródła

- [Wymagania Firemki](../../brainstorms/2026-09-07-jdg-requirements.md)
- [Pełny plan techniczny](../../plans/2026-09-09-jdg-application-plan.md)
- [Plan aktywnego zadania](firemka-jdg-plan.md)
