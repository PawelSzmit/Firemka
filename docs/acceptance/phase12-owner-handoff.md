# Co pozostało do uruchomienia pilotażu Firemki

Stan na 2026-09-15: aplikacja i lokalna bramka techniczna są gotowe, a Krok 1 zakończył się zatwierdzonym protokołem pierwszego wydania i pierwszym pushem kodu do GitHuba. Pilotaż pozostaje zablokowany. Ten dokument opisuje działania w kolejności, która ogranicza ryzyko. Samo przeczytanie ani wykonanie lokalnych przygotowań nie zmienia VPS-a i nie uruchamia produkcji.

Nie wklejaj do rozmowy haseł, tokenu KSeF, danych SMTP, kodów odzyskiwania ani prywatnego klucza SSH. Codex wskaże właściwe bezpieczne miejsce dopiero podczas konkretnego kroku.

## Krok 1 — utrwalić pierwszą wersję kodu

Potrzebna jest zgoda właściciela na pierwszy lokalny commit. Kolejność jest następująca:

1. Codex sprawdza zakres i sekrety, pomija katalogi tymczasowe `.playwright-cli/` i `tmp/` oraz uruchamia pełną bramkę.
2. Codex tworzy pierwszy lokalny commit zawierający sprawdzony kod, testy, dokumentację i zatwierdzone artefakty odbiorowe.
3. Dopiero ze skrótu tego commita powstaje identyfikator wydania. Z dokładnie tej czystej rewizji Codex buduje i skanuje obrazy WWW, workera, Caddy i PostgreSQL.
4. Codex dopisuje do protokołu skrót kodu, identyfikator wydania, identyfikatory obrazów oraz najnowszą migrację `20260914170341_Phase11Backups`.
5. Protokół jest przygotowany do osobnego, małego commita dowodowego. Zgodnie z późniejszą zgodą właściciela Codex może utworzyć lokalny commit po zielonym review protokołu i wysłać sprawdzone commity do `https://github.com/PawelSzmit/Firemka`; wdrożenie nadal wymaga osobnego polecenia.

A1 można zamknąć dopiero po zapisaniu i zatwierdzeniu tego protokołu. Zgoda na lokalne commity i push do wskazanego repozytorium nie jest zgodą na wdrożenie.

## Krok 2 — przygotować docelowy VPS bez zmieniania go

Właściciel podaje nazwę domeny oraz potwierdza, pod jaką zapisaną nazwą Mac łączy się z VPS-em. Nie podaje hasła ani klucza. Właściciel wybiera też miejsce alarmu, które naprawdę zauważy, na przykład istniejący monitoring hosta lub inną uzgodnioną usługę.

Codex najpierw wykona tylko odczytowy audyt systemu, aktualizacji, zapory, portów, miejsca na dysku i Dockera. Każde ostrzeżenie zostanie przedstawione osobno przed zmianą. Ten etap przygotowuje A3, ale niczego sam nie naprawia.

## Krok 3 — osobno zatwierdzić ograniczone wdrożenie

Właściciel zezwolił już na push sprawdzonych commitów do wskazanego repozytorium GitHub. Dopiero osobne polecenie może zezwolić na wdrożenie. Przed wdrożeniem trzeba potwierdzić punkt powrotu. Pierwsze uruchomienie pozostawia produkcyjny KSeF, SMTP i automatyczne wystawianie wyłączone.

Codex uruchomi migrację jako osobny krok, sprawdzi HTTPS, certyfikat, nagłówki, `/health`, prywatność bazy, ograniczenia kontenerów i działanie alarmu. Po wdrożeniu obserwacja trwa co najmniej 30 minut. Ten etap obejmuje A4–A5; A3 zostanie zamknięte dopiero po zielonym audycie prawdziwego VPS-a.

## Krok 4 — przejść próby telefonu i Maca

