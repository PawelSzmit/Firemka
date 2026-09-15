# Ponowne review fazy 2 — po naprawie leasingu i proweniencji plików

Data: 2026-09-10  
Status: **GOTOWE DO KONTYNUACJI**

## Znaleziska

Brak P1, P2 i P3 w zakresie Fazy 2.

## Sprawdzenie wcześniejszych P2

### Odnawianie aktywnego leasingu — zamknięte

- `BackgroundJobQueue.RenewLeaseAsync` przedłuża blokadę atomowo tylko wtedy, gdy zadanie nadal działa, wskazany worker pozostaje właścicielem, a poprzedni leasing jeszcze nie wygasł.
- `BackgroundJobLeaseRenewer` otwiera oddzielny kontekst bazy, dlatego odnowienie nie wykonuje równoległych operacji na kontekście używanym przez wykonawcę zadania.
- `BackgroundJobRunner` odnawia leasing okresowo, anuluje wykonawcę po utracie własności i nie zapisuje zakończenia ani błędu w imieniu poprzedniego właściciela.
- Test długiego zadania przekracza pierwotny czas leasingu i potwierdza brak przejęcia przez drugi worker. Oddzielny test potwierdza anulowanie po utracie leasingu, a test PostgreSQL sprawdza atomowe odnowienie i późniejsze odzyskanie po awarii.

### Pochodzenie i powiązanie wersji pliku — zamknięte

- `StoredFileContext` wymaga typowanego pochodzenia, rodzaju rekordu, niepustego identyfikatora i dodatniego numeru wersji.
- `StoredFileService` sprawdza kontekst przed zapisem treści, a `StoredFile` przechowuje wszystkie cztery wartości razem z właścicielem, typem, rozmiarem i SHA-256.
- Migracja `StoredFileProvenance` dodaje wymagane kolumny i indeks wersji. Odmawia nadania fałszywych wartości istniejącym plikom; było to bezpieczne, ponieważ przed formularzem dokumentów zweryfikowano pustą tabelę.
- Test metadanych potwierdza zapis wszystkich pól, a test migracji przechodzi w przód, cofa tylko nową warstwę, następnie cofa rdzeń i ponownie odtwarza najnowszy model.

## Cztery perspektywy

- **Bezpieczeństwo i dane:** dostęp do treści nadal wymaga właściciela, pliki nie są publiczne, a nowy kontekst poprawia ślad pochodzenia bez ujawniania danych w logach.
- **Wydajność i odporność:** odnowienie jest pojedynczą warunkową aktualizacją; równoległe dodanie, przejęcie, odnowienie i odzyskanie po awarii zostały sprawdzone na PostgreSQL.
- **Architektura i typy:** mechanizm odnowienia jest oddzielony od wykonawcy i kontekstu jego pracy; plik nie może zostać zapisany bez typowanej proweniencji.
- **Scenariusze i testy:** ścieżki sukcesu, powtórzenia, równoległości, awarii, długiego działania, utraty leasingu, złego pliku, złego właściciela i niedozwolonego stanu mają pokrycie.

## Dowody

- kompilacja Release: 0 ostrzeżeń, 0 błędów;
- pełne testy: 36/36 zielone, w tym izolowany PostgreSQL;
- formatowanie: bez zmian;
- EF Core: model zgodny z trzema migracjami;
- Compose: składnia poprawna, lokalne usługi zdrowe, migracje zakończone;
- dane lokalnej próby: jedno konto, zakończony kreator, TOTP włączone, brak plików biznesowych;
- zależności: brak zgłoszonych znanych podatności NuGet;
- próbki urzędowe: cztery walidacje XSD zielone.

## Decyzja bramki

Wynik: **0× P1, 0× P2, 0× P3**. Faza 2 jest gotowa do kontynuacji; można rozpocząć Fazę 3. Nadal obowiązuje osobna bramka danych z Fazy 0 przed obliczeniami podatkowymi i rzeczywistym księgowaniem. Nie wykonano commita, pushu ani wdrożenia produkcyjnego.
