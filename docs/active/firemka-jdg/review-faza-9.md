# Review fazy 9 — zamknięcie roku, PDF i roczny JPK

Data: 2026-09-14  
Status: **WYMAGA POPRAWEK PRZED FAZĄ 10**

## Znaleziska

### P1 — pierwszej korekty roku nie można ponownie zamknąć

Przy zamykaniu otwartej korekty nowe migawki miesięcy są dołączane do istniejącej wersji roku, ale warstwa zapisu rozpoznaje je jako wcześniejsze rekordy do aktualizacji. Próbuje wtedy przenieść migawki z poprzedniej, niezmiennej wersji do nowej. PostgreSQL odrzuca operację, a korekta nie może zostać zakończona.

**Wymagana naprawa:** nowe migawki korekty trzeba jawnie zapisać jako nowe rekordy, pozostawiając historię poprzedniego zamknięcia bez zmian. Test PostgreSQL ma zamknąć co najmniej drugą wersję roku i porównać komplet migawek obu wersji.

### P2 — korekta miesiąca może minąć się z zamknięciem roku

Zamknięcie roku chroni się blokadą PostgreSQL, ale zapis danych rocznych, rozpoczęcie korekty roku i rozpoczęcie korekty miesiąca nie używają tej samej blokady. Dwie karty mogą więc równocześnie sprawdzić stary stan, po czym jedna zamknie rok na wcześniejszej wersji danych, a druga otworzy korektę miesiąca albo zapisze nowe dane roczne. Równoczesne rozpoczęcie korekty roku może dodatkowo zakończyć się technicznym błędem unikalności.

**Wymagana naprawa:** wszystkie operacje zmieniające wejścia zamknięcia danego roku mają korzystać ze wspólnej blokady transakcyjnej. Powtórzenie identycznej korekty ma być idempotentne, a różna równoczesna zmiana ma zwracać kontrolowany komunikat. Test PostgreSQL ma wymusić wyścig zamknięcia roku z korektą miesiąca i potwierdzić, że nigdy nie pozostaje zamknięty rok z otwartą korektą miesiąca.

### P2 — można zatwierdzić i oznaczyć jako wysłany nieaktualny roczny JPK

Operacje JPK wyszukują wersję tylko po identyfikatorze. Po zamknięciu korekty nadal można więc zatwierdzić lub wysłać wcześniejszy plik, a ekran pokazuje przyciski akcji także przy historycznych wersjach. To grozi świadomym oznaczeniem niewłaściwego XML jako aktualnego zgłoszenia.

**Wymagana naprawa:** zatwierdzenie i oznaczenie wysyłki dopuścić wyłącznie dla najnowszej wersji roku oraz ukryć te czynności przy historii. Nadal trzeba pozwolić dopisać wynik do starszej wersji, jeżeli została rzeczywiście oznaczona jako wysłana przed rozpoczęciem korekty.

### P2 — późno wystawiona faktura może nadać rocznemu JPK datę spoza roku

Przychód jest rozpoznawany w potwierdzonym miesiącu usługi, ale generator roczny preferuje datę wystawienia faktury bez sprawdzenia roku. Faktura za grudzień 2026 wystawiona w styczniu 2027 może więc trafić do JPK za 2026 z datą 2027, mimo że sumy dokumentu odpowiadają zamkniętym miesiącom 2026.

**Wymagana naprawa:** po wymaganym potwierdzeniu rozpoznania sprzedaży użyć daty wystawienia tylko wtedy, gdy należy do zamykanego roku; w innym przypadku użyć potwierdzonego miesiąca usługi. Test ma sprawdzić datę w XML, nie tylko sumę i zgodność z XSD.

### P2 — podwójny zapis tego samego UPO zwraca błąd zamiast tego samego wyniku

Przy konflikcie współbieżności przegrany plik jest usuwany, ale nawet identyczny wynik i identyczne UPO kończą się komunikatem o zmianie w innej karcie. To nie uszkadza danych, lecz zwykłe podwójne kliknięcie wygląda jak nieudana operacja.

**Wymagana naprawa:** po konflikcie odczytać zwycięski zapis. Identyczny wynik, referencja i skrót pliku mają zwrócić ten sam stan; różna odpowiedź ma pozostać czytelnym konfliktem. Oba warianty wymusić na PostgreSQL i potwierdzić brak osieroconych plików.

## Bezpieczeństwo i izolacja danych

- Roczne PDF, XML i potwierdzenia pozostają prywatne oraz przypisane do właściciela i konkretnej wersji.
- Aplikacja nie wysyła rocznego JPK automatycznie; wymagane są osobne zatwierdzenie, ręczna wysyłka i zapis wyniku.
- Korekty zachowują wcześniejsze wersje zamknięcia, deklaracji i wygenerowanych plików.

## Decyzja bramki

Wynik: **1× P1, 4× P2, 0× P3**. Faza 10 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do pięciu napraw, następnie ponowne `dev-docs-review` fazy 9. Nie wykonano commita, pushu ani wdrożenia.
