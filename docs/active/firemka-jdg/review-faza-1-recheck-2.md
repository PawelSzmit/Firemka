# Ponowne review fazy 1 — po uruchomieniu Compose i naprawie Caddy

Data review: 2026-09-09  
Status: **ZABLOKOWANE — 1× P1, 2× P2**

## Zakres ponownej kontroli

Sprawdzono techniczną naprawę Caddy, faktyczne uruchomienie lokalnego Compose, HTTPS przez reverse proxy, aktualne testy .NET oraz warunki bezpieczeństwa ciasteczek i logów. Nie rozpoczęto prac z fazy 2.

## Ustalenia

### P1 — brakuje ręcznej próby w przeglądarce i na telefonie

**Dowód.** Techniczna część środowiska działa: PostgreSQL jest `healthy`, migracja kończy się kodem `0`, WWW jest `healthy`, worker działa, a `https://localhost/health` zwraca `Healthy`. Lokalny certyfikat Caddy nie jest jednak jeszcze zaufany przez macOS — zwykłe połączenie zwraca błąd niezaufanego wystawcy. Publiczny certyfikat lokalnego Caddy wyeksportowano jedynie do `/private/tmp`; nie zainstalowano go w pęku kluczy. Nie wykonano też kreatora z kodem z rzeczywistej aplikacji TOTP na telefonie.

**Skutek.** Nie ma dowodu, że użytkownik może bez ostrzeżenia certyfikatu przejść cały kreator właściciela, dodać klucz do aplikacji TOTP i zalogować się drugim składnikiem na MacBooku.

**Wymagane działanie.** Po wyraźnej zgodzie właściciela zaufać wyłącznie certyfikatowi lokalnego testowego Caddy, otworzyć `https://localhost`, utworzyć testowe konto właściciela, dodać pokazany klucz ręcznie w aplikacji TOTP, potwierdzić kod i od razu zapisać kody odzyskiwania poza Firemką. Bez danych rzeczywistej firmy i bez wklejania żadnych sekretów do czatu.

### P2 — ciasteczko ochrony formularzy nie ma flagi `Secure`

**Dowód.** Rzeczywista odpowiedź `GET https://localhost/Setup` przez Caddy ustawia ciasteczko anty-CSRF bez atrybutu `Secure`. W [Program.cs](../../../src/Firemka.Web/Program.cs#L36-L39) nie ma konfiguracji `AddAntiforgery`, a domyślna polityka `SecurePolicy` tego ciasteczka to `None` ([Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.antiforgeryoptions.cookie?view=aspnetcore-10.0)). Plan wymaga bezpiecznych ciasteczek w fazie 1.

**Skutek.** Ciasteczko technicznie pomocnicze, lecz należące do ochrony formularzy, może zostać wysłane przy połączeniu HTTP. Nie jest to ciasteczko sesyjne, dlatego problem ma rangę P2, ale nie powinien zostać przed kolejną fazą.

**Naprawa.** W środowisku innym niż `Development` wymusić `CookieSecurePolicy.Always` dla anty-CSRF oraz dodać test odpowiedzi HTTPS/testowej, który wymaga atrybutu `Secure`. Testowe żądania muszą korzystać z HTTPS, aby nie zataić błędu w kliencie testowym.

### P2 — pusty worker zapisuje wpis informacyjny co sekundę

**Dowód.** [Worker.cs](../../../src/Firemka.Worker/Worker.cs#L5-L14) wykonuje pętlę z `LogInformation` i opóźnieniem jednej sekundy. Faktyczny log kontenera potwierdził kolejne wpisy „Worker running at” co sekundę.

**Skutek.** W nieaktywnym jeszcze workerze powstaje około 86 tysięcy bezużytecznych wpisów dziennie. Utrudnia to odczyt zdarzeń bezpieczeństwa i niepotrzebnie zużywa dysk oraz zasoby logowania.

**Naprawa.** Do czasu wprowadzenia prawdziwego harmonogramu pozostawić worker uśpiony, reagujący na zatrzymanie procesu, oraz ewentualnie zapisać pojedynczy wpis startowy. Ponownie uruchomić Compose i potwierdzić, że po kilku sekundach nie powstają kolejne wpisy.

## Potwierdzone elementy

- `deploy/Caddy.Dockerfile` kopiuje wersjonowany `Caddyfile` do obrazu, a `compose.yaml` nie ma już montowania pliku z katalogu Pulpit. Pełne `docker compose up --build --wait` przeszło na sztucznych danych.
- Caddy poprawnie przekazuje żądania do WWW: endpoint zdrowia zwrócił `Healthy`, konfiguracja przechodzi `caddy validate`, a `caddy fmt --diff` nie wykazuje nieformatowanych zmian.
- Nagłówki HSTS, CSP, X-Frame-Options, Referrer-Policy i Permissions-Policy są obecne w odpowiedzi przez Caddy. Próbne ciasteczko o sztucznej wartości zostało w logu Caddy zredagowane jako `REDACTED`; log nie ujawnił jego wartości.
- `dotnet restore Firemka.sln --locked-mode`, `dotnet test Firemka.sln --no-restore` oraz `dotnet format Firemka.sln --verify-no-changes --no-restore` przeszły. Zestaw ma 13 zielonych testów.
- Architektura nadal zachowuje prywatny backend WWW bez opublikowanego portu oraz przekazuje schemat HTTPS przez domyślne nagłówki reverse proxy Caddy ([dokumentacja Caddy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy)).

## Decyzja bramki

Faza 2 nie może się rozpocząć. Następny `dev-docs-execute` naprawia wyłącznie oba P2. Potem wymagane jest kolejne `dev-docs-review` fazy 1. P1 pozostaje jawna i wymaga później ręcznej akcji właściciela w macOS oraz na telefonie.
