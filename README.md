# Firemka

Prywatna aplikacja dla jednoosobowej działalności. Obecna wersja techniczna obejmuje profil firmy, dokumenty, faktury, reguły kosztowe, miesięczne i roczne rozliczenie, pliki urzędowe, płatności, powiadomienia oraz zaszyfrowane kopie. Integracje produkcyjne pozostają wyłączone do czasu podpisanego pilotażu i zewnętrznego potwierdzenia wyników.

## Co działa w fazie 1

- jednorazowe utworzenie konta właściciela;
- obowiązkowy drugi składnik logowania (TOTP) i jednorazowe kody odzyskiwania;
- zaufane urządzenia ważne 30 dni oraz przycisk unieważniający wszystkie;
- prywatne strony, ochrona formularzy przed fałszywymi żądaniami, limit prób logowania, nagłówki bezpieczeństwa i audyt logowania;
- migracja PostgreSQL, health check i kontenery: PostgreSQL, WWW, worker oraz Caddy.

## Co działa w fazie 2

- wersjonowana historia korekt oraz kontrolowane zmiany stanów dokumentu;
- prywatny magazyn plików poza katalogiem WWW, z limitem 20 MB, kontrolą rzeczywistego typu PDF/JPEG/PNG, sumą SHA-256, pochodzeniem i powiązaniem z wersją rekordu;
- bezpieczny podgląd dostępny wyłącznie po zalogowaniu i tylko właścicielowi pliku;
- trwała kolejka w PostgreSQL z odnawianym leasingiem, ponowieniami, opóźnieniem i ochroną przed podwójnym dodaniem zadania;
- transakcyjny outbox, audyt i granice dla przyszłych integracji KSeF, OCR, płatności oraz wysyłek.

Pliki są przechowywane w nazwanym woluminie `private-files` i są dostępne wyłącznie po sprawdzeniu właściciela dokumentu.

## Co działa w fazach 3–4

- profil firmy, kontrahenta i dopisywane okresy ustawień bez zmiany wcześniejszych miesięcy;
- ekran „Mój miesiąc” bez prezentowania niegotowych obliczeń podatkowych jako wyniku;
- skrzynka dokumentów przyjmująca PDF, JPEG i PNG do 20 MB, z prywatnym podglądem obok formularza danych;
- lokalne wydobycie tekstu z PDF oraz OCR zdjęć przez workera (`pdftotext`, Poppler i Tesseract); wynik jest wyłącznie podpowiedzią z pewnością poszczególnych pól;
- ręczne potwierdzenie wymaganych danych albo oznaczenie dokumentu jako niezwiązanego z firmą wraz z audytem decyzji;
- przychodzący adapter oficjalnego API KSeF 2.0, osobne adresy Test/Demo/Produkcja, przyrostowy kursor, historię synchronizacji, kontrolę skrótu XML i ochronę przed duplikatami;
- produkcja KSeF jest podwójnie zablokowana: samym wyłączeniem integracji oraz osobną flagą `KSEF_ALLOW_PRODUCTION`, która domyślnie pozostaje `false`.

Surowe XML KSeF są zachowywane przed ekstrakcją. Aplikacja odrzuca niepoprawny XML i deklaracje DTD. Integracja pozostaje wyłączona, dopóki właściciel nie przekaże bezpiecznie testowego tokenu; tokenu nie wolno wpisywać do repozytorium ani dokumentacji.

## Co działa w fazach 5–12

- wersjonowane szkice faktur, ręczne zatwierdzenie i bezpieczna granica wysyłki KSeF z ochroną przed duplikatem;
- reguły kosztowe, osobne zapisy KPiR i VAT oraz import stałego CSV z domowej ładowarki bez automatycznego uznania podstawy podatkowej;
- kalkulacja i świadome zamknięcie miesiąca, historia korekt oraz blokady nierozwiązanych danych;
- prywatne, wersjonowane pliki JPK_V7M, JPK_PKPIR i ZUS DRA z lokalną kontrolą XSD i ręcznym zapisem wyniku wysyłki;
- zamknięcie roku, roczny raport PDF i historia rocznych korekt;
- pełne ręczne płatności, oszczędne powiadomienia oraz pulpit „Mój miesiąc”;
- szyfrowane kopie pobierane na Maca, kontrolowana rotacja, archiwa roczne i odtworzenie wyłącznie do czystej instancji;
- przypięte obrazy bazowe, ograniczenia kontenerów, odczytowy audyt VPS, monitor zdrowia oraz procedury wydania, awarii i wycofania.

Automatyczna wysyłka do urzędów, import wyciągów bankowych i automatyczne dopasowanie płatności nie należą do pierwszej wersji. Wyniki podatkowe oraz rzeczywiste pliki urzędowe wymagają odbioru na uzgodnionych danych i w aktualnych narzędziach urzędowych.

