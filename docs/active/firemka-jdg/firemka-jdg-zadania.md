# Firemka — lista zadań i bram review

Ostatnia aktualizacja: 2026-09-15

## Kolejka wykonawcza

Właściciel wykonania: Codex. Każdy wpis jest wykonywany przez `dev-docs-execute` tylko po zielonej bramce review poprzedniej fazy, chyba że plan źródłowy jednoznacznie odracza zewnętrzną bramkę do późniejszego, zależnego zakresu.

- [x] Faza 0 — zebrano źródła urzędowe, kontrakty, zależności XSD i scenariusze strukturalne; P1 dotyczące danych referencyjnych pozostaje bramką produkcyjnych reguł podatkowych, ale zgodnie z planem źródłowym nie blokuje fazy 1.
- [x] Faza 1 — utworzono szkielet .NET, lokalne środowisko, testy bazowe i bezpieczne logowanie 2FA; Compose, zaufany lokalny HTTPS oraz ręczna próba TOTP i ponownego logowania przeszły, a graficzny QR jest generowany lokalnie z tekstowym planem awaryjnym.
- [x] Faza 2 — wdrożono wersjonowany rdzeń danych, prywatny magazyn plików, audyt, outbox, trwałe zadania i idempotencję; migracja w przód/cofnięcie oraz awaria workera zostały sprawdzone na prawdziwym PostgreSQL.
- [x] Faza 3 — wdrożono profil firmy i kontrahenta, historyczne okresy ustawień, otwieranie lat na skali podatkowej oraz responsywny ekran „Mój miesiąc”; odbiór wykonano na telefonie 390 px i komputerze 1440 px na wyłącznie sztucznych danych.
- [x] Faza 4 — wdrożono skrzynkę dokumentów, bezpieczny import KSeF, lokalny OCR i ręczne potwierdzenie danych; próba na testowym KSeF oraz zdjęciu z telefonu pozostaje zewnętrzną bramką odbiorczą.
- [x] Faza 5 — wdrożono bezpieczny zakres techniczny abonamentu, wersji roboczych, kontrolowanego procesu KSeF i domyślnie wyłączonej automatyzacji; rzeczywisty adapter i próba testowego KSeF pozostają zewnętrzną bramką odbiorczą.
- [x] Faza 6 — wdrożono reguły kosztowe, osobne KPiR/VAT, blokady polityk samochodu i zestawienie domowego ładowania; profil CSV został dopasowany do stałego eksportu domowej ładowarki.
- [x] Faza 7 — wdrożono kalkulatory, kontrolę braków, niezmienne zamknięcie miesiąca, wersjonowane korekty i audytowalne rozwiązanie konfliktów źródła; końcowe review jest zielone.
- [x] Faza 8 — wdrożono wersjonowane, prywatne pliki JPK_V7M(3), JPK_PKPIR(3) i ZUS DRA KEDU 2.27, lokalną walidację XSD, osobne zatwierdzenie, ręczny eksport, wynik z potwierdzeniem oraz łańcuch korekt; nie ma automatycznej wysyłki.
- [x] Faza 9 — wdrożono zamknięcie roku, roczny PDF, roczny JPK z ręczną historią wysyłki i kontrolowaną korektę roku; końcowe review jest zielone.
- [x] Faza 10 — wdrożyć pełne płatności, powiadomienia e-mail i docelowy dashboard.
- [x] Faza 11 — wdrożono zaszyfrowane kopie, klient macOS, rotację i bezpieczne odtworzenie na czystych danych syntetycznych; rzeczywista instalacja Mac i odtworzenie na drugim urządzeniu pozostają zewnętrzną bramką produkcyjną.
- [ ] Faza 12 — zakres techniczny utwardzenia, testów syntetycznych i procedur jest gotowy; prawdziwy pilotaż oraz zewnętrzne próby pozostają otwarte i blokują zamknięcie fazy.

## Bramy review

Właściciel review: Codex przez `dev-docs-review`. Wykonanie kolejnej fazy jest zakazane, dopóki jej review nie ma statusu gotowe do kontynuacji bez P1/P2.

