# ADR 0002 — niezmienialna historia finansowa

Data: 2026-09-09  
Status: zaakceptowany do implementacji fazy 2.

## Decyzja

Faktura wystawiona, decyzja księgowa, wpis księgi, zamknięcie miesiąca/roku, plik urzędowy oraz potwierdzenie są wersjami, nie rekordami edytowanymi w miejscu.

Każda korekta:

1. wskazuje poprzednią wersję;
2. zawiera powód i autora;
3. tworzy nowy snapshot wejść i wyników;
4. pokazuje różnicę;
5. oznacza zależne dokumenty jako wymagające korekty;
6. nigdy nie wysyła korekty automatycznie.

## Uzasadnienie

Wymagania wyraźnie zakazują cichego zastępowania wysłanych dokumentów i zamkniętych okresów. Wersjonowanie jest także konieczne, aby pokazać wpływ poprawki na PIT/VAT/ZUS.

## Konsekwencje

- W bazie potrzebne są identyfikatory wersji, relacje poprzednik/następca, daty i audyt.
- Widoki domyślne pokazują aktualną wersję, a historia jest dostępna osobno.
- Dokument urzędowy jest powiązany z konkretnym snapshotem zamknięcia.

## Źródła

- R8, R17-R18, R36-R38 w [wymaganiach](../brainstorms/2026-09-07-jdg-requirements.md)
- [Plan techniczny](../plans/2026-09-09-jdg-application-plan.md)
