# Review fazy 2 — dane, pliki, historia i trwałe zadania

Data: 2026-09-10  
Status: **WYMAGA NAPRAWY P2 PRZED FAZĄ 3**

## Znaleziska

### P2 — aktywne zadanie może zostać przejęte drugi raz po dwóch minutach

`BackgroundJobRunner` ustawia stały, dwuminutowy leasing i uruchamia wykonawcę bez przedłużania blokady (`src/Firemka.Infrastructure/Jobs/BackgroundJobRunner.cs`, wiersze 5–31). `BackgroundJobQueue` uznaje każde zadanie z wygasłym leasingiem za gotowe do ponownego przejęcia (`src/Firemka.Infrastructure/Jobs/BackgroundJobQueue.cs`, wiersze 94–105 i 170–175). To prawidłowo odzyskuje zadanie po awarii, ale podczas legalnej operacji trwającej ponad dwie minuty drugi worker może zacząć ten sam proces, mimo że pierwszy nadal pracuje. Klucz idempotencji ogranicza utworzenie dwóch rekordów kolejki, lecz sam nie chroni wszystkich przyszłych skutków OCR, eksportu lub komunikacji z usługą zewnętrzną.

**Wymagana naprawa:** dodać okresowe, atomowe przedłużanie leasingu przez osobne połączenie/kontekst bazy, zatrzymać wykonanie po utracie leasingu i przetestować długie zadanie oraz próbę przejęcia przez drugi worker.

### P2 — metadane pliku nie zapisują jego pochodzenia ani powiązanej wersji rekordu

Plan wymaga, aby każdy plik miał sumę, typ, rozmiar, pochodzenie i powiązanie z konkretną wersją rekordu (`docs/plans/2026-09-09-jdg-application-plan.md`, rozdział 6.3). `StoredFile` oraz migracja zapisują właściciela, klucz, nazwę, typ, rozmiar, SHA-256 i czas, ale nie zapisują pochodzenia ani identyfikatora/wersji powiązanego rekordu (`src/Firemka.Infrastructure/Files/StoredFile.cs`, wiersze 3–20; migracja `CoreDataFilesAndJobs`, wiersze 110–126). Bez tych pól później nie da się wiarygodnie ustalić, czy plik był ręcznie wgrany, pobrany z KSeF czy wygenerowany oraz do której wersji dokumentu należy.

**Wymagana naprawa:** wprowadzić wymagany, typowany kontekst pliku z pochodzeniem, rodzajem rekordu, jego identyfikatorem i numerem wersji; rozszerzyć migrację i test odczytu metadanych.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** pliki są poza katalogiem publicznym, mają losowe nazwy, limit, kontrolę sygnatury i właściciela; anonimowy podgląd jest blokowany. Nie znaleziono P1 ani wycieku danych.
- **Wydajność i odporność:** atomowe dodanie zadania i leasing na PostgreSQL są poprawne dla równoległych workerów, lecz brak odnawiania aktywnego leasingu daje opisane P2 dla długich zadań.
- **Architektura i typy:** granice `Domain`/`Application`/`Infrastructure` oraz porty przyszłych integracji są zachowane. Brakuje typowanego pochodzenia i powiązania wersji pliku.
- **Scenariusze i testy:** 34 testy przechodzą, w tym prawdziwy PostgreSQL, migracja w przód/cofnięcie, wyścig idempotencji, odzyskanie po awarii, prywatność pliku i niezmienność korekty. Brakuje testu aktywnego odnawiania długiego leasingu oraz pełnej proweniencji pliku.

## Dowody wykonane podczas review

- pełne rozwiązanie: 34/34 testy zielone, w tym izolowany PostgreSQL;
- kompilacja Release: 0 ostrzeżeń, 0 błędów;
- `dotnet format --verify-no-changes`: bez zmian;
- EF Core: brak zmian modelu bez migracji;
- skan NuGet: brak zgłoszonych znanych podatności;
- Compose i lokalne HTTPS: zdrowe, konto właściciela i TOTP zachowane po migracji;
- zapisane próbki XML: cztery walidacje XSD zielone po pobraniu lokalnej kopii pliku odłożonego przez macOS do chmury.

## Decyzja bramki

Wynik: **0× P1, 2× P2, 0× P3**. Zgodnie z regułą tego zadania Faza 3 pozostaje zablokowana do naprawienia obu P2 i ponownego review Fazy 2. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
