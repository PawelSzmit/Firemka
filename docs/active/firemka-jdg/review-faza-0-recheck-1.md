# Ponowne review fazy 0 — po naprawie P2

Data review: 2026-09-09  
Status po erracie: **P2 ZAMKNIĘTE; P1 POZOSTAJE BRAMKĄ PRODUKCYJNYCH REGUŁ PODATKOWYCH**

## Ustalenia

### P1 — nadal brak niezależnie potwierdzonych danych referencyjnych i reguł podatkowych

**Dowód.** Zebrane kontrakty i próbki techniczne nie zastępują danych firmy. Rejestr nadal oznacza PIT, import usług, zdrowotną i samochód jako obszary wymagające danych lub potwierdzenia ([`tax-decisions-2026.md`, wiersze 20-28](../../research/tax-decisions-2026.md#L20-L28)). Dla testu podatkowego wymagane są zanonimizowane dokumenty, daty, decyzje VAT/PIT/ZUS, oczekiwane wpisy oraz osoba potwierdzająca ([wiersze 32-45](../../research/tax-decisions-2026.md#L32-L45)). Inwentaryzacja dalej wskazuje brak danych firmy, dokumentów, CSV, umowy samochodu i niezależnego potwierdzenia ([`official-sources-2026.md`, wiersze 63-72](../../research/official-sources-2026.md#L63-L72)).

**Skutek.** Faza 0 nie daje podstawy do produkcyjnych obliczeń ani automatycznego księgowania.

**Działanie wymagane od właściciela.** Bez dodawania danych wrażliwych do repozytorium: bezpiecznie przekazać profil firmy i datę startu, zanonimizowane próbki dokumentów/CSV oraz dane samochodu, jeśli auto ma wejść do zakresu. Następnie niezależny księgowy lub doradca musi zatwierdzić tabelę decyzji i co najmniej jeden miesiąc referencyjny z oczekiwanymi wynikami.

## Potwierdzenie usunięcia P2

- Cztery migawkowe XSD nadal mają zgodne SHA-256, a OpenAPI nadal przechodzi kontrolę JSON.
- Wszystkie zależności importowane przez XSD zostały zapisane lokalnie z osobnymi SHA-256; katalog XML rozwiązuje je bez zmiany urzędowych migawek.
- Każdy XSD ma odpowiednią próbkę XML. Pełna kontrola `bash docs/research/contracts/validate-fixtures.sh` przeszła dla FA(3), JPK_V7M(3), JPK_PKPIR(3) oraz KEDU 2.27 z opcją `--nonet`.
- Próbki są jawnie opisane jako techniczne i nie są podstawą wyliczeń podatkowych ani wysyłki ([`official-sources-2026.md`, wiersze 34-44](../../research/official-sources-2026.md#L34-L44)).

## Zakres sprawdzenia

- integralność migawek, zależności i próbek przez SHA-256;
- składnia katalogu XML i składnia skryptu walidującego;
- lokalna walidacja czterech par XSD/XML bez Internetu;
- pochodzenie próbek oraz brak pozostawionych znaczników szablonu;
- granice między walidacją struktury a prawidłowością podatkową.

## Errata decyzji bramki

Wcześniejsze zdanie, że P1 blokuje fazę 1, było zbyt szerokie. Plan źródłowy wprost dopuszcza budowę technicznego szkieletu bez ukończenia jednostki 0 ([`2026-09-09-jdg-application-plan.md`, wiersz 251](../../plans/2026-09-09-jdg-application-plan.md#L251)). P2 jest zamknięte. P1 pozostaje otwarte jako bramka produkcyjnych obliczeń, automatycznego księgowania oraz każdej późniejszej funkcji wymagającej rzeczywistych danych — nie jako blokada bezpiecznej fazy 1.

## Polecenia kontrolne wykonane w review

```text
(cd docs/research/contracts && shasum -a 256 -c SHA256SUMS)
(cd docs/research/contracts/dependencies && shasum -a 256 -c SHA256SUMS)
(cd docs/research/contracts/fixtures && shasum -a 256 -c SHA256SUMS)
xmllint --noout docs/research/contracts/catalog.xml
bash -n docs/research/contracts/validate-fixtures.sh
bash docs/research/contracts/validate-fixtures.sh
jq empty docs/research/contracts/ksef-test-openapi-2026-09-09.json
```