Pozostałe działania właściciela prowadzące od gotowej wersji lokalnej do kontrolowanego pilotażu są opisane prostym językiem w [instrukcji odbioru Fazy 12](docs/acceptance/phase12-owner-handoff.md).

## Pierwsze uruchomienie lokalne

1. Zainstaluj .NET SDK 10 oraz Docker Desktop i uruchom Docker Desktop.
2. Skopiuj `.env.example` do `.env`. Do bezpiecznej, lokalnej próby użyj `localhost` jako domeny, `local@example.test` jako e-maila i własnego długiego hasła testowej bazy. Plik `.env` jest ignorowany przez Git.
3. Uruchom `docker compose up --build -d`, a następnie sprawdź stan poleceniem `docker compose ps`.
4. Caddy utworzy dla `localhost` lokalny certyfikat HTTPS. Docker nie może automatycznie dodać go do zaufanych certyfikatów macOS. Wyeksportuj wyłącznie publiczny certyfikat poleceniem:

   ```sh
   docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt /private/tmp/firemka-caddy-local-root.crt
   ```

   Otwórz **Dostęp do pęku kluczy**, wybierz pęk **System**, zaimportuj ten plik, otwórz jego szczegóły i w sekcji „Zaufanie” wybierz „Zawsze ufaj”. macOS poprosi o hasło administratora. Rób to wyłącznie dla certyfikatu, który właśnie wyeksportowałeś z własnego lokalnego kontenera Caddy. [Instrukcja Caddy dla Dockera](https://caddyserver.com/docs/running#docker) wyjaśnia, dlaczego ten krok jest potrzebny.
5. Otwórz `https://localhost` w przeglądarce. Pierwsza osoba, która przejdzie kreator, zostanie jedynym właścicielem konta. W aplikacji TOTP na telefonie zeskanuj pokazany kod QR, podaj sześciocyfrowy kod i zapisz kody odzyskiwania poza Firemką. Gdy skanowanie nie jest możliwe, pod kodem QR pozostaje klucz do ręcznego dodania konta.

Przy prawdziwej domenie zastąp `localhost` oraz `local@example.test` własnymi wartościami. Wtedy Caddy pobierze zwykły publiczny certyfikat i nie należy instalować lokalnego certyfikatu testowego. Po każdej zmianie `Caddyfile` uruchom ponownie `docker compose up --build -d`, ponieważ konfiguracja jest celowo kopiowana do obrazu Caddy podczas budowania.

Do pracy bez kontenerów ustaw połączenie lokalnie, nigdy w pliku śledzonym przez Git:

```sh
dotnet user-secrets set "ConnectionStrings:Firemka" "Host=localhost;Port=5432;Database=firemka;Username=firemka;Password=..." --project src/Firemka.Web
dotnet user-secrets set "ConnectionStrings:Firemka" "Host=localhost;Port=5432;Database=firemka;Username=firemka;Password=..." --project src/Firemka.Worker
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Firemka.Infrastructure --startup-project src/Firemka.Web
dotnet run --project src/Firemka.Web
```

## Sprawdzenie jakości

Zwykły szybki zestaw testów:

```sh
dotnet test Firemka.sln
```

Końcowa bramka Fazy 12 znajduje się w `deploy/scripts/verify-phase12-local.sh`. Wymaga Dockera, Docker Scout, `jq` i osobnej testowej bazy PostgreSQL wskazanej przez `FIREMKA_TEST_POSTGRES`; ustawienie `PHASE12_REQUIRE_POSTGRES=1` zapobiega przypadkowemu pominięciu tych prób. Skrypt niczego nie wdraża.

Testy obejmują m.in. brak publicznej rejestracji, odrzucenie logowania samym hasłem, TOTP, pojedyncze użycie kodu odzyskiwania, wygaśnięcie/unieważnienie zaufanego urządzenia, ochronę formularzy, prywatność stron i plików, wersjonowanie historii oraz odporność trwałych zadań na awarię i powtórzenie.

Testy Fazy 4 obejmują również powtórzoną i przerwaną synchronizację KSeF, wykrycie zmiany XML bez nadpisania źródła, kontrolę nagłówka SHA-256, złośliwy XML, nieczytelny skan, ręczne potwierdzenie oraz formularz działający na polskim zapisie liczb.

## Granice pilotażu

Nie wpisuj do repozytorium danych firmy, dokumentów klientów, tokenów KSeF, haseł ani kodów odzyskiwania. Technicznie działający moduł nie oznacza jeszcze zgody na produkcję. Wynik lokalnej kontroli zapisano w `docs/acceptance/phase12-local-gate.md`. Przed pilotażem trzeba przejść i podpisać `docs/acceptance/phase12-pilot-checklist.md`, w tym niezależne porównanie miesiąca referencyjnego, próby KSeF/SMTP, import plików urzędowych i pełne odtworzenie kopii.
