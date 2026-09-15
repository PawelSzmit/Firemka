# Kopie Firemki i odtworzenie — instrukcja właściciela

## Zanim włączysz kopie

1. W Firemce otwórz **Kopie** i wygeneruj token. Pokaże się tylko raz.
2. W aplikacji macOS **Dostęp do pęku kluczy** utwórz dwa bezpieczne hasła ogólne dla swojego konta Mac:
   - nazwa usługi `pl.firemka.backup.token` — hasło to token z Firemki;
   - nazwa usługi `pl.firemka.backup.recovery` — nowe, unikalne hasło odzyskiwania o długości co najmniej 16 znaków.
3. Drugą kopię hasła odzyskiwania zapisz poza Makiem, np. w menedżerze haseł lub bezpiecznym miejscu offline. Nie zapisuj go obok plików `.fmbak`.
4. Ustal docelowy folder. Propozycja: `~/Documents/Firemka/Backups`.
5. Z katalogu projektu uruchom instalator, podając adres HTTPS Firemki i pełną ścieżkę folderu. Instalator nie przyjmuje tokenu ani hasła jako argumentu.
6. Sprawdź w panelu Firemki, czy pojawiła się data ostatniej poprawnej kopii.

Klient startuje po logowaniu i co godzinę pyta serwer o plan. Gdy Mac był wyłączony, pierwsze uruchomienie nadrabia brakującą kopię. Po udanej weryfikacji zostaje pięć najnowszych kopii dziennych. Archiwa roczne trafiają do `Archives` i nie są usuwane przez rotację.

## Gdy kopia nie działa

- Nie usuwaj ostatniej poprawnej kopii.
- Otwórz **Kopie** i sprawdź prosty kod ostatniego błędu.
- Sprawdź, czy Mac ma wolne miejsce, dostęp do internetu oraz czy oba wpisy nadal istnieją w Pęku kluczy.
- Po kradzieży lub utracie Maca natychmiast unieważnij token w Firemce. Na nowym Macu utwórz nowy token; istniejące pliki nadal otwiera to samo hasło odzyskiwania.

## Próba odtworzenia

1. Przygotuj drugi, czysty PostgreSQL oraz puste foldery plików i kluczy. Nie używaj działającej instancji.
2. Klientem wykonaj `restore-extract` do pustego folderu. Polecenie najpierw sprawdza szyfrowanie i cały manifest; przy błędzie usuwa częściowy wynik.
3. Utwórz plik `PGPASSFILE` z uprawnieniami `0600`. Hasła bazy nie wpisuj w argumentach poleceń ani w historii terminala.
4. Użyj obrazu WWW dokładnie tej wersji Firemki, którą chcesz uruchomić po odtworzeniu. Ma on `dotnet`, `jq`, narzędzia PostgreSQL i plik `/app/Firemka.Web.dll`.
5. Ustaw `PGHOST`, `PGDATABASE`, `PGUSER`, `PGPASSFILE`, `ConnectionStrings__Firemka` oraz `FIREMKA_WEB_DLL=/app/Firemka.Web.dll`. Następnie uruchom `deploy/scripts/restore/restore-clean-instance.sh` z trzema pełnymi ścieżkami: rozpakowana kopia, pusty folder plików, pusty folder kluczy. Nie wpisuj wartości połączenia w argumentach ani historii terminala; w środowisku kontenera użyj tego samego sekretu, co usługa migracji.
6. Skrypt odmówi pracy, jeśli brakuje migratora, baza albo foldery nie są puste. Po odtworzeniu obowiązkowo wykona migracje bieżącej aplikacji, a dopiero potem porówna skróty plików i dokładne liczby rekordów z manifestem. Bez poprawnego przejścia tych kroków nie ogłosi sukcesu.
7. Uruchom aplikację z odzyskanymi folderami, sprawdź `/health`, logowanie z 2FA, listę dokumentów oraz skróty plików. Dopiero wtedy można rozważyć przełączenie ruchu.

Produkcji nie wolno uruchamiać, dopóki ta procedura nie przejdzie na osobnym urządzeniu/VPS i wynik nie zostanie zapisany w protokole odbioru.
