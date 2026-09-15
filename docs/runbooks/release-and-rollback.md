# Wydanie i bezpieczny powrót Firemki

Ta instrukcja dotyczy kontrolowanego wydania na VPS. Samo jej wykonanie w repozytorium nie uruchamia produkcji. Każde wydanie musi mieć jedną osobę prowadzącą i drugą, która potwierdza wynik checklisty.

## Informacje zapisywane przed rozpoczęciem

- identyfikator wydania i data;
- skrót rewizji kodu oraz skróty wszystkich obrazów;
- numer najnowszej migracji bazy;
- nazwa ostatniej poprawnej kopii poza VPS, jej czas i wynik próbnego odczytu;
- skrót poprzednich obrazów, do których można wrócić;
- osoba podejmująca decyzję o kontynuacji albo wycofaniu.

Sekretów, haseł, tokenów KSeF i pełnych danych klienta nie wpisujemy do protokołu, argumentów poleceń ani historii terminala.

## Identyfikacja obrazów i punkt powrotu

Na VPS cztery wartości `FIREMKA_WEB_IMAGE`, `FIREMKA_WORKER_IMAGE`, `FIREMKA_CADDY_IMAGE` i `FIREMKA_POSTGRES_IMAGE` muszą wskazywać unikalne oznaczenie danego wydania, na przykład datę połączoną ze skrótem kodu. Wartości `:local` i `:latest` są wyłącznie do pracy lokalnej i blokują wydanie. Po zbudowaniu zapisz identyfikator każdego obrazu z `docker image inspect`; to on rozstrzyga, co naprawdę zostało uruchomione.

Nie nadpisuj znacznika poprzedniego wydania i nie usuwaj jego obrazu przed zamknięciem okna obserwacji. Powrót do wersji zgodnej z bazą polega na przywróceniu czterech poprzednich wartości w `.env` i uruchomieniu Compose z opcją zabraniającą ponownego budowania. Jeżeli stary obraz nie jest zgodny z nowym schematem bazy, zamiast niego obowiązuje odtworzenie na czystej instancji.

## Bramka przed wydaniem

1. Uruchom lokalną bramkę `deploy/scripts/verify-phase12-local.sh` z obowiązkowym testem PostgreSQL. Wynik ma być zielony.
2. Sprawdź, czy wszystkie obrazy są przypięte skrótem i skan zależności nie zgłasza znanej podatności.
3. Na VPS uruchom odczytowy `deploy/scripts/operations/audit-vps.sh`. Nie naprawiaj kilku problemów naraz; każdy wynik najpierw zapisz.
4. Potwierdź poprawną kopię poza VPS nie starszą niż 24 godziny oraz dostęp do hasła odzyskiwania przechowywanego oddzielnie.
5. Potwierdź, że prawdziwa próba odtworzenia danej wersji była zaliczona. Jeśli nie — wydanie jest zablokowane.
6. KSeF produkcyjny, e-mail i automatyczne wystawianie mają pozostać wyłączone, chyba że ich osobne pozycje odbioru są podpisane.
7. Zarezerwuj okno bez wykonywania księgowych czynności i poinformuj jedynego użytkownika.

## Kontrolowane wydanie

1. Zapisz bieżący wynik `docker compose ... images` i `docker compose ... ps` jako punkt powrotu.
2. Pobierz albo zbuduj dokładnie zatwierdzone obrazy pod czterema unikalnymi nazwami wydania. Nie używaj `local`, `latest` ani ponownie wykorzystanego znacznika.
3. Uruchom jednorazową usługę migracji i sprawdź jej kod zakończenia. Nie uruchamiaj nowego WWW po nieudanej migracji.
4. Uruchom WWW i worker z nakładką `deploy/compose.production.yaml`; dopiero zdrowe WWW pozwala uruchomić Caddy.
5. Sprawdź HTTPS, `/health`, stronę logowania, nagłówki ochronne oraz brak publicznego portu bazy.
6. Zaloguj się hasłem i 2FA, ale nie wykonuj jeszcze nieodwracalnej czynności. Sprawdź „Mój miesiąc”, dokumenty, płatności i kopie.
7. Wykonaj ręczną kopię, poczekaj na potwierdzony zapis na Macu i sprawdź jej obecność.
8. Obserwuj logi i monitor przez co najmniej 30 minut. Dopiero potem zamknij okno wydania.

## Stały monitor po odbiorze VPS

Szablon `deploy/systemd/firemka-health.service.template` uruchamia odczytową kontrolę HTTPS, certyfikatu, `/health` i miejsca na dysku, a `deploy/systemd/firemka-health.timer` powtarza ją co pięć minut. Przed instalacją administrator zastępuje wyłącznie `__PROJECT_DIR__` pełną ścieżką projektu i `__FIREMKA_DOMAIN__` zatwierdzoną domeną. Gotowy plik usługi oraz timer kopiuje do `/etc/systemd/system`, przeładowuje systemd, uruchamia jedną kontrolę ręczną i dopiero po jej zielonym wyniku włącza timer.

Nie włączaj timera z nierozwiązanymi znacznikami szablonu. Po instalacji zapisz wynik `systemctl status firemka-health.timer`, wynik ręcznego uruchomienia usługi i miejsce, w którym operator zobaczy nieudane jednostki lub otrzyma powiadomienie z monitoringu hosta. Sam wpis w dzienniku systemowym nie zastępuje uzgodnionego kanału alarmowego.

## Kiedy natychmiast przerwać

- migracja zwróciła błąd;
- `/health` nie jest zielony lub HTTPS/certyfikat jest błędny;
- nie działa logowanie 2FA albo dostęp do prywatnych plików;
- nowa wersja zmienia kwoty względem zatwierdzonego wyniku bez wyjaśnienia;
- kopia nie dochodzi na Maca lub nie przechodzi weryfikacji;
- pojawiła się możliwość podwójnego wysłania albo utraty historii.

## Wybór drogi powrotu

| Sytuacja | Bezpieczna decyzja |
|---|---|
| Nowa wersja nie uruchomiła migracji | Przywrócić poprzednie cztery nazwy obrazów i uruchomić je bez budowania; baza nie została zmieniona. |
| Migracja przeszła, a poprzedni obraz jest zgodny z nowym schematem | Po potwierdzeniu zgodności uruchomić poprzedni obraz i powtórzyć testy odczytowe. |
| Migracja przeszła, a poprzedni obraz nie jest zgodny | Nie cofać migracji w działającej bazie. Odtworzyć ostatnią pełną kopię do nowej, czystej instancji zgodnie z runbookiem kopii. |
| Podejrzenie błędnych zapisów po wydaniu | Zatrzymać czynności użytkownika, zachować logi i kopię stanu, a następnie odtworzyć do osobnej instancji do porównania. |
| Problem wyłącznie w workerze | Zatrzymać worker; WWW pozostawić tylko wtedy, gdy nie może zlecać ryzykownych nowych działań. |

Powrót kończy się tym samym zestawem kontroli co wydanie: HTTPS, `/health`, 2FA, prywatne pliki, dane miesiąca i nowa poprawna kopia. Nie kasujemy uszkodzonego środowiska przed zachowaniem dowodów i potwierdzeniem kompletnego odtworzenia.