- [x] Review fazy 0 — wykonane; bramka zablokowana przez P1/P2, szczegóły w `review-faza-0.md`.
- [x] Ponowne review fazy 0 po naprawie P2 — P2 zamknięte; errata doprecyzowuje, że P1 jest bramką produkcyjną, nie blokadą szkieletu fazy 1; szczegóły w `review-faza-0-recheck-1.md`.
- [x] Review fazy 1 — znaleziono 1× P1 i 2× P2; szczegóły w `review-faza-1.md`. Bramka nie pozwala na fazę 2.
- [x] Ponowne review fazy 1 po naprawie P2 — P2 zamknięte, nadal 1× P1; szczegóły w `review-faza-1-recheck-1.md`. Bramka nie pozwala na fazę 2.
- [x] Ponowne review fazy 1 po naprawie Caddy i realnym Compose — znaleziono 1× P1 oraz 2× P2; szczegóły w `review-faza-1-recheck-2.md`. Bramka nie pozwala na fazę 2.
- [x] Ponowne review fazy 1 po naprawie ciasteczka i workera — P2 zamknięte, nadal 1× P1; szczegóły w `review-faza-1-recheck-3.md`. Bramka nie pozwala na fazę 2.
- [x] Ponowne review fazy 1 po ręcznym TOTP i naprawie limitera — 0× P1, 0× P2, 1× nieblokujące P3; szczegóły w `review-faza-1-recheck-4.md`. Bramka pozwala rozpocząć fazę 2.
- [x] Piąte ponowne review fazy 1 po dodaniu lokalnego QR — 0× P1, 0× P2, 0× P3; szczegóły w `review-faza-1-recheck-5.md`. Wszystkie uwagi fazy 1 są zamknięte.
- [x] Review fazy 2 — znaleziono 2× P2: brak odnawiania aktywnego leasingu oraz brak pochodzenia/powiązania wersji pliku; szczegóły w `review-faza-2.md`. Bramka nie pozwala na fazę 3.
- [x] Ponowne review fazy 2 po naprawie P2 — 0× P1, 0× P2, 0× P3; szczegóły w `review-faza-2-recheck-1.md`. Bramka pozwala rozpocząć fazę 3.
- [x] Review fazy 3 — znaleziono 4× P2: niepełny profil firmy, brak zmian okresowych VAT/ZUS/pojazdu, możliwość otwarcia niekolejnego roku i użycie UTC dla bieżącego miesiąca; szczegóły w `review-faza-3.md`. Bramka nie pozwala na fazę 4.
- [x] Ponowne review fazy 3 po naprawie P2 — 0× P1, 0× P2, 0× P3; szczegóły w `review-faza-3-recheck-1.md`. Bramka pozwala rozpocząć fazę 4.
- [x] Review fazy 4 — znaleziono 1× zewnętrzne P1, 5× P2 i 1× P3; Faza 5 pozostaje zablokowana do naprawy P2 i ponownego review.
- [x] Ponowne review fazy 4 po naprawie P2/P3 — 0× P2 i 0× P3; techniczna bramka pozwala rozpocząć bezpieczny zakres Fazy 5 na sztucznych danych, a zewnętrzne P1 nadal blokuje rzeczywiste KSeF i produkcję.
- [x] Review fazy 5 — znaleziono 1× wcześniej odroczone P1 zewnętrzne i 3× P2; Faza 6 pozostaje zablokowana do naprawy P2 i ponownego review.
- [x] Ponowne review fazy 5 po pierwszej naprawie P2 — trzy pierwotne P2 zamknięte; znaleziono 2 nowe P2 dotyczące wyłączenia automatyzacji po wysłaniu i równoczesnego otwarcia odrzucenia.
- [x] Drugie ponowne review fazy 5 — 0× P1/P2/P3 w zaimplementowanym zakresie; wszystkie pięć P2 zamknięte, zewnętrzne KSeF nadal odroczone. Bramka pozwala kontynuować bezpieczny zakres techniczny.
- [x] Review fazy 6 — 0× P1, 0× P2 i 1× odroczona obserwacja P3; szczegóły w `review-faza-6.md`. Bramka pozwala rozpocząć fazę 7.
- [x] Review fazy 7 — znaleziono 4× P2 i 1× P3; faza 8 pozostaje zablokowana do naprawy i ponownego review.
- [x] Ponowne review fazy 7 po pierwszej naprawie — pierwotne 4× P2 i 1× P3 zamknięte; znaleziono nowy P2: konflikt źródła nie ma ścieżki wyjaśnienia i trwale blokuje miesiąc.
- [x] Drugie ponowne review fazy 7 — ścieżka rozwiązania działa, ale znaleziono nowy P2: kolejna różnica nadpisuje poprzednią decyzję, a konfliktowy plik KSeF nie jest zachowany do porównania.
- [x] Trzecie ponowne review fazy 7 — dopisywana historia jest kompletna; znaleziono P2 w wyścigu rozwiązania konfliktu z jednoczesnym wykryciem kolejnej różnicy.
- [x] Czwarte ponowne review fazy 7 — 0× P1, 0× P2, 0× P3; wszystkie znaleziska zamknięte, bramka pozwala rozpocząć fazę 8.
- [x] Review fazy 8 — wykryto 2× P1, 2× P2 i 1× P3; Faza 9 pozostaje zablokowana do naprawy znaczenia pól VAT i ZUS DRA, pochodzenia profilu, równoległego UPO i walidacji profilu.
- [x] Ponowne review fazy 8 — pierwotne problemy zamknięte; znaleziono 2 nowe P2 i 2 nowe P3 dotyczące profilu VAT, daty potwierdzenia, brakujących tekstów i podwójnego portu wysyłki.
- [x] Drugie ponowne review fazy 8 — poprzednie problemy zamknięte; znaleziono 1 nowy P2 i 1 nowy P3 dotyczące widocznej historii oraz czasu serwera.
- [x] Trzecie ponowne review fazy 8 — 0× P1, 0× P2, 0× P3 w bezpiecznym zakresie technicznym; bramka pozwala rozpocząć Fazę 9.
- [x] Review fazy 9 — znaleziono 1× P1 i 4× P2 dotyczące zamknięcia korekty, atomowości korekt, aktualnej wersji JPK, daty późnej faktury i równoległego zapisu UPO; Faza 10 pozostaje zablokowana do naprawy i ponownego review.
- [x] Ponowne review fazy 9 — 0× P1, 0× P2, 0× P3; wszystkie pięć znalezisk zamknięte, bramka pozwala rozpocząć Fazę 10 w bezpiecznym zakresie technicznym.
- [x] Review fazy 10 — znaleziono 3× P2 dotyczące ponownego błędu tego samego dnia, przypisania ogólnej awarii zadania i braku twardego TLS; wszystkie naprawiono, a ponowne review ma 0× P1/P2/P3.
- [x] Review fazy 11 — znaleziono 3× P2: opcjonalne migracje po porównaniu, zbyt szerokie sprzątanie folderu po błędzie i możliwość zakończenia dwóch rodzajów zleceń jednym raportem. Faza 12 pozostaje zablokowana do naprawy i ponownego review.
- [x] Ponowne review fazy 11 — wszystkie 3× P2 naprawione; dodatkowo odizolowano pliki zależności publikacji Mac. Wynik 0× P1/P2/P3 w zakresie technicznym pozwala rozpocząć Fazę 12.
- [x] Review fazy 12 — 0× P1/P2/P3 w kodzie i lokalnej konfiguracji; 5× zewnętrzne P1 blokuje pełny odbiór, pilotaż i produkcję. Szczegóły w `review-faza-12.md`.
- [x] Ponowne review fazy 12 po częściowym rozpoczęciu checklisty — lokalny punkt skanów i przypięcia obrazów ma pełny dowód; 0× nowych P1/P2/P3, a 5× zewnętrzne P1 pozostaje otwarte. Szczegóły w `review-faza-12-recheck-1.md`.
- [x] Drugie ponowne review fazy 12 — przykład domowego ładowania 10 000 Wh ma dokładny dowód obliczenia i braku automatycznego księgowania; 0× nowych P1/P2/P3. Szczegóły w `review-faza-12-recheck-2.md`.
- [x] Trzecie ponowne review fazy 12 — znana reguła kosztowa działa raz, a zmiana danych podatkowych zatrzymuje automat; 0× nowych P1/P2/P3. Szczegóły w `review-faza-12-recheck-3.md`.
- [x] Czwarte ponowne review fazy 12 — braki, idempotentne zamknięcie i niezmienna korekta miesiąca mają bezpośrednie dowody; 0× nowych P1/P2/P3. Szczegóły w `review-faza-12-recheck-4.md`.
- [x] Przygotowano prowadzenie właściciela przez pozostałe bramki — `docs/acceptance/phase12-owner-handoff.md` mapuje A1–F6 na osiem bezpiecznie uporządkowanych kroków i wyraźnie rozdziela commit, push oraz wdrożenie.
- [x] Piąte ponowne review fazy 12 — znaleziono 1× P2 w kolejności nadawania identyfikatora wersji i budowania obrazów; szczegóły w `review-faza-12-recheck-5.md`.
- [x] Szóste ponowne review fazy 12 — P2 zamknięte; kolejność pierwszego commita, obrazów i późniejszego protokołu jest odtwarzalna, 0× P1/P2/P3 w tym zakresie. Szczegóły w `review-faza-12-recheck-6.md`.

