# Piąte ponowne review fazy 1 — lokalny kod QR TOTP

Data review: 2026-09-15  
Status: **GOTOWE — 0× P1, 0× P2, 0× P3**

## Znaleziska

Nie znaleziono otwartego P1, P2 ani P3 w zakresie poprawki QR. Ostatnie P3 Fazy 1 jest zamknięte.

## Zamknięcie wcześniejszego P3

- [TwoFactor.cshtml.cs](../../../src/Firemka.Web/Pages/Setup/TwoFactor.cshtml.cs#L111-L149) tworzy adres `otpauth://` i generuje z niego PNG lokalnie przez QRCoder. Nie ma połączenia z usługą zewnętrzną ani zapisu obrazu na dysku.
- [TwoFactor.cshtml](../../../src/Firemka.Web/Pages/Setup/TwoFactor.cshtml#L7-L27) pokazuje responsywny kod QR, a pod nim zachowuje klucz tekstowy i pełny adres jako dwa sposoby awaryjne.
- [AuthenticationFlowTests.cs](../../../tests/Firemka.Web.Tests/AuthenticationFlowTests.cs#L199-L229) potwierdza, że odpowiedź zawiera prawdziwy obraz PNG, tekstowy plan awaryjny i zakaz zapisywania sekretu w pamięci podręcznej.
- Instrukcja pierwszego uruchomienia w [README.md](../../../README.md) prowadzi teraz najpierw przez skanowanie QR i jasno opisuje ręczny wariant awaryjny.

## Ocena czterech obszarów

- **Bezpieczeństwo i dane:** sekret pozostaje w lokalnej odpowiedzi kreatora; generator nie korzysta z sieci, odpowiedź ma `no-store`, a istniejąca polityka bezpieczeństwa dopuszcza obrazy `data:` bez osłabiania skryptów. Skan NuGet i czterech obrazów nie wykrył podatności krytycznych ani wysokich.
- **Wydajność:** mały PNG powstaje tylko podczas jednorazowego kreatora oraz ponownego wyświetlenia jego błędu. Nie jest zapisywany ani buforowany, co jest właściwe dla sekretu TOTP i pomijalne dla skali jednego właściciela.
- **Architektura:** zmiana pozostaje w modelu i widoku strony konfiguracji. Jedna centralnie przypięta zależność ma blokadę wersji; pozostałe warstwy i kod produkcyjny kolejki nie zostały naruszone.
- **Scenariusze:** sprawdzono nowe konto, poprawny PNG, tekstowy wariant awaryjny, zakaz cache, zachowanie QR po błędnym kodzie oraz układ 1440 px i 390 px. Wcześniejsza ręczna próba potwierdza działanie TOTP i ponownego logowania.

## Świeża weryfikacja

- Test QR został najpierw uruchomiony na czerwono, a po implementacji przeszedł.
- Ręczna kontrola przeglądarkowa: widok desktopowy i telefoniczny bez przycięć; błędny kod zachowuje ekran oraz pokazuje czytelny komunikat; konsola ma 0 błędów i ostrzeżeń.
- Pełna bramka Fazy 12: 311/311 testów zielonych, 0 pominiętych; kompilacja ma 0 ostrzeżeń i błędów.
- Wszystkie 12 prób PostgreSQL przeszło. Skan NuGet jest czysty, a obrazy WWW, workera, Caddy i PostgreSQL mają `0C / 0H`.
- Stary test odnowienia leasingu otrzymał realistyczny margines planisty po odtworzeniu losowej porażki pod obciążeniem pełnego zestawu. Test docelowy i pełna bramka przeszły; nie zmieniono zachowania produkcyjnego.

## Decyzja bramki

Wszystkie znaleziska Fazy 1 są zamknięte. Poprawka QR jest gotowa i nie wprowadza nowego P1, P2 ani P3.

Decyzja nie oznacza zgody na wdrożenie. Cała Faza 12 i projekt nadal oczekują na prawdziwy VPS, niezależny miesiąc referencyjny, testowe KSeF/SMTP i narzędzia urzędowe, pełne próby telefonu/Maca oraz rzeczywistą kopię z odtworzeniem na drugim urządzeniu.

Nie wykonano commita, pushu ani wdrożenia.
