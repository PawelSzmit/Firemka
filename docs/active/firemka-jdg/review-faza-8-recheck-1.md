# Ponowne review fazy 8 — po pierwszej naprawie

Data: 2026-09-14  
Status: **PIERWOTNE PROBLEMY ZAMKNIĘTE; 2× P2 I 2× P3 DO NAPRAWY PRZED FAZĄ 9**

## Zamknięte znaleziska pierwotnego review

- JPK_V7M poprawnie rozdziela poprzednią nadwyżkę, sumę VAT naliczonego, bieżącą nadwyżkę i przeniesienie; nie żąda zwrotu bez decyzji właściciela.
- ZUS DRA ma kod terminu `6`, a identyfikatory ZUS i cele złożenia JPK wynikają z rzeczywistych oznaczonych wysyłek.
- Artefakt wskazuje dokładną wersję profilu i generatora, a zmiana wejścia tworzy nową niezatwierdzoną wersję.
- Identyczny równoczesny wynik/UPO jest idempotentny, różny daje czytelny konflikt, a przegrany plik jest usuwany.
- Puste PESEL i kody nie kończą się już `NullReferenceException`.

## Nowe znaleziska

### P2 — zmieniony profil VAT może po cichu usunąć JPK_V7M z kompletu zamkniętego miesiąca

Faza 7 pozwala zamknąć miesiąc tylko dla czynnego VAT miesięcznego. Po zamknięciu można jednak dodać nowy okres profilu VAT obejmujący ten historyczny miesiąc. `ResolveKinds` nie odrzuca takiej rozbieżności: dla profilu innego niż `ActiveMonthly` po prostu nie dodaje JPK_V7M i nadal tworzy JPK_PKPIR oraz ZUS DRA (`FilingService.cs`, wiersze 764–779). Powstaje częściowy komplet oparty na kalkulacji zamkniętej przy innym profilu, bez komunikatu o wymaganej korekcie.

**Wymagana naprawa:** w obecnym zakresie obsługującym wyłącznie czynny VAT miesięczny zmieniony/nieobsługiwany profil ma twardo blokować cały eksport czytelnym komunikatem, tak jak profil ZUS. Dodać test: zamknięty miesiąc → historyczna zmiana profilu VAT → brak jakiegokolwiek artefaktu.

### P2 — potwierdzenie profilu może mieć pustą albo przyszłą datę

Profil urzędowy zapisuje `ConfirmedOn` bez sprawdzenia względem daty utworzenia (`FilingProfileVersion.cs`, wiersze 89–105). Formularz ma pole wymagane, lecz `DateOnly` przy bezpośrednim wywołaniu może mieć wartość domyślną, a przeglądarka pozwala wpisać przyszłą datę. Taki profil jest traktowany jako potwierdzony i odblokowuje generowanie dokumentów urzędowych, mimo niewiarygodnego dowodu kontroli.

**Wymagana naprawa:** odrzucić pustą oraz przyszłą datę potwierdzenia; porównanie wykonywać z polską datą biznesową przekazanego czasu. Dodać test domeny i test strony dla przyszłej daty.

### P3 — powtórne zapisanie profilu z brakującym tekstem może nadal dać techniczny wyjątek

Gdy profil już istnieje, serwis najpierw wykonuje `Matches`. Metoda wywołuje `Trim()` na imieniu, nazwisku i opisie dowodu (`FilingService.cs`, wiersze 934–943), zanim fabryka domenowa wykona kontrolowaną walidację. Klient inny niż formularz może więc otrzymać `NullReferenceException`.

**Wymagana naprawa:** przed porównaniem rozpoznać brakujące wymagane teksty i przekazać je do kontrolowanej walidacji domenowej. Pokryć przypadek testem serwisu dla istniejącego profilu.

### P3 — istnieją dwie różne granice `ISubmissionGateway`

Port przyszłej wysyłki został już utworzony w Fazie 2 w `Application.ExternalServices`. Faza 8 dodała drugi, niezgodny kontrakt o tej samej nazwie w `Application.Filings` (`FilingContracts.cs`, wiersze 125–132). Test sprawdza tylko nowszy typ, co pozostawia niejednoznaczną architekturę i ryzyko rejestracji niewłaściwej usługi w przyszłości.

**Wymagana naprawa:** zachować jeden wcześniejszy port `Application.ExternalServices.ISubmissionGateway`, usunąć duplikat i sprawdzić brak rejestracji dokładnie tego wspólnego kontraktu.

## Pozostałe kontrole

- Zwykły dokument elektroniczny lub papierowy bez numeru KSeF otrzymuje `BFK`; `OFF` nie jest nadawany automatycznie.
- JPK_PKPIR jest oznaczony jako podgląd narastający i nie może zostać oznaczony jako wysłany przed Fazą 9. Oficjalny roczny termin oraz pola spisu z natury pozostają jawnie udokumentowane.
- Pliki i UPO są prywatne, pobranie deklaracji wymaga zatwierdzenia, strony wymagają logowania i zachowują ochronę formularzy.
- Nie ma zarejestrowanej implementacji wysyłki do US/ZUS; ręczny import w aktualnych narzędziach urzędowych pozostaje zewnętrzną bramką P1 przed rzeczywistym użyciem.
- Dowody automatyczne: 238 zielonych testów i 7 pominiętych wcześniejszych prób PostgreSQL, osobny zielony test Fazy 8 na PostgreSQL, Release 0/0, czysty format i model EF, cztery poprawne próbki XSD, poprawna konfiguracja Compose oraz brak zgłoszonych podatności w 12 projektach.

## Decyzja bramki

Wynik: **0 otwartych pierwotnych P1/P2/P3; 2 nowe P2 i 2 nowe P3**. Faza 9 pozostaje zablokowana do naprawy i kolejnego `dev-docs-review`. Nie wykonano commita, pushu ani wdrożenia.
