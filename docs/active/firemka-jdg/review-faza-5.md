# Review fazy 5 — abonament, wersje robocze i KSeF wychodzący

Data: 2026-09-11  
Status: **WYMAGA NAPRAWY P2; ZEWNĘTRZNA BRAMKA KSeF NADAL OTWARTA**

## Znaleziska

### P1 zewnętrzne — nie ma odebranego adaptera ani próby testowego KSeF

Plan wymaga wysłania FA(3) w testowym KSeF, pobrania statusu i UPO oraz porównania dokumentu zapisanego z treścią zwróconą przez KSeF. Bieżąca aplikacja celowo rejestruje `UnconfiguredOutgoingKsefGateway`, który nie wykonuje połączenia (`src/Firemka.Infrastructure/DependencyInjection.cs`, wiersze 130–135; `src/Firemka.Infrastructure/Ksef/Outgoing/UnconfiguredOutgoingKsefGateway.cs`, wiersze 5–26). Generator i cały proces zostały sprawdzone na kontrolowanych odpowiedziach, ale nie potwierdza to uwierzytelnienia, rzeczywistej asynchroniczności serwera, sposobu odzyskania referencji po niepewnej odpowiedzi ani dokładnej postaci pobranego XML.

**Wymagana czynność:** po bezpiecznym przekazaniu dostępu testowego wdrożyć i odebrać rzeczywisty adapter na podstawie zatwierdzonego klienta KSeF. Wykonać wysłanie, odpowiedź oczekującą, niepewny wynik, odrzucenie, przyjęcie, pobranie UPO i porównanie treści. Do tego czasu `Enabled`, `AdapterConfigured`, produkcja i automatyzacja muszą pozostać wyłączone. To wcześniej odroczona bramka zewnętrzna, a nie nowa regresja kodu.

### P2 — automatyczne zadanie nie wraca po status ani UPO

Handler automatyczny wywołuje `IssueAsync` i kończy się niezależnie od zwróconego stanu (`src/Firemka.Infrastructure/Sales/SalesDraftScheduler.cs`, wiersze 92–119). Tymczasem `IssueAsync` poprawnie zwraca stan `Sending` dla odpowiedzi oczekującej i `DeliveryUncertain` po zerwanym połączeniu (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 293–331 oraz 469–483). Runner uzna takie zadanie za zakończone, a harmonogram ponownie używa tego samego klucza tylko pierwszego dnia miesiąca. W rzeczywistym asynchronicznym KSeF faktura może więc pozostać bez końcowego numeru i UPO, dopóki właściciel sam nie otworzy ekranu i nie kliknie sprawdzenia statusu.

**Wymagana naprawa:** wynik oczekujący lub niepewny musi zaplanować idempotentne sprawdzenie statusu albo bezpiecznie oznaczyć zadanie do kontrolowanego ponowienia. Ponowienie ma wywołać wyłącznie sprawdzenie statusu, nigdy drugi `SendAsync`. Dodać test handlera, w którym pierwsza odpowiedź jest `Pending` lub niepewna, a kolejne wykonanie kończy fakturę z UPO przy dokładnie jednym wysłaniu.

### P2 — odrzucenie przez KSeF pozostawia miesiąc bez drogi naprawy

Po statusie `Rejected` serwis zawsze zwraca zapisany błąd i nie pozwala wrócić do edycji (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 218–220 i 452–467). Widok pokazuje powód, ale formularz edycji jest dostępny tylko dla `Draft`, a przycisk wystawienia dla odrzuconej faktury jest wyłączony (`src/Firemka.Web/Pages/Invoices/Sales/Details.cshtml`, wiersze 84–91 i 123–127). Jednocześnie unikalność firmy i miesiąca blokuje utworzenie drugiego szkicu. Odrzucony, niewystawiony dokument staje się więc trwałą ślepą uliczką.

**Wymagana naprawa:** dodać świadomą czynność „Popraw odrzuconą wersję”, która zachowuje odrzuconą próbę i jej XML w historii, tworzy kolejną rewizję roboczą tego samego miesięcznego dokumentu oraz nadaje nowy klucz próby wysyłki. Nie wolno ponawiać starej transmisji ani zmieniać dokumentu przyjętego przez KSeF. Dodać test odrzucenie → zachowana historia → poprawa → nowe wysłanie bez naruszenia zasady jednej faktury miesięcznej.

