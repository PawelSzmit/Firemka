# Checklista pilotażu Firemki

Status dokumentu: **NIEPODPISANA — brak zgody na produkcję**

Każdy punkt wymaga daty, osoby wykonującej, krótkiego wyniku i ścieżki do dowodu bez sekretów. Pole pozostaje puste, jeśli próba nie została naprawdę wykonana.

## A. Wersja i środowisko

- [ ] **A1.** Zapisano identyfikator kodu, obrazu WWW, workera, Caddy i PostgreSQL oraz najnowszą migrację.
- [x] **A2.** Obrazy są przypięte skrótami, pełna lokalna bramka jest zielona, skan NuGet nie zgłasza znanej podatności, a skan obrazów nie zgłasza podatności krytycznej ani wysokiej. — **2026-09-15, Codex, ZALICZONE**; dowód: [lokalna bramka Fazy 12](phase12-local-gate.md).
- [ ] **A3.** VPS ma wspierany system, aktualizacje bezpieczeństwa, aktywną zaporę i publiczne wyłącznie zatwierdzone porty.
- [ ] **A4.** Kontenery mają ograniczone uprawnienia, zasoby i rotację logów; baza nie ma portu publicznego.
- [ ] **A5.** HTTPS, certyfikat, HSTS, nagłówki, `/health`, miejsce na dysku i monitor zostały sprawdzone; timer działa, a nieudany wynik dociera uzgodnionym kanałem do właściciela.

## B. Dane referencyjne i pełny miesiąc

- [ ] **B1.** Niezależny księgowy/doradca potwierdził profil podatkowy, wersje przepisów i miesiąc referencyjny.
- [ ] **B2.** Wprowadzono wyłącznie uzgodniony, zanonimizowany zestaw dokumentów miesiąca referencyjnego.
- [ ] **B3.** Porównano przychód, KPiR, VAT, PIT, ZUS i terminy pozycja po pozycji; każda różnica ma wyjaśnienie.
- [x] **B4.** Znany koszt zaksięgował się bez ponownej decyzji, a zmiana danych podatkowo istotnych zatrzymała automatyzację. — **2026-09-15, Codex, ZALICZONE na danych syntetycznych**; dowód: [lokalna bramka Fazy 12](phase12-local-gate.md), testy `CostAccountingWorkflowTests` i `CostRuleTests`.
- [x] **B5.** Domowe ładowanie 10 000 Wh przy 0,91 zł dało 10 kWh i 9,10 zł, bez automatycznego uznania podstawy podatkowej. — **2026-09-15, Codex, ZALICZONE na danych syntetycznych**; dowód: [lokalna bramka Fazy 12](phase12-local-gate.md), testy `ChargingDomainTests` i `HomeChargingWorkflowTests`.
- [x] **B6.** Zamknięcie miesiąca zablokowały braki, a po ich rozwiązaniu utworzyło właściwe, niezmienne wersje. — **2026-09-15, Codex, ZALICZONE na danych syntetycznych**; dowód: [lokalna bramka Fazy 12](phase12-local-gate.md), testy `MonthClosingWorkflowTests`.

## C. Telefon i Mac

- [ ] **C1.** Na telefonie 390 px wykonano logowanie hasłem i TOTP, przejście „Mój miesiąc”, podgląd dokumentu i zatwierdzenie bez ucięć.
- [ ] **C2.** Na Macu wykonano logowanie, pobranie prywatnego pliku i wylogowanie; nowe urządzenie ponownie wymagało 2FA.
- [ ] **C3.** Unieważnienie wszystkich zaufanych urządzeń zablokowało wcześniejsze zaufanie.
- [ ] **C4.** Kod odzyskiwania zadziałał dokładnie raz; pozostałe kody są przechowywane poza aplikacją.
- [ ] **C5.** Przećwiczono utratę telefonu bez wyłączania 2FA.

## D. Integracje i pliki urzędowe

- [ ] **D1.** Testowe KSeF pobrało dokument bez duplikatu i zachowało różnice źródeł.
- [ ] **D2.** Testowa faktura FA(3) przeszła: wysyłka, stan oczekujący/niepewny, przyjęcie, numer KSeF i UPO bez drugiego wysłania.
- [ ] **D3.** Odrzucenie testowej faktury zachowało poprzednią wersję, a poprawa utworzyła nową.
- [ ] **D4.** JPK_V7M i roczny JPK_PKPIR przeszły import w aktualnym narzędziu urzędowym; wynik i wersję narzędzia zapisano.
- [ ] **D5.** ZUS DRA przeszło próbę w Płatniku/ePłatniku; wynik i wersję narzędzia zapisano.
- [ ] **D6.** Testowy SMTP przez TLS dostarczył pojedyncze wiadomości bez kwot, załączników i duplikatów.
- [ ] **D7.** KSeF produkcyjny, SMTP produkcyjny i automatyczne wystawianie nadal są wyłączone po próbie.

## E. Kopia i odtworzenie

- [ ] **E1.** Wskazano prawdziwy folder Maca, token jest w Pęku kluczy, a hasło odzyskiwania ma drugą kopię poza Makiem.
- [ ] **E2.** Ręczna i zaległa kopia powstały jako pojedyncze pliki; rotacja pozostawiła pięć poprawnych kopii dziennych.
- [ ] **E3.** Archiwum roczne pozostało poza rotacją.
- [ ] **E4.** Kopię przeniesiono na drugi komputer/VPS i odtworzono do czystej instancji właściwym migratorem.
- [ ] **E5.** Po odtworzeniu zgadzają się wszystkie liczniki i skróty, działa `/health`, 2FA, dokumenty i pobieranie plików.
- [ ] **E6.** Złe hasło i uszkodzony plik zostały odrzucone bez zmiany istniejących danych.

## F. Awarie, aktualizacja i decyzja

- [ ] **F1.** Przećwiczono awarię KSeF, SMTP, workera, pełnego dysku i kończącego się certyfikatu zgodnie z runbookiem.
- [ ] **F2.** Próbna aktualizacja zachowała dane, historię, 2FA i kopie.
- [ ] **F3.** Próbny powrót użył zgodnego obrazu albo czystego odtworzenia; nie cofano migracji w ciemno.
- [ ] **F4.** Pierwszy rzeczywisty miesiąc ma pozostać ręczny: właściciel zatwierdza fakturę, księgowania, wysyłki i pełne płatności.
- [ ] **F5.** Nie pozostała żadna nierozstrzygnięta reguła używana automatycznie.
- [ ] **F6.** Właściciel podpisał zgodę na ograniczony pilotaż i zapisał datę ponownej oceny.

## Decyzja końcowa

- Wynik: `ZABLOKOWANY / PILOTAŻ RĘCZNY / POWRÓT`
- Data i czas:
- Wersja:
- Osoba wykonująca:
- Osoba potwierdzająca:
- Dowody:
- Otwarte wyjątki i termin ich zamknięcia:
