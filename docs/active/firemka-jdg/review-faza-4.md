# Review fazy 4 — dokumenty przychodzące, KSeF i OCR

Data: 2026-09-10  
Status: **WYMAGA NAPRAWY P2; ZEWNĘTRZNA BRAMKA KSeF NADAL OTWARTA**

## Znaleziska

### P1 — brak rzeczywistej próby testowego KSeF i dokumentów właściciela

Plan wymaga ręcznej próby z rzeczywistym PDF, zdjęciem z telefonu i fakturą pobraną z testowego KSeF (`docs/plans/2026-09-09-jdg-application-plan.md`, wiersze 319–341). Nie przekazano jeszcze dostępu testowego KSeF ani przykładowych dokumentów. Utworzone sztuczne PDF i PNG potwierdzają działanie lokalnych narzędzi, ale nie weryfikują rzeczywistego układu faktur, jakości zdjęcia z telefonu, uwierzytelnienia ani odpowiedzi serwera KSeF. Dodatkowo plan wskazuje oficjalny pakiet klienta, podczas gdy bieżący adapter implementuje potrzebny fragment bezpośrednio na podstawie oficjalnego OpenAPI (`src/Firemka.Infrastructure/Ksef/Incoming/KsefIncomingHttpGateway.cs`, wiersze 10–123); wyszukiwanie pakietu na publicznym NuGet nie zwraca `KSeF.Client`.

**Wymagana czynność:** po bezpiecznym przekazaniu testowego dostępu uruchomić pełną synchronizację, ponowienie, wygaśnięcie uprawnienia i porównać zapisany XML z odpowiedzią KSeF. Osobno wgrać zanonimizowany rzeczywisty PDF i zdjęcie wykonane telefonem. Przed tym testem albo użyć oficjalnego klienta, albo świadomie zatwierdzić i udokumentować bezpośredni adapter OpenAPI. Ta bramka blokuje funkcje KSeF Fazy 5 i wszystkie działania produkcyjne, ale zgodnie z zasadą aktywnego planu nie blokuje niezależnych, bezpiecznych prac technicznych na sztucznych danych.

### P2 — wycofanie transakcji bazy nie usuwa gotowego pliku z dysku

Wgrywanie ręczne obejmuje transakcją dokument, metadane i zadanie, lecz po zapisaniu fizycznego pliku dalszy błąd przy zapisie audytu lub kolejki wycofa tylko bazę (`src/Firemka.Infrastructure/Documents/IncomingDocumentService.cs`, wiersze 26–72). Taki sam przypadek istnieje przy imporcie KSeF (`src/Firemka.Infrastructure/Ksef/Incoming/KsefIncomingSyncService.cs`, wiersze 86–136). `StoredFileService` usuwa plik wyłącznie wtedy, gdy zawiedzie jego własny pierwszy zapis do bazy. Powtarzana awaria może więc pozostawiać niewidoczne pliki i z czasem zapełnić dysk.

**Wymagana naprawa:** dodać jawne sprzątanie fizycznej kopii po nieudanym zakończeniu całej operacji oraz test kontrolowanej awarii po zapisaniu pliku, który potwierdza brak rekordu, zadania i pliku.

### P2 — anulowanie OCR nie kończy procesu i pozostawia próbę jako aktywną

Silnik zabija proces tylko przy własnym 45-sekundowym limicie. Gdy anulowanie pochodzi z zatrzymania workera lub utraty leasingu, filtr wyjątku pomija ten blok i uruchomiony `pdftotext` albo Tesseract może nadal pracować (`src/Firemka.Infrastructure/Ocr/ProcessDocumentTextExtractionEngine.cs`, wiersze 121–133). Jednocześnie handler świadomie nie zapisuje końca anulowanej próby, więc rekord pozostaje na zawsze w stanie `Running` (`src/Firemka.Infrastructure/Documents/DocumentExtractionJobHandler.cs`, wiersze 37–50 i 87–97). Kolejne przejęcie zadania tworzy następną próbę, ale historia błędnie sugeruje, że poprzednia nadal trwa.

**Wymagana naprawa:** przy każdym anulowaniu zakończyć całe drzewo procesu, a próbę oznaczyć osobnym stanem `Interrupted` z datą końca bez zmiany dokumentu na trwały błąd. Dodać test procesu oraz test przejęcia zadania po anulowaniu.

### P2 — brak pewności pola jest pokazywany jako pewność całego dokumentu

`ConfidenceFor` zwraca ogólną pewność, gdy słownik nie zawiera danego pola (`src/Firemka.Web/Pages/Invoices/Incoming/Details.cshtml.cs`, wiersze 22–25). Lokalny ekstraktor celowo nie zapisuje pewności dla pola, którego nie odczytał. W efekcie brakująca np. waluta może dostać na ekranie wartość 85%, mimo że nie została rozpoznana. Walidacja nadal blokuje potwierdzenie, ale komunikat podważa zasadę, że OCR jest wyłącznie ostrożną podpowiedzią.

**Wymagana naprawa:** dla ręcznego PDF/zdjęcia pokazywać `brak podpowiedzi`, jeśli konkretnego pola nie ma w słowniku. Dla danych KSeF zapisać jawne pewności pól albo wyświetlić osobną etykietę źródła urzędowego. Dodać test brakującego pola przy wysokiej pewności pozostałych.

