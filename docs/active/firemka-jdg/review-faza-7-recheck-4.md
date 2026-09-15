# Czwarte ponowne review fazy 7 — bramka końcowa

Data: 2026-09-13  
Status: **GOTOWE DO KONTYNUACJI**

## Wynik

Wynik końcowy: **0× P1, 0× P2, 0× P3** w zaimplementowanym zakresie technicznym fazy 7.

Wszystkie znaleziska z review fazy 7 i trzech ponownych kontroli zostały zamknięte:

- nieobsługiwane profile VAT/ZUS blokują zamknięcie;
- zaksięgowany dokument z nierozwiązanym konfliktem blokuje miesiąc i zmianę odcisku;
- równoległe korekty nie mogą wspólnie obniżyć sumy poniżej zera;
- spóźnioną sprzedaż rozwiązuje tylko bezkwotowa decyzja właściwego typu;
- dokumenty miesiąca są filtrowane w bazie;
- konflikt źródła ma obowiązkowe, właścicielskie wyjaśnienie i może zostać zamknięty;
- każda nowa różnica ma osobny niezmienny wpis oraz prywatny plik, a wcześniejsze decyzje pozostają widoczne;
- identyczne ponowienie nie duplikuje pliku ani historii, a awaria sprząta wyłącznie niedokończony zapis;
- kontrola wersji dokumentu zapobiega rozjechaniu stanu z historią przy równoczesnym rozwiązaniu i nowej różnicy;
- blokada zamknięcia czyta otwarte wpisy bezpośrednio z dopisywanej historii.

## Bezpieczeństwo i dane

- Formularz rozwiązania wymaga logowania, ochrony anty-CSRF i właściciela dokumentu.
- Pierwotne i konfliktowe pliki otwiera ten sam właścicielski, prywatny mechanizm plików.
- Tekst decyzji jest kodowany w HTML, ma limit 2000 znaków i nie jest kopiowany do metadanych audytu.
- Skróty plików uczestniczą w wykrywaniu identycznych różnic; zawartość nie jest nadpisywana.
- Zewnętrzny dostęp testowy KSeF i niezależne potwierdzenie rzeczywistych reguł podatkowych pozostają osobnymi bramkami przed użyciem rzeczywistych danych i produkcją.

## Migracje i zgodność

- `20260913102725_Phase7SourceConflictResolution` dodaje szybki stan rozwiązania konfliktu.
- `20260913104501_Phase7SourceConflictHistory` dodaje dopisywaną historię i przenosi do niej wcześniejsze konflikty bez ich usuwania.
- Rzeczywisty PostgreSQL przeszedł migrację do najnowszej wersji, cofnięcie do fazy 6, migrację pośrednią, przeniesienie starego konfliktu oraz ponowne dojście do końca.
- Wymuszony nieaktualny zapis na PostgreSQL kończy się konfliktem wersji, a zapisany stan zawsze odpowiada jedynemu łańcuchowi historii.

## Dowody

- Pełny zestaw: **224/224 testy zielone** — 1 kontraktowy, 71 domenowych, 4 aplikacyjne, 111 infrastruktury, 2 E2E i 35 WWW.
- Cztery testy PostgreSQL fazy 7 są zielone, w tym migracja, atomowe zamknięcie, równoległe korekty i konflikt źródła.
- Kompilacja Release: 0 ostrzeżeń i 0 błędów.
- Formatowanie: bez zmian.
- Model EF: brak oczekujących zmian po migracjach.
- Cztery syntetyczne próbki XML przechodzą zapisane XSD.
- Wszystkie 12 projektów nie ma zgłoszonych znanych podatności NuGet.

## Decyzja bramki

Faza 7 jest zamknięta technicznie i faza 8 może się rozpocząć w bezpiecznym zakresie na danych syntetycznych. Nie wykonano commita, pushu ani wdrożenia.
