# Firemka — rejestr decyzji podatkowych i blokad

Status: dokument roboczy; nie jest opinią podatkową ani instrukcją złożenia deklaracji.  
Sprawdzone źródła: 2026-09-09.

## Cel

Oddzielić:

- zachowanie produktu zatwierdzone w wymaganiach;
- fakty wynikające z aktualnych źródeł urzędowych;
- wartości i kwalifikacje, których nie można bezpiecznie wywnioskować bez dokumentów firmy lub potwierdzenia specjalisty.

Kod może automatyzować wyłącznie pozycje ze statusem `ZATWIERDZONE DLA AUTOMATU`. Obecnie nie ma takich pozycji podatkowych. Obliczenie matematyczne kosztu energii ma osobny, niepodatkowy status.

## Rejestr

| Id | Obszar | Status | Potwierdzone dziś | Dane / decyzja konieczna przed automatem | Konsekwencja dla kodu |
|---|---|---|---|---|---|
| TAX-001 | forma opodatkowania | UZGODNIONE PRODUKTOWO | Pierwsza wersja obsługuje tylko skalę. | Potwierdzenie roku startu i faktycznego profilu podatkowego. | Model `TaxYear` ma tylko skalę; brak aktywnego przełącznika ryczałtu. |
| TAX-002 | PIT na skali | WYMAGA POTWIERDZENIA | MF potwierdza KPiR, zaliczki i wspólną kwotę wolną dla dochodów na skali. | Dochody etatowe używane przez płatnika, sposób uwzględnienia kwoty wolnej, konkretne odliczenia i zatwierdzony miesiąc referencyjny. | Kalkulator parametryczny i testy tylko po zatwierdzeniu danych wejściowych. |
| VAT-001 | miesięczny VAT / JPK | UZGODNIONE PRODUKTOWO + KONTRAKT | Wymagania określają VAT miesięczny; JPK_V7M(3) jest pobrany. | Dane kontrahenta, rzeczywiste stawki, rodzaje transakcji i potwierdzenie oznaczeń. | Osobne okresy VAT oraz walidator dokumentów; nie wybierać oznaczeń wyłącznie po nazwie dostawcy. |
| VAT-002 | import usług | WYMAGA DOKUMENTÓW | MF opisuje import usług i miejsce świadczenia; różnice zależą od transakcji. | Każda faktyczna faktura: podmiot, kraj, NIP/VAT UE, waluta, usługa, data. | Reguła zatrzymuje automatyczne księgowanie przy zmianie kraju, waluty, VAT lub usługi. |
| ZUS-001 | zdrowotna | WYMAGA POTWIERDZENIA | ZUS wskazuje 9% podstawy dla ogólnej zasady; dla 2026 od lutego minimalna składka dla skali/liniowego wynosi 432,54 zł przy podstawie 4 806 zł. | Rejestracja, data rozpoczęcia, zbieg tytułów, faktyczny okres ubezpieczenia i roczne rozliczenie. | Parametry zdrowotnej są wersjonowane datami, a nie zaszyte w kodzie. |
| ZUS-002 | społeczne | PROFIL DO POTWIERDZENIA | Wymagania zakładają tylko zdrowotną z powodu etatu. | Potwierdzenie podstawy z etatu i ewentualnych wyjątków z ZUS/księgowym. | Nie generować DRA/RCA ani nie pomijać społecznych bez jawnego profilu obowiązującego od daty. |
| CAR-001 | najem/leasing auta | WYMAGA UMOWY | Wymagania obejmują oba warianty; źródła MF wskazują, że szczegóły zależą od tytułu i wydatku. | Wybrana umowa, wartość pojazdu, opłaty, serwis, ubezpieczenie, VAT i użytek. | Osobne polityki dla raty, eksploatacji, ubezpieczenia; brak jednej reguły „auto EV”. |
| CAR-002 | użytek mieszany | WYMAGA POTWIERDZENIA | Produkt potwierdza użytek mieszany. | Właściwa kwalifikacja PIT/VAT każdego rodzaju wydatku dla konkretnej umowy. | Przed zatwierdzeniem nie tworzyć automatycznych wpisów; wskazać brak właścicielowi. |
| ENERGY-001 | domowe ładowanie — matematyka | ZATWIERDZONE NIEPODATKOWO | 10 000 Wh / 1000 = 10 kWh; 10 kWh × 0,91 zł = 9,10 zł brutto. | Przykładowy CSV, źródło stawki po zmianie, podstawa dokumentacyjna i podatkowa. | Wersjonowana stawka energii i własne zestawienie; wynik nie tworzy wpisu KPiR/VAT bez decyzji. |
| ENERGY-002 | prywatna faktura za prąd | UZGODNIONE PRODUKTOWO | Nie księgować ogólnej domowej faktury energii jako kosztu. | Brak. | Brak automatycznego importu tej faktury do kosztów. |
| DOC-001 | dokumenty US/ZUS | UZGODNIONE PRODUKTOWO | Pierwsza wersja eksportuje pliki i zapisuje potwierdzenia; automatyczna wysyłka jest późniejsza. | Potwierdzony zestaw formularzy i test importu. | Port do przyszłej wysyłki, ale żadnego automatycznego submission. |

## Obowiązkowe dane referencyjne przed fazą 7

Do każdego testu podatkowego potrzebne są: wejściowe dokumenty w formie zanonimizowanej, data dokumentu i księgowania, kurs jeśli dotyczy, decyzja o VAT/PIT/ZUS, oczekiwane wpisy KPiR/VAT, kwota zaliczki/składki, źródło i osoba potwierdzająca.

Minimalny zestaw to:

1. zwykły miesiąc z jedną sprzedażą i kosztami rzeczywistymi;
2. miesiąc bez przychodu;
3. miesiąc ze stratą;
4. korekta po zamknięciu;
5. faktura za poprzedni okres wystawiona w następnym miesiącu;
6. roczne zamknięcie.

Brak którejkolwiek z tych wartości jest blokadą dla produkcyjnego kalkulatora, nie zachętą do użycia przybliżenia.

## Źródła

- [MF — skala](https://podatki.gov.pl/podatki-firmowe/pit/informacje-podstawowe/co-jest-opodatkowane/opodatkowanie-wedlug-skali-podatkowej)
- [MF — VAT](https://www.podatki.gov.pl/podatki-firmowe/vat/informacje-podstawowe)
- [MF — miejsce świadczenia usług](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/miejsce-swiadczenia-opodatkowania)
- [MF — moment obowiązku VAT](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/kiedy-powstaje-obowiazek-podatkowy)
- [ZUS — składka zdrowotna](https://www.zus.pl/firmy/rozliczenia-z-zus/skladki-na-ubezpieczenia/zdrowotne)
- [ZUS — kalkulator zdrowotnej 2026](https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej)
- [Wymagania Firemki](../brainstorms/2026-09-07-jdg-requirements.md)