Właściciel wykonuje kliknięcia, a Codex prowadzi krok po kroku i zapisuje wyłącznie wyniki bez sekretów:

1. telefon 390 px: hasło, TOTP, „Mój miesiąc”, podgląd dokumentu i zatwierdzenie;
2. Mac: logowanie, pobranie prywatnego pliku, wylogowanie i ponowne 2FA na nowym urządzeniu;
3. unieważnienie wszystkich zaufanych urządzeń;
4. użycie jednego kodu odzyskiwania dwa razy — pierwsze ma zadziałać, drugie zostać odrzucone;
5. symulacja utraty telefonu z odzyskaniem dostępu bez wyłączania 2FA.

Ten etap obejmuje C1–C5. Kodów i haseł nie zapisujemy w dowodzie.

## Krok 5 — przygotować niezależny miesiąc referencyjny

Właściciel uzgadnia z księgowym lub doradcą jeden zanonimizowany miesiąc, profil podatkowy i wersje zasad. Potrzebne są potwierdzone wartości przychodu, KPiR, VAT, PIT, ZUS oraz terminy. Oferta samochodu i podstawa księgowania domowego ładowania muszą być rozstrzygnięte oddzielnie.

Codex wprowadzi wyłącznie uzgodnione dane, porówna każdą pozycję i nie zaznaczy różnicy bez wyjaśnienia. Ten etap obejmuje B1–B3. B4–B6 są już zaliczone wyłącznie technicznie na danych syntetycznych.

## Krok 6 — przetestować usługi i pliki urzędowe

Właściciel przygotowuje dostęp testowy KSeF, testową skrzynkę SMTP z TLS oraz aktualne narzędzia do JPK i ZUS. Sekrety trafiają do bezpiecznej konfiguracji poza repozytorium, nie do rozmowy.

Codex prowadzi pobranie i wysłanie testowej faktury, oczekiwanie i wynik niepewny, przyjęcie, odrzucenie, poprawę, numer KSeF i UPO. Właściciel wykonuje ręczny import JPK i ZUS w oficjalnych narzędziach. Na końcu ponownie sprawdzamy, że integracje produkcyjne są wyłączone. Ten etap obejmuje D1–D7.

## Krok 7 — skonfigurować prawdziwą kopię i odtworzenie

Właściciel wskazuje folder na Macu oraz tworzy hasło odzyskiwania z drugą kopią przechowywaną poza Makiem. Token i hasło trafiają do Pęku kluczy, nigdy do repozytorium ani rozmowy.

Codex prowadzi instalację klienta, ręczną i zaległą kopię, rotację pięciu plików, archiwum roczne oraz przeniesienie kopii na drugi czysty komputer lub VPS. Odtworzenie musi zakończyć się zgodnymi licznikami i skrótami, zielonym `/health`, działającym 2FA i plikami. Złe hasło oraz uszkodzona kopia muszą zostać odrzucone bez zmiany danych. Ten etap obejmuje E1–E6.

## Krok 8 — przećwiczyć awarie i podjąć decyzję

Na środowisku pilotażowym przechodzimy awarie KSeF, SMTP, workera, miejsca na dysku i certyfikatu, a potem próbną aktualizację oraz bezpieczny powrót. Pierwszy rzeczywisty miesiąc pozostaje całkowicie ręczny. Na końcu właściciel sprawdza nierozstrzygnięte reguły, wybiera wynik i datę ponownej oceny.

Ten etap obejmuje F1–F6. Dopiero F6 oraz komplet A1–F5 pozwalają zmienić status checklisty z „NIEPODPISANA”.

## Najbliższa potrzebna decyzja

Krok 1 jest zakończony lokalnie. Do Kroku 2 właściciel podaje nazwę domeny, zapisaną nazwę połączenia Maca z VPS-em oraz wybiera kanał alarmowy, który rzeczywiście zauważy. Nie podaje hasła, tokenu ani prywatnego klucza.
