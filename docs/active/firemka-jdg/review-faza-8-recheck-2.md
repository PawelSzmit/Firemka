# Drugie ponowne review fazy 8

Data: 2026-09-14  
Status: **POPRZEDNIE PROBLEMY ZAMKNIĘTE; 1× P2 I 1× P3 DO NAPRAWY**

## Zamknięte problemy z poprzedniego review

- Nieobsługiwany albo historycznie zmieniony profil VAT blokuje cały komplet i nie zostawia artefaktów.
- Pusta oraz przyszła data potwierdzenia profilu są odrzucane względem polskiej daty biznesowej.
- Brakujący tekst przy istniejącym profilu daje kontrolowany błąd walidacji.
- Pozostał jeden wcześniejszy `Application.ExternalServices.ISubmissionGateway`; żadna implementacja wysyłkowa nie jest zarejestrowana.

## Nowe znaleziska

### P2 — ekran nie pokazuje zapisanej historii ręcznej wysyłki

Artefakt trwale zapisuje opis i datę zatwierdzenia, opis i datę ręcznej wysyłki oraz opis i datę wyniku. Ekran pokazuje jednak tylko bieżący status, datę utworzenia i końcową referencję wyniku. Po przejściu do `Accepted` albo `Rejected` właściciel nie widzi już, kiedy i gdzie oznaczył wysyłkę, ani na jakiej podstawie zatwierdził konkretną wersję (`Filings.cshtml`, wiersze 58–68 i 111–115). To nie spełnia wymaganego czytelnego rozdzielenia „zatwierdzono”, „wysłano” i „przyjęto/odrzucono”.

**Wymagana naprawa:** pokazać przy każdej wersji wszystkie zapisane etapy wraz z polską datą i referencją. Test strony ma przejść przez zatwierdzenie, ręczną wysyłkę i wynik, a następnie potwierdzić widoczność całej historii.

### P3 — data utworzenia zależy od strefy czasowej serwera

Widok używa `CreatedAtUtc.ToLocalTime()` (`Filings.cshtml`, wiersz 62). Na docelowym VPS „czas lokalny” może oznaczać UTC, a nie Warszawę. Ten sam zapis będzie więc wyświetlany inaczej na MacBooku i serwerze, szczególnie przy granicy dnia.

**Wymagana naprawa:** dodać jedno wspólne przeliczenie chwili na `Europe/Warsaw`, użyć go dla całej historii Fazy 8 i pokryć przejście przez północ testem jednostkowym.

## Decyzja bramki

Wynik: **0 otwartych wcześniejszych P1/P2/P3; 1 nowy P2 i 1 nowy P3**. Faza 9 pozostaje zablokowana do naprawy i kolejnego review. Zewnętrzny import JPK/KEDU nadal pozostaje bramką przed rzeczywistym użyciem. Nie wykonano commita, pushu ani wdrożenia.
