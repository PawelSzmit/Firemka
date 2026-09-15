# Firemka — plan techniczny aplikacji do własnej JDG

Data: 2026-09-09  
Poziom planu: głęboki  
Status: gotowy do przygotowania dokumentacji wykonawczej; bez zgody na implementację, commit, publikację ani wdrożenie

## 1. Problem i cel

Firemka ma być prywatną aplikacją WWW dla jednej firmy i jednego właściciela. Ma prowadzić od dokumentów źródłowych do zamknięcia miesiąca i roku, ograniczając ręczne działania przy jednej cyklicznej fakturze sprzedaży i kilku powtarzalnych kosztach.

Najtrudniejszą częścią nie będzie interfejs, lecz:

- poprawne i możliwe do wyjaśnienia reguły KPiR, VAT, PIT i ZUS;
- bezpieczna i odporna na ponowienia integracja z KSeF;
- niezmienialna historia księgowań, korekt, dokumentów i potwierdzeń;
- możliwość pełnego odtworzenia danych z zaszyfrowanej kopii;
- oddzielenie czynności „obliczono”, „zatwierdzono”, „wyeksportowano”, „wysłano”, „przyjęto” i „zapłacono”.

Plan zakłada pierwszy rzeczywisty okres od rozpoczęcia działalności, prawdopodobnie od października 2026, ale dokładna data będzie ustawieniem, a nie wartością wpisaną na stałe w kodzie.

## 2. Źródła i śledzenie wymagań

Głównym źródłem prawdy jest [dokument wymagań](../brainstorms/2026-09-07-jdg-requirements.md). [Analiza pomysłów](../ideation/2026-09-07-jdg-ideation.md) służy wyłącznie jako kontekst; propozycje odrzucone lub zastąpione w wymaganiach nie wracają do zakresu.

| Obszar | Wymagania | Jednostki planu |
|---|---|---|
| prywatna aplikacja, profil roczny i główny ekran | R1, R3-R4, R43-R45 | 1-3, 10 |
| sprzedaż abonamentowa i KSeF | R9-R13, R33, R35, R46 | 5 |
| dokumenty zakupowe, OCR i reguły | R5-R8, R14, R30, R32, R34 | 4, 6 |
| samochód i ładowanie | R23-R29 | 6 |
| KPiR, VAT, PIT, ZUS i zamknięcie miesiąca | R2, R15-R18, R31-R32 | 0, 7-8 |
| płatności i przyszły import wyciągu | R19-R21 | 2, 10, F1 |
| rok i korekty | R18, R31, R36-R38 | 2, 8-9 |
| powiadomienia | R22, R39 | 10 |
| kopie i odtworzenie | R40-R42 | 11 |
| przyszła automatyczna wysyłka | R16 | 2, 8, F2 |

## 3. Zakres pierwszej wersji

Pierwsza wersja obejmuje:

- jedną JDG, jedno konto właściciela i jednego polskiego odbiorcę abonamentu;
- aplikację WWW dostosowaną do komputera i telefonu;
- hasło, kod z aplikacji uwierzytelniającej, kody odzyskiwania i zaufane urządzenia na maksymalnie 30 dni;
- profil działalności i reguły obowiązujące w określonych okresach oraz latach;
- wyłącznie skalę podatkową, miesięczny VAT i początkowo tylko składkę zdrowotną;
- odbieranie i wystawianie faktur przez KSeF 2.0, z zachowaniem oryginalnych danych i statusów;
- ręczne dodawanie PDF/zdjęcia, odczyt danych oraz obowiązkowe sprawdzenie odczytu;
- reguły powtarzalnych kosztów zatwierdzane przy pierwszym dokumencie;
- KPiR, rejestry VAT, miesięczne wyliczenia PIT i składki zdrowotnej;
- ręczne oznaczanie pełnych płatności;
- przygotowanie, pobranie i wersjonowanie plików do ręcznej wysyłki do US/ZUS;
- zapis potwierdzeń i kontrolowane przygotowanie korekt;
- zamknięcie miesiąca i roku oraz roczny PDF danych firmowych;
- obsługę najmu albo leasingu samochodu elektrycznego używanego mieszanie, po poznaniu konkretnej umowy;
- publiczne ładowanie z faktur oraz domowe ładowanie z CSV i okresowej ceny energii;
- e-maile zgodne z R39;
- pięć rotacyjnych, zaszyfrowanych kopii na MacBooku, ręczne pobranie kopii, archiwum roczne i pełny test odtworzenia;
- domyślnie wyłączoną automatyzację cyklicznej faktury sprzedaży, którą może włączyć wyłącznie właściciel.

## 4. Poza zakresem pierwszej wersji

- ryczałt i podatek liniowy;
- pełne roczne zeznanie PIT, PIT-11, ulgi i wspólne rozliczenie małżonków;
- wiele firm, wielu użytkowników, pracownicy, kadry, magazyn, CRM i aplikacja natywna na telefon;
- częściowe płatności;
- bezpośrednie połączenie z bankiem oraz import wyciągu;
- automatyczna wysyłka dokumentów do US i ZUS;
- samodzielne podejmowanie decyzji podatkowych przez AI;
- automatyczne księgowanie domowego ładowania przed potwierdzeniem jego podstawy dokumentacyjnej i podatkowej;
- migracja wcześniejszych ksiąg istniejącej działalności;
- wdrożenie na VPS przed przejściem wszystkich bramek bezpieczeństwa i poprawności.

Przyszłe funkcje mają mieć przygotowane granice w danych i kodzie, ale nie wolno budować ich „przy okazji”.

## 5. Stan obecny

- Projekt zawiera tylko dokument ideacyjny i wymagania.
- Nie ma kodu, testów, konfiguracji, repozytorium Git ani lokalnego `AGENTS.md`.
- Nie ma istniejącego wzorca kodu do kopiowania.
- VPS, MacBook, dane firmy, rzeczywiste dokumenty, umowa samochodu i CSV ładowania nie zostały jeszcze zbadane.
- Źródła urzędowe wskazane w dokumentach są świeże, ale każda wersja XSD, API i reguła liczbowa musi być ponownie sprawdzona w dniu implementacji i przed pierwszym rzeczywistym rozliczeniem.

## 6. Decyzje architektoniczne

### 6.1 Jedna aplikacja zamiast wielu usług

Rekomendowany jest modułowy monolit w C# na .NET 10 LTS:

- ASP.NET Core Razor Pages do responsywnego interfejsu;
- ASP.NET Core Identity do logowania, kodów TOTP i kodów odzyskiwania;
- Entity Framework Core i PostgreSQL 18 jako baza danych;
- osobny proces `Worker`, ale z tego samego rozwiązania i tych samych modułów, do synchronizacji KSeF, OCR, harmonogramów, e-maili i generowania plików;
- Docker Compose na VPS: reverse proxy, aplikacja WWW, worker i PostgreSQL;
- bez Redisa i bez kolejnego serwera kolejek w pierwszej wersji; trwałe zadania, blokady i ponowienia są zapisywane w PostgreSQL.