## Do poprawy po review fazy 12

- [x] P2 — Krok 1 ma poprawną kolejność: weryfikacja, pierwszy commit, wersja z jego skrótu, obrazy z czystej rewizji i osobno zatwierdzany commit dowodowy przed pushem.
- [ ] P1 zewnętrzne — uruchomić audyt i monitor na docelowym VPS, potwierdzić zaporę, porty, publiczne HTTPS, certyfikat, miejsce, limity kontenerów oraz działający kanał alarmowy.
- [ ] P1 zewnętrzne — uzyskać potwierdzenie profilu i miesiąca referencyjnego przez niezależnego księgowego/doradcę oraz wyjaśnić każdą różnicę.
- [ ] P1 zewnętrzne — przeprowadzić testowe KSeF, import JPK/ZUS w aktualnych narzędziach urzędowych i pojedynczą dostawę SMTP przez TLS, zachowując produkcyjne przełączniki wyłączone.
- [ ] P1 zewnętrzne — przejść pełne scenariusze telefonu i Maca, w tym odzyskanie dostępu i unieważnienie zaufanych urządzeń.
- [ ] P1 zewnętrzne — zainstalować klienta kopii na Macu, utworzyć hasło offline i odtworzyć prawdziwą kopię na drugim czystym urządzeniu/VPS.
- [ ] P1 zewnętrzne — podpisać decyzję końcową w `docs/acceptance/phase12-pilot-checklist.md`; do tego czasu brak zgody na pilotaż i produkcję.

