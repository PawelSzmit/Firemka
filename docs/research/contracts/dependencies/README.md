# Zależności lokalnej walidacji XSD

Pobrano: 2026-09-09. Te pliki są dokładnymi zależnościami wskazanymi w `schemaLocation` przez migawki XSD w katalogu nadrzędnym. Oryginalne migawki nie zostały zmienione. Plik [`../catalog.xml`](../catalog.xml) przekierowuje ich adresy urzędowe do lokalnych kopii, dzięki czemu kontrola XML nie pobiera nic z Internetu.

| Plik | Źródło |
|---|---|
| `ElementarneTypyDanych_v10-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/ElementarneTypyDanych_v10-0E.xsd` |
| `KodyKrajow_v10-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/KodyKrajow_v10-0E.xsd` |
| `KodyKrajow_v13-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2023/09/06/eD/KodyKrajow/KodyKrajow_v13-0E.xsd` |
| `KodyUrzedowSkarbowych_v8-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/KodyUrzedowSkarbowych/KodyUrzedowSkarbowych_v8-0E.xsd` |
| `StrukturyDanych_v10-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/StrukturyDanych_v10-0E.xsd` |
| `StrukturyDanych_v12-0E.xsd` | `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/09/13/eD/DefinicjeTypy/StrukturyDanych_v12-0E.xsd` |
| `xmldsig-core-schema.xsd` | `https://www.w3.org/TR/xmldsig-core/xmldsig-core-schema.xsd` |

Sprawdzenie sum uruchamia się z tego katalogu:

```text
(cd docs/research/contracts/dependencies && shasum -a 256 -c SHA256SUMS)
```
