# Powiązanie wymagań R1–R46 z dowodami

Status „techniczny” oznacza dowód na danych syntetycznych. Nie zastępuje pozycji „bramka zewnętrzna”, gdy potrzebne są rzeczywiste dane, urządzenie lub usługa.

| Wymóg | Stan | Główny dowód lub brakująca bramka |
|---|---|---|
| R1 | Techniczny + bramka zewnętrzna | Responsywne strony i testy 390/1440 px; prawdziwy VPS i telefon w checkliście Fazy 12. |
| R2 | Częściowy | Moduły ksiąg, VAT/PIT/ZUS i granice KSeF istnieją; rzeczywiste KSeF oraz import urzędowy są otwarte. |
| R3 | Techniczny + bramka danych | Profil skali/VAT/ZUS jest wersjonowany; dane firmy i potwierdzenie księgowe są otwarte. |
| R4 | Techniczny | Tylko skala jest dostępna; ryczałt nie ma aktywnego przełącznika. |
| R5 | Techniczny + bramka danych | Znana reguła księguje automatycznie; prawdziwe dokumenty wymagają odbioru. |
| R6 | Częściowy | AI/VPS/samochód mają ścieżki; internet i konkretna umowa samochodu wymagają danych. |
| R7 | Techniczny | Pierwszy rodzaj wymaga zatwierdzenia reguły, kolejne zgodne używają jej automatycznie. |
| R8 | Techniczny | Wyjątek dokumentu i zmiana reguły na przyszłość są osobnymi czynnościami. |
| R9 | Techniczny + KSeF test | Szkic nie wysyła się bez kliknięcia; realna wysyłka testowa pozostaje otwarta. |
| R10 | Techniczny + bramka pilotażu | Automatyzacja jest domyślnie wyłączona i wymaga dwóch zgód; nie wolno jej włączać przed pilotażem. |
| R11 | Techniczny | Okresowe stawki abonamentu i pojedynczy wyjątek nie zmieniają wystawionych faktur. |
| R12 | Techniczny | Harmonogram tworzy szkic 1. dnia z siedmiodniowym terminem. |
| R13 | Techniczny | Ręczne zatwierdzenie w tym samym miesiącu używa bieżącej daty i pełnego okresu. |
| R14 | Techniczny + próbki realne | PDF/zdjęcie, OCR i ręczna korekta działają na syntetycznych plikach; realna próbka jest otwarta. |
| R15 | Techniczny + import urzędowy | Pliki i historia ręcznej wysyłki istnieją; import w narzędziach urzędowych jest otwarty. |
| R16 | Przygotowana granica | Port przyszłej wysyłki zachowuje wersje; automatyczna wysyłka US/ZUS jest poza MVP. |
| R17 | Techniczny + miesiąc referencyjny | Podsumowanie i blokady poprzedzają świadome zamknięcie; porównanie z księgowym jest otwarte. |
| R18 | Techniczny | Korekta zachowuje historię, wpływ na kwoty i nowe wersje dokumentów. |
| R19 | Techniczny | Tylko pełna ręczna płatność zamyka należność; kwota częściowa jest odrzucana. |
| R20 | Poza MVP | Model płatności ma źródło i identyfikator importu; sam import wyciągu należy do F1. |
| R21 | Poza MVP | Zasada dopasowań jest zapisana w planie F1; nie jest aktywna w pierwszej wersji. |
| R22 | Techniczny + SMTP | Trwałe oszczędne powiadomienia istnieją; rzeczywista skrzynka i dostarczenie są otwarte. |
| R23 | Techniczny + umowa | Cztery jawne polityki samochodu są obsługiwane; właściwa wymaga oferty i potwierdzenia. |
| R24 | Techniczny + podstawa księgowa | CSV i publiczne faktury mają ścieżki; podstawa domowego kosztu pozostaje do potwierdzenia. |
| R25 | Techniczny | Test 10 000 Wh → 10 kWh → 9,10 zł przechodzi. |
| R26 | Techniczny | Historia stawek od miesiąca nie przelicza wcześniejszych raportów. |
| R27 | Techniczny + bramka podatkowa | Stały format ładowarki generuje zestawienie; nie zatwierdza sam podstawy podatkowej. |
| R28 | Techniczny | Prywatna faktura energii nie jest automatycznie księgowana jako koszt. |
| R29 | Techniczny + umowa | Najem i leasing są rozdzielone; decyzja czeka na konkretną ofertę. |
| R30 | Techniczny + dokumenty | Reguły nie ufają samej marce; rzeczywiste faktury OpenAI/Anthropic/OVH są bramką. |
| R31 | Techniczny + weryfikacja | Roczny PDF zawiera część firmową i jawnie wyłącza pełny PIT; dane wymagają porównania. |
| R32 | Techniczny | Nierozwiązany dokument blokuje miesiąc, a decyzja „niezwiązany” pozostaje w historii. |
| R33 | Techniczny | Pierwszy rozpoczęty miesiąc jest pełny, bez proporcjonalnego zmniejszenia. |
| R34 | Techniczny | Zmiana podatkowo istotnych danych zatrzymuje regułę, zwykła zmiana kwoty nie. |
| R35 | Techniczny | Spóźniona faktura zachowuje okres i nie usuwa faktury kolejnego miesiąca. |
| R36 | Techniczny + rok referencyjny | Rok wymaga zamkniętych miesięcy i osobnego kliknięcia; wartości wymagają odbioru. |
| R37 | Techniczny | Prywatny roczny PDF jest dostępny ponownie i ma kontrolę treści/renderowania. |
| R38 | Techniczny | Korekta roku wymaga powodu i pozostawia wszystkie poprzednie wersje. |
| R39 | Techniczny + SMTP | Harmonogram, czas warszawski i dzienna deduplikacja są testowane; dostarczenie jest otwarte. |
| R40 | Techniczny + prawdziwy Mac | Rotacja, nadrabianie i raport błędu działają syntetycznie; instalacja `launchd` jest otwarta. |
| R41 | Techniczny + drugie urządzenie | Pełny pojedynczy pakiet i archiwum roczne są testowane; fizyczne przeniesienie jest otwarte. |
| R42 | Techniczny + hasło offline | Szyfrowanie i złe hasło są testowane; właściciel musi utworzyć i zachować sekret. |
| R43 | Techniczny + próba właściciela | Hasło, TOTP i jednorazowy kod odzyskiwania są testowane; TOTP właściciela działa lokalnie. |
| R44 | Techniczny + urządzenia | Zaufanie 30 dni i unieważnienie są testowane; scenariusz utraty telefonu/Maca jest otwarty. |
| R45 | Techniczny + odbiór urządzeń | Dashboard scala czynności, kwoty, terminy i kopię; finalna próba telefonu/Maca jest otwarta. |
| R46 | Techniczny | Nowa stawka aktualizuje przyszłe szkice, zachowuje wystawione i chroni ręczny wyjątek. |

## Kryterium decyzji

Lokalny zakres techniczny można uznać za gotowy po zielonym `verify-phase12-local.sh` i review bez P1/P2 w kodzie. Pilotaż pozostaje zablokowany, dopóki wszystkie pozycje oznaczone „bramka” nie mają dowodu w podpisanej checkliście. Funkcje R20–R21 pozostają jawnie poza pierwszą wersją i nie mogą być przedstawiane jako gotowe.