## Do poprawy po review fazy 11

- [x] P2 — migrator bieżącej aplikacji jest obowiązkowy, wykonuje się przed końcową kontrolą i nie pozwala ogłosić sukcesu bez zgodnej bazy.
- [x] P2 — klient odtworzenia usuwa po błędzie wyłącznie pliki i foldery utworzone przez siebie, nigdy całą bieżącą zawartość celu.
- [x] P2 — raport sukcesu akceptuje jeden rodzaj zlecenia i sprawdza dzienną/roczną nazwę, właściwy rok oraz obsługiwaną wersję formatu.

## Do poprawy po review fazy 0

- [ ] P1 — zewnętrzna bramka produkcyjna: właściciel bezpiecznie dostarcza dane referencyjne, zanonimizowane dokumenty/CSV i potwierdzenie tabeli decyzji oraz miesiąca referencyjnego przez niezależnego księgowego lub doradcę. Blokuje kalkulatory i automatyczne księgowanie, nie pusty szkielet techniczny.
- [x] P2 — dodano testowe XML dla FA(3), JPK_V7M(3), JPK_PKPIR(3) i KEDU 2.27; `bash docs/research/contracts/validate-fixtures.sh` przechodzi lokalnie bez sieci.

## Do poprawy po review fazy 1

- [x] P1 — ręcznie uruchomić Docker Desktop, `docker compose up --build`, migrację PostgreSQL, health checki i przejść kreator właściciela z aplikacją TOTP na telefonie. Nie wprowadzać sekretów do Git ani czatu.
  - Próba z 2026-09-09: silnik Docker działa, a PostgreSQL, migracja, WWW i worker startują na sztucznych danych. Caddy blokuje się przy montowaniu `Caddyfile` z katalogu Pulpit przez Docker Desktop; potrzebne jest usunięcie tej zależności od montowania albo nadanie Dockerowi dostępu do Pulpitu, a następnie ponowna próba.
  - Naprawa techniczna z 2026-09-09: Caddy ma teraz własny obraz z `deploy/Caddy.Dockerfile`, który kopiuje wersjonowany `Caddyfile` podczas budowania. Pełny zestaw `firemka-phase1-test-caddy-image` osiągnął stan gotowy na danych `firemka_test`: PostgreSQL `healthy`, migracja `Exited (0)`, WWW `healthy`, worker uruchomiony i Caddy działa na portach 80/443. `https://localhost/health` zwróciło `Healthy` w kontrolowanej próbie technicznej.
  - 2026-09-10: po wyraźnej zgodzie właściciela porównano SHA-256 publicznego certyfikatu lokalnego Caddy (`692FF61CC94CB0B67E9775D641FF9DFDB02F573BCED7F6BE07BE6B640D6A3297`) i dodano go wyłącznie do pęku użytkownika macOS. Zwykłe `https://localhost/health` zwraca `Healthy`, bez pomijania sprawdzania certyfikatu.
  - Safari otworzył `https://localhost/Setup` jako bezpieczną stronę i wyświetlił formularz jednorazowego konta właściciela. Chrome oraz wbudowana przeglądarka Codexa nadal zgłaszają nieufność do lokalnego wystawcy; nie obchodzono ich ostrzeżeń. Test manualny jest więc przygotowany w Safari, która korzysta bezpośrednio z systemowego magazynu zaufania.
  - 2026-09-10: po naprawie przekierowań zasobów i limitera właściciel ukończył kreator przez ręczne dodanie klucza do aplikacji uwierzytelniającej, potwierdził kod TOTP, wylogował się i zalogował ponownie. Baza potwierdza zakończony kreator i włączone TOTP bez odczytywania poufnych wartości.
