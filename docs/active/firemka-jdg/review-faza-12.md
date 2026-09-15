# Review Fazy 12 — utwardzenie i gotowość pilotażowa

Data: 2026-09-15  
Decyzja: **zakres techniczny gotowy; cała Faza 12 i pilotaż zablokowane przez zewnętrzne P1**  
Wynik kodu: **0× P1, 0× P2, 0× P3**  
Otwarte bramki zewnętrzne: **5× P1**

## Znaleziska

### P1 zewnętrzne — brak odbioru prawdziwego VPS i monitoringu

Audyt i monitor są przygotowane, ale nie zostały uruchomione na docelowym serwerze z publiczną domeną, rzeczywistym certyfikatem, zaporą i uzgodnionym kanałem alarmowym. Bez tego nie ma dowodu, że publicznie dostępne są wyłącznie zatwierdzone porty, certyfikat odnawia się, a alarm rzeczywiście dociera do właściciela.

### P1 zewnętrzne — brak niezależnego miesiąca referencyjnego

Reguły, blokady i pełny syntetyczny miesiąc przechodzą automatycznie, lecz niezależny księgowy lub doradca nie potwierdził jeszcze profilu podatkowego oraz porównania przychodu, KPiR, VAT, PIT, ZUS i terminów na uzgodnionych danych. Automatyczne księgowanie prawdziwych dokumentów pozostaje zablokowane.

### P1 zewnętrzne — brak prób usług i narzędzi urzędowych

Nie wykonano rzeczywistej próby testowego KSeF, importu JPK/ZUS w aktualnych narzędziach urzędowych ani dostarczenia wiadomości przez testowe SMTP z TLS. Produkcyjny KSeF, e-mail oraz automatyczne wystawianie muszą pozostać wyłączone.

### P1 zewnętrzne — brak pełnych prób właściciela na telefonie i Macu

TOTP właściciela było wcześniej sprawdzone lokalnie, a przepływ automatyczny używa nowej sesji logowania. Nadal nie przeprowadzono całej checklisty na telefonie 390 px i Macu: zaufania urządzenia, jednokrotnego kodu odzyskiwania, utraty telefonu oraz unieważnienia wszystkich urządzeń.

### P1 zewnętrzne — brak prawdziwej kopii i odtworzenia na drugim urządzeniu

Szyfrowanie, rotacja i pełne odtworzenie danych syntetycznych są zielone. Właściciel nie wskazał jednak docelowego folderu, nie utworzył osobnej kopii hasła offline, nie zainstalował klienta Mac i nie odtworzył pakietu na drugim czystym komputerze lub VPS.

## Ocena czterech obszarów

- **Bezpieczeństwo i dane:** wszystkie obrazy bazowe są przypięte skrótami; cztery obrazy mają 0 podatności krytycznych/wysokich w Docker Scout; skan NuGet jest czysty; integracje ryzykowne są wyłączone domyślnie; kontenery działają bez administratora i z ograniczeniami (`deploy/Caddy.Dockerfile`, `deploy/Postgres.Dockerfile`, `deploy/compose.production.yaml`).
- **Wydajność i odporność:** usługi mają limity CPU, pamięci, procesów i rotację logów. Kopie oraz prywatne pliki są przetwarzane strumieniowo, a test rzeczywistego zestawu potwierdził poprawny start PostgreSQL, migracji, WWW, workera i HTTPS.
- **Architektura i utrzymanie:** migracja jest osobnym krokiem poprzedzającym WWW; obrazy wydania mają osobne zmienne i nie dopuszczają `local/latest` w audycie; procedura powrotu rozróżnia zgodny poprzedni obraz od odtworzenia czystej instancji. R1–R46 mają jawne dowody albo otwarte bramki.
- **Scenariusze:** pełna bramka ma 311 zielonych testów bez pominięć i wszystkie 12 prób PostgreSQL. Obejmuje lokalny kod QR i tekstowy plan awaryjny TOTP, ponowne logowanie, pełny miesiąc właściciela, niepowodzenia integracji, migracje, równoległość, kopię i odtworzenie. Dowód zapisano w `docs/acceptance/phase12-local-gate.md`.

## Decyzja bramki

Nie znaleziono problemu P1/P2/P3 w kodzie ani lokalnej konfiguracji, więc techniczny zakres planu jest gotowy. Nie wolno jednak uznać całej Fazy 12, pilotażu ani produkcji za zakończone. Zgodnie z projektem fazy 12 wymagane są prawdziwe urządzenia, usługi, dane referencyjne, VPS i podpis właściciela. Następnym krokiem jest wykonanie `docs/acceptance/phase12-pilot-checklist.md` bez zaznaczania pozycji, dla których nie ma dowodu.

Nie wykonano commita, pushu ani wdrożenia.
