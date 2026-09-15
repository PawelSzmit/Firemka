# Firemka — źródła urzędowe i kontrakty techniczne

Sprawdzone: 2026-09-09  
Cel: utrwalić dokładne wejścia do przyszłych testów kontraktowych, bez zamrażania przepisów na przyszłość.

## Zasada użycia

Pliki w `contracts/` są migawką techniczną pobraną 2026-09-09. Ich suma kontrolna potwierdza dokładnie to, co zostało pobrane tego dnia. Nie oznacza to, że należy użyć tej samej wersji po zmianie komunikatu MF albo ZUS. Przed implementacją danego eksportu i przed każdym użyciem produkcyjnym należy:

1. sprawdzić stronę źródłową i komunikaty zmian;
2. pobrać aktualny kontrakt do nowego pliku z datą;
3. policzyć SHA-256;
4. porównać różnicę semantyczną oraz uruchomić zestaw testów kontraktowych;
5. zapisać decyzję w dokumentacji wydania.

## Pobrane kontrakty

| Zastosowanie | Lokalny plik | Oficjalne źródło | Wersja / data obowiązywania | SHA-256 | Kontrola |
|---|---|---|---|---|---|
| API KSeF — środowisko integracyjne | `contracts/ksef-test-openapi-2026-09-09.json` | `https://api-test.ksef.mf.gov.pl/docs/v2/openapi.json` | OpenAPI 3.0.4; strona integratorów zmieniona 2026-06-09 | `17618897cd67fbf324b869d2c609a11fff88cfb480144fefdf46991059597f7e` | `jq empty` |
| e-Faktura FA(3) | `contracts/fa3-2026-09-09.xsd` | `https://crd.gov.pl/wzor/2025/06/25/13775/schemat.xsd` | FA(3), od 2026-02-01 zastępuje FA(2) | `b646b6b525f51adf1bb2545f111fc8ca6e7aa6dd2f98948f1667d3695c06d958` | `xmllint --noout` |
| JPK_V7M(3) | `contracts/jpk-v7m3-2026-09-09.xsd` | `https://crd.gov.pl/wzor/2025/12/19/14090/schemat.xsd` | JPK_V7M(3), od 2026-02-01 | `1870324001ba1318f0535841b963571e970b45ef32ba5ca3d433f0dc5da412df` | `xmllint --noout` |
| JPK_PKPIR(3) | `contracts/jpk-pkpir3-2026-09-09.xsd` | `https://www.gov.pl/attachment/70b78fa0-9183-400d-a967-eeacdcb87e57` | JPK_PKPIR(3), strona struktur wersja 12.0 z 2026-08-04 | `a09c8f5f0812fa78fb103366d77fd8e31b45e9f9cfdb0bdb33a3950e578a4498` | `xmllint --noout` |
| KEDU ZUS | `contracts/kedu-2.27-2026-09-09.xsd` | [BIP ZUS — KEDU 2.27](https://bip.zus.pl/pl/inne/wymagania-dla-oprogramowania-interfejsowego/dokumenty-ubezpieczeniowe) | specyfikacja 2.27, ważna od 2026-04-25; namespace `KEDU_5_7`, schema `5.7.0` | `7f853a09c26a5a5d35f03e2eaa310de70a0a21efa7740d9be27e718741bbbaf5` | `xmllint --noout` |

Pełna lista sum znajduje się w [SHA256SUMS](contracts/SHA256SUMS). Sprawdzenie należy uruchomić z katalogu kontraktów:

```text
(cd docs/research/contracts && shasum -a 256 -c SHA256SUMS)
```

Wszystkie pliki zostały sprawdzone parserem JSON albo XML; to dowodzi tylko ich poprawności składniowej, nie poprawności wyliczeń podatkowych.

### Lokalna walidacja dokumentów XML

Pobrane XSD odwołują się do zewnętrznych XSD przez `schemaLocation`. Nie zmieniamy migawkowych kontraktów: [`contracts/catalog.xml`](contracts/catalog.xml) wiąże ich dokładne adresy z lokalnymi kopiami opisanymi w [`contracts/dependencies/README.md`](contracts/dependencies/README.md). Cztery sztuczne lub urzędowe próbki techniczne są opisane w [`contracts/fixtures/README.md`](contracts/fixtures/README.md).

Świeża kontrola przechodzi przez lokalny katalog XML i bez dostępu sieciowego:

```text
bash docs/research/contracts/validate-fixtures.sh
```

Taki wynik potwierdza zgodność struktury konkretnego XML z konkretną wersją XSD. Nie potwierdza poprawności gospodarczej, podatkowej ani prawa do wysyłki dokumentu.

## Potwierdzone fakty integracyjne

### Zamknięcie roku i dane do zewnętrznego PIT - sprawdzenie 2026-09-14

- [MF - rozliczenie dochodów opodatkowanych skalą](https://www.podatki.gov.pl/podatki-firmowe/pit/informacje-podstawowe/co-jest-opodatkowane/rozliczenie-dochodow-opodatkowanych-skala-podatkowa): działalność na skali rozlicza się w PIT-36/PIT-36S z właściwymi załącznikami; termin przypada od 15 lutego do 30 kwietnia kolejnego roku.
- [MF - PIT-36 za 2025 rok w Twój e-PIT](https://www.podatki.gov.pl/twoj-e-pit/pit-36-za-2025-rok): część dotycząca działalności obejmuje przychody, koszty, dochód albo stratę i zaliczki, a dane do PIT/B przenosi się z KPiR. Jest to podstawa zakresu firmowego PDF, nie podstawa do tworzenia pełnego zeznania.
- [MF - broszura JPK_PKPIR(3)](https://www.podatki.gov.pl/media/pbqdgqcp/broszura_informacyjna_dot_jpk_pkpir-3.pdf): roczny plik zawiera dane ustalenia dochodu oraz wartości spisu z natury, w tym pola `P_1` i `P_2`.
- [ZUS - roczne rozliczenie składki zdrowotnej za 2025 r.](https://www.zus.pl/-/roczne-rozliczenie-sk%C5%82adki-zdrowotnej-za-2025-r.): roczne rozliczenie zdrowotnej jest odrębną częścią dokumentu ZUS DRA/RCA i wymaga wykazania danych osobno dla formy opodatkowania. Regułę dla roku 2026 trzeba ponownie sprawdzić przed rzeczywistym rozliczeniem w 2027 roku.
- [ZUS - roczna podstawa składki zdrowotnej](https://www.zus.pl/-/roczna-podstawa-wymiaru-sk%C5%82adki-na-ubezpieczenie-zdrowotne-os%C3%B3b-prowadz%C4%85cych-dzia%C5%82alno%C5%9B%C4%87-gospodarcz%C4%85): dochód roczny uwzględnia różnice remanentowe, a minimalna podstawa zależy od liczby miesięcy podlegania ubezpieczeniu. Wynik rozliczenia rocznego porównuje składkę roczną z sumą miesięcznych składek należnych; faktyczne wpłaty Firemka pokazuje osobno.
- [PDFsharp/MigraDoc - dokumentacja techniczna 6.2.4](https://docs.pdfsharp.net/): stabilna wersja wspiera .NET 10, działa na macOS/Linux/Windows i generuje PDF lokalnie.
- [PDFsharp/MigraDoc - licencja MIT](https://docs.pdfsharp.net/General/License/License.html): biblioteka może być użyta bez zależności od przychodu firmy; informacja licencyjna pozostaje w paczce NuGet.

- [Wsparcie dla integratorów KSeF 2.0](https://ksef.podatki.gov.pl/ksef-na-okres-obligatoryjny/wsparcie-dla-integratorow/) udostępnia osobne środowiska produkcyjne, integracyjne i Demo. Integracyjne wymaga zanonimizowanych danych i nie wywołuje skutków prawnych. Demo korzysta z prawdziwych uprawnień, ale faktury także nie wywołują skutków prawnych.
- [Struktura FA(3)](https://ksef.podatki.gov.pl/informacje-ogolne-ksef-20/struktura-logiczna-fa-3/) obowiązuje od 2026-02-01 i zastąpiła FA(2). Pobrany kontrakt OpenAPI nadal opisuje także inne obsługiwane schematy, więc Firemka musi jawnie ustawiać FA(3), a nie zakładać tego domyślnie.
- [JPK_V7M(3)](https://www.podatki.gov.pl/podatki-firmowe/jednolity-plik-kontrolny/jpk_vat-z-deklaracja/pliki-do-pobrania) obowiązuje od 2026-02-01. Strona zawiera również przykładowy plik dla osoby fizycznej — ma zostać użyty w przyszłych testach kontraktowych, nie jako dane rzeczywistej firmy.
- [Broszura JPK_VAT od 1 lutego 2026](https://www.podatki.gov.pl/media/wgbkrejs/broszura-jpk_vat-z-deklaracj%C4%85-od-1-lutego-2026-r.pdf), ponownie sprawdzona 2026-09-13, wymaga w `NrDostawcy` numeru bez prefiksu kraju, a przy jego braku wartości `BRAK`; wtedy opcjonalny kod kraju pozostaje pusty. Generator korzysta z kraju zapisanego przy księgowaniu kosztu zamiast zakładać `PL`.
- Ta sama [broszura JPK_VAT](https://www.podatki.gov.pl/media/wgbkrejs/broszura-jpk_vat-z-deklaracj%C4%85-od-1-lutego-2026-r.pdf), sprawdzona ponownie 2026-09-14, rozróżnia znaczenie pól KSeF: `OFF` dotyczy wyłącznie faktury wystawionej w trybie awarii z art. 106nf ust. 1 bez nadanego jeszcze numeru, a zwykła faktura elektroniczna lub papierowa wystawiona poza KSeF otrzymuje `BFK`. Sam brak numeru KSeF nie może automatycznie oznaczać `OFF`; techniczny zakres Firemki bez jawnej klasyfikacji trybu awarii używa `BFK`.
- [JPK w podatkach dochodowych](https://www.gov.pl/web/kas/struktury-jpk-w-podatkach-dochodowych) oraz [broszura JPK_PKPIR(3)](https://www.podatki.gov.pl/media/pbqdgqcp/broszura_informacyjna_dot_jpk_pkpir-3.pdf), sprawdzone ponownie 2026-09-14, wskazują strukturę dla KPiR i roczny termin powiązany z terminem zeznania. Pola `P_1` i `P_2` oznaczają wartość spisu z natury na początek i koniec roku. Narastający plik fazy 8 jest tylko technicznym podglądem z zablokowaną wysyłką; rzeczywiste wartości remanentu i plik roczny muszą zostać świadomie ustalone w fazie 9.
- [BIP ZUS](https://bip.zus.pl/pl/inne/wymagania-dla-oprogramowania-interfejsowego/dokumenty-ubezpieczeniowe) udostępnia KEDU 2.27, dane testowe i specyfikacje testów. Przed bezpośrednią integracją ZUS potrzebne są odrębne testy wymagane dla oprogramowania interfejsowego. W pierwszej wersji Firemka przygotowuje plik do ręcznego importu, nie uruchamia automatycznej wysyłki.
- [Poradnik ZUS DRA](https://www.zus.pl/documents/10182/167567/poradnik%2BZUS%2BDRA.pdf), ponownie sprawdzony 2026-09-14, wskazuje w bloku I kod terminu `6` dla pozostałych płatników składających do 20. dnia oraz identyfikator `01` dla pierwszego kompletu. [Instrukcja korekty ZUS](https://bip.zus.pl/pl/web/guest/firmy/rozliczenia-z-zus/dokumenty-rozliczeniowe/korekta-dokumentow) wymaga kolejnego numeru `02`, `03` itd. dla korekt tego samego miesiąca.

## Źródła reguł biznesowych do dalszej weryfikacji

| Obszar | Oficjalne źródło | Wniosek dla implementacji |
|---|---|---|
| skala PIT | [MF — opodatkowanie według skali](https://podatki.gov.pl/podatki-firmowe/pit/informacje-podstawowe/co-jest-opodatkowane/opodatkowanie-wedlug-skali-podatkowej) | KPiR i zaliczki są obowiązkami w trakcie roku; kod ma trzymać parametry i terminy w wersjonowanym zestawie reguł, nie na stałe w widoku. |
| VAT i zagraniczne usługi | [MF — podstawy VAT](https://www.podatki.gov.pl/podatki-firmowe/vat/informacje-podstawowe), [MF — miejsce świadczenia usług](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/miejsce-swiadczenia-opodatkowania), [MF — moment obowiązku](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/kiedy-powstaje-obowiazek-podatkowy) | kraj wystawcy, status VAT, waluta i rodzaj usługi są danymi wejściowymi; marka dostawcy nie może sama uruchamiać reguły. |
| zdrowotna | [ZUS — zdrowotne](https://www.zus.pl/firmy/rozliczenia-z-zus/skladki-na-ubezpieczenia/zdrowotne), [ZUS — kalkulator 2026](https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej) | składka na skali zależy od profilu i okresu. Parametry roku składkowego muszą mieć datę obowiązywania; faktyczny zbieg tytułów i rejestracja wymagają potwierdzenia. |
| samochód elektryczny | [MF — Podatki 2026](https://www.podatki.gov.pl/media/rr5pgg25/podatki-2026-przewodnik-dla-inwestorow.pdf), [MF — objaśnienia samochodowe](https://www.podatki.gov.pl/media/5973/obja%C5%9Bnienia-podatkowe-samochody-9-kwietnia-2020-r.pdf) | rodzaj umowy, wartość samochodu, rozbicie raty, VAT i użytek mieszany są obowiązkowymi wejściami. Nie tworzyć jednej ogólnej reguły „samochód elektryczny”. |

## Luki, które nie mogą zostać przemilczane

- Brak danych firmy, kontrahenta, daty startu, kwoty abonamentu i stawki VAT.
- Brak rzeczywistych dokumentów OpenAI/Anthropic/OVH oraz informacji o podmiocie wystawiającym, kraju i walucie.
- Brak umowy lub oferty samochodu oraz potwierdzenia sposobu rozliczenia domowego ładowania.
- Brak przykładowego CSV, więc nie można potwierdzić kolumn, kodowania, strefy czasowej i jednostki.
- Brak testowego KSeF, danych SMTP, adresu VPS i lokalnego folderu kopii.
- Brak niezależnego potwierdzenia tabeli reguł i wyników referencyjnych.

Żadna z tych luk nie może zostać zastąpiona domyślną wartością w obliczeniach podatkowych.