Uzasadnienie: oficjalny klient KSeF 2.0 jest dostępny dla .NET i zawiera aplikację demonstracyjną, scenariusze integracyjne oraz obsługę wymaganych operacji kryptograficznych. Jeden stos technologiczny ogranicza liczbę rzeczy, które właściciel będzie musiał utrzymywać.

### 6.2 Granice modułów

```text
Firemka.sln
src/
  Firemka.Domain/          reguły i modele bez zależności od WWW, bazy i KSeF
  Firemka.Application/     przypadki użycia, uprawnienia, walidacja i orkiestracja
  Firemka.Infrastructure/  PostgreSQL, pliki, KSeF, OCR, XML, PDF, e-mail
  Firemka.Web/             Razor Pages, logowanie i interfejs responsywny
  Firemka.Worker/          trwałe zadania, harmonogramy, ponowienia i outbox
  Firemka.BackupClient/    mały klient macOS pobierający i weryfikujący kopie
tests/
  Firemka.Domain.Tests/
  Firemka.Application.Tests/
  Firemka.Infrastructure.Tests/
  Firemka.Contract.Tests/
  Firemka.E2E.Tests/
deploy/
  compose.yaml
  Caddyfile
  scripts/
docs/
  adr/
  research/
  test-cases/
```

`Domain` nie może zależeć od KSeF, OCR, bazy ani widoków. Dzięki temu reguły podatkowe można testować na zatwierdzonych przykładach, a późniejsza automatyczna wysyłka nie wymaga ponownego budowania obliczeń.

### 6.3 Model danych i niezmienność historii

Główne grupy danych:

- `Company`, `TaxYear`, `TaxProfilePeriod`, `VehicleProfile`, `EnergyRatePeriod`;
- `Counterparty`, `SubscriptionRatePeriod`, `SalesInvoiceDraft`, `InvoiceVersion`;
- `SourceDocument`, `StoredFile`, `ExtractionAttempt`, `ExtractedField`;
- `CostRule`, `CostRuleVersion`, `AccountingDecision`, `LedgerEntryRevision`, `VatEntryRevision`;
- `MonthlyPeriod`, `CalculationRun`, `MonthlyClosingVersion`, `AnnualClosingVersion`;
- `FilingArtifactVersion`, `SubmissionReceipt`, `CorrectionCase`;
- `Payment`, z polem źródła `manual` albo w przyszłości `statement_import`;
- `BackgroundJob`, `OutboxMessage`, `NotificationDelivery`;
- `AuditEvent`, `BackupRecord`, `TrustedDevice`.

Zasady wspólne:

- kwot nie zapisujemy jako liczb zmiennoprzecinkowych; używamy `decimal` i jawnej reguły zaokrąglania dla każdego obliczenia;
- oryginalna waluta, kwota, kurs, źródło kursu i wynik w PLN są osobnymi danymi;
- okres kosztu/PIT oraz okres VAT są rozdzielone;
- wszystkie znaczące reguły mają zakres dat i wersję;
- wystawionych faktur, zamknięć, wyeksportowanych plików i potwierdzeń nie edytujemy w miejscu; korekta tworzy nową wersję powiązaną z poprzednią;
- każdy plik ma skrót SHA-256, typ, rozmiar, pochodzenie i powiązanie z konkretną wersją rekordu;
- każde wywołanie zewnętrzne ma klucz idempotencji, aby ponowienie nie tworzyło drugiej faktury, wpisu ani e-maila;
- znaczniki techniczne zapisujemy w UTC, a daty podatkowe i terminy wyliczamy jawnie w strefie `Europe/Warsaw`.

### 6.4 Stany procesów

Dokument kosztowy:

```text
pozyskany -> dane_do_sprawdzenia -> reguła_do_ustalenia -> zaksięgowany
                        \-> niezwiązany_z_firmą
                        \-> błąd_do_naprawy
```

Faktura sprzedaży:

```text
plan -> wersja_robocza -> wysyłanie_do_KSeF -> wystawiona
                              \-> wynik_niepewny -> sprawdzenie_statusu
                              \-> odrzucona -> poprawiona_wersja_robocza
```

Dokument urzędowy:

```text
przygotowany -> wyeksportowany -> wysłany -> przyjęty
                                  \-> odrzucony -> korekta/ponowienie
```

Zamknięcie miesiąca nie oznacza wysłania dokumentów ani ich zapłaty. Eksport nie oznacza wysłania, a wysłanie nie oznacza przyjęcia.

### 6.5 Pliki, OCR i poufność

- Oryginały przechowujemy poza katalogiem publicznym WWW, w trwałym wolumenie, pod losowym identyfikatorem.
- PDF z warstwą tekstową najpierw odczytujemy bez OCR.
- Zdjęcie lub skan przechodzi przez lokalny silnik Tesseract; wynik jest tylko propozycją danych.
- Każde pole ma wartość, źródło i poziom pewności. Przed księgowaniem dokumentu spoza KSeF właściciel widzi dokument obok formularza i potwierdza dane.
- Jeśli lokalny odczyt okaże się zbyt słaby, decyzja o zewnętrznym dostawcy OCR/AI wymaga osobnej oceny prywatności, kosztu i regulaminu. Plan nie zakłada wysyłania dokumentów księgowych do zewnętrznego modelu.

### 6.6 Bezpieczeństwo

- Rejestracja publiczna jest wyłączona; pierwsze i jedyne konto powstaje w kontrolowanym kreatorze uruchomieniowym.
- Samo hasło nigdy nie wystarcza do zwykłego logowania. Drugi krok to TOTP lub jednorazowy kod odzyskiwania.
- „Zaufaj temu urządzeniu” używa losowego tokenu zapisanego w przeglądarce jako bezpieczne ciasteczko, a w bazie jako skrót. Wygasa po 30 dniach i można go unieważnić pojedynczo albo przez „Wyloguj wszystkie urządzenia”.
- Kody odzyskiwania są pokazywane tylko podczas generowania, przechowywane w postaci skrótów i można je unieważnić przez wygenerowanie nowego zestawu.
- Ciasteczka: `Secure`, `HttpOnly`, odpowiednie `SameSite`; formularze mają ochronę CSRF, logowanie ma limit prób, a odpowiedzi mają CSP i pozostałe nagłówki bezpieczeństwa.
- Klucze KSeF, hasła SMTP i klucze ochrony danych nie trafiają do repozytorium ani zwykłych logów. Sekrety są dostarczane przez pliki sekretów/zmienne środowiska i szyfrowane kluczem spoza bazy.
- Logi nie zawierają pełnych faktur, tokenów, haseł, NIP/PESEL ani załączników.

### 6.7 Kopie na MacBooku