- [x] P2 — audytować sukces i błąd logowania podczas wznowienia niedokończonego kreatora właściciela oraz dodać test.
- [x] P2 — dodać test integracyjny limitu 10 żądań logowania / 15 minut i odpowiedzi `429` dla kolejnego żądania.
- [x] P2 — w środowisku innym niż `Development` wymuszono flagę `Secure` dla ciasteczka anty-CSRF, dostosowano klienta testowego do HTTPS i dodano test, który wykrywa jej brak.
  - Test `Antiforgery_cookie_is_secure_outside_development` był czerwony bez flagi, po poprawce jest zielony. Rzeczywista odpowiedź `https://localhost/Setup` przez Caddy zawiera `secure; samesite=strict; httponly`.
- [x] P2 — zastąpiono sekundową pętlę informacyjnych logów workera stanem uśpienia do czasu wprowadzenia prawdziwych zadań i potwierdzono po Compose brak powtarzających się wpisów.
  - Po sześciu sekundach działania świeżo przebudowany worker ma tylko pojedynczy wpis `Worker started; no jobs are enabled in phase 1.` zamiast wpisu co sekundę.
- [x] P3 — graficzny kod QR jest generowany lokalnie w pamięci aplikacji, bez zewnętrznej usługi; klucz tekstowy i adres `otpauth://` pozostają planem awaryjnym.

## Do poprawy po review fazy 2

- [x] P2 — leasing jest okresowo odnawiany przez osobny kontekst bazy; utrata leasingu anuluje wykonawcę bez nadpisania nowego właściciela, a test długiego zadania blokuje równoczesne przejęcie.
- [x] P2 — każdy nowy plik wymaga typowanego pochodzenia oraz rodzaju, identyfikatora i numeru wersji powiązanego rekordu; migracja i test metadanych obejmują te pola.

## Do poprawy po review fazy 3

- [x] P2 — dodano do profilu, migracji i kreatora wymagany adres firmy oraz opis usługi abonamentowej.
- [x] P2 — dodano dopisywane okresy i historię zmian VAT, ZUS oraz pojazdu, z testem niezmienności wcześniejszego miesiąca.
- [x] P2 — otworzyć można wyłącznie kolejny rok podatkowy; błędne lata są pokryte testami i czytelnym błędem.
- [x] P2 — bieżący miesiąc i domyślny nowy okres są wyznaczane według `Europe/Warsaw` przez testowalne źródło czasu.

## Do poprawy po review fazy 4

- [ ] P1 — zewnętrzna bramka: bezpiecznie przekazać dostęp testowy KSeF, zanonimizowany rzeczywisty PDF i zdjęcie z telefonu; wykonać pełną próbę oraz zatwierdzić użycie oficjalnego klienta albo bezpośredniego adaptera OpenAPI.
- [x] P2 — po nieudanej całej operacji usuwany jest fizyczny plik oraz wycofywane są rekordy; kontrolowana awaria przechodzi także na PostgreSQL.
- [x] P2 — anulowanie OCR kończy całe drzewo procesu, zapisuje próbę jako przerwaną i pozwala zadaniu zostać wznowionym.
- [x] P2 — brakującemu polu nie jest przypisywana ogólna pewność OCR; dane KSeF korzystają z jawnie odrębnego pochodzenia.
- [x] P2 — konflikt źródła KSeF jest widoczny na liście i w szczegółach niezależnie od końcowego statusu dokumentu.
- [x] P2 — skrzynka ma stabilne stronicowanie po 25 rekordów, maksymalny rozmiar strony i filtr statusu.
- [x] P3 — sztuczne próbki OCR są w `tests/Fixtures/phase4`, a brak rzeczywistego narzędzia powoduje błąd testu zamiast pozornego sukcesu.

