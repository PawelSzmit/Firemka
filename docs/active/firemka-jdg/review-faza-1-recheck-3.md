# Ponowne review fazy 1 — po naprawie ciasteczka i workera

Data review: 2026-09-09  
Status: **ZABLOKOWANE — 1× P1, 0× P2**

## Ustalenia

### P1 — ręczny test lokalnego certyfikatu i TOTP na telefonie nadal nie został wykonany

**Dowód.** Pełne środowisko Compose działa na sztucznych danych, lecz macOS nie ufa jeszcze lokalnemu wystawcy Caddy. Zwykłe połączenie z `https://localhost` kończy się komunikatem o niezaufanym wystawcy; certyfikat znajduje się tylko w `/private/tmp/firemka-caddy-local-root.crt` i nie został dodany do pęku kluczy. Nie przeprowadzono również kreatora właściciela z rzeczywistą aplikacją TOTP na telefonie.

**Skutek.** Nie można jeszcze potwierdzić pełnej ścieżki użytkownika: zaufany lokalny HTTPS → utworzenie konta → ręczne dodanie klucza TOTP → kod → zapis kodów odzyskiwania → wejście na stronę prywatną.

**Wymagane działanie.** Właściciel musi wyraźnie zatwierdzić dodanie zweryfikowanego, lokalnego certyfikatu Caddy do zaufanych certyfikatów macOS, a następnie przejść kreator z telefonem. Nie używamy danych firmy, sekretów KSeF ani haseł w rozmowie.

### Brak P2

Obie poprawki z poprzedniego review są potwierdzone. Nie znaleziono nowego P2 w zakresie fazy 1.

## Zweryfikowane naprawy

### P2 zamknięte — ciasteczko anty-CSRF ma flagę `Secure`

Test `Antiforgery_cookie_is_secure_outside_development` został najpierw uruchomiony czerwono: otrzymane ciasteczko nie zawierało `secure`. Po dodaniu polityki `CookieSecurePolicy.Always` poza środowiskiem `Development` w [Program.cs](../../../src/Firemka.Web/Program.cs#L36-L46) test jest zielony. Klienci testowi korzystają z HTTPS, więc sprawdzają rzeczywistą ścieżkę przeglądarki, a nie sztuczne HTTP.

Potwierdzenie integracyjne: odpowiedź `GET https://localhost/Setup` przez Caddy ustawia ciasteczko z atrybutami `secure; samesite=strict; httponly`.

### P2 zamknięte — worker nie zapisuje pustego komunikatu co sekundę

[Worker.cs](../../../src/Firemka.Worker/Worker.cs#L5-L19) zapisuje pojedynczy komunikat startowy i czeka na anulowanie procesu. Świeży kontener po pełnym `docker compose up --build --wait` działał ponad sześć sekund, a jego log zawierał wyłącznie jeden wpis `Worker started; no jobs are enabled in phase 1.` — bez poprzedniej pętli sekundowych wpisów.

## Potwierdzone elementy fazy 1

- pełne Compose na danych `firemka_test`: PostgreSQL `healthy`, migracja `Exited (0)`, WWW `healthy`, worker i Caddy uruchomione;
- Caddy działa z obrazem zawierającym wersjonowany `Caddyfile`, bez montowania pliku z Pulpitu; `caddy validate` i `docker compose config --quiet` przechodzą;
- `https://localhost/health` zwraca `Healthy`; odpowiedzi mają HSTS, CSP i pozostałe nagłówki bezpieczeństwa;
- Caddy redaguje próbne ciasteczko w logu jako `REDACTED`;
- `dotnet restore Firemka.sln --locked-mode`, `dotnet test Firemka.sln --no-restore` oraz `dotnet format Firemka.sln --verify-no-changes --no-restore` przechodzą. Zestaw ma 14 zielonych testów.

## Ważne ograniczenie przed produkcją

Lokalny zestaw zawiera wyłącznie sztuczną bazę i nie ma danych firmy ani produkcyjnego sekretu chroniącego klucze Data Protection. Nie jest to zgoda na wdrożenie. Sposób dostarczenia takiego zewnętrznego sekretu oraz kontrola VPS pozostają osobną bramką przed pilotażem i użyciem rzeczywistych danych.

## Decyzja bramki

P2 są zamknięte, ale Faza 2 nadal **nie może się rozpocząć**, ponieważ P1 wymaga ręcznej próby z zaufanym lokalnym HTTPS i aplikacją TOTP na telefonie. Następny krok wykonawczy jest ograniczony do tej ręcznej bramki, po której trzeba ponownie wykonać `dev-docs-review` fazy 1.
