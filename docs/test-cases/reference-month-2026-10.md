# Firemka — scenariusze referencyjne miesiąca

Status: strukturalne scenariusze produktu; kwoty podatkowe pozostają puste do niezależnego potwierdzenia.

## Dlaczego nie wpisujemy teraz kwot podatkowych

Brakuje danych firmy, dokumentów i potwierdzonej kwalifikacji podatkowej. Wpisanie przykładowego PIT/VAT/ZUS jako oczekiwanego wyniku mogłoby później zostać błędnie użyte jak prawdziwa reguła. Poniższe przypadki są więc gotowe do automatyzacji zachowania produktu, ale nie do potwierdzenia podatku.

## M-01 — zwykły miesiąc

**Wejście:** abonament za październik, zatwierdzona faktura sprzedaży, komplet zatwierdzonych dokumentów kosztowych.  
**Oczekiwane zachowanie:** miesiąc pokazuje pełną listę dokumentów i prognozę. Po „Zatwierdź rozliczenie miesiąca” powstaje wersja zamknięcia oraz wersje dokumentów, ale nie następuje wysyłka ani oznaczenie płatności.  
**Dane wymagane przed wynikiem podatkowym:** dokumenty, kwoty, VAT, kursy, KPiR/VAT/PIT/ZUS potwierdzone przez specjalistę.

## M-02 — brak przychodu

**Wejście:** brak faktury sprzedaży, dokumenty kosztowe dla okresu.  
**Oczekiwane zachowanie:** system nie tworzy fikcyjnej faktury; pokazuje wynik z dostępnych danych i braki, jeśli reguła dokumentu nie jest ustalona.  
**Dane wymagane przed wynikiem podatkowym:** potwierdzone zasady PIT/VAT/ZUS dla danego miesiąca.

## M-03 — strata

**Wejście:** zatwierdzone koszty przewyższające przychód.  
**Oczekiwane zachowanie:** obliczenie oraz zamknięcie zachowują stratę jako dane, nie zastępują jej zerem i pokazują wyjaśnienie.  
**Dane wymagane przed wynikiem podatkowym:** właściwy sposób rozliczenia kosztów oraz skutki dla PIT i zdrowotnej.

## M-04 — korekta po zamknięciu

**Wejście:** poprawka zaksięgowanej faktury po utworzeniu zamknięcia.  
**Oczekiwane zachowanie:** zachowana jest wcześniejsza wersja; nowa wersja pokazuje różnicę, wskazuje dokumenty wymagające korekty i czeka na zatwierdzenie.  
**Niezmiennik:** nie wolno cicho podmienić historycznej wersji ani wysłać korekty automatycznie.

## M-05 — faktura za poprzedni okres zatwierdzona później

**Wejście:** wersja robocza za wrzesień zatwierdzona 2026-10-02.  
**Oczekiwane zachowanie:** data wystawienia `2026-10-02`, termin płatności `2026-10-09`, okres usługi nadal wrzesień; system ostrzega o opóźnieniu i nie pomija odrębnej wersji roboczej za październik.

## M-06 — domowe ładowanie

**Wejście:** `10000 Wh`, stawka obowiązująca `0,91 zł brutto/kWh`.  
**Oczekiwany wynik matematyczny:** `10 kWh`, `9,10 zł brutto`.  
**Niezmiennik:** wygenerowanie zestawienia nie tworzy automatycznie wpisu KPiR ani VAT bez zatwierdzonej podstawy.

## M-07 — zmiana reguły kosztowej

**Wejście:** zatwierdzona reguła, potem dokument z inną kwotą oraz drugi z inną walutą.  
**Oczekiwane zachowanie:** zmiana wyłącznie kwoty może przejść automatycznie; zmiana waluty zatrzymuje dokument i pokazuje powód. Edycja jednego dokumentu nie zmienia reguły przyszłej bez jawnego wyboru.

## M-08 — brak danych blokujący miesiąc

**Wejście:** dokument firmowy bez wymaganych danych lub bez reguły.  
**Oczekiwane zachowanie:** zamknięcie jest zablokowane z czytelną listą spraw. Właściciel może oznaczyć dokument jako niezwiązany z firmą, zachowując uzasadnienie.

## Uzupełnienie przed użyciem w testach kalkulatora

Każdy przypadek otrzyma identyfikator danych, wersję reguł, pełne wartości wejściowe i niezależnie potwierdzone wyniki. Dopiero wtedy powstanie automatyczny test „golden master”.

## Źródła

- [Wymagania Firemki](../brainstorms/2026-09-07-jdg-requirements.md)
- [Rejestr decyzji podatkowych](../research/tax-decisions-2026.md)