### P2 — równoległe sprawdzenia końcowego statusu nie mają obsługi konfliktu

Konflikt współbieżności jest obsłużony przy przejściu szkicu do wysyłki (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 279–290), ale nie przy zapisie wyniku statusu (`src/Firemka.Infrastructure/Sales/SalesInvoiceService.cs`, wiersze 381–487). Dwa procesy mogą odczytać ten sam stan `Sending`, oba pobrać `Accepted`, utworzyć lokalną wersję i spróbować obrócić ten sam znacznik współbieżności. Pierwszy zapis wygra, a drugi może zwrócić nieobsłużony błąd bazy zamiast bezpiecznie odczytać już zapisany wynik. Obecny test równoległości zatrzymuje się przed tą częścią procesu.

**Wymagana naprawa:** objąć wszystkie zapisy wyniku statusu obsługą konfliktu. Przegrany proces ma wyczyścić lokalne zmiany i zwrócić aktualny stan z bazy. Dodać test dwóch równoczesnych odpowiedzi `Accepted`, który potwierdza jedną niezmienną wersję, jeden końcowy numer KSeF, brak błędu i brak dodatkowego wysłania.

## Podsumowanie czterech perspektyw

- **Bezpieczeństwo i dane:** właściciel jest sprawdzany przy każdym odczycie i poleceniu, produkcja oraz automatyzacja mają oddzielne blokady, a audyt nie zawiera tokenów. Brak rzeczywistego adaptera jest obecnie bezpiecznie domknięty, lecz pozostaje zewnętrzną bramką odbioru.
- **Wydajność i odporność:** unikalny miesiąc, klucz idempotencji, zapis stanu przed kontaktem z siecią i test PostgreSQL chronią przed podwójnym pierwszym wysłaniem. Brakuje jednak cyklu automatycznego domknięcia statusu i obsługi wyścigu dwóch sprawdzeń.
- **Architektura i typy:** domena, port aplikacyjny, generator XML i adapter są rozdzielone. Stany `Draft`, `Sending`, `DeliveryUncertain`, `Rejected` i `Issued` są jawne, lecz stan odrzucenia nie ma dozwolonej ścieżki naprawczej.
- **Scenariusze i testy:** R12, R13, R33, R35 i R46, walidacja FA(3), awaria sieci, limit czasu, odrzucenie, różnica treści oraz równoległe pierwsze wysłanie są pokryte. Brakuje automatycznego dokończenia odpowiedzi oczekującej, poprawy po odrzuceniu oraz równoległego końcowego statusu.

## Dowody wykonane podczas review

- pełny zestaw bez zewnętrznej bazy: 113 testów zielonych, 1 test PostgreSQL pominięty zgodnie z warunkiem środowiskowym;
- osobny PostgreSQL: test migracji, równoległego szkicu i równoległego pierwszego wysłania zielony;
- ręczny odbiór na sztucznym koncie: lista i szczegóły sprzedaży działają na 390 px, a zapis `1650,50` daje `1 650,50 zł`; znaleziony wcześniej błąd separatora ma test dla przecinka i kropki;
- kompilacja Release: 0 ostrzeżeń i 0 błędów;
- EF Core: brak zmian modelu bez migracji;
- formatowanie, cztery próbki XML oraz składnia Compose: czyste;
- skan zależności: brak zgłoszonych znanych podatności NuGet;
- lokalne Compose po przebudowie: PostgreSQL i WWW zdrowe, worker i Caddy działają, `https://localhost/health` zwraca `Healthy`;
- istniejące dane zachowane: 1 konto z TOTP, 0 firm, 0 faktur sprzedaży i 0 ustawień automatyzacji.

## Decyzja bramki

Wynik: **1× wcześniej odroczone P1 zewnętrzne, 3× P2, 0× P3**. Faza 6 pozostaje zablokowana. Następny krok to `dev-docs-execute` ograniczony do trzech P2, a następnie ponowne `dev-docs-review` Fazy 5. Rzeczywisty KSeF i wszystkie działania produkcyjne pozostają wyłączone do zamknięcia P1. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