Zwykła przeglądarka nie może niezawodnie zapisywać plików do stałego folderu po wybudzeniu komputera. Dlatego potrzebny jest mały `Firemka.BackupClient` dla macOS, uruchamiany przy logowaniu i okresowo przez `launchd`.

Przepływ:

1. serwer przygotowuje spójny strumień: logiczny zrzut PostgreSQL, wszystkie pliki, klucze ochrony danych potrzebne do odczytu, wersję aplikacji, manifest i sumy kontrolne;
2. klient Mac pobiera strumień po HTTPS z osobnym, możliwym do unieważnienia tokenem tylko do kopii;
3. klient szyfruje dane lokalnie hasłem odzyskiwania, zapisanym w Pęku kluczy macOS, i tworzy jeden plik `firemka-backup-<UTC>-v<format>.fmbak`;
4. klient odszyfrowuje manifest w trybie weryfikacji, sprawdza kompletność i dopiero wtedy oznacza kopię jako poprawną;
5. po poprawnej weryfikacji zachowuje pięć najnowszych kopii dziennych; starsza nie jest usuwana przed potwierdzeniem nowej;
6. jeśli Mac jest wyłączony, `launchd` uruchamia zaległe pobranie po ponownym zalogowaniu/wybudzeniu;
7. roczna kopia `firemka-archive-<rok>-...fmbak` trafia do osobnego podfolderu i nie podlega rotacji;
8. ręczne „Utwórz kopię teraz” zleca kopię i pokazuje postęp oraz wynik.

Domyślny folder do potwierdzenia z właścicielem: `~/Documents/Firemka/Backups`. Hasło odzyskiwania należy dodatkowo zapisać w menedżerze haseł lub bezpiecznej kopii offline; nie wolno umieszczać go w pliku `.fmbak`.

Odtworzenie zawsze odbywa się do pustej instancji albo po wykonaniu osobnej kopii bezpieczeństwa bieżącej instancji. Kreator najpierw robi próbę, pokazuje zawartość i wersję, a dopiero po wyraźnym potwierdzeniu przełącza dane. Test pełnego odtworzenia jest warunkiem produkcji.

## 7. Jednostki implementacyjne

### Jednostka 0 — bramka faktów, źródeł i rzeczywistych przykładów

**Cel:** zablokować wpisywanie do kodu niepotwierdzonych zasad podatkowych i niesprawdzonych formatów.

**Pliki:**

- `docs/research/official-sources-2026.md`;
- `docs/research/tax-decisions-2026.md`;
- `docs/research/vps-and-mac-inventory.md`;
- `docs/test-cases/reference-month-2026-10.md`;
- `docs/test-cases/reference-year-2026.md`;
- `docs/adr/0001-technology-stack.md`;
- `docs/adr/0002-financial-versioning.md`;
- `docs/adr/0003-backup-format-and-restore.md`.

**Wzorce:** kontrakt urzędowy zapisany z wersją i skrótem; test „golden master”, w którym zatwierdzone wejście ma dokładnie określony wynik.

**Prace:**

- sprawdzić VPS tylko odczytowo: system, CPU, RAM, dysk, Docker, reverse proxy, domenę, pocztę i sposób przechowywania sekretów;
- pobrać aktualne kontrakty OpenAPI KSeF i XSD FA(3), JPK_V7M, JPK_PKPIR i KEDU wraz z datą i skrótem pliku;
- ustalić na źródłach urzędowych zasady skali, VAT, zdrowotnej, zagranicznych zakupów, samochodu i terminów;
- zebrać zanonimizowane kopie rzeczywistych faktur OpenAI/Anthropic/OVH, ofertę albo umowę samochodu i przykładowy CSV;
- przygotować co najmniej jeden zwykły miesiąc, miesiąc bez przychodu, stratę, korektę, opóźnioną fakturę i zamknięcie roku z oczekiwanymi wynikami;
- zlecić księgowemu lub doradcy jednorazowe potwierdzenie tabeli decyzji oraz wyników referencyjnych przed rzeczywistym użyciem.

**Ryzyka:** źródło urzędowe może opisywać inną wersję formularza niż narzędzie importujące; marka dostawcy nie określa kraju ani zasad VAT; deklaracja o etacie nie zastępuje potwierdzenia zbiegu tytułów ZUS.

**Testy i odbiór:** każdy parametr ma źródło, datę obowiązywania i przykład; każdy XSD ma testowy plik przechodzący walidację; żaden nierozstrzygnięty przypadek nie jest oznaczony jako automatyczny.

**Weryfikacja:** przegląd kompletności tabeli decyzji, niezależne potwierdzenie wyników referencyjnych oraz zapis wersji i skrótów pobranych kontraktów.

**Bramka:** bez zakończenia tej jednostki można zbudować techniczny szkielet, ale nie wolno wdrożyć produkcyjnych obliczeń ani księgowania.

### Jednostka 1 — szkielet projektu, środowiska i bezpieczne logowanie

**Cel:** uruchomić pustą, prywatną aplikację na komputerze deweloperskim oraz powtarzalne środowisko testowe.

**Pliki:** `Firemka.sln`, `Directory.Build.props`, `Directory.Packages.props`, projekty w `src/`, projekty testowe, `compose.yaml`, `Caddyfile`, `.env.example`, `.gitignore`, `README.md`.

**Wzorce:** oficjalny klient .NET KSeF i demonstracyjna aplikacja ASP.NET; ASP.NET Core Identity; migracje EF Core; centralnie przypięte wersje paczek i plik blokady zależności.

**Prace:**

- utworzyć moduły z granicami z rozdziału 6.2;
- skonfigurować PostgreSQL, health checki, migracje i lokalne sekrety;
- zbudować jednorazowy kreator właściciela, TOTP, kody odzyskiwania, listę zaufanych urządzeń i wylogowanie wszystkich urządzeń;
- ustawić 30-dniowe wygaśnięcie zaufania, ochronę formularzy, limit logowania, nagłówki bezpieczeństwa i audyt zdarzeń logowania;
- utworzyć pustą nawigację: „Mój miesiąc”, „Faktury”, „Księgi”, „Rozliczenia”, „Ustawienia”.

**Ryzyka:** przypadkowe pozostawienie rejestracji lub trybu bez 2FA, utrata dostępu po błędnej obsłudze kodów odzyskiwania, niespójne sekrety między WWW i workerem.

**Testy:** brak publicznej rejestracji; logowanie samym hasłem odrzucone; TOTP i pojedynczy kod odzyskiwania działają; kod nie działa drugi raz; zaufanie wygasa i można je unieważnić; dane i sekrety są niedostępne bez sesji.

**Weryfikacja:** testy jednostkowe i integracyjne, skan zależności, ręczna próba na MacBooku i telefonie.

### Jednostka 2 — rdzeń danych, pliki, historia i trwałe zadania

