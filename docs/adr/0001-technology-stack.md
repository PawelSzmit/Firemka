# ADR 0001 — stos technologiczny Firemki

Data: 2026-09-09  
Status: zaakceptowany do szkieletu; konkretne wersje zależności zostaną przypięte w fazie 1.

## Decyzja

Firemka będzie modułowym monolitem w C#/.NET 10:

- ASP.NET Core Razor Pages jako aplikacja WWW;
- ASP.NET Core Identity dla konta właściciela, TOTP, kodów odzyskiwania i zaufanych urządzeń;
- PostgreSQL 18 jako relacyjna baza;
- Entity Framework Core dla migracji i zapisu;
- proces `Worker` z tej samej solucji dla zadań trwałych;
- osobny `BackupClient` dla macOS;
- Docker Compose jako docelowy sposób uruchomienia na VPS.

## Uzasadnienie

Oficjalne wsparcie KSeF udostępnia bibliotekę .NET i aplikację demonstracyjną. Jeden stos redukuje liczbę osobnych procesów i miejsc, w których mogą zaginąć dane księgowe. Razor Pages wystarcza dla prywatnej aplikacji formularzowej i responsywnych tabel bez budowy osobnego SPA.

## Konsekwencje

- Domena i obliczenia są niezależne od ASP.NET, EF, KSeF i OCR.
- Worker i WWW dzielą modele oraz konfigurację, lecz uruchamiają się jako osobne procesy.
- Brak Redisa w pierwszej wersji oznacza, że zadania muszą być trwałe, idempotentne i blokowane w PostgreSQL.
- Konkretny OCR, biblioteka PDF i format szyfrowania kopii zostaną wybrane test-first w odpowiednich fazach.

## Alternatywy odrzucone na teraz

- osobny frontend SPA + API + kolejka + worker: zbyt wiele usług dla jednej prywatnej firmy;
- Python jako główny stos: brak oficjalnego klienta KSeF o takim poziomie wsparcia jak .NET;
- natywna aplikacja mobilna: poza zakresem, strona responsywna wystarcza.

## Źródła

- [KSeF — wsparcie dla integratorów](https://ksef.podatki.gov.pl/ksef-na-okres-obligatoryjny/wsparcie-dla-integratorow/)
- [Oficjalny klient KSeF dla .NET](https://github.com/CIRFMF/ksef-client-csharp)
- [Plan techniczny](../plans/2026-09-09-jdg-application-plan.md)
