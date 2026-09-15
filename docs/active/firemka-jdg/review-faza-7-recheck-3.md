# Trzecie ponowne review fazy 7 — zapis równoległy konfliktu

Data: 2026-09-13  
Status: **WYMAGA POPRAWKI PRZED FAZĄ 8**

## Wynik sprawdzenia historii

Każda nowa różnica KSeF ma osobny wpis, prywatny plik, skrót i decyzję. Identyczne ponowienie nie tworzy duplikatu. Kontrolowana awaria usuwa nowy plik oraz niedokończony wpis, nie naruszając pierwotnego dokumentu. Migracja zachowuje wcześniejsze konflikty.

## Nowe znalezienie

### P2 — równoczesne wyjaśnienie i nowy konflikt mogą rozjechać stan dokumentu z historią

Historia konfliktów jest dopisywana, ale szybki stan używany na liście i przy zamknięciu miesiąca nadal znajduje się także w rekordzie dokumentu. Rekord ma licznik wersji, lecz baza nie używa go jeszcze do wykrywania równoczesnego zapisu. Jeśli właściciel rozwiąże konflikt A dokładnie wtedy, gdy synchronizacja zapisuje nowy konflikt B, ostatni zapis może ustawić dokument jako rozwiązany mimo otwartego wpisu B. To może błędnie usunąć blokadę miesiąca.

**Wymagana naprawa:** włączyć kontrolę wersji rekordu dokumentu, czytać blokadę również z dopisywanej historii i zamieniać przegrany równoległy zapis wyjaśnienia na czytelny komunikat wymagający odświeżenia. Dodać wymuszony test PostgreSQL, w którym rozwiązanie konfliktu A ściga się z konfliktem B i stan końcowy zawsze odpowiada historii.

## Decyzja bramki

Wynik: **0× P1, 1× P2, 0× P3**. Faza 8 pozostaje zablokowana do naprawy wyścigu i kolejnego `dev-docs-review`. Nie wykonano commita, pushu ani wdrożenia.
