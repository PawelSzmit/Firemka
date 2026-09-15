# Ponowne review fazy 5 — po pierwszej naprawie P2

Data: 2026-09-11  
Status: **PIERWOTNE P2 ZAMKNIĘTE; 2 NOWE P2 DO NAPRAWY; ZEWNĘTRZNA BRAMKA KSeF NADAL OTWARTA**

## Znaleziska

### P1 zewnętrzne — rzeczywisty adapter i testowe KSeF nadal nieodebrane

Nie zmieniła się wcześniej odroczona bramka: aplikacja nadal używa twardo wyłączonego `UnconfiguredOutgoingKsefGateway`, więc nie wykonano rzeczywistego wysłania FA(3), oczekiwania na status, pobrania UPO ani porównania XML zwróconego przez testowy KSeF. Kontrolowane odpowiedzi i prawdziwy PostgreSQL potwierdzają zachowanie aplikacji, ale nie potwierdzają protokołu zewnętrznego.

**Wymagana czynność:** po bezpiecznym przekazaniu dostępu testowego zatwierdzić klienta/adapter, wykonać pełną próbę na środowisku testowym i pozostawić produkcję wyłączoną do odrębnego odbioru.

### P2 — wyłączenie automatyzacji po wysłaniu zatrzymuje domknięcie statusu

Automatyczny handler poprawnie prosi kolejkę o ponowienie dla `Sending` i `DeliveryUncertain` (`src/Firemka.Infrastructure/Sales/SalesDraftScheduler.cs`, wiersze 103–126). Każde kolejne wykonanie wywołuje jednak `IssueAsync(automatic: true)`, a serwis sprawdza zgodę na automatyzację i bieżące ustawienie właściciela przed odczytaniem faktury (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 214–236). Jeśli właściciel lub operator wyłączy przyszłą automatyzację po pierwszym `SendAsync`, wszystkie ponowienia zakończą się przed `GetStatusAsync`. Już wysłana faktura może pozostać bez numeru KSeF i UPO mimo działającego zadania.

**Wymagana naprawa:** rozdzielić zgodę na nowe automatyczne wysłanie od obowiązku domknięcia transmisji już rozpoczętej. Dla `Sending`/`DeliveryUncertain` wolno wykonać wyłącznie sprawdzenie statusu po zwykłej walidacji dostępności adaptera, bez wymagania nadal włączonej zgody na nowe wysyłki. Test ma wyłączyć automatyzację między `Pending` a `Accepted` i potwierdzić UPO przy jednym wysłaniu.

### P2 — równoczesne otwarcie odrzuconej faktury może zwrócić błąd bazy

Nowa operacja odczytuje `Rejected`, przygotowuje wersję historii, zmienia fakturę na `Draft` i zapisuje bez obsługi konfliktu (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 190–211). Dwa żądania mogą odczytać ten sam znacznik współbieżności i oba przygotować wersję 1. Pierwsze wygra; drugie napotka konflikt znacznika albo unikalnej wersji i przekaże wyjątek bazy aż do strony, która obsługuje tylko kontrolowany błąd biznesowy. Dane nie powinny się zduplikować, ale użytkownik może zobaczyć błąd 500 po podwójnym kliknięciu lub równoczesnym żądaniu.

**Wymagana naprawa:** potraktować równoczesne ponowne otwarcie idempotentnie. Przegrany proces ma wyczyścić własne zmiany, odczytać już otwarty szkic i zakończyć bez drugiej wersji. Dodać test na rzeczywistym PostgreSQL dla dwóch równoczesnych operacji i potwierdzić jeden szkic, jedną zachowaną odrzuconą wersję oraz brak błędu.

## Potwierdzone zamknięcie wcześniejszych P2

- Zadanie automatyczne ponawia stan oczekujący i przy niezmienionej zgodzie dochodzi do `Issued` przy jednym `SendAsync`; osobny limit 101 prób zapewnia kontrolowane, ograniczone czasowo odpytania.
- Odrzucenie można świadomie otworzyć do poprawy. Historia zawiera odrzuconą wersję 1, przyjęta próba tworzy wersję 2, klucz idempotencji się zmienia, a nadal istnieje jedna faktura dla miesiąca.
- Dwa równoczesne wyniki `Accepted` na prawdziwym PostgreSQL kończą się jednym stanem `Issued`, jedną wersją i bez dodatkowego wysłania.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** sprawdzanie właściciela, wyłączona produkcja i brak sekretów w audycie są zachowane. Wyłączenie przyszłej automatyzacji nie może jednak porzucać transmisji już wykonanej.
- **Wydajność i odporność:** ponowienia są ograniczone, opóźniane i nie wykonują drugiego wysłania. Końcowy wyścig statusów jest obsłużony; analogicznej ochrony wymaga nowa operacja ponownego otwarcia.
- **Architektura i typy:** historia wersji ma poprawny łańcuch i jawne stany. Walidacja zgody na nowe wysłanie jest obecnie umieszczona zbyt wcześnie względem rozpoznania operacji statusowej.
- **Scenariusze i testy:** trzy wcześniejsze P2 mają testy, w tym PostgreSQL. Brakuje testu cofnięcia zgody po rozpoczęciu transmisji oraz równoległego ponownego otwarcia.

## Dowody wykonane podczas review

- pełny zestaw bez zewnętrznej bazy: 115 testów zielonych, 1 test PostgreSQL pominięty warunkowo;
- osobny test PostgreSQL obejmujący dwa równoczesne `Accepted`: zielony;
- kompilacja Release: 0 ostrzeżeń i 0 błędów;
- formatowanie, model EF, cztery próbki XML i składnia Compose: czyste;
- skan zależności: brak zgłoszonych znanych podatności NuGet;
- przebudowane lokalne Compose: endpoint zdrowia działa, najnowsza migracja jest zastosowana, zachowano jedno konto z TOTP i nadal nie ma danych firmy ani faktur.

## Decyzja bramki

Wynik: **1× wcześniej odroczone P1 zewnętrzne, 2× P2, 0× P3**. Trzy pierwotne P2 są zamknięte, ale Faza 6 pozostaje zablokowana do naprawy dwóch nowych przypadków i kolejnego review. Następny krok to `dev-docs-execute` wyłącznie dla tych dwóch P2. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
