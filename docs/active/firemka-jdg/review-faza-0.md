# Review fazy 0 — źródła, dane wejściowe i dokumentacja

Data review: 2026-09-09  
Status: **BLOKUJE PRZEJŚCIE DO FAZY 1**

## Ustalenia

### P1 — brak niezależnie potwierdzonych danych referencyjnych i reguł podatkowych

**Dowód.** Plan wymaga zanonimizowanych dokumentów rzeczywistych, przykładowego CSV oraz jednorazowego potwierdzenia tabeli decyzji i wyników referencyjnych przez księgowego lub doradcę ([plan, wiersze 241-249](../../plans/2026-09-09-jdg-application-plan.md#L241-L249)). Rejestr decyzji wprost oznacza PIT, import usług, zdrowotną oraz samochód jako wymagające danych lub potwierdzenia ([rejestr, wiersze 20-28](../../research/tax-decisions-2026.md#L20-L28)), a poprawność kalkulatora uzależnia od pełnych danych wejściowych i osoby potwierdzającej ([wiersze 32-45](../../research/tax-decisions-2026.md#L32-L45)). Inwentaryzacja wymienia brakujące dane firmy, dokumenty, CSV, umowę samochodu i niezależne potwierdzenie ([wiersze 51-60](../../research/official-sources-2026.md#L51-L60)).

**Skutek.** Nie wolno uznać obliczeń PIT/VAT/ZUS ani automatycznego księgowania za poprawne produkcyjnie. To nie jest usterka do „domyślnego” uzupełnienia przez kod.

**Naprawa wymagana od właściciela.** Bez umieszczania danych wrażliwych w repozytorium należy bezpiecznie przekazać: profil firmy i datę startu; zanonimizowane próbki dokumentów oraz CSV; ofertę/umowę samochodu, jeśli auto ma wejść do zakresu; a następnie uzyskać od niezależnego księgowego lub doradcy zatwierdzenie tabeli decyzji i co najmniej jednego miesiąca referencyjnego z oczekiwanymi wynikami.

### P2 — brak plików XML będących rzeczywistymi przykładami walidacji XSD

**Dowód.** Faza 0 wymaga, aby *każdy XSD miał testowy plik przechodzący walidację* ([plan, wiersz 247](../../plans/2026-09-09-jdg-application-plan.md#L247)). W katalogu `docs/research/contracts/` są obecnie cztery XSD i OpenAPI, a dokumentacja zapisuje wyłącznie kontrolę ich składni (`xmllint --noout`), która nie sprawdza danych dokumentu ([źródła, wiersze 20-32](../../research/official-sources-2026.md#L20-L32)). Kontrola katalogu nie wykazała żadnej próbki `.xml`.

**Skutek.** Nie ma jeszcze powtarzalnego dowodu, że narzędzie waliduje utworzone dokumenty względem właściwego schematu.

**Naprawa do wykonania przez Codex w fazie 0.** Dodać do `docs/research/contracts/fixtures/` po jednej bezpiecznej, jasno oznaczonej próbce XML dla FA(3), JPK_V7M(3), JPK_PKPIR(3) i KEDU 2.27 — z oficjalnego przykładu testowego albo z minimalnego sztucznego przykładu zgodnego ze schematem. Każdy ma mieć polecenie walidacji i przejść je w świeżej kontroli. Próbki nie mogą zawierać danych rzeczywistej firmy ani zostać użyte do obliczeń podatkowych.

## Zakres sprawdzenia

- kompletność dokumentów wymaganych przez fazę 0;
- pochodzenie, wersje i sumy kontraktów KSeF/FA(3)/JPK/KEDU;
- rozdzielenie faktów urzędowych, decyzji produktowych i danych wymagających potwierdzenia;
- gotowość scenariuszy miesiąca i roku do przyszłych testów;
- bezpieczeństwo granic: brak sekretów, brak automatycznej wysyłki i brak deklarowania niepotwierdzonego podatku.

## Co przeszło kontrolę

- Pięć migawkowych kontraktów zostało zapisanych z SHA-256. Świeże sprawdzenie `shasum -a 256 -c SHA256SUMS` przeszło dla wszystkich plików.
- Cztery XSD przechodzą kontrolę składni `xmllint --noout`, a OpenAPI przechodzi `jq empty`.
- Dokumentacja jawnie oddziela testowe środowisko KSeF od produkcji oraz nie włącza automatycznej wysyłki.
- Scenariusze miesiąca i roku opisują oczekiwane zachowanie bez pozorowania konkretnych kwot PIT/VAT/ZUS.
- Nie znaleziono w dokumentacji sekretów ani danych osobowych.

## Decyzja bramki

**Nie przechodzimy do fazy 1.** Zgodnie z regułą aktywnego zadania, P1 i P2 muszą być usunięte przed kolejną fazą. Najpierw wykonam naprawę P2 w ramach fazy 0, a następnie przeprowadzę ponowne review. P1 pozostanie otwartą bramką do czasu otrzymania danych i niezależnego potwierdzenia od właściciela.

## Polecenia kontrolne wykonane w review

```text
(cd docs/research/contracts && shasum -a 256 -c SHA256SUMS)
xmllint --noout docs/research/contracts/*.xsd
jq empty docs/research/contracts/ksef-test-openapi-2026-09-09.json
find docs/research/contracts -maxdepth 1 -type f -print
```

