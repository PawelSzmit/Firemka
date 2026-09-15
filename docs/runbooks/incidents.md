# Awaria Firemki — pierwsza pomoc

Najważniejsza zasada: zatrzymaj czynność, która może powielić wysyłkę albo zapis, zachowaj komunikat i czas zdarzenia, a dopiero potem naprawiaj. Nie wpisuj sekretów do zgłoszeń ani logów.

| Problem | Pierwszy bezpieczny ruch | Czego nie robić | Warunek wznowienia |
|---|---|---|---|
| KSeF nie odpowiada lub wynik jest niepewny | Wyłącz nowe automatyczne wysyłki i sprawdź status istniejącej próby po jej referencji. | Nie wysyłaj ponownie tego samego dokumentu jako nowej faktury. | Jednoznaczny stan przyjęcia/odrzucenia albo świadoma decyzja operatora po sprawdzeniu KSeF. |
| Plik JPK/ZUS został odrzucony | Zachowaj dokładną wersję, komunikat i potwierdzenie; rozpocznij korektę przez aplikację. | Nie nadpisuj poprzedniego XML ani nie oznaczaj go jako przyjęty. | Nowa wersja przechodzi lokalne XSD i próbny import, a właściciel zatwierdził ją osobno. |
| E-mail nie dochodzi | Sprawdź status powiadomienia i testową skrzynkę; ważne terminy obsłuż ręcznie w Firemce. | Nie włączaj kilku równoległych mechanizmów ponawiania. | Jedna testowa wiadomość dochodzi przez TLS i nie ma duplikatów. |
| Worker stoi lub stale się restartuje | Zatrzymaj worker, zachowaj ostatnie logi i sprawdź kolejkę trwałych zadań. | Nie usuwaj kolejki i nie zmieniaj ręcznie stanów na sukces. | Przyczyna usunięta, jedno kontrolowane zadanie przechodzi, ponowienie nie tworzy duplikatu. |
| Kończy się miejsce na VPS | Zatrzymaj importy i generowanie plików; ustal, co zajmuje miejsce. | Nie usuwaj wolumenów bazy, plików, kluczy ani ostatniej kopii. | Co najmniej 20% wolnego miejsca i potwierdzona poprawna kopia poza VPS. |
| Certyfikat kończy się lub HTTPS nie działa | Wstrzymaj logowanie i połączenia klienta kopii; sprawdź DNS, Caddy i odnowienie. | Nie obchodź sprawdzania certyfikatu i nie przechodź na zwykły HTTP. | Prawidłowy łańcuch TLS, minimum 14 dni ważności i zielony monitor. |
| Utrata telefonu | Użyj bezpiecznie przechowanego kodu odzyskiwania, zaloguj się i unieważnij wszystkie zaufane urządzenia. | Nie wyłączaj 2FA i nie przesyłaj kodów odzyskiwania przez czat/e-mail. | Nowy telefon ma skonfigurowane TOTP, stare zaufanie jest unieważnione, logowanie sprawdzone. |
| Utrata Maca | Unieważnij token kopii w Firemce i zabezpiecz konto Apple/urządzenie. | Nie zmieniaj hasła odzyskiwania, jeśli jest potrzebne do istniejących kopii. | Nowy Mac ma nowy token, hasło z oddzielnego źródła i zaliczoną kopię oraz próbę odczytu. |
| Kopia nie powstaje | Nie usuwaj ostatniej poprawnej kopii; sprawdź miejsce, Pęk kluczy, sieć i kod błędu. | Nie oznaczaj ręcznie próby jako sukces i nie rotuj plików. | Nowa kopia przechodzi pełną lokalną weryfikację i serwer pokazuje jej czas. |
| Aktualizacja nie działa | Zatrzymaj nowe czynności i użyj tabeli decyzji w runbooku wydania. | Nie cofaj migracji bazy w ciemno. | Poprzednia zgodna wersja działa albo czyste odtworzenie przechodzi pełną checklistę. |
| Podejrzenie przejęcia konta | Unieważnij zaufane urządzenia, token kopii i aktywne sesje; zachowaj audyt. | Nie kasuj logów i nie kontynuuj pracy na podejrzanym urządzeniu. | Nowe hasło/2FA, bezpieczne urządzenie i przegląd audytu nie pokazują dalszej aktywności. |

Po każdym incydencie zapisz: czas warszawski i UTC, wersję aplikacji, objaw, wykonane bezpieczne kroki, wynik oraz osobę, która zgodziła się na wznowienie. Dane dokumentów dołączaj tylko w bezpiecznym miejscu, nigdy do publicznego zgłoszenia.
