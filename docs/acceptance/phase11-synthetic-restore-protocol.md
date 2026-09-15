# Protokół technicznej próby kopii i odtworzenia — Faza 11

Data: 2026-09-15  
Zakres: wyłącznie dane syntetyczne, bez VPS produkcyjnego i bez prywatnych dokumentów  
Wynik: **zaliczony dla bezpiecznego zakresu technicznego**

## Sprawdzony przebieg

1. Najnowsze migracje zostały nałożone na czystą bazę PostgreSQL.
2. Serwer utworzył w jednej migawce logiczny zrzut wszystkich tabel, prywatny plik testowy, klucz ochrony danych i manifest z rozmiarami oraz SHA-256.
3. Pakiet został pobrany strumieniowo, zaszyfrowany porcjami do `.fmbak`, odszyfrowany do pliku tymczasowego i sprawdzony przed otrzymaniem nazwy końcowej.
4. Kopia została rozpakowana do pustego folderu, a zrzut przywrócony do osobnej, pustej bazy.
5. Dla każdej tabeli wymienionej w manifeście porównano dokładną liczbę rekordów. Porównano również treść i SHA-256 wszystkich testowych plików oraz kluczy.
6. Odczyt przywróconych rekordów przez bieżący model aplikacji zakończył się powodzeniem.
7. Osobna próba obrazu serwera z PostgreSQL 18 pobrała przez prawdziwy strumień HTTP kompletny ZIP: 181 730 bajtów, 58 tabel w manifeście, zrzut bazy i rzeczywisty klucz ochrony danych wygenerowany przez testową instancję WWW.

## Próby awaryjne

- złe hasło, zmieniony fragment i ucięty koniec `.fmbak` są odrzucane;
- przerwane pobieranie oraz symulowany pełny dysk nie pozostawiają pozornej poprawnej kopii;
- błędne lub powtórzone wpisy manifestu są odrzucane;
- awaria zgłoszenia sukcesu do serwera pozostawia lokalny dziennik i przy następnym uruchomieniu zgłasza już sprawdzony plik bez ponownego pobierania;
- dopiero poprawna nowa kopia uruchamia rotację 5→5, a archiwum roczne pozostaje poza rotacją;
- dwa równoczesne żądania na PostgreSQL pozostawiają jedno aktywne zlecenie i jeden aktywny token bez błędu serwera;
- odtworzenie odmawia pracy z niepustą bazą albo folderem.

## Dowody automatyczne

- pełny zestaw rozwiązania: 293 testy zaliczone, 12 warunkowych testów PostgreSQL pominiętych w zwykłym przebiegu;
- oba testy PostgreSQL Fazy 11 uruchomione osobno: zaliczone;
- obraz WWW: `pg_dump 18.6`, użytkownik bez uprawnień administratora, pełny eksport HTTP zaliczony;
- obraz WWW zawiera obowiązkowe narzędzia odtworzenia i migrator, a kontrolowany build po publikacji klienta Mac nadal przechodzi z blokowanymi zależnościami;
- klient macOS `osx-arm64`: publikacja samodzielnego programu i uruchomienie pomocy zaliczone;
- kompilacja Release: 0 ostrzeżeń i 0 błędów;
- składnia skryptów, plik `launchd`, Compose, model migracji EF, przypięte XSD i skan znanych podatności: zaliczone.

## Otwarta bramka przed produkcją

Ten protokół nie zastępuje próby na drugim urządzeniu. Przed uruchomieniem produkcyjnym właściciel musi wskazać docelowy folder Maca, utworzyć i zachować poza kopią hasło odzyskiwania, zainstalować `launchd`, a następnie przeprowadzić odtworzenie na drugim, czystym urządzeniu lub VPS. Po starcie z odzyskanej kopii trzeba sprawdzić `/health`, logowanie z 2FA, dokumenty i pobieranie prywatnego pliku. Dopiero osobno podpisany wynik zamyka zewnętrzną bramkę P1.

Wszystkie tymczasowe kontenery, sieci, bazy i pakiety tej próby zostały usunięte po weryfikacji. Nie wykonano commita, pushu ani wdrożenia.
