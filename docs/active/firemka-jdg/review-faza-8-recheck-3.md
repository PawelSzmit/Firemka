# Trzecie ponowne review fazy 8 — końcowa bramka

Data: 2026-09-14  
Status: **GOTOWE DO BEZPIECZNEGO ZAKRESU TECHNICZNEGO FAZY 9**

## Wynik

Review nie znalazło otwartego P1, P2 ani P3 w zaimplementowanym zakresie technicznym Fazy 8. Wszystkie problemy z pierwszego review i dwóch kolejnych przeglądów zostały naprawione oraz pokryte testami.

## Potwierdzone obszary

- JPK_V7M poprawnie zapisuje poprzednią nadwyżkę, VAT naliczony, bieżącą nadwyżkę i przeniesienie bez niezamówionego zwrotu. Dokument bez numeru KSeF używa `BFK`, a nie awaryjnego `OFF`.
- ZUS DRA używa terminu `6`; cel złożenia JPK oraz identyfikator ZUS zmieniają się dopiero po rzeczywistym oznaczeniu wysyłki.
- Artefakt wskazuje zamknięcie, kalkulację, dokładną wersję profilu, schematu i generatora. Korekta lub zmiana wejścia tworzy nową niezatwierdzoną wersję z poprzednikiem.
- Nieobsługiwany profil VAT albo ZUS blokuje cały komplet. Generator nie tworzy częściowych dokumentów zastępczych.
- Profil odrzuca brakujące dane, niemożliwą datę urodzenia oraz pustą lub przyszłą datę potwierdzenia liczoną według dnia w Polsce.
- Generowanie i zapis wyniku są odporne na równoległość PostgreSQL. Identyczny wynik jest idempotentny, różny daje czytelny konflikt, a przegrany plik jest sprzątany.
- Prywatny plik deklaracji jest niedostępny przed zatwierdzeniem. UPO i artefakty są filtrowane po właścicielu. Formularze wymagają logowania i ochrony anty-CSRF.
- Ekran pokazuje pełną historię zatwierdzenia, ręcznej wysyłki i wyniku wraz z referencjami oraz czasem `Europe/Warsaw`.
- Istnieje jeden wspólny port przyszłej wysyłki i nie ma jego implementacji ani rejestracji. Żadna czynność Fazy 8 nie łączy się z US ani ZUS.

## Dowody

- Pełny zestaw: **243 testy zaliczone, 7 pominiętych warunkowych testów wcześniejszych faz**.
- Osobny test Fazy 8 na rzeczywistym PostgreSQL: migracja przód/cofnięcie/przód, równoległe generowanie, identyczne i różne UPO, sprzątanie plików oraz regeneracja po zmianie profilu — zaliczony.
- Kompilacja Release: 0 ostrzeżeń, 0 błędów. Format i model EF są czyste.
- Cztery próbki XML przechodzą przypięte XSD. Compose z `.env.example` jest poprawny. Skan 12 projektów nie zgłasza znanych podatności.

## Granice

JPK_PKPIR pozostaje narastającym podglądem technicznym i nie można go oznaczyć jako wysłany przed zamknięciem roku. Właściwe wartości spisu z natury i plik roczny należą do Fazy 9. Ręczny import JPK i KEDU w aktualnych narzędziach urzędowych nadal jest zewnętrzną bramką przed użyciem rzeczywistym. Nie wykonano commita, pushu ani wdrożenia.

## Decyzja

Wynik: **0× P1, 0× P2, 0× P3** w bezpiecznym zakresie technicznym. Faza 9 może się rozpocząć wyłącznie w zakresie lokalnym i na danych syntetycznych.