**Cel:** stworzyć wspólną podstawę dla wszystkich funkcji finansowych bez jeszcze wpisanych reguł podatkowych.

**Pliki:** encje w `Firemka.Domain`, konfiguracje EF i migracje w `Firemka.Infrastructure/Persistence`, magazyn plików w `Infrastructure/Files`, kolejka/outbox w `Infrastructure/Jobs`, usługi audytu w `Application/Auditing`.

**Wzorce:** wersje dopisywane zamiast nadpisywania, transakcyjny outbox i jawna maszyna stanów.

**Prace:**

- wdrożyć encje i wersjonowanie z rozdziału 6.3;
- utworzyć prywatny magazyn plików, walidację typu/rozmiaru, sumy kontrolne i bezpieczny podgląd;
- wdrożyć transakcyjny outbox, trwałe zadania z leasingiem, ponowieniami, opóźnieniem i kluczem idempotencji;
- zdefiniować porty `IKsefGateway`, `IFilingExporter`, `ISubmissionGateway`, `IDocumentExtractor`, `IPaymentMatcher`, aby funkcje przyszłe nie zmieniały domeny;
- dodać jawne stany dokumentów i walidowane przejścia między nimi.

**Ryzyka:** migracja uszkadzająca historię, podwójne wykonanie zadania po restarcie, niespójność bazy i magazynu plików.

**Testy:** awaria procesu nie gubi zadania; dwa identyczne polecenia tworzą jeden skutek; załącznik nie jest publicznie dostępny; nie można przeskoczyć niedozwolonego stanu; korekta nie nadpisuje poprzedniej wersji.

**Weryfikacja:** testy integracyjne na prawdziwym PostgreSQL, kontrolowana awaria workera i przegląd migracji w przód oraz cofnięcia na danych sztucznych.

### Jednostka 3 — profil firmy, rok podatkowy i skorupa „Mój miesiąc”

**Cel:** zapisać dane obowiązujące w czasie i pokazać jeden punkt wejścia do pracy.

**Pliki:** `Domain/Companies`, `Domain/TaxYears`, `Application/Onboarding`, `Web/Pages/Setup`, `Web/Pages/Month`, `Web/Pages/Settings`.

**Wzorce:** dane z okresem obowiązywania oraz projekcja dashboardu wyliczana z danych źródłowych.

**Prace:**

- kreator danych firmy, kontrahenta, daty startu i ustawień roku;
- tylko skala podatkowa jako dozwolona forma w wersji 1; bez widocznego, nieaktywnego przełącznika ryczałtu;
- osobne okresy dla ustawień ZUS, VAT, cen energii, pojazdu i abonamentu;
- szkielet „Mój miesiąc” oparty na zapytaniu agregującym statusy, a nie na duplikowanych polach;
- otwarcie nowego roku bez zmiany lat wcześniejszych.

**Ryzyka:** ciche przeliczenie historii po zmianie ustawienia lub pokazanie niepełnej prognozy jako kwoty ostatecznej.

**Testy:** zmiana ustawienia od nowego okresu nie zmienia zamkniętych miesięcy; nie można wybrać ryczałtu; rozpoczęcie w środku miesiąca zachowuje pełny abonament; ekran działa na wąskim telefonie i komputerze.

**Weryfikacja:** automatyczne testy okresów oraz ręczny odbiór kreatora i dashboardu na obu rodzajach urządzeń.

### Jednostka 4 — skrzynka dokumentów, KSeF przychodzący i OCR

**Cel:** bezpiecznie pozyskać wszystkie dokumenty i doprowadzić je do potwierdzonych danych.

**Pliki:** `Domain/Documents`, `Application/Documents`, `Infrastructure/Ksef/Incoming`, `Infrastructure/Ocr`, `Web/Pages/Invoices/Incoming`, testowe próbki w `tests/Fixtures` wyłącznie z danymi sztucznymi.

**Wzorce:** najpierw zachowanie surowego źródła, potem ekstrakcja; adapter zewnętrznego API oddzielony od domeny; import bezpieczny przy ponowieniu.

**Prace:**

- integracja z oficjalnym klientem KSeF na środowisku testowym, osobna konfiguracja test/demo/produkcja;
- synchronizacja po identyfikatorze KSeF i zakresie czasu bez duplikatów, z kursorem i wznowieniem;
- zachowanie surowego XML, numeru KSeF, dat i historii synchronizacji;
- przyjmowanie PDF/JPEG/PNG, kontrola typu i limitu, ekstrakcja tekstu/OCR w workerze;
- formularz dokument-obok-danych, pewność pól, ręczna poprawa i zapis decyzji;
- status „niezwiązany z firmą” z powodem i audytem.

**Ryzyka:** duplikat po wznowieniu synchronizacji, błędne zaufanie do OCR, złośliwy lub zbyt duży plik, wygaśnięcie uprawnienia KSeF.

**Testy:** ponowna synchronizacja nie dubluje faktury; przerwanie w połowie jest wznawiane; zmieniony plik jest wykrywany; nieczytelny skan można uzupełnić ręcznie; błędne lub brakujące pole blokuje dalszy proces, ale wskazuje rozwiązanie.

**Weryfikacja:** automatyczne testy importu oraz ręczna próba z rzeczywistym PDF, zdjęciem z telefonu i fakturą KSeF na środowisku testowym.

### Jednostka 5 — abonament, wersje robocze i KSeF wychodzący

**Cel:** przygotowywać i bezpiecznie wystawiać jedną miesięczną fakturę sprzedaży.

**Pliki:** `Domain/Sales`, `Application/Sales`, `Infrastructure/Ksef/Outgoing`, `Worker/Jobs/SalesDrafts`, `Web/Pages/Invoices/Sales`.

**Wzorce:** stanowa obsługa wysyłki i idempotentne polecenie wystawienia powiązane z jednym okresem abonamentu.

**Prace:**

- okresowe stawki abonamentu i tworzenie wersji roboczej 1. dnia miesiąca;
- termin płatności siedem dni od faktycznej daty wystawienia;
- okres usługi niezależny od daty zatwierdzenia i bez proporcjonalnego pomniejszenia;
- ostrzeżenie przy zatwierdzeniu w kolejnym miesiącu oraz zachowanie osobnej faktury bieżącej;
- zmiana pojedynczej wersji roboczej oddzielona od stawki na przyszłość;
- wykrycie ręcznie zmienionej wersji roboczej przed zastąpieniem nową stawką;
- wysyłka FA(3), sprawdzenie statusu przed ponowieniem po niepewnym wyniku, pobranie UPO/statusu i niezmienialny zapis wystawionej wersji;
- przełącznik automatyzacji domyślnie wyłączony, z ostrzeżeniem i audytem; żadnego samoczynnego włączania po czasie;
- blokada/klucz idempotencji gwarantujący co najwyżej jedną fakturę za dany okres abonamentu.

