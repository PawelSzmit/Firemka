# Review fazy 10 — płatności i powiadomienia

Data: 2026-09-14  
Status: **WYMAGA NAPRAWY PRZED FAZĄ 11**

## Wynik

Review znalazło trzy problemy P2 w świeżych ścieżkach powiadomień. Model pełnej płatności, przypięcie do wersji należności, terminy, prywatność wiadomości i równoległy zapis PostgreSQL są poprawne.

## Znaleziska

### P2 — ponownie pojawiający się błąd dokumentu był tłumiony do następnego dnia

Klucz sprawy zawierał tylko identyfikator dokumentu. Po rozwiązaniu i ponownym pojawieniu się błędu tego samego dnia istniejący klucz dzienny blokował nowe powiadomienie, mimo że była to nowa wersja stanu.

Wymagana naprawa: uwzględnić wersję stanu dokumentu w kluczu sprawy i dodać próbę rozwiązanie → ponowne pojawienie się tego samego dnia.

### P2 — ogólna awaria zadania mogła być przypisana każdemu właścicielowi

Starszy rekord zadania nie zawiera identyfikatora właściciela, a harmonogram odczytywał wszystkie awarie dla każdej firmy. W modelu wielokontowym prowadziłoby to do fałszywego alertu o cudzym zadaniu. Dodatkowo awaria zadania wysyłki e-mail mogła tworzyć nowe zadanie wysyłki e-mail o tej awarii.

Wymagana naprawa: ogólne zadania bez właściciela pokazywać tylko przy jednym koncie, wykluczyć zadania samego kanału e-mail i nigdy nie zgadywać odbiorcy.

### P2 — konfiguracja pozwalała świadomie wyłączyć TLS dla SMTP

Domyślne szyfrowanie było włączone, ale ustawienie `EnableSsl=false` pozwalało uruchomić połączenie bez TLS. Przy skonfigurowanym loginie mogłoby to narazić dane dostępowe do skrzynki.

Wymagana naprawa: twardo odrzucić niezabezpieczoną konfigurację przed próbą sieciową oraz nie podstawiać poświadczeń systemowych przy anonimowym SMTP.

## Potwierdzone obszary

- Płatność może mieć wyłącznie dokładną pełną kwotę i jest przypięta do identyfikatora oraz wersji faktury albo zamknięcia miesiąca.
- Korekta miesiąca tworzy nową nieopłaconą należność bez zmiany historii poprzedniej płatności.
- Terminy PIT/ZUS i VAT przesuwają się z weekendów i świąt na następny dzień roboczy.
- Formularz wymaga logowania, właściciela, daty i unikalnej referencji; kwota jest ponownie odczytywana po stronie serwera.
- Wiadomości nie zawierają kwot, załączników ani danych dokumentu i prowadzą wyłącznie do strony wymagającej logowania.
- SMTP jest domyślnie wyłączone, a brak danych dostawcy nie powoduje prawdziwej wysyłki.
- Wymuszony wyścig PostgreSQL tworzy jedną płatność, jedno powiadomienie i jeden wpis outbox.

## Decyzja

Wynik: **0× P1, 3× P2, 0× P3** w zaimplementowanym zakresie. Faza 11 pozostaje zamknięta do naprawy wszystkich trzech P2 i ponownego review.
