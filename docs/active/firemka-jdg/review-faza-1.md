# Review fazy 1 — szkielet, środowisko i bezpieczne logowanie

Data review: 2026-09-09  
Status: **ZABLOKOWANE — 1× P1, 2× P2**

## Ustalenia

### P1 — nie wykonano ręcznej próby kontenerów, PostgreSQL i telefonu

**Dowód.** `compose.yaml` przechodzi wyłącznie kontrolę składni YAML. W środowisku wykonawczym nie ma polecenia `docker`, dlatego nie można było uruchomić zestawu z PostgreSQL, migratorem, WWW, workerem i Caddy. Nie dało się też sprawdzić realnego TLS/proxy, migracji na PostgreSQL, health checków ani jednorazowego kreatora na MacBooku i telefonie. To pozostaje wymagane przez jednostkę 1 w [planie źródłowym](../../plans/2026-09-09-jdg-application-plan.md#L253-L273).

**Skutek.** Automatyczne testy potwierdzają logikę w izolowanej bazie testowej, a wygenerowany skrypt migracji potwierdza składnię SQL, ale nie dowodzą działania pełnego lokalnego środowiska.

**Wymagane działanie.** Uruchomić Docker Desktop na MacBooku, uzupełnić lokalny `.env` prawdziwą testową domeną i hasłem, a następnie wykonać `docker compose up --build`. Trzeba przejść kreator na telefonie z aplikacją TOTP, sprawdzić health checki oraz unieważnienie zaufanego urządzenia. Nie przekazywać żadnych sekretów w czacie ani do Git.

### P2 — wznowienie niedokończonego kreatora nie trafia do audytu logowania

**Dowód.** [`Resume.cshtml.cs`](../../../src/Firemka.Web/Pages/Setup/Resume.cshtml.cs#L27-L43) sprawdza e-mail i hasło przez `OwnerBootstrapService`, po czym zapisuje bilet kreatora. Sam serwis sprawdza hasło w [`OwnerBootstrapService.cs`](../../../src/Firemka.Infrastructure/Identity/OwnerBootstrapService.cs#L82-L99), ale żadna ścieżka sukcesu ani błędu nie wywołuje `IAuthenticationAuditService`.

**Skutek.** Zdarzenia uwierzytelnienia w specjalnym, ale realnym ekranie logowania właściciela znikają z historii bezpieczeństwa. To nie spełnia w pełni wymogu audytu logowania z fazy 1.

**Naprawa.** Dodać audyt sukcesu i błędu wznowienia kreatora bez zapisywania hasła, uzupełnić test integracyjny oraz ponownie uruchomić zestaw testów.

### P2 — brak automatycznej próby zachowania limitu logowania

**Dowód.** Limit 10 żądań na 15 minut jest skonfigurowany w [`Program.cs`](../../../src/Firemka.Web/Program.cs#L46-L63), jednak obecne testy bezpieczeństwa sprawdzają 2FA, odzyskiwanie, zaufane urządzenia, CSRF i prywatne strony, a nie odpowiedź `429` po przekroczeniu limitu.

**Skutek.** Przypadkowa zmiana polityki logowania może przejść przez testy, mimo że limit jest wymaganiem tej fazy.

**Naprawa.** Dodać odizolowany test integracyjny, który potwierdza 10 dozwolonych żądań i odrzucenie kolejnego kodem `429`, bez osłabiania limitu produkcyjnego.

## Potwierdzone elementy

- Szkielet ma wymagane moduły i projekty testowe; `Domain` nie zależy od WWW ani bazy, a `Application` nie zależy od `Infrastructure`.
- Wersje paczek są centralne, pliki blokady istnieją dla wszystkich projektów, a `dotnet restore Firemka.sln --locked-mode` oraz `dotnet test Firemka.sln --no-restore` przeszły: 11 testów.
- Brak publicznej rejestracji, logowanie samym hasłem nie kończy sesji, TOTP działa, kod odzyskiwania działa tylko raz, a zaufane urządzenie można wygasić lub unieważnić — potwierdzają to testy integracyjne.
- Formularze mają ochronę anty-CSRF, strony bez sesji są chronione, tokeny zaufanych urządzeń są przechowywane jako skróty, a skan zależności nie wykazał znanych podatności.
- Migracja PostgreSQL i idempotentny skrypt migracji zostały wygenerowane bez użycia danych firmy. W repozytorium nie znaleziono rzeczywistych kluczy ani tokenów; są tylko zmienne środowiskowe i bezpieczne placeholdery.

## Zakres kontroli

- zgodność z jednostką 1 planu źródłowego;
- bezpieczeństwo, skalowanie i granice modułów;
- scenariusze 2FA, odzyskiwania, zaufanych urządzeń, CSRF, prywatności stron i migracji;
- `dotnet format --verify-no-changes`, przywrócenie w trybie blokady, wszystkie testy oraz skan podatności;
- składnia `compose.yaml` i statyczna inspekcja Dockerfile/Caddyfile.

## Decyzja bramki

Nie wolno rozpocząć fazy 2. Następny `dev-docs-execute` naprawia oba P2. Po ich naprawie należy ponownie wykonać `dev-docs-review` fazy 1. P1 wymaga później bezpośredniej, ręcznej próby kontenerów i telefonu po uruchomieniu Docker Desktop.
