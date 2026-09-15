# Ponowne review fazy 11 — kopie i odtworzenie

Data: 2026-09-15  
Status: **ZALICZONE — można rozpocząć bezpieczny zakres techniczny fazy 12**

## Wynik

Wszystkie trzy problemy P2 z pierwszego review zostały naprawione i odtworzone testami, które przed zmianą były czerwone. Ponowne review nie znalazło nowego błędu P1, P2 ani P3 w zaimplementowanym zakresie technicznym.

## Zamknięte znaleziska

- Skrypt odtworzenia wymaga pliku migratora z dokładnie odtwarzanej wersji WWW i połączenia aplikacji z bazą. Migracje wykonują się po przywróceniu danych, ale przed końcowym porównaniem skrótów i liczników oraz przed komunikatem sukcesu. Obraz WWW zawiera `dotnet`, `jq`, PostgreSQL 18 i właściwy plik aplikacji.
- Klient zapisuje listę plików i folderów utworzonych przez siebie. Po anulowaniu albo błędzie usuwa tylko te wpisy, a katalogi wyłącznie wtedy, gdy nadal są puste. Test dodaje obcy plik już po rozpoczęciu operacji i potwierdza, że pozostaje nienaruszony.
- Raport sukcesu odrzuca jednoczesne zlecenie ręczne i roczne, nieobsługiwaną wersję oraz nazwę niezgodną z rodzajem lub rokiem archiwum. Walidacja i odczyt zlecenia następują przed zmianą stanu sukcesu.

## Dodatkowa poprawka wykryta podczas ponownej weryfikacji

Próbna publikacja samodzielnego klienta Mac wcześniej zmieniała wspólne pliki blokad zależności na wariant `osx-arm64`, co psuło późniejszy kontrolowany build obrazu Linux. Instalator publikuje teraz z osobnymi, tymczasowymi plikami blokad dla każdego projektu. Próba utworzyła działający program Mach-O arm64, nie zmieniła blokad źródłowych, a następujący po niej obraz serwera zbudował się w trybie `--locked-mode`.

## Dowody

- pełny zestaw: **293 zielone testy**, 12 warunkowych testów PostgreSQL pominiętych w zwykłym przebiegu;
- oba testy Fazy 11 uruchomione osobno na PostgreSQL 16: zielone;
- kompilacja Release: 0 ostrzeżeń i 0 błędów; formatowanie bez zmian;
- model EF bez brakującej migracji; cztery przypięte próbki XSD i Compose poprawne;
- wszystkie 12 projektów bez zgłoszonych znanych podatności NuGet;
- skrypty Bash i szablon `launchd` poprawne;
- obraz WWW zbudowany w trybie blokowanych zależności: `pg_dump 18.6`, `jq 1.7`, użytkownik 10001 i właściwy migrator;
- samodzielny klient Mac: działający plik Mach-O arm64, bez zmiany źródłowych blokad zależności.

## Granice zewnętrzne

Nie wykonano instalacji `launchd` w prawdziwym folderze użytkownika, nie utworzono rzeczywistego hasła odzyskiwania ani nie przeprowadzono odtworzenia na drugim urządzeniu/VPS. To nadal obowiązkowa zewnętrzna bramka P1 przed pilotażem i produkcją, lecz nie blokuje lokalnego utwardzania i przygotowania odbioru w Fazie 12.

## Decyzja

Wynik: **0× nowych P1, 0× P2, 0× P3**. Techniczna bramka Fazy 11 jest zamknięta. Nie wykonano commita, pushu ani wdrożenia.
