# Próbki techniczne XML

Cel tych plików jest wyłącznie techniczny: sprawdzić, czy lokalny walidator potrafi zastosować konkretną wersję XSD wraz z jej zależnościami. Nie są to dokumenty Firemki, dane referencyjne do PIT/VAT/ZUS ani pliki do wysyłki.

| Plik | Pochodzenie | Granica użycia |
|---|---|---|
| `fa3-synthetic-from-ksef-sdk.xml` | Sztuczna pochodna szablonu `invoice-template_v3.xml` z publicznego projektu [CIRFMF/ksef-client-java](https://github.com/CIRFMF/ksef-client-java/blob/main/demo-web-app/src/main/resources/xml/invoices/sample/invoice-template_v3.xml); trzy znaczniki zastąpiono wyłącznie testowymi wartościami. | Walidacja struktury FA(3), nigdy wysyłka. |
| `jpk-v7m3-official-person.xml` | Oficjalny przykład MF dla JPK_V7M(3), osoba fizyczna: `https://www.podatki.gov.pl/media/sf3h1dey/jpk_v7m-3-przyk%C5%82adowy-plik-w-formacie-xml.xml`. | Walidacja struktury, nie dane firmy. |
| `jpk-pkpir3-synthetic.xml` | Minimalna sztuczna próbka utworzona z wymaganych pól XSD JPK_PKPIR(3). | Walidacja struktury, nie kalkulacja podatku. |
| `kedu-2.27-official-dra.xml` | Plik `DRA.xml` z oficjalnego [zestawu danych testowych ZUS 2.27](https://bip.zus.pl/pl/inne/wymagania-dla-oprogramowania-interfejsowego/dokumenty-ubezpieczeniowe). | Walidacja struktury, nie deklaracja Firemki. |

Sprawdzenie sum uruchamia się z tego katalogu:

```text
(cd docs/research/contracts/fixtures && shasum -a 256 -c SHA256SUMS)
```

Pełną lokalną walidację wszystkich czterech par XML/XSD uruchamia się z katalogu projektu:

```text
bash docs/research/contracts/validate-fixtures.sh
```

Skrypt używa `catalog.xml` i `--nonet`: podczas walidacji nie pobiera z Internetu brakujących importów.
