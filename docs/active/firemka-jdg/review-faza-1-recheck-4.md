# Ponowne review fazy 1 — po ręcznym TOTP i naprawie limitera

Data review: 2026-09-10  
Status: **GOTOWE DO KONTYNUACJI — 0× P1, 0× P2, 1× P3**

## Ustalenia

### P3 — kreator TOTP nie pokazuje graficznego kodu QR

**Dowód.** Ekran [TwoFactor.cshtml](../../../src/Firemka.Web/Pages/Setup/TwoFactor.cshtml#L7-L18) pokazuje klucz tekstowy oraz adres `otpauth://`, ale nie renderuje obrazu QR. Instrukcja wprost prowadzi użytkownika przez ręczne dodanie konta.

**Skutek.** Konfiguracja wymaga kilku dodatkowych kroków w telefonie i może zaskoczyć osobę oczekującą skanowania QR. Nie jest to błąd poprawności ani bezpieczeństwa: właściciel dodał konto ręcznie, potwierdził kod TOTP, wylogował się i ponownie zalogował z powodzeniem.

**Zalecenie.** W przyszłej poprawce wygody wygenerować QR lokalnie po stronie Firemki, bez wysyłania sekretu do zewnętrznej usługi. Zachować klucz tekstowy jako plan awaryjny i dodać test sprawdzający obecność bezpiecznie zakodowanego adresu `otpauth://`.

## Podsumowanie kontroli

### Ręczna bramka P1 została zamknięta

- Zweryfikowany lokalny certyfikat Caddy jest zaufany w pęku użytkownika macOS, a Safari korzysta z `https://localhost` bez omijania kontroli certyfikatu.
- Właściciel ukończył jednorazowy kreator, dodał klucz do aplikacji uwierzytelniającej, potwierdził kod TOTP, wylogował się i zalogował ponownie.
- Bez odczytywania danych konta potwierdzono w bazie: dokładnie 1 użytkownik, 1 konto z włączonym TOTP, zakończony kreator oraz po jednym udanym zdarzeniu `totp-initial-setup` i `totp`.
- Po zakończeniu kreatora `/Setup` przekierowuje do `/Account/Login`, strona logowania odpowiada `200`, a anonimowe wejście na stronę prywatną nadal przekierowuje do logowania.

### Naprawa błędu pustego pobrania jest potwierdzona

- [Program.cs](../../../src/Firemka.Web/Program.cs#L54-L82) ogranicza wyłącznie żądania `POST`; zwykłe wyświetlenia stron nie zużywają limitu. Odpowiedź `429` ma czytelną polską treść i właściwy typ tekstowy.
- [Program.cs](../../../src/Firemka.Web/Program.cs#L110) udostępnia zasoby statyczne przed zalogowaniem.
- [RateLimitTests.cs](../../../tests/Firemka.Web.Tests/RateLimitTests.cs#L8-L91) sprawdza wielokrotne wejścia na kreator, dostępność CSS/JS oraz granicę dziesięciu prób uwierzytelnienia. Testy najpierw odtworzyły błąd, a po poprawce przechodzą 3/3.
- W działającym Compose wszystkie używane pliki CSS/JS, łącznie z publikowanym pakietem stylów, odpowiadają `200`; jedenaście kolejnych wejść na formularz także odpowiedziało `200`.

### Cztery obszary review

- **Bezpieczeństwo:** TOTP jest obowiązkowe, strony prywatne pozostają chronione, formularze mają anty-CSRF, ciasteczko anty-CSRF ma `Secure`, odpowiedzi mają CSP/HSTS i pozostałe nagłówki, a skan NuGet nie wykrył znanych podatności. QR, jeśli zostanie dodany, nie może korzystać z usługi zewnętrznej.
- **Wydajność:** zwykłe pobrania stron i zasobów nie tworzą już lawiny przekierowań ani nie zużywają limitu logowania; worker pozostaje uśpiony bez pustej pętli logów.
- **Architektura:** zmiana pozostała w centralnej konfiguracji potoku HTTP i nie narusza podziału modułów. Obraz aplikacji działa jako użytkownik bez uprawnień administratora.
- **Scenariusze:** automatycznie sprawdzone są konfiguracja TOTP, ponowne logowanie, kod odzyskiwania używany jednokrotnie, zaufane urządzenia, blokada stron prywatnych, anty-CSRF, limit prób i zasoby niezalogowanego ekranu. Ręczna próba telefonu potwierdziła najważniejszą ścieżkę użytkownika.

## Świeża weryfikacja

- `dotnet restore Firemka.sln --locked-mode` — zakończone powodzeniem.
- `dotnet test tests/Firemka.Web.Tests/Firemka.Web.Tests.csproj --no-restore` — 11/11 testów zielonych.
- `dotnet format Firemka.sln --verify-no-changes --no-restore` — bez zmian formatowania.
- `dotnet list Firemka.sln package --vulnerable --include-transitive` — brak znanych podatnych paczek w aktualnych źródłach NuGet.
- `docker compose config --quiet` i `caddy validate` — zakończone powodzeniem; PostgreSQL, WWW, worker i Caddy są zdrowe, a `/health` zwraca `Healthy`.
- Pełny zestaw rozwiązania ma 15 zielonych testów. Jedyny czerwony test kontraktowy jest jawną bramką Fazy 0 oczekującą na oficjalny walidator próbek i nie dotyczy zakresu Fazy 1.

## Decyzja bramki

Faza 1 jest **gotowa do kontynuacji**. Nie ma P1 ani P2. P3 dotyczące QR jest nieblokującą poprawką wygody. Można rozpocząć Fazę 2, zachowując odrębną bramkę Fazy 0 dla obliczeń podatkowych, rzeczywistych dokumentów firmy i automatycznego księgowania.

Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
