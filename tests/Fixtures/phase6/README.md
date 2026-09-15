# Sztuczne dane fazy 6

`charging-synthetic.csv` nie zawiera danych właściciela ani danych rzeczywistej firmy.
Plik ma łącznie 10 000 Wh i służy wyłącznie do sprawdzenia wyniku 10 kWh oraz 9,10 zł przy stawce 0,91 zł/kWh.

`charging-home-charger-format.csv` odwzorowuje stały układ eksportu z domowej ładowarki:
identyfikator sesji, urządzenie, status, początek i koniec, czas w sekundach,
energię w Wh, moc w W oraz dwie kolumny NFC. Wiersze z każdym statusem zachowują
zużytą energię; status jest informacją źródłową, a nie powodem do cichego pominięcia.