**Ryzyka:** ponowienie po niepewnym wyniku tworzące drugą fakturę, pomylenie daty wystawienia z okresem usługi, niezamierzone zastąpienie ręcznej kwoty.

**Testy:** przypadki z R12, R13, R33, R35 i R46; błąd sieci po wysyłce; odrzucenie FA(3); dwa równoległe zadania; ręczna i automatyczna ścieżka generują ten sam dokument wejściowy.

**Weryfikacja:** walidacja FA(3), scenariusze w testowym KSeF i porównanie zapisanej faktury z danymi zwróconymi przez KSeF.

**Bramka:** najpierw testowe KSeF; produkcyjna automatyzacja pozostaje wyłączona do osobnej decyzji właściciela po kilku zweryfikowanych miesiącach.

### Jednostka 6 — reguły kosztów, KPiR/VAT i samochód

**Cel:** księgować znane koszty automatycznie, a nowe lub istotnie zmienione bezpiecznie zatrzymywać.

**Pliki:** `Domain/Accounting`, `Domain/Vat`, `Domain/Vehicles`, `Application/Accounting`, `Application/Charging`, `Web/Pages/Invoices/Review`, `Web/Pages/Ledgers`, migracje i testy referencyjne.

**Wzorce:** wersjonowana reguła z kryteriami dopasowania, jawna decyzja użytkownika oraz oddzielne zapisy KPiR i VAT pochodzące z jednego źródła.

**Prace:**

- reguła rozpoznaje co najmniej podmiot, kraj, walutę, VAT i rodzaj usługi; kwota i zwykła zmiana daty nie unieważniają dopasowania;
- pierwsza faktura tworzy propozycję, którą właściciel zatwierdza; kolejne pełne dopasowania księgują się automatycznie;
- edycja jednego dokumentu oraz „stosuj do kolejnych” są dwoma osobnymi poleceniami;
- każda decyzja tworzy wyjaśnienie wpływu na KPiR, VAT, PIT i ewentualnie zdrowotną;
- osobne polityki dla najmu/leasingu, eksploatacji, ubezpieczenia i ładowania; ich uruchomienie zależy od danych umowy;
- import CSV jest powtarzalny i nie dubluje wierszy; jednostkę Wh potwierdza profil importu;
- stawka energii obowiązuje od wybranego miesiąca; miesięczne zestawienie pokazuje wiersze, kWh, stawkę i koszt;
- zestawienie domowego ładowania nie staje się automatycznie kosztem/VAT przed potwierdzeniem podstawy podatkowej;
- faktura za prywatną energię domową nie jest księgowana jako osobny koszt.

**Ryzyka:** zbyt szeroka reguła automatyczna, utożsamienie marki z podmiotem wystawcy, zastosowanie jednej proporcji do wszystkich kosztów auta, księgowanie niepotwierdzonego zestawienia ładowania.

**Testy:** 10 000 Wh = 10 kWh = 9,10 zł przy 0,91 zł/kWh; zmiana stawki nie rusza historii; zmiana kwoty pasuje do reguły, a zmiana kraju/waluty/VAT/usługi ją zatrzymuje; poprawka jednej faktury nie zmienia reguły; ponowny CSV nie dubluje danych.

**Weryfikacja:** porównanie zapisów ze zweryfikowanymi przykładami z jednostki 0 i ręczna kontrola każdej pierwszej reguły kosztowej.

### Jednostka 7 — silnik obliczeń i zamknięcie miesiąca

**Cel:** policzyć miesiąc w sposób powtarzalny, wyjaśnialny i blokowany przy brakach.

**Pliki:** `Domain/Calculations`, `Application/MonthClosing`, `Web/Pages/Month`, `Web/Pages/Settlements/Month`, `tests/ReferenceCalculations`.

**Wzorce:** czyste funkcje obliczeniowe, wersjonowane parametry, pełny snapshot wejścia i wyjaśnialne składniki wyniku.

**Prace:**

- wersjonowane kalkulatory KPiR, VAT, skali PIT i składki zdrowotnej;
- parametry roczne mają źródło i okres obowiązywania; wersja kalkulatora i pełny zestaw wejść trafiają do `CalculationRun`;
- wynik pokazuje składniki i zaokrąglenia prostym językiem;
- walidator kompletności zwraca listę konkretnych problemów oraz link do naprawy;
- „Zatwierdź rozliczenie miesiąca” tworzy niezmienialny `MonthlyClosingVersion` i dopiero wtedy zleca generowanie dokumentów;
- poprawka pokazuje różnicę kwot, wymaga powodu i oznacza zależne dokumenty jako wymagające korekty.

**Ryzyka:** zastosowanie niewłaściwego roku składkowego, błąd narastający PIT, różne zaokrąglenia w podsumowaniu i pliku urzędowym, zamknięcie na niepełnych danych.

**Testy:** złote przypadki z jednostki 0; zwykły miesiąc, brak przychodu, strata, start w środku miesiąca, zakup zagraniczny, koszt samochodu, korekta przed i po wysyłce, granice progów i reguły zaokrąglania.

**Weryfikacja:** raport różnic wobec niezależnych wyników referencyjnych, kontrola ścieżki wyjaśnienia każdej kwoty i ręczny odbiór blokad zamknięcia.

**Bramka:** zgodność z XSD nie wystarcza. Wyniki muszą odpowiadać niezależnie zatwierdzonym wyliczeniom referencyjnym.

### Jednostka 8 — dokumenty do US/ZUS, potwierdzenia i korekty

**Cel:** przygotować wersjonowane pliki do ręcznej wysyłki oraz zapisać ich dalszy los.

**Pliki:** `Domain/Filings`, `Application/Filings`, `Infrastructure/Filings/Jpk`, `Infrastructure/Filings/Zus`, `Web/Pages/Settlements/Filings`, kontrakty/XSD w kontrolowanym katalogu zasobów z metadanymi źródła.

**Wzorce:** osobny generator dla każdej wersji schematu, lokalna walidacja XSD i wersja artefaktu powiązana ze snapshotem zamknięcia.

**Prace:**

- generować tylko dokumenty wynikające z potwierdzonego profilu, w tym JPK_V7M(3), wymagany JPK_PKPIR(3) oraz właściwy zestaw ZUS/KEDU;
- walidować XML lokalnie przeciw dokładnej wersji XSD;
- każdą wygenerowaną wersję wiązać z zamknięciem i skrótem wejść;
- dostarczyć krótką instrukcję ręcznej wysyłki w aktualnym narzędziu urzędowym;
- umożliwić oznaczenie jako wysłane i dodać UPO/potwierdzenie, status przyjęty/odrzucony oraz datę;
- korekta tworzy nowy plik wskazujący poprzednią wersję i wymaga osobnego zatwierdzenia;
- `ISubmissionGateway` pozostaje granicą dla F2; eksport nie wywołuje automatycznej wysyłki.

