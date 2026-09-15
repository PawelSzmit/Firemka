# Review fazy 11 — kopie i odtworzenie

Data: 2026-09-15  
Status: **BLOKADA FAZY 12 — wymagane poprawki P2**

## Wynik

Review potwierdziło działający, szyfrowany format, strumieniowy eksport, rotację, klienta Mac, token tylko do kopii, alarm 36 godzin i pełne odtworzenie syntetycznej bazy. Znaleziono jednak trzy problemy P2 w ścieżce odtworzenia i raportowania sukcesu. Faza 12 nie może się rozpocząć przed ich naprawą i ponownym review.

## Znaleziska

### P2 — odtworzenie może zakończyć się bez migracji bieżącej aplikacji

`deploy/scripts/restore/restore-clean-instance.sh:75-84` porównuje liczniki przed migracją, a samą migrację uruchamia tylko wtedy, gdy opcjonalnie ustawiono `FIREMKA_WEB_DLL`. Bez tej zmiennej skrypt wypisuje komunikat o zakończeniu mimo braku zgodności przywróconej bazy z bieżącą wersją aplikacji. To nie spełnia projektu Fazy 11 i może pozostawić poprawnie odtworzone dane, których aktualna aplikacja nie potrafi uruchomić.

Wymagana poprawka: migrator ma być obowiązkowym, sprawdzonym wejściem; musi wykonać się przed końcowym porównaniem i komunikatem sukcesu. Instrukcja ma opisać uruchomienie skryptu we właściwym obrazie oraz obowiązkowy health check.

### P2 — sprzątanie po błędzie może usunąć cudzy, równolegle dodany plik

`src/Firemka.BackupClient/BackupRestoreExtractor.cs:17-56` sprawdza pusty folder tylko na początku, lecz po dowolnym błędzie usuwa rekurencyjnie całą jego bieżącą zawartość. Jeśli w trakcie długiego odszyfrowywania albo kopiowania inny proces lub użytkownik doda tam plik, sprzątanie usunie także ten plik. To narusza zasadę braku cichego nadpisania i rozszerza skutki awarii poza dane utworzone przez klienta.

Wymagana poprawka: klient śledzi dokładnie utworzone przez siebie pliki i foldery, usuwa wyłącznie je, nie używa rekurencyjnego czyszczenia całego celu i pozostawia obce wpisy bez zmian.

### P2 — jedno zgłoszenie może błędnie zakończyć dwa rodzaje zleceń

`src/Firemka.Infrastructure/Backups/BackupService.cs:168-188` przyjmuje jednocześnie identyfikator kopii ręcznej i archiwum rocznego oraz nie wiąże nazwy pliku z rodzajem i rokiem zlecenia. Błędny klient może więc oznaczyć archiwum roczne jako wykonane zwykłym plikiem dziennym albo zakończyć dwa zlecenia jednym raportem.

Wymagana poprawka: raport może dotyczyć najwyżej jednego rodzaju zlecenia; zwykły plik musi mieć nazwę dzienną, a archiwum nazwę z właściwym rokiem. Wersja formatu musi być obsługiwana. Testy mają odrzucać wszystkie błędne kombinacje bez zmiany stanu.

## Potwierdzone elementy

- AES-256-GCM, PBKDF2-SHA256, losowa sól, osobne nonce i uwierzytelniony koniec wykrywają złe hasło, zmianę i ucięcie.
- Pakiet obejmuje logiczną migawkę PostgreSQL, prywatne pliki, klucze i manifest. Prawdziwy strumień HTTP z obrazu PostgreSQL 18 zakończył się poprawnie po naprawie asynchronicznego ZIP.
- Zapis `.partial` → weryfikacja → atomowa nazwa końcowa działa, podobnie rotacja pięciu i osobne archiwa roczne.
- Nieudane zgłoszenie sukcesu jest ponawiane z lokalnego, ponownie zweryfikowanego dziennika bez ponownego pobrania.
- Panel, dashboard i powiadomienie respektują konfigurację oraz próg 36 godzin.
- Pełny zestaw ma 286 zielonych testów; dwa testy Fazy 11 na PostgreSQL przeszły osobno. Release, format, EF, XSD, Compose, obraz, plist, skrypty i skan zależności są poza powyższymi znaleziskami czyste.

## Granice zewnętrzne

Prawdziwy folder Maca, hasło odzyskiwania przechowywane offline, instalacja `launchd` oraz odtworzenie na drugim urządzeniu/VPS pozostają wcześniej odroczoną bramką P1 przed produkcją. Nie blokują naprawy technicznej P2, lecz bez nich nie wolno uruchomić pilotażu na danych firmy.

## Decyzja

Wynik: **0× nowe P1, 3× P2, 0× P3** w zaimplementowanym zakresie. Następny krok to `dev-docs-execute` ograniczony do trzech P2, a potem ponowne `dev-docs-review` Fazy 11. Nie wykonano commita, pushu ani wdrożenia.
