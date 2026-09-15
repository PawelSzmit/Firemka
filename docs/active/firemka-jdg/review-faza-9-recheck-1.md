# Ponowne review fazy 9 — końcowa bramka

Data: 2026-09-14  
Status: **GOTOWE DO BEZPIECZNEGO ZAKRESU TECHNICZNEGO FAZY 10**

## Wynik

Ponowne review nie znalazło otwartego P1, P2 ani P3 w zaimplementowanym zakresie technicznym Fazy 9. Jedno P1 i cztery P2 z pierwszego przeglądu zostały naprawione oraz pokryte testami.

## Potwierdzone naprawy

- Ponowne zamknięcie korekty tworzy osobny komplet nowych migawek miesięcy. Wersja 1 i wersja 2 zachowują po trzy różne rekordy i nie modyfikują własnej historii.
- Wspólna blokada roczna obejmuje zamknięcie roku, zapis danych rocznych, rozpoczęcie korekty roku oraz rozpoczęcie i zamknięcie korekty miesiąca. Wymuszony wyścig nigdy nie pozostawia zamkniętego roku z otwartym miesiącem.
- Dwa równoczesne rozpoczęcia tej samej korekty roku zwracają tę samą wersję. Różna zmiana kończy się kontrolowanym komunikatem, a nie błędem bazy.
- Zatwierdzenie i oznaczenie ręcznej wysyłki są dostępne wyłącznie dla najnowszej wersji rocznego JPK. Historyczna wersja, która została wysłana wcześniej, nadal może później otrzymać UPO albo wynik odrzucenia.
- Faktura rozpoznana w grudniu, lecz wystawiona w styczniu następnego roku, otrzymuje w rocznym JPK datę należącą do potwierdzonego miesiąca i roku. Test sprawdza wartość `K_2` w wygenerowanym XML.
- Dwa identyczne wyniki z tym samym UPO są idempotentne. Przy dwóch różnych wynikach dokładnie jeden wygrywa, drugi otrzymuje czytelny konflikt, a przegrany plik jest usuwany.

## Dowody

- Pełny zestaw: **256 testów zaliczonych, 9 warunkowych testów PostgreSQL pominiętych w zwykłym przebiegu**.
- Dwa osobne testy Fazy 9 na rzeczywistym PostgreSQL: migracja przód/cofnięcie/przód, równoległe zamknięcie, zamknięcie korekty, identyczna korekta, identyczne i różne UPO, sprzątanie plików oraz wyścig roku z miesiącem — zaliczone.
- Kompilacja Release: 0 ostrzeżeń, 0 błędów. Formatowanie i model EF są czyste.
- Cztery próbki XML przechodzą przypięte XSD. Compose z `.env.example` jest poprawny. Skan wszystkich 12 projektów nie zgłasza znanych podatności.
- Wcześniej wyrenderowany roczny PDF A4 nadal odpowiada temu samemu generatorowi; naprawy nie zmieniały układu ani treści PDF.

## Granice

Roczny PDF pozostaje zestawieniem pomocniczym, a nie pełnym PIT-36/PIT/B. Roczny JPK wymaga ręcznego importu i wysyłki w aktualnym narzędziu urzędowym; produkcyjne użycie nadal wymaga zewnętrznej próby na zatwierdzonych danych. Zaszyfrowane archiwum i klient Mac pozostają zakresem Fazy 11. Nie wykonano commita, pushu ani wdrożenia.

## Decyzja

Wynik: **0× P1, 0× P2, 0× P3** w bezpiecznym zakresie technicznym. Faza 10 może się rozpocząć lokalnie, bez uruchamiania produkcyjnych płatności ani wysyłki e-mail do rzeczywistych odbiorców.