**Ryzyka:** poprawny technicznie XML z błędną treścią, rozjazd wersji XSD i narzędzia urzędowego, przypięcie UPO do niewłaściwej wersji.

**Testy:** XSD, import testowego JPK w Kliencie JPK WEB i KEDU w ePłatniku/Płatniku, odrzucone potwierdzenie, ponowny eksport, korekta po przyjęciu, brak podwójnej wysyłki.

**Weryfikacja:** automatyczny raport walidacji oraz udokumentowany ręczny import każdego rodzaju pliku w aktualnym narzędziu odbiorcy.

**Ręczna weryfikacja:** właściciel wykonuje pełną próbę w narzędziu urzędowym na danych testowych; wynik i zrzut wersji narzędzia trafiają do dokumentacji odbioru.

### Jednostka 9 — zamknięcie roku i PDF do zewnętrznego PIT

**Cel:** zamknąć rok bez budowania pełnego zeznania PIT.

**Pliki:** `Domain/AnnualClosing`, `Application/AnnualClosing`, `Infrastructure/Pdf`, `Web/Pages/Settlements/Year`, testy PDF i scenariusze roczne.

**Wzorce:** roczny snapshot z wersjami, niemutowalne zamknięcie i osobny przypadek użycia korekty.

**Prace:**

- dopuścić „Zamknij rok” dopiero po zamknięciu wszystkich miesięcy i usunięciu braków;
- pokazać podsumowanie przychodów, kosztów, dochodu/straty, zaliczek należnych i wpłaconych, zdrowotnej i jej rocznego rozliczenia;
- przygotować wymagany plik KPiR oraz PDF z danymi firmowymi potrzebnymi w zewnętrznej aplikacji PIT;
- zachować PDF i umożliwić ponowne pobranie;
- zablokować zwykłą edycję zamkniętego roku;
- „Rozpocznij korektę” wymaga powodu, zachowuje poprzednią wersję i prowadzi do nowych wersji zależnych dokumentów;
- zlecić trwałe archiwum roczne poza rotacją pięciu kopii.

**Ryzyka:** zamknięcie z brakującym miesiącem, pominięcie danych potrzebnych zewnętrznemu PIT, częściowa korekta pozostawiająca niespójne dokumenty.

**Testy:** niepełny rok nie zamyka się; PDF ma wszystkie potwierdzone pola i poprawnie renderuje polskie znaki; wcześniejszy rok nie zmienia się po otwarciu kolejnego; korekta zachowuje oba zestawy dokumentów.

**Weryfikacja:** automatyczna kontrola danych PDF, wizualne renderowanie wszystkich stron i porównanie rocznego podsumowania z sumą zamkniętych miesięcy.

### Jednostka 10 — płatności, powiadomienia i pełny ekran „Mój miesiąc”

**Cel:** pokazać właścicielowi dokładnie, co wymaga działania, i przypomnieć mu bez codziennego zaglądania.

**Pliki:** `Domain/Payments`, `Domain/Notifications`, `Application/Dashboard`, `Application/Payments`, `Infrastructure/Email`, `Worker/Jobs/Notifications`, strony `Month` i `Payments`.

**Wzorce:** outbox e-mail, unikalny klucz zdarzenia i projekcja „Mój miesiąc” wyliczana z systemów źródłowych.

**Prace:**

- ręczne oznaczanie pełnej płatności faktury lub należności wraz z datą; inna kwota nie zamyka pozycji;
- model płatności ma zewnętrzny identyfikator i źródło, aby późniejszy import nie dublował ręcznych danych;
- dashboard pokazuje najbliższą czynność, dokumenty do sprawdzenia, prognozy VAT/PIT/ZUS, terminy, płatności, stan miesiąca i stan kopii;
- e-maile wysyłane o 08:00 czasu `Europe/Warsaw`: faktura od 1. dnia, terminy US/ZUS 7 dni, 2 dni i w dniu terminu, faktura klienta dzień po terminie;
- nowy błąd wysyłany od razu, a ponowienie tej samej nierozwiązanej sprawy najwyżej raz na dobę;
- unikalny klucz powiadomienia zapobiega duplikatom po restarcie workera;
- wiadomość nie zawiera załączników ani nadmiarowych danych księgowych; prowadzi do aplikacji po zalogowaniu.

**Ryzyka:** spam po restarcie, pominięte przypomnienie przez błąd strefy czasowej, fałszywe oznaczenie częściowej wpłaty jako pełnej.

**Testy:** pełna i niepełna kwota, strefa czasowa i dni wolne dla terminów, restart workera, wielokrotne uruchomienie harmonogramu, rozwiązanie i ponowne pojawienie się błędu, niedostępny SMTP.

**Weryfikacja:** testowa skrzynka e-mail, ręczna kontrola harmonogramu na zegarze testowym i porównanie dashboardu z rekordami źródłowymi.

### Jednostka 11 — kopia, klient Mac i pełne odtworzenie

**Cel:** spełnić R40-R42 na rzeczywistym MacBooku, nie tylko utworzyć archiwum na tym samym VPS.

**Pliki:** `Firemka.BackupClient`, `Infrastructure/Backups`, `Web/Pages/Backups`, `deploy/macos/`, `deploy/scripts/restore`, `docs/runbooks/backup-and-restore.md`.

**Wzorce:** strumieniowe archiwum z manifestem, szyfrowanie uwierzytelnione, zapis atomowy i weryfikacja przed rotacją.

**Prace:**

- wersjonowany format `.fmbak`, manifest, szyfrowanie uwierzytelnione i kontrola kompletności;
- klient macOS, wpis `launchd`, Pęk kluczy i token tylko do pobierania kopii;
- harmonogram, ręczne uruchomienie, nadrabianie po niedostępności Maca, rotacja pięciu poprawnych kopii;
- status ostatniej poprawnej kopii widoczny w aplikacji i alarm, gdy kopia jest zaległa lub uszkodzona;
- archiwum roczne poza rotacją;
- bezpieczny kreator odtworzenia do czystej instancji, zgodność wersji, migracje do przodu i zakaz cichego nadpisania istniejących danych;
- instrukcja utraty MacBooka, zmiany tokenu i odzyskania na nowym komputerze.

**Ryzyka:** fałszywie oznaczona poprawna kopia, utrata hasła, archiwum bez części załączników, niezgodna wersja restore, przypadkowe nadpisanie danych.

**Testy:** przerwane pobieranie, pełny dysk, zła suma, złe hasło, brak Maca przez kilka dni, rotacja 5→5, odtworzenie na drugim komputerze/VPS, porównanie liczby rekordów i skrótów wszystkich plików.

**Weryfikacja:** protokół pełnego odtworzenia zawierający manifest, wynik kontroli rekordów i plików oraz potwierdzenie uruchomienia aplikacji z odzyskanych danych.

**Bramka:** produkcja dopiero po udokumentowanym odtworzeniu kompletnej kopii na czystym środowisku.

