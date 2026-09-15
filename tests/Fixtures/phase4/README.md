# Sztuczne próbki Fazy 4

Próba lokalnego OCR używa wyłącznie wygenerowanych danych:

- `phase4-synthetic-invoice.pdf` — prawidłowy, jednokartkowy PDF z warstwą tekstową;
- `phase4-synthetic-invoice-photo.png` — sztuczny obraz imitujący zdjęcie faktury;
- `docs/research/contracts/fixtures/fa3-synthetic-from-ksef-sdk.xml` — sztuczny XML zgodny z FA(3), używany do kontroli kontraktu.

Żaden plik nie zawiera danych istniejącej osoby, firmy ani rzeczywistego numeru KSeF. Binaria w tym folderze są trwałymi próbkami testowymi; ich kopie w `output/pdf` służą wyłącznie do oglądania. Ręczna próba z rzeczywistym, zanonimizowanym PDF, zdjęciem z telefonu i testowym KSeF pozostaje zewnętrzną bramką przed użyciem na danych właściciela.
