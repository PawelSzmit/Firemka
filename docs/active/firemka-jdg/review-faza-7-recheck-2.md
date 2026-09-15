# Drugie ponowne review fazy 7 — historia rozwiązania konfliktu

Data: 2026-09-13  
Status: **WYMAGA POPRAWKI PRZED FAZĄ 8**

## Wynik sprawdzenia ostatniego P2

Dodana ścieżka prawidłowo wymaga wyjaśnienia, sprawdza właściciela, zapisuje audyt, zachowuje status zaksięgowanego dokumentu i usuwa blokadę miesiąca tylko po rozwiązaniu konfliktu. Ta część ma zielone testy strony, serwisu, domeny i PostgreSQL.

## Nowe znalezienie

### P2 — kolejny konflikt nadpisuje decyzję, a różniące się źródło nie jest zachowane

Model przechowuje tylko jeden bieżący skrót, datę i tekst rozwiązania. Gdy po rozwiązaniu pojawi się rzeczywiście inna treść, pola poprzedniego rozwiązania są zerowane, a data wykrycia zastępowana. W efekcie wcześniejsza decyzja znika z dokumentu. Dodatkowo synchronizacja oblicza skrót nowego XML, lecz nie zapisuje prywatnej kopii tej różniącej się treści. Właściciel widzi więc prośbę o porównanie źródeł, ale w aplikacji ma dostęp tylko do pierwotnie zachowanego pliku.

To narusza przyjętą zasadę dopisywania historii zamiast jej nadpisywania i osłabia dowód dla decyzji odblokowującej zamknięcie miesiąca.

**Wymagana naprawa:** każdy nowy konflikt ma tworzyć osobny, niezmienny wpis historii z datą, skrótem i prywatnie zachowanym różniącym się plikiem. Rozwiązanie ma uzupełniać wyłącznie otwarty wpis o datę i wyjaśnienie. Szczegóły dokumentu powinny umożliwiać właścicielowi otwarcie obu źródeł i pokazywać wcześniejsze decyzje. Powtórzenie identycznej różnicy nie może tworzyć kolejnego wpisu ani pliku. Zapis konfliktowego pliku, wpisu i audytu musi być transakcyjny oraz sprzątać plik fizyczny po kontrolowanej awarii.

## Decyzja bramki

Wynik: **0× P1, 1× P2, 0× P3**. Faza 8 nadal jest zablokowana. Następny krok to `dev-docs-execute` ograniczony do dopisywanej historii konfliktów i zachowania alternatywnego źródła, a potem trzecie ponowne `dev-docs-review` fazy 7. Nie wykonano commita, pushu ani wdrożenia.
