# Piąte ponowne review Fazy 12 — instrukcja działań właściciela

Data review: 2026-09-15  
Decyzja: **1× P2 do naprawy przed użyciem instrukcji**  
Wynik: **0× nowych P1, 1× P2, 0× P3**

## Znaleziska

### P2 — identyfikator wersji i obrazy są zapisywane w niemożliwej kolejności

[phase12-owner-handoff.md](../../acceptance/phase12-owner-handoff.md) mówi, że Codex przed pierwszym commitem zapisze skrót kodu oraz identyfikatory obrazów. Skrót trwałej rewizji powstaje dopiero w chwili utworzenia commita. Obrazy używane jako dowód wydania powinny następnie zostać zbudowane z dokładnie tej rewizji, a wynik trzeba utrwalić bez zmiany zawartości, którą obrazy reprezentują.

**Skutek:** A1 mogłoby wskazywać skrót nieistniejący w chwili budowy albo obrazy z innego stanu katalogu niż zapisany commit. Taki dowód nie pozwala jednoznacznie odtworzyć wydania.

**Naprawa:** rozdzielić Krok 1 na: kontrolę zakresu i zieloną bramkę, pierwszy commit kodu, nadanie wersji z jego skrótu, budowę i skan obrazów z czystej rewizji oraz osobny commit zawierający wyłącznie protokół dowodowy. Push pozostaje osobną zgodą i następuje dopiero po obu commitach.

## Ocena czterech obszarów

- **Bezpieczeństwo i dane:** instrukcja prawidłowo zakazuje sekretów w rozmowie i rozdziela commit, push i wdrożenie. `.playwright-cli/` oraz `tmp/` są wykluczone; trwałe artefakty pozostają jawne do kontroli zakresu.
- **Wydajność:** dokument nie zmienia działania aplikacji. Ponowne skany i 30-minutowa obserwacja są proporcjonalne do ryzyka wydania.
- **Architektura i utrzymanie:** P2 narusza odtwarzalność powiązania kod → obraz → wdrożenie. Pozostałe mapowanie A1–F6 jest kompletne i zachowuje granice planu.
- **Scenariusze:** uwzględniono brak zgody, odczytowy audyt przed zmianą, wyłączone integracje, urządzenia, niezależny miesiąc, kopię, awarie i powrót. Brakowało poprawnej kolejności dla pierwszej wersji.

## Decyzja bramki

Instrukcja nie jest jeszcze gotowa do użycia. Następny `dev-docs-execute` poprawia wyłącznie P2, po czym wymagane jest szóste ponowne `dev-docs-review` Fazy 12. Nie wykonano commita, pushu ani wdrożenia.
