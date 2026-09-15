# Ponowne review fazy 1 — po naprawie P2

Data review: 2026-09-09  
Status: **ZABLOKOWANE — 1× P1, 0× P2**

## Zakres ponownej kontroli

Sprawdzono wyłącznie dwie naprawy P2 wskazane w `review-faza-1.md`, ich testy oraz aktualny warunek P1. Nie rozpoczęto prac z fazy 2.

## Zweryfikowane naprawy

### P2 zamknięte — audyt wznowienia kreatora właściciela

`Resume.cshtml.cs` zapisuje teraz niezależnie wynik nieudanego i udanego sprawdzenia hasła przy wznowieniu konfiguracji właściciela. Rekord zawiera wyłącznie identyfikator użytkownika, gdy jest znany, wynik, nazwę metody `setup-resume-password` i adres IP; nie zawiera hasła ani kodu TOTP.

Test `Resuming_owner_setup_audits_successful_and_failed_password_attempts` tworzy niedokończony kreator, wykonuje próbę z błędnym hasłem i próbę poprawną, a następnie sprawdza oba zdarzenia w historii audytu. Przed implementacją test był czerwony, ponieważ kolekcja zdarzeń była pusta; po implementacji przechodzi.

### P2 zamknięte — kontrola rzeczywistego limitu logowania

Test `Login_endpoints_reject_the_eleventh_request_from_the_same_client` korzysta z niezależnego środowiska testowego z takim samym limitem produkcyjnym: 10 żądań na 15 minut, bez kolejki. Potwierdza dziesięć odpowiedzi dopuszczonych i `429` dla jedenastej. Produkcyjne ustawienie w `Program.cs` pozostało niezmienione.

## P1 pozostaje otwarte — nie wykonano próby kontenerów i telefonu

W aktualnym środowisku nadal nie ma polecenia `docker` (`command -v docker` kończy się kodem 1). Nie można więc rzetelnie potwierdzić lokalnego uruchomienia Compose, rzeczywistej migracji PostgreSQL, health checków przez Caddy ani kreatora TOTP na telefonie. Kontrola YAML i testy w pamięci nie są ich zamiennikiem.

Do zamknięcia P1 potrzebne jest uruchomienie Docker Desktop, lokalny plik `.env` z testowymi sekretami zachowanymi poza Git i czatem, a potem ręczne przejście kreatora na MacBooku oraz telefonie. Ta czynność nie wymaga danych rzeczywistej firmy.

## Wykonane kontrole

- `dotnet restore Firemka.sln --locked-mode` — powodzenie;
- `dotnet test Firemka.sln --no-restore --disable-build-servers -m:1` — powodzenie: 13 testów;
- `dotnet format Firemka.sln --verify-no-changes --no-restore` — powodzenie;
- ukierunkowane testy obu napraw P2 — powodzenie: 2 testy;
- statyczny przegląd kodu audytu, testu limitu oraz konfiguracji limitera;
- ponowna kontrola dostępności Docker Desktop — brak narzędzia `docker`.

## Decyzja bramki

P2 nie pozostają. Faza 2 nadal **nie może się rozpocząć**, ponieważ P1 wymaga realnego uruchomienia lokalnych kontenerów oraz ręcznego testu TOTP na telefonie. Następna praca wykonawcza ma dotyczyć wyłącznie tej bramki P1 po uruchomieniu Docker Desktop.
