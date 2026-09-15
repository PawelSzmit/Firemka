# Ponowne review fazy 4 — po naprawie P2 i P3

Data: 2026-09-11  
Status: **GOTOWE DO BEZPIECZNEGO ZAKRESU TECHNICZNEGO FAZY 5; ZEWNĘTRZNE KSeF NADAL ZABLOKOWANE**

## Znaleziska

### P1 zewnętrzne — rzeczywista bramka KSeF i dokumentów nadal otwarta

Nie przekazano testowego dostępu KSeF, zanonimizowanego rzeczywistego PDF ani zdjęcia wykonanego telefonem. Nie można więc potwierdzić uwierzytelnienia, zgodności bieżącego adaptera z pełnym zachowaniem testowego serwera ani jakości OCR na dokumentach właściciela. To jest ta sama jawnie odroczona bramka z pierwszego review, a nie nowy błąd kodu.

**Decyzja:** bramka nadal blokuje rzeczywistą integrację wychodzącą KSeF w Fazie 5, wszystkie dane firmy i każde działanie produkcyjne. Zgodnie z zasadą aktywnego planu nie blokuje niezależnego modelu domenowego, generowania i walidacji sztucznego FA(3), idempotencji ani interfejsu pozostającego domyślnie wyłączonym.

Nie znaleziono nowych P2 ani P3.

## Zamknięcie wcześniejszych ustaleń

### P2 — sprzątanie pliku i transakcji: zamknięte

`IncomingDocumentService` oraz `KsefIncomingSyncService` wycofują transakcję, usuwają fizyczną kopię i czyszczą śledzony graf po błędzie całej operacji. Test ręcznego uploadu sprawdza brak dokumentu, metadanych, zadania i pliku; test KSeF sprawdza brak XML po awarii audytu. Osobna próba PostgreSQL celowo psuje kolejkę po fizycznym zapisie i potwierdza pełne wycofanie.

### P2 — anulowanie OCR: zamknięte

`ProcessDocumentTextExtractionEngine` zabija całe drzewo procesu zarówno po 45 sekundach, jak i po anulowaniu workera, a następnie czeka na jego koniec. `DocumentExtractionJobHandler` odtwarza stan dokumentu i kończy próbę jako `Interrupted`, bez tworzenia trwałego błędu dokumentu. Test uruchamia prawdziwy blokujący proces, anuluje go, sprawdza brak działającego PID oraz osobno potwierdza stan próby i możliwość ponowienia.

### P2 — pewność pola: zamknięte

Dla ręcznego PDF/zdjęcia brak wpisu konkretnego pola daje `brak podpowiedzi`, niezależnie od oceny całego dokumentu. Dane pochodzące z KSeF są rozróżnione przez typowane pochodzenie. Test WWW potwierdza jedną wartość 82% dla odczytanego pola i pięć jawnych braków przy ogólnej ocenie 91%.

### P2 — konflikt źródła w stanie końcowym: zamknięte

Kontrakt listy przekazuje flagę i czas konfliktu. Lista oraz szczegóły pokazują niezależne czerwone ostrzeżenie nawet dla zaksięgowanego lub odrzuconego dokumentu. Test domeny obejmuje oba stany końcowe, a test WWW sprawdza widoczność na liście i w szczegółach.

### P2 — nieograniczona lista: zamknięte

Zapytanie ma stronę, rozmiar ograniczony do 50 i opcjonalny filtr statusu. Domyślny ekran pobiera po 25 rekordów, sortuje stabilnie po dacie i identyfikatorze oraz pokazuje nawigację. Test 25 dokumentów składa trzy strony bez duplikatu ani pominięcia i sprawdza filtr.

### P3 — pozornie zielony test OCR: zamknięte

PDF i PNG są trwałymi próbkami w `tests/Fixtures/phase4`. Test wyszukuje wymagane programy w `PATH`, a ich brak kończy test błędem. Na tym Macu rzeczywiście uruchomiły się `pdftotext` oraz Tesseract i odczytały numer oraz kwotę z obu plików.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** prywatność, właściciel pliku, limity, kontrola sygnatur, bezpieczny XML, sprzątanie po awarii i brak sekretów w repozytorium są pokryte. Nie znaleziono P2.
- **Wydajność i odporność:** stronicowanie ogranicza pamięć, transakcje i SHA-256 zabezpieczają import, proces OCR jest domykany, a kolejka zachowuje wznowienie. Nie znaleziono P2.
- **Architektura i typy:** granice domena/aplikacja/infrastruktura pozostają zachowane; pochodzenie, status próby, konflikt i filtr są typowane. Bez testowego KSeF nie zatwierdzono jeszcze decyzji oficjalny klient kontra bezpośredni adapter OpenAPI.
- **Scenariusze i testy:** wcześniejsze braki mają teraz testy błędu i granic. Zewnętrzna próba pozostaje jedynym nieudowodnionym zakresem.

## Dowody wykonane podczas ponownego review

- pełny zestaw bez zewnętrznej bazy: 85/85 testów zielonych;
- osobny PostgreSQL: 1/1 test zielony, łącznie z kontrolowaną awarią po fizycznym zapisie pliku;
- prawdziwy proces OCR: po anulowaniu PID nie działa, a po teście nie pozostał proces próbny;
- prawdziwe lokalne narzędzia: PDF i PNG odczytane przez `pdftotext`/Tesseract;
- kompilacja Release w obrazach Docker: bez ostrzeżeń i błędów;
- EF Core: brak zmian modelu bez migracji;
- formatowanie, cztery kontrakty XML i składnia Compose: czyste;
- skan zależności: brak zgłoszonych znanych podatności NuGet;
- lokalne Compose: PostgreSQL i WWW zdrowe, migracja zakończona kodem 0, worker działa, `https://localhost/health` zwraca `Healthy`;
- istniejące dane techniczne zachowane: 1 konto, TOTP włączone, 0 firm i 0 dokumentów;
- po przebudowie wygasła wcześniejsza sesja Safari i ekran poprawnie skierował do logowania; nie używano autouzupełniania ani danych dostępowych.

## Decyzja bramki

Wynik: **1× wcześniej odroczone P1 zewnętrzne, 0× P2, 0× P3**. Techniczna bramka Fazy 4 jest zielona. Można rozpocząć wyłącznie niezależny i bezpieczny zakres Fazy 5 na sztucznych danych, z domyślnie wyłączoną wysyłką. Rzeczywisty test KSeF i produkcja pozostają zablokowane do zamknięcia P1. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