## Do poprawy po review fazy 5

- [ ] P1 — zewnętrzna bramka: bezpiecznie przekazać dostęp testowy KSeF, zatwierdzić rzeczywisty klient/adapter i wykonać wysłanie FA(3), stan oczekujący, niepewny wynik, odrzucenie, przyjęcie, UPO oraz porównanie treści. Do tego czasu wysyłka produkcyjna i automatyzacja pozostają wyłączone.
- [x] P2 — automatyczne zadanie dla stanu `Sending` lub `DeliveryUncertain` ponawia wyłącznie sprawdzenie statusu, ma wydłużony kontrolowany budżet prób i kończy się numerem KSeF/UPO bez drugiego wysłania; test potwierdza dokładnie jedno `SendAsync`.
- [x] P2 — odrzuconą, niewystawioną fakturę można świadomie otworzyć do poprawy z zachowaniem odrzuconego XML i odpowiedzi w wersji 1, nowym kluczem wysyłki oraz przyjętym wynikiem w wersji 2, bez tworzenia drugiej faktury dla miesiąca.
- [x] P2 — dwa równoległe sprawdzenia `Accepted` na rzeczywistym PostgreSQL bezpiecznie zwracają jeden zapisany wynik i jedną niezmienną wersję bez błędu współbieżności ani dodatkowego wysłania.
- [x] P2 — wyłączenie zgody właściciela i blokady operatora dla przyszłej automatyzacji po rozpoczęciu wysyłki nie zatrzymuje bezpiecznego pobierania końcowego statusu i UPO; test kończy fakturę przy jednym `SendAsync`.
- [x] P2 — dwa równoczesne żądania otwarcia odrzuconej faktury są idempotentne na PostgreSQL: pozostaje jeden szkic i jedna zachowana wersja bez błędu bazy.

## Do poprawy po review fazy 6

- [x] P3 — wspólne podsumowanie błędów i skrypt walidacji nie używają już niedozwolonego stylu inline; polskie kwoty z przecinkiem i mobilne menu przechodzą odbiór bez błędów CSP.

## Do poprawy po review fazy 7

- [x] P2 — nieobsługiwany profil VAT lub ZUS blokuje zamknięcie osobnym kodem i prowadzi do odpowiedniej sekcji ustawień.
- [x] P2 — zaksięgowany dokument z konfliktem źródła KSeF blokuje zamknięcie i uczestniczy w odcisku wykrywającym zmianę po zamknięciu.
- [x] P2 — walidacja i zapis różnych równoczesnych korekt są atomowe w transakcji szeregowej; wymuszony wyścig PostgreSQL przepuszcza najwyżej jedną korektę.
- [x] P2 — spóźnioną fakturę rozwiązuje wyłącznie jawne, bezkwotowe potwierdzenie okresu rozpoznania sprzedaży; inne typy wpisów nie usuwają blokady.
- [x] P3 — zapytanie o nierozwiązane dokumenty ogranicza dane już w bazie do dat wybranego miesiąca, z granicami UTC obliczonymi dla czasu warszawskiego.
- [x] P2 — dodano audytowalne rozwiązanie konfliktu źródła z obowiązkowym wyjaśnieniem, zachowaniem historii, ponownym otwarciem wyłącznie przy nowej różnicy i testami właściciela, strony oraz PostgreSQL.
- [x] P2 — zastąpiono nadpisywany stan dopisywaną historią konfliktów, zachowano prywatną kopię każdej nowej różniącej się treści i zapewniono transakcyjny zapis bez duplikatów oraz osieroconych plików.
- [x] P2 — włączono kontrolę wersji dokumentu, a wymuszony test PostgreSQL potwierdza spójność przy równoczesnym wyjaśnieniu konfliktu A oraz zapisie nowego konfliktu B.

## Do poprawy po review fazy 8

