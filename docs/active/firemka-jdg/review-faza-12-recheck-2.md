# Drugie ponowne review Fazy 12 — przykład domowego ładowania

Data review: 2026-09-15  
Decyzja: **dokładny przykład syntetyczny zaliczony; podstawa podatkowa nadal niepotwierdzona**  
Wynik bieżącej zmiany: **0× nowych P1, 0× P2, 0× P3**

## Znaleziska

Nie znaleziono nowego P1, P2 ani P3. Zaznaczony punkt checklisty ma bezpośredni test wartości wejściowej, wyniku i bezpiecznej granicy podatkowej.

Otwarte pozostaje zewnętrzne potwierdzenie, czy i na jakiej podstawie domowe ładowanie może być kosztem tej konkretnej firmy. To odrębna bramka danych i decyzji księgowej; aplikacja celowo jej nie podejmuje.

## Sprawdzony zakres

- [ChargingDomainTests.cs](../../../tests/Firemka.Domain.Tests/ChargingDomainTests.cs#L49-L70) podaje dokładnie 10 000 Wh i stawkę 0,91 zł, a następnie sprawdza 10 kWh, 9,10 zł oraz stan „podstawa podatkowa niepotwierdzona”.
- [HomeChargingWorkflowTests.cs](../../../tests/Firemka.Infrastructure.Tests/HomeChargingWorkflowTests.cs#L42-L62) potwierdza te same wartości po imporcie i trwałym zapisie oraz zachowanie historycznej stawki po późniejszej zmianie.
- [HomeChargingWorkflowTests.cs](../../../tests/Firemka.Infrastructure.Tests/HomeChargingWorkflowTests.cs#L85-L101) potwierdza, że błędny plik nie tworzy partii, sesji, kosztu, wpisu KPiR ani wpisu VAT.
- [ChargingReport.cs](../../../src/Firemka.Domain/Charging/ChargingReport.cs#L7-L95) używa obliczeń dziesiętnych, jawnego zaokrąglenia oraz zawsze przypisuje niepotwierdzony stan podatkowy.

## Ocena czterech obszarów

- **Bezpieczeństwo i dane:** próby używają wyłącznie danych syntetycznych. Nie utrwalono danych firmy ani decyzji podatkowej.
- **Wydajność:** obliczenie sumuje sesje jednego raportu i nie zmienia ścieżki wykonania. Review nie wykryło regresji wydajnościowej.
- **Architektura i typy:** energia i pieniądze są liczone jako `decimal`, z osobnymi wartościami Wh, kWh, stawki i kosztu. Decyzja podatkowa pozostaje oddzielona od technicznego zestawienia.
- **Scenariusze:** sprawdzono poprawną wartość, zmianę stawki w przyszłości oraz niepoprawny import bez częściowego zapisu i bez automatycznego księgowania.

## Świeża weryfikacja

- `ChargingDomainTests.Ten_thousand_wh_at_ninety_one_grosze_is_ten_kwh_and_nine_ten` — 1/1 zielony.
- `HomeChargingWorkflowTests.Report_uses_existing_month_rate_and_remains_unchanged_after_a_new_rate` oraz `Invalid_file_writes_nothing_and_charging_never_changes_tax_ledgers` — 2/2 zielone.

## Decyzja bramki

Punkt przykładu 10 000 Wh można pozostawić zaznaczony jako zaliczony na danych syntetycznych. Nie wolno na tej podstawie zaznaczać potwierdzenia profilu podatkowego, rzeczywistego miesiąca ani decyzji o księgowaniu domowego ładowania.

Pełny pilotaż nadal jest zablokowany przez wcześniej opisane zewnętrzne P1. Nie wykonano commita, pushu ani wdrożenia.