### P2 — konflikt źródła może być niewidoczny po zaksięgowaniu lub odrzuceniu dokumentu

Wykrycie innego XML pod tym samym numerem KSeF ustawia flagę konfliktu, lecz dla statusów `Booked` i `UnrelatedToBusiness` zachowuje dotychczasowy status (`src/Firemka.Domain/Documents/SourceDocument.cs`, wiersze 204–220). Kontrakt widoku nie przekazuje flagi `HasSourceConflict`, a lista i szczegóły opierają komunikat wyłącznie na statusie. W rezultacie zaksięgowany dokument nadal wygląda jak poprawnie zaksięgowany, mimo że źródło integralności zgłosiło konflikt.

**Wymagana naprawa:** przekazać flagę i datę konfliktu do listy oraz szczegółów i pokazać niezależne, wyraźne ostrzeżenie dla każdego statusu. Dodać test konfliktu dokumentu już zaksięgowanego i oznaczonego jako niezwiązany.

### P2 — lista dokumentów rośnie bez ograniczenia

Każde wejście do skrzynki pobiera i materializuje wszystkie dokumenty właściciela (`src/Firemka.Infrastructure/Documents/IncomingDocumentService.cs`, wiersze 77–85), a następnie renderuje je w jednej tabeli. Dla jednej firmy nadal będą to tysiące rekordów po kilku latach. Czas odpowiedzi i pamięć będą rosły wraz z historią, mimo że bieżąca skrzynka powinna przede wszystkim pokazywać nowe sprawy.

**Wymagana naprawa:** wprowadzić ograniczoną stronę wyników ze stabilnym sortowaniem i filtrem statusu, a testem potwierdzić brak pominięć i powtórzeń między stronami.

### P3 — test narzędzi OCR może przejść bez wykonania sprawdzenia

Test rzeczywistych narzędzi kończy się sukcesem, gdy nie znajdzie plików wyłącznie pod ścieżkami Homebrew dla Apple Silicon, i czyta próbki z `output/pdf` zamiast z deklarowanego katalogu `tests/Fixtures` (`tests/Firemka.Infrastructure.Tests/LocalDocumentExtractorTests.cs`, wiersze 56–70). Na innym komputerze daje to fałszywie zielony wynik.

**Zalecenie:** umieścić sztuczne próbki w `tests/Fixtures/phase4`, wykrywać programy przez środowisko uruchomieniowe i jawnie oznaczać brak narzędzia jako pominiętą bramkę albo uruchamiać test w obrazie workera.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** pliki są prywatne, sprawdzane pod względem rozmiaru, sygnatury i bezpiecznego XML; podgląd wymaga właściciela i nie jest buforowany. Do poprawy pozostaje sprzątanie pliku po wycofaniu całej operacji.
- **Wydajność i odporność:** kursor strony, SHA-256, unikalny numer KSeF, trwałe zadania i wznowienie chronią przed zwykłym duplikatem. Proces OCR trzeba jednak zawsze kończyć przy anulowaniu, a skrzynkę ograniczyć stronami.
- **Architektura i typy:** domena nie zależy od OCR ani HTTP, a adapter zewnętrzny jest odseparowany. Bez rzeczywistej próby nie można potwierdzić zgodności ręcznie zaimplementowanego fragmentu API z oficjalnym klientem i uwierzytelnieniem KSeF.
- **Scenariusze i testy:** pokryto ponowienie, przerwanie strony, zmieniony XML, zły skrót, wygasły token, zły typ pliku, nieczytelny odczyt i ręczne potwierdzenie. Brakuje awarii po fizycznym zapisie, anulowania procesu, konfliktu po stanie końcowym, paginacji oraz pełnej bramki zewnętrznej.

## Dowody wykonane podczas review

- pełny zestaw bez zewnętrznej bazy: 76/76 testów zielonych;
- osobny PostgreSQL: 1/1 test zielony, w tym migracja w przód, cofnięcie i ponowne nałożenie;
- EF Core: brak zmian modelu bez migracji;
- formatowanie, cztery kontrakty XML i składnia Compose: czyste;
- skan zależności: brak zgłoszonych znanych podatności NuGet;
- lokalne Compose po przebudowie: PostgreSQL i WWW zdrowe, migracja zakończona kodem 0, worker działa, `https://localhost/health` zwraca `Healthy` bez pomijania certyfikatu;
- istniejące konto zachowane: 1 właściciel, TOTP włączone, 0 firm i 0 dokumentów;
- ręczny odbiór Safari w tej próbie był niemożliwy, ponieważ Mac był zablokowany; nie obchodzono blokady.

## Decyzja bramki

Wynik: **1× P1 zewnętrzne, 5× P2, 1× P3**. Faza 5 nie może rozpocząć się przed naprawą pięciu P2 i ponownym review Fazy 4. Po ich zamknięciu zewnętrzna P1 nadal zablokuje rzeczywiste funkcje KSeF Fazy 5 i produkcję, lecz nie musi blokować niezależnego, bezpiecznego modelu domenowego i testów opartych wyłącznie na sztucznych danych. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
