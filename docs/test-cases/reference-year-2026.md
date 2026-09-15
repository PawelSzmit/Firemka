# Firemka — scenariusze referencyjne roku 2026

Status: strukturalne scenariusze zamknięcia roku; nie zawiera niepotwierdzonych kwot podatkowych.

## Y-01 — zamknięcie pierwszego roku

**Założenie produktu:** działalność zaczyna się w 2026, prawdopodobnie w październiku; dokładna data zostanie wprowadzona w profilu firmy.  
**Wejście:** wszystkie miesiące od daty startu są zatwierdzone, brak nierozwiązanych dokumentów.  
**Oczekiwane zachowanie:** aplikacja pokazuje roczne podsumowanie, wymagane dane KPiR, dane rocznej zdrowotnej oraz dane firmowe do zewnętrznego PIT; właściciel osobno klika „Zamknij rok”.

## Y-02 — blokada zamknięcia roku

**Wejście:** choć jeden miesiąc jest niezatwierdzony lub ma brak.  
**Oczekiwane zachowanie:** rok nie może się zamknąć; system wskazuje konkretny miesiąc i problem.

## Y-03 — PDF dla zewnętrznego PIT

**Wejście:** prawidłowo zamknięty rok.  
**Oczekiwane zachowanie:** właściciel może pobrać PDF danych firmowych oraz pobrać go ponownie. PDF pozostaje związany z konkretną wersją rocznego zamknięcia.

## Y-04 — korekta zamkniętego roku

**Wejście:** właściciel wybiera „Rozpocznij korektę” i podaje powód.  
**Oczekiwane zachowanie:** wcześniejsza wersja i dokumenty pozostają dostępne; nowa wersja pokazuje różnice i przygotowuje nowe dokumenty do zatwierdzenia. Zwykła edycja zamkniętego roku jest zablokowana.

## Dane, bez których test nie może mieć wartości podatkowej

- potwierdzona data startu i zestaw zamkniętych miesięcy;
- rzeczywiste dane KPiR/VAT/PIT/ZUS każdego miesiąca;
- wynik rocznej zdrowotnej potwierdzony niezależnie;
- wymagany zestaw JPK oraz jego wersja;
- pola przekazywane do zewnętrznej aplikacji PIT.

## Źródła

- [Wymagania Firemki](../brainstorms/2026-09-07-jdg-requirements.md)
- [JPK w podatkach dochodowych](https://www.gov.pl/web/kas/struktury-jpk-w-podatkach-dochodowych)
- [Rejestr decyzji podatkowych](../research/tax-decisions-2026.md)