### Jednostka 12 — utwardzenie, pilotaż i uruchomienie

**Cel:** przejść z programu testowego do kontrolowanego użycia przy rzeczywistej firmie.

**Pliki:** `docs/runbooks/`, `docs/acceptance/`, konfiguracja produkcyjna w `deploy/`, testy dymne i procedura wycofania.

**Wzorce:** powtarzalne wdrożenie z przypiętymi obrazami, checklista odbioru i pilotaż z ręcznym zatwierdzaniem.

**Prace:**

- kopia VPS, aktualizacje systemu, HTTPS, firewall, ograniczenie portów, monitoring dysku/certyfikatu/health checków;
- analiza zależności i obrazów kontenerów, test uprawnień, logów i sesji;
- porównanie pełnego miesiąca z wyliczeniem referencyjnym;
- test na telefonie i MacBooku: dokument, zatwierdzenie, pobranie pliku i 2FA;
- próbna synchronizacja KSeF, import do narzędzi urzędowych i pełne odtworzenie kopii;
- pierwszy rzeczywisty miesiąc w trybie ręcznym: właściciel zatwierdza fakturę sprzedaży, sprawdza księgowania, sam wysyła dokumenty i oznacza płatności;
- zapis procedury awarii KSeF, odrzucenia pliku, braku e-maila, utraty telefonu/Maca i powrotu do ostatniej poprawnej wersji.

**Ryzyka:** uznanie zielonego health checku za dowód poprawności księgowej, włączenie automatyzacji za wcześnie, brak drogi powrotu po aktualizacji.

**Testy:** pełna ścieżka od logowania do zamknięcia miesiąca, kontrolowana awaria KSeF/SMTP/workera, próba aktualizacji i wycofania, test wszystkich scenariuszy odbioru z wymagań.

**Weryfikacja:** podpisana checklista pilotażu zawierająca wersje aplikacji i schematów, wyniki referencyjne, test telefonu/Maca, import urzędowy oraz dowód odtworzenia kopii.

**Kryterium odbioru:** wszystkie kryteria sukcesu z wymagań mają test lub udokumentowaną próbę; wszystkie krytyczne bramki przeszły; brak nierozstrzygniętej reguły używanej automatycznie.

## 8. Rozwój po pierwszej wersji

### F1 — import wyciągu

- ustalić bank i formaty CSV/MT940;
- zapisywać skrót pliku i identyfikator wiersza, aby ponowny import był bezpieczny;
- jednoznaczne dopasowania potwierdzać automatycznie, niejednoznaczne pokazywać jako propozycje;
- import tworzy `Payment`, nie drugi koszt ani przychód;
- dodać test konfliktu z wcześniejszą płatnością ręczną.

### F2 — automatyczna wysyłka do US/ZUS

- osobno potwierdzić możliwości, podpis, uprawnienia i API dla każdego typu dokumentu;
- wykorzystać te same `FilingArtifactVersion`, walidatory i stany co eksport ręczny;
- wysyłka zawsze odnosi się do konkretnej wersji i ma klucz idempotencji;
- korekty nadal wymagają jawnego zatwierdzenia właściciela;
- automatyzacja nie znosi zatwierdzenia miesiąca.

### F3 — ryczałt

- osobny moduł zasad, ewidencji, zdrowotnej i dokumentów;
- wybór dostępny dopiero po implementacji, testach referencyjnych i potwierdzeniu właściwej stawki/PKWiU;
- nowa forma działa od nowego roku i nigdy nie przelicza poprzednich lat.

## 9. Strategia testów i dowodów

### Automatyczne

- testy jednostkowe domeny dla wszystkich reguł, granic, dat i zaokrągleń;
- testy właściwości: suma pozycji, brak ujemnych/zdublowanych skutków, stabilność ponowienia;
- testy integracyjne z prawdziwym PostgreSQL uruchamianym w kontenerze;
- testy kontraktowe KSeF i walidacja XML względem przypiętych XSD;
- testy złotych plików PDF/XML na danych sztucznych;
- Playwright dla głównych ścieżek na rozmiarze telefonu i komputera;
- testy bezpieczeństwa dostępu do stron, plików, CSRF, sesji, 2FA i limitu logowania;
- testy awarii workera, ponowień i idempotencji;
- test kopia→czyste środowisko→odtworzenie→porównanie rekordów i plików.

### Ręczne

- KSeF w środowisku testowym/demo;
- import JPK/KEDU w aktualnym narzędziu urzędowym;
- porównanie PIT/VAT/ZUS z zatwierdzonymi przykładami;
- odczyt rzeczywistych faktur i CSV po zanonimizowaniu materiału testowego;
- pełny miesiąc na telefonie i MacBooku;
- utrata TOTP z użyciem kodu odzyskiwania;
- utrata zaufanego urządzenia i unieważnienie sesji;
- pełne odtworzenie na drugim, pustym środowisku.

### Polecenia jakości przewidziane po utworzeniu projektu

```text
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
docker compose config
```

Dokładne polecenia testów E2E i skanów bezpieczeństwa należy dopisać do `README.md`, gdy zostaną przypięte konkretne narzędzia.

## 10. Ryzyka i ograniczenia

| Ryzyko | Ograniczenie |
|---|---|
| zmiana prawa, XSD lub API | wersje, daty obowiązywania, cykliczny przegląd źródeł i test importu przed nowym okresem |
| błędna reguła podatkowa | przykłady referencyjne potwierdzone niezależnie; automatyzacja tylko zatwierdzonych przypadków |
| podwójna faktura lub dokument | klucze idempotencji, unikalne ograniczenia bazy i sprawdzenie statusu przed ponowieniem |
| OCR odczyta błędne dane | pewność pola, obowiązkowy podgląd i ręczne potwierdzenie danych spoza KSeF |
| zmiana danych dostawcy | dopasowanie reguły zatrzymuje się przy zmianie kraju, waluty, VAT lub usługi |
| utrata/kradzież VPS | szyfrowanie sekretów, HTTPS, najmniejsze uprawnienia i kompletna kopia poza VPS |
| MacBook jest wyłączony | klient `launchd` nadrabia kopię po dostępności, a dashboard i e-mail pokazują zaległość |
| zgubione hasło kopii | jasne ostrzeżenie, zapis w menedżerze haseł/offline; hasła nie da się odzyskać z kopii |
| kopia istnieje, ale nie działa | automatyczna kontrola manifestu oraz obowiązkowe okresowe pełne odtworzenie |
| mały VPS | audyt przed wyborem limitów kontenerów i przed produkcją; OCR może wymagać ograniczenia równoległości |
| zbyt szeroki pierwszy zakres | jednostki wdrażane kolejno, z bramką odbioru po każdej; funkcje F1-F3 poza pierwszą wersją |

## 11. Aktualne źródła do przypięcia w jednostce 0

