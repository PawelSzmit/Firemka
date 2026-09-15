# Drugie ponowne review fazy 5 — po pełnej naprawie P2

Data: 2026-09-11  
Status: **GOTOWE DO KONTYNUACJI W BEZPIECZNYM ZAKRESIE TECHNICZNYM**

## Znaleziska

Nie znaleziono nowych P1, P2 ani P3 w zaimplementowanym zakresie technicznym fazy 5.

Pozostaje wcześniej odroczona zewnętrzna bramka P1: brak odebranego rzeczywistego adaptera i próby z testowym KSeF. Nie jest to regresja kodu ani brak możliwy do zamknięcia bez dostępu przekazanego poza repozytorium. Do czasu tej próby `Enabled`, `AdapterConfigured`, automatyzacja i produkcja pozostają wyłączone.

## Potwierdzenie napraw

- Automatyczne zadanie dla `Sending` i `DeliveryUncertain` prosi kolejkę o kontrolowane ponowienie, a kolejna próba wywołuje wyłącznie `GetStatusAsync`. Test `Pending` → cofnięcie zgody właściciela i blokada operatora → `Accepted` kończy się numerem KSeF i UPO przy jednym `SendAsync`.
- Zgoda właściciela i operatora nadal jest obowiązkowa dla każdej nowej automatycznej wysyłki szkicu. Rozdzielenie dotyczy tylko obowiązkowego domknięcia transmisji już rozpoczętej.
- Odrzucona faktura zachowuje pełny stan próby jako niezmienną wersję, wraca do szkicu z nowym kluczem idempotencji i po poprawie tworzy kolejną powiązaną wersję. Nadal istnieje jedna faktura dla firmy i miesiąca.
- Dwa równoczesne otwarcia odrzucenia są idempotentne: konflikt wersji lub znacznika jest rozpoznawany, przegrany kontekst jest czyszczony, a zapis kończy się jednym szkicem i jedną wersją historii.
- Dwa równoczesne wyniki `Accepted` zwracają jeden zapisany wynik, jedną wersję i nie powodują dodatkowego wysłania.

## Cztery perspektywy

- **Bezpieczeństwo i dane:** każda operacja używa identyfikatora właściciela; audyt nie zawiera tokenu ani danych logowania; produkcja i rzeczywisty adapter są twardo wyłączone. Historia zawiera dokumenty fakturowe zgodnie z ich przeznaczeniem i pozostaje w prywatnej bazie.
- **Wydajność i odporność:** sprawdzanie statusu jest opóźniane, ma limit 101 prób i nie duplikuje transmisji. Unikalne indeksy oraz znacznik współbieżności są obsługiwane zarówno przy wysłaniu, wyniku końcowym, jak i ponownym otwarciu.
- **Architektura i typy:** port aplikacyjny, domena, generator FA(3), adapter, handler i interfejs są rozdzielone. Stany oraz dozwolone przejścia są jawne, a wersje tworzą jednoznaczny łańcuch.
- **Scenariusze i testy:** pokryto pełny miesiąc, późne zatwierdzenie, ręczną kwotę, zmianę stawki, odrzucenie i poprawę, limit czasu, niepewną dostawę, różnicę treści, automatyczne ponowienie po cofnięciu zgody oraz trzy wyścigi PostgreSQL: szkic, pierwsze wysłanie i końcowy status/ponowne otwarcie.

## Dowody

- pełny zestaw bez zewnętrznej bazy: 115 testów zielonych, 1 test PostgreSQL pominięty warunkowo;
- osobny test PostgreSQL: zielony, w tym równoczesny szkic, pierwsze wysłanie, dwa `Accepted` i dwa ponowne otwarcia odrzucenia;
- kompilacja Release: 0 ostrzeżeń i 0 błędów;
- `dotnet format --verify-no-changes`: czysto;
- EF Core: brak zmian modelu bez migracji;
- cztery kontrolne XML przechodzą właściwe XSD bez sieci;
- konfiguracja Compose jest poprawna, a skan zależności nie zgłasza znanych podatności;
- lokalne Compose po przebudowie jest zdrowe, ma migrację `20260911135310_SalesInvoicesAndOutgoingKsef`, zachowało jedno konto z TOTP oraz nadal ma zero firm, faktur i ustawień automatyzacji.

## Decyzja bramki

Wynik zaimplementowanego zakresu: **0× P1, 0× P2, 0× P3**. Wszystkie pięć P2 znalezionych w dwóch poprzednich review jest zamkniętych. Faza 5 jest gotowa do kontynuacji w bezpiecznym zakresie technicznym. Zewnętrzna bramka testowego KSeF nadal blokuje rzeczywistą integrację i każde użycie produkcyjne. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