- [x] P1 — poprawiono semantykę `P_39`, `P_48`, `P_53` i `P_62` w JPK_V7M, usunięto niezamówione pola zwrotu `P_54`/`P_540` i dodano test nadwyżki oraz poprzedniego przeniesienia.
- [x] P1 — ZUS DRA używa kodu terminu `6`; identyfikator `01`, `02`, `03` i cel złożenia JPK wynikają z rzeczywistych ręcznych wysyłek, a nie technicznych wersji roboczych.
- [x] P2 — artefakt wskazuje dokładną wersję profilu, schematu i generatora; zmiana któregokolwiek wejścia tworzy nową niezatwierdzoną wersję.
- [x] P2 — równoczesny identyczny wynik/UPO jest idempotentny, różny zwraca czytelny konflikt, a przegrany plik jest sprzątany; scenariusze przeszły na PostgreSQL.
- [x] P3 — puste wartości profilu oraz niemożliwa data urodzenia zwracają kontrolowany komunikat walidacji.
- [x] P2 — zwykła faktura elektroniczna lub papierowa bez numeru KSeF otrzymuje `BFK`; `OFF` nie jest już domyślnie nadawany bez potwierdzonego trybu awarii KSeF.

## Do poprawy po ponownym review fazy 8

- [x] P2 — nieobsługiwany albo historycznie zmieniony profil VAT blokuje cały komplet zamiast po cichu pomijać JPK_V7M.
- [x] P2 — pusta lub przyszła data niezależnego potwierdzenia profilu jest odrzucana względem polskiej daty biznesowej.
- [x] P3 — porównanie z istniejącym profilem nie wywołuje `Trim()` na brakującym tekście przed kontrolowaną walidacją.
- [x] P3 — pozostaje jeden wspólny `Application.ExternalServices.ISubmissionGateway`, a test potwierdza brak jego rejestracji.

## Do poprawy po drugim ponownym review fazy 8

- [x] P2 — ekran każdej wersji pokazuje zapisane zatwierdzenie, ręczną wysyłkę oraz wynik wraz z datami i referencjami.
- [x] P3 — wszystkie chwile Fazy 8 są wyświetlane w strefie `Europe/Warsaw`, niezależnie od strefy VPS.

## Do poprawy po review fazy 9

- [x] P1 — ponowne zamknięcie korekty tworzy nowe migawki miesięcy bez zmiany poprzedniej wersji i przechodzi na PostgreSQL.
- [x] P2 — wspólna blokada roczna obejmuje zamknięcie roku, dane roczne oraz korekty roku i miesięcy; wymuszony wyścig PostgreSQL zachowuje spójny stan i idempotencję.
- [x] P2 — zatwierdzenie i ręczna wysyłka rocznego JPK dotyczą wyłącznie najnowszej wersji, a wynik można później dopisać do historycznej wersji wysłanej wcześniej.
- [x] P2 — późna faktura rozpoznana w miesiącu usługi ma w rocznym JPK datę należącą do zamykanego roku.
- [x] P2 — równoczesny identyczny wynik/UPO jest idempotentny, a różny zwraca czytelny konflikt bez osieroconych plików.

## Do poprawy po review fazy 10

- [x] P2 — ponowne pojawienie się błędu dokumentu tego samego dnia otrzymuje nowy klucz oparty na wersji stanu i nowe pojedyncze powiadomienie.
- [x] P2 — ogólne zadanie bez właściciela nie jest przypisywane wielu kontom, a awaria kanału e-mail nie tworzy pętli nowych e-maili.
- [x] P2 — SMTP bez TLS jest twardo blokowane przed połączeniem i nie korzysta z systemowych poświadczeń przy pustym loginie.

## Wymagane dane od właściciela

- [ ] Potwierdzona data rozpoczęcia firmy, dane firmy/klienta, abonament i VAT.
- [x] Przykładowy eksport CSV z domowej ładowarki został przekazany; jego układ jest ustawiony jako domyślny, a kopia testowa w repozytorium zawiera wyłącznie dane syntetyczne.
- [ ] Oferta lub umowa samochodu wraz z rozbiciem opłat.
- [ ] Decyzja/źródło dotyczące księgowania domowego ładowania.
- [ ] Dostęp testowy do KSeF przekazany bez umieszczania sekretów w tym pliku.
- [ ] Dane SMTP lub wybór dostawcy.
- [ ] Docelowy VPS i sposób bezpiecznego audytu.
- [ ] Folder kopii na MacBooku oraz oddzielnie przechowywane hasło odzyskiwania.