Sprawdzone ponownie 2026-09-09. Podczas implementacji należy pobrać aktualne pliki źródłowe, zapisać ich skróty i sprawdzić komunikaty zmian.

- [MF — wsparcie dla integratorów KSeF 2.0](https://ksef.podatki.gov.pl/ksef-na-okres-obligatoryjny/wsparcie-dla-integratorow/) — OpenAPI, środowiska, scenariusze i oficjalne biblioteki .NET/Java; strona zmieniona 2026-06-09.
- [CIRF/MF — oficjalny klient KSeF dla .NET](https://github.com/CIRFMF/ksef-client-csharp) — uzasadnia wybór .NET; wersję pakietu należy przypiąć i aktualizować kontrolowanie.
- [MF — struktura FA(3)](https://ksef.podatki.gov.pl/informacje-ogolne-ksef-20/struktura-logiczna-fa-3/) — aktualna struktura faktury obowiązująca od 2026-02-01.
- [MF — pliki JPK_V7M(3)/JPK_V7K(3)](https://www.podatki.gov.pl/podatki-firmowe/jednolity-plik-kontrolny/jpk_vat-z-deklaracja/pliki-do-pobrania) — XSD i przykłady; strona zaktualizowana 2026-08-06.
- [MF — broszura JPK_PKPIR(3)](https://www.podatki.gov.pl/media/pbqdgqcp/broszura_informacyjna_dot_jpk_pkpir-3.pdf) — struktura obowiązująca od 2026-01-01 i zakres pierwszego obowiązku dla miesięcznego JPK_V7M.
- [ZUS BIP — aktualne wymagania dla dokumentów ubezpieczeniowych](https://bip.zus.pl/pl/inne/wymagania-dla-oprogramowania-interfejsowego/dokumenty-ubezpieczeniowe) — bieżące XSD KEDU i zestawy testowe; nie polegać wyłącznie na starszej stronie pomocy ePłatnika.
- [ZUS — kalkulator składki zdrowotnej](https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej) — źródło porównawcze parametrów i przykładów, nie biblioteka do automatycznego wywoływania.
- [MF — Podatki 2026, przewodnik](https://www.podatki.gov.pl/media/rr5pgg25/podatki-2026-przewodnik-dla-inwestorow.pdf) — m.in. limit dla elektrycznych samochodów osobowych; konkretna umowa i użytek mieszany nadal wymagają osobnej kwalifikacji.
- [Microsoft — MFA w ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0) i [wybór systemu tożsamości](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0) — wzorzec 2FA i zarządzania kontem.
- [Microsoft — .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview) — wydanie LTS wspierane przez trzy lata.
- [PostgreSQL — polityka wersji](https://www.postgresql.org/support/versioning/) — PostgreSQL 18 jest wspierany do listopada 2030; na wdrożeniu używać bieżącej poprawkowej wersji 18.x.

## 12. Dane i działania wymagane od właściciela

Nie są potrzebne do napisania szkieletu, ale są konieczne przed wskazanymi jednostkami lub przed produkcją:

1. Przed jednostką 3: dokładna data startu, dane firmy, dane klienta, kwota abonamentu netto/brutto, VAT i opis usługi.
2. Przed jednostką 4: dostęp testowy KSeF oraz przykładowe faktury; sekrety przekazywane lokalnie, nigdy w dokumencie planu ani czacie.
3. Przed jednostką 6: przykładowy CSV, rzeczywiste faktury AI/VPS, oferta/umowa samochodu, wartość pojazdu i rozbicie usług w racie.
4. Przed uruchomieniem automatycznego księgowania domowego ładowania: pisemnie ustalona podstawa dokumentacyjna i sposób PIT/VAT.
5. Przed jednostką 8: potwierdzenie faktycznego zestawu dokumentów ZUS i US oraz test importu na aktualnych narzędziach.
6. Przed jednostką 10: dane serwera SMTP lub decyzja o dostawcy wiadomości; adres odbiorcy pozostaje `pawel@sitback.pl`.
7. Przed jednostką 11: potwierdzenie folderu kopii, utworzenie hasła odzyskiwania i wskazanie bezpiecznego miejsca jego dodatkowego przechowania.
8. Przed produkcją: udostępnienie VPS do audytu, ręczny odbiór na telefonie/Macu i zatwierdzenie referencyjnego miesiąca.

## 13. Otwarte pytania wykonawcze

Pytania te nie blokują planu, ale mają wskazane bramki:

- jaki dokładnie VPS i domena będą użyte oraz czy zasoby wystarczą dla OCR;
- jakie są ostateczne dane działalności, data startu i ewentualne dane początkowe;
- jak właściciel uwierzytelni Firemkę w KSeF i jaki jest cykl odnowienia certyfikatu;
- jakie podmioty, kraje, waluty i treść usługi występują na prawdziwych fakturach OpenAI/Anthropic/OVH;
- czy samochód będzie najmem czy leasingiem i jak umowa rozdziela finansowanie, serwis, ubezpieczenie i inne opłaty;
- jakie kolumny, kodowanie, separator, strefę czasu i jednostkę ma prawdziwy CSV ładowania;
- czy lokalny OCR osiąga wystarczającą jakość; dopiero test może uzasadnić zewnętrzną usługę;
- jaki katalog kopii jest wygodny oraz gdzie poza kopią będzie przechowywane hasło odzyskiwania;
- jaki dokładny zestaw formularzy ZUS wynika z rejestracji i sytuacji etatowej;
- kto niezależnie potwierdzi reguły oraz miesiąc referencyjny przed produkcją.

## 14. Zalecana kolejność i następny krok

Kolejność zależności:

```text
0 bramka faktów
  -> 1 szkielet i logowanie
  -> 2 dane/historia/zadania
  -> 3 profil i Mój miesiąc
  -> 4 dokumenty/OCR/KSeF wejściowy
  -> 5 sprzedaż/KSeF wyjściowy
  -> 6 księgowanie/samochód
  -> 7 obliczenia i miesiąc
  -> 8 pliki urzędowe
  -> 9 rok
  -> 10 płatności/powiadomienia/dashboard
  -> 11 kopie i odtworzenie
  -> 12 kontrolowany pilotaż
```

Jednostki 4 i 5 mogą być realizowane równolegle dopiero po ustabilizowaniu jednostek 1-3, ale odbiór KSeF powinien nastąpić wspólnie. Jednostkę 11 można technicznie rozpocząć po jednostce 2, lecz musi objąć finalny zestaw danych po jednostce 10.

Zalecany następny krok: użyć `dev-docs`, aby utworzyć aktywne dokumenty wykonawcze dla **wyłącznie jednostki 0**. Po jej zamknięciu należy osobno zatwierdzić rozpoczęcie jednostki 1. Każda kolejna jednostka powinna mieć osobny odbiór i aktualizację planu; commit, push i wdrożenie wymagają odrębnego polecenia.
