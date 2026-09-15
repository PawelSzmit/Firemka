# ADR 0003: format kopii i bezpieczne odtworzenie

Status: zaakceptowane dla pierwszej wersji technicznej  
Data: 2026-09-14

## Decyzja

Serwer tworzy strumieniowy ZIP zawierający spójny logiczny zrzut PostgreSQL, prywatne pliki, klucze ochrony danych i manifest SHA-256. Klient macOS szyfruje ten strumień lokalnie do wersjonowanego pliku `.fmbak` porcjami AES-256-GCM. Klucz 256-bitowy powstaje z hasła odzyskiwania przez PBKDF2-SHA256 z 600 000 iteracji i losową solą.

Każda porcja ma osobny nonce i znacznik autentyczności. Osobno uwierzytelniony znacznik końca uniemożliwia ciche skrócenie pliku o całe końcowe porcje. Nagłówek, numer porcji i długość są danymi uwierzytelnianymi. Hasło odzyskiwania ani token pobrania nie należą do kopii i są odczytywane z Pęku kluczy macOS.

Nowy plik otrzymuje nazwę końcową dopiero po odszyfrowaniu do pliku tymczasowego oraz sprawdzeniu obecności, rozmiaru i SHA-256 każdego wpisu z manifestu. Dopiero potem wolno usunąć najstarszą z więcej niż pięciu kopii dziennych. Archiwa roczne są przechowywane osobno i nie podlegają rotacji.

Odtworzenie jest dozwolone wyłącznie do pustej bazy i pustych folderów. Najpierw klient odszyfrowuje i sprawdza kopię, następnie operator przywraca zrzut, pliki i klucze, uruchamia migracje do przodu oraz porównuje dokładne liczby rekordów z manifestem. Przełączenie ruchu następuje dopiero po health checku.

## Skutki

- Kopia jest poufna także po skopiowaniu z Maca, a zmiana lub ucięcie danych jest wykrywane.
- Utrata jedynego hasła odzyskiwania oznacza nieodwracalną utratę dostępu; właściciel musi przechować drugą kopię hasła poza Makiem.
- Token serwerowy można unieważnić bez zmiany hasła istniejących archiwów.
- Pełna próba na drugim urządzeniu i prawdziwym VPS pozostaje obowiązkową bramką produkcyjną.
