---
date: 2026-09-07
updated: 2026-09-08
topic: jdg
---

# Firemka — wymagania aplikacji do własnej JDG

Status: wymagania produktowe uzgodnione; dokument jest gotowy do przygotowania planu technicznego. Przed implementacją reguł podatkowych konieczna jest ich weryfikacja na aktualnych źródłach urzędowych i rzeczywistych dokumentach właściciela.
Punkt wyjścia: [analiza pomysłów](../ideation/2026-09-07-jdg-ideation.md). Propozycje z analizy nie są automatycznie zatwierdzonymi wymaganiami.

## Problem

Właściciel chce samodzielnie prowadzić rozliczenia niewielkiej JDG, ograniczyć koszt księgowości i rozwijać własne narzędzie dla satysfakcji. Aplikacja ma ograniczyć ręczną pracę przy jednej fakturze sprzedaży i kilku powtarzalnych kosztach.

## Requirements

- R1. Prywatna aplikacja WWW dla jednej firmy, wygodna na komputerze i telefonie; możliwy hosting na istniejącym VPS.
- R2. Wystawianie i odbieranie faktur przez KSeF, prowadzenie ksiąg, rejestrów VAT oraz wyliczanie PIT i ZUS; przygotowywanie dokumentów wymaganych do wysyłki do US/ZUS.
- R3. Początkowy profil: skala podatkowa, miesięczny VAT czynny, tylko zdrowotna według podanej sytuacji etatowej. Jedna faktura za dostęp do SaaS dla polskiej firmy. Start od założenia działalności, prawdopodobnie październik 2026.
- R4. Pierwsza wersja obsługuje wyłącznie skalę podatkową. Ustawienia i rozliczenia są przypisane do lat, aby przyszłe rozszerzenie nie zmieniało wcześniejszych okresów. Ryczałt pozostaje udokumentowaną opcją rozwoju, bez implementacji i bez aktywnego przełącznika w pierwszej wersji. Wybór ryczałtu przy otwieraniu nowego roku będzie dostępny dopiero po wdrożeniu i sprawdzeniu jego obsługi. Rozważane 8,5% i 12% nie oznaczają potwierdzenia stawki właściwej dla usługi.
- R5. Znane, powtarzalne koszty są księgowane automatycznie, bez każdorazowego zatwierdzania. Użytkownik może edytować sposób ich rozliczenia.
- R6. Zakres kosztów obejmuje samochód w najmie/leasingu, AI, VPS i ewentualnie internet. Dokumenty spoza KSeF muszą mieć ścieżkę wprowadzenia.
- R7. Przy pierwszej fakturze danego rodzaju aplikacja proponuje sposób rozliczenia. Użytkownik zatwierdza go jednorazowo; kolejne podobne faktury pasujące do zatwierdzonej reguły są księgowane automatycznie.
- R8. Edycja sposobu księgowania domyślnie dotyczy tylko wybranej faktury. Osobna opcja „Stosuj tę zmianę również do kolejnych faktur” aktualizuje regułę na przyszłość; nie zmienia automatycznie innych wcześniej zaksięgowanych dokumentów.
- R9. Aplikacja przygotowuje co miesiąc wersję roboczą faktury sprzedaży na podstawie zapisanych danych. Początkowo wystawienie i wysłanie do KSeF następuje wyłącznie po kliknięciu użytkownika; przedtem może on sprawdzić i zmienić dokument.
- R10. Dostępna jest domyślnie wyłączona opcja automatycznego wystawiania i wysyłania cyklicznej faktury sprzedaży. Właściciel włącza ją sam, gdy na podstawie kilku miesięcy użytkowania uzna działanie aplikacji za poprawne. Aplikacja nie włącza jej samoczynnie po upływie czasu. Harmonogram i zasady wyjątków wymagają dopracowania.
- R11. Abonament sprzedażowy ma stałą kwotę uzgodnioną z klientem na dłuższy okres; nie zależy od liczby użytkowników. Właściciel może zmienić kwotę na pojedynczej wersji roboczej faktury lub ustalić nową wysokość abonamentu dla kolejnych miesięcy. Zmiana stawki na przyszłość nie modyfikuje już wystawionych faktur. Proponowany sposób wskazania początku nowej stawki: wybór miesiąca obowiązywania.
- R12. Abonament jest fakturowany z góry za rozpoczynający się miesiąc. Zaplanowany dzień wystawienia to 1. dzień miesiąca, a termin płatności wynosi 7 dni. W trybie ręcznym aplikacja przygotowuje wtedy wersję roboczą i czeka na kliknięcie właściciela; dopiero po włączeniu automatyzacji wystawia i wysyła fakturę samodzielnie zgodnie z harmonogramem.
- R13. Przy ręcznym zatwierdzeniu po 1. dniu, ale w tym samym miesiącu, faktura otrzymuje bieżącą datę wystawienia i termin płatności 7 dni od tej daty. Obejmuje nadal cały ten miesiąc, bez proporcjonalnego zmniejszania abonamentu z powodu późniejszego kliknięcia. Zatwierdzenie dopiero w kolejnym miesiącu nie jest rozstrzygnięte tą zasadą.
- R14. Pierwsza wersja umożliwia dodanie faktury spoza KSeF jako PDF lub zdjęcia i odczytuje z niej dane. Przed księgowaniem użytkownik sprawdza odczytane dane i może je poprawić lub uzupełnić ręcznie, szczególnie przy nieczytelnym dokumencie. Po potwierdzeniu danych aplikacja stosuje zatwierdzoną regułę księgowania; dla nowego rodzaju kosztu obowiązuje jednorazowe zatwierdzenie sposobu rozliczenia z R7. Sprawdzenie odczytu dokumentu nie oznacza ponownego zatwierdzania znanej reguły podatkowej.

- R15. Pierwsza wersja przygotowuje gotowe pliki do US i ZUS oraz instrukcję ich wysłania. Właściciel podpisuje i wysyła je w narzędziach urzędowych, następnie zapisuje potwierdzenie przyjęcia w aplikacji.
- R16. Wersja docelowa ma automatycznie wysyłać dokumenty do US i ZUS. Już przy planowaniu pierwszej wersji należy uwzględnić tę funkcję zarówno w przygotowaniu dokumentów, jak i doborze technologii. Dokument przeznaczony do ręcznego eksportu ma nadawać się do wykorzystania w przyszłej wysyłce, z zachowaniem jego wersji i powiązania z rozliczeniem. Szczegółowy mechanizm podpisu, uprawnienia i możliwości automatyzacji dla poszczególnych dokumentów wymagają weryfikacji; nie zakładamy z góry braku udziału właściciela w autoryzacji.

- R17. Aplikacja na bieżąco wylicza należności na podstawie dostępnych danych. Przed finalizacją pokazuje podsumowanie miesiąca i wykryte braki. Ostateczny zestaw dokumentów miesięcznych powstaje dopiero po kliknięciu właściciela „Zatwierdź rozliczenie miesiąca”. Zatwierdzenie miesiąca nie wymaga ponownego zatwierdzania każdej znanej faktury kosztowej. Nie oznacza wysłania dokumentów ani zapłaty należności.

- R18. Poprawienie księgowania zachowuje historię zmian i pokazuje wpływ na kwoty rozliczenia. Jeżeli zmiana dotyczy już wysłanych dokumentów, aplikacja wskazuje, które wymagają korekty, i przygotowuje wymagane korekty do zatwierdzenia przez właściciela. Nie zastępuje po cichu wcześniej wysłanych wersji. Korekta nie jest automatycznie wysyłana przed jej zatwierdzeniem, również po uruchomieniu docelowej automatycznej wysyłki.

- R19. W pierwszej wersji właściciel ręcznie oznacza faktury sprzedaży, koszty oraz należności PIT, VAT i ZUS jako zapłacone, podając datę pełnej płatności. Aplikacja pokazuje pozostałe należności; przelewy właściciel wykonuje w banku. Płatności częściowe nie są obsługiwane w pierwszej wersji. Kwota inna od pełnej należności wymaga ręcznego wyjaśnienia i nie oznacza automatycznie faktury jako zapłaconej.
- R20. Docelowo właściciel może wgrać wyciąg bankowy za dany miesiąc. Aplikacja odczytuje transakcje i automatycznie uzupełnia informacje o płatnościach, dopasowując je do faktur i należności podatkowo-składkowych. Jest to import dostarczonego pliku, bez wymagania bezpośredniego połączenia z bankiem. Bank, format wyciągu i zasady niejednoznacznych dopasowań pozostają do ustalenia. Plan pierwszej wersji powinien uwzględniać możliwość późniejszego dodania importu do ręcznej obsługi płatności.

- R21. Przy docelowym imporcie wyciągu jednoznaczne transakcje są przypisywane do płatności automatycznie. Przy niejednoznacznym dopasowaniu aplikacja przedstawia propozycję do zatwierdzenia przez właściciela i nie zapisuje jej jako potwierdzonej płatności bez jego decyzji.

- R22. Aplikacja wysyła powiadomienia e-mail na adres `pawel@sitback.pl`: o fakturze sprzedaży czekającej na wystawienie, zbliżających się terminach rozliczeń i płatności oraz błędach pobierania dokumentów. Powiadomienia mają umożliwiać reakcję bez konieczności codziennego zaglądania do aplikacji. Dokładny harmonogram przypomnień pozostaje do dopracowania. Jest to wymaganie przyszłej aplikacji; w ramach warsztatu nie uruchomiono wysyłki ani przypomnień.

- R23. Samochód będzie elektryczny i użytkowany w sposób mieszany: firmowo i prywatnie. Pierwsza wersja ma obsługiwać koszty samochodu zgodnie z tym profilem. Szczegółowe zasady dla najmu/leasingu, eksploatacji i ubezpieczenia wymagają danych pojazdu oraz umowy; samo potwierdzenie napędu i użytku mieszanego nie ustala jednakowego sposobu rozliczenia wszystkich wydatków.

- R24. Pierwsza wersja obejmuje oba sposoby ładowania samochodu: domowe na podstawie wgrywanego pliku CSV z danymi ładowania oraz publiczne na podstawie faktur (według właściciela sporadyczne). Format CSV, źródło ceny energii i dokumenty potrzebne do rozliczenia domowego ładowania pozostają do ustalenia. Nie zakładamy, że sam CSV stanowi wystarczającą podstawę księgowania lub odliczenia VAT.

- R25. CSV domowego ładowania zawiera zużycie energii w Wh, bez kosztu. Aplikacja przelicza Wh na kWh przez podzielenie przez 1000, następnie mnoży zużycie w kWh przez podaną przez właściciela stawkę 0,91 zł brutto/kWh. Jest to wyliczenie kosztu energii; sposób ujęcia w księgach i VAT wymaga osobnego ustalenia na podstawie dokumentów źródłowych.

- R26. Właściciel może zmienić cenę energii brutto za kWh od wybranego miesiąca. Aplikacja zachowuje historię stawek i przy rozliczaniu danego okresu stosuje właściwą dla niego cenę. Wprowadzenie nowej stawki nie zmienia wcześniejszych rozliczeń; początkowa stawka wynosi 0,91 zł brutto/kWh.

- R27. Dane domowego ładowania pochodzą z aplikacji telefonu połączonej z kablem ładującym, która mierzy energię i eksportuje statystyki do CSV. Zgodnie z wcześniejszym doprecyzowaniem właściciela wartości w pliku są w Wh; jednostkę potwierdzić na przykładowym pliku. Właściciel nie ma osobnych faktur za energię do ładowania samochodu. Oczekiwany wynik to własne miesięczne zestawienie tabelaryczne przygotowane z CSV, ze zużyciem energii i kosztem według stawki okresu. Użytkownik określa je jako własny dokument księgowy; jego wystarczalność do ujęcia kosztu podatkowego i ewentualnego odliczenia VAT nie została potwierdzona. Generowanie zestawienia nie może samo oznaczać zatwierdzenia podstawy podatkowej.

- R28. Ogólna faktura za energię domową jest wystawiana na właściciela prywatnie. Zgodnie z jego decyzją nie jest księgowana jako koszt w aplikacji. Obsługa domowego ładowania opiera się na własnym zestawieniu z CSV i stawce za kWh. Decyzja ta nie oznacza rezygnacji z zamiaru rozliczania samego ładowania ani potwierdzenia, że zestawienie bez dodatkowych dowodów wystarczy podatkowo. Zasady jego ujęcia należy zweryfikować przed implementacją automatycznego księgowania. Nie wymagać ponownie decyzji o księgowaniu całej domowej faktury.

- R29. Obsługa samochodu ma uwzględniać najem długoterminowy oraz leasing jako możliwe warianty. Właściciel skłania się ku najmowi, ale podejmie decyzję na podstawie otrzymanej oferty. Nie wybieramy za niego wariantu ani nie zakładamy identycznego sposobu rozliczenia obu umów. Szczegóły rodzaju leasingu, wartości pojazdu, opłat i usług w racie należy ustalić z konkretnej oferty lub umowy przed uruchomieniem właściwego księgowania.

- R30. Typowi dostawcy kosztów to OpenAI (główny dostawca AI), ewentualnie Anthropic oraz OVH (VPS). Aplikacja ma obsługiwać dokumenty tych dostawców. Nazwa marki nie przesądza podmiotu wystawiającego fakturę, kraju, waluty ani sposobu rozliczenia VAT — dane należy ustalić z rzeczywistych dokumentów. Preferencja dostawcy zakupów AI nie stanowi wyboru technologii odczytu dokumentów w Firemce.

- R31. W zakresie rocznego PIT aplikacja przygotowuje wyłącznie zestawienie danych firmowych potrzebnych do rozliczenia w innym programie. Nie przygotowuje pełnego zeznania rocznego, nie wymaga PIT-11 z etatu i nie obsługuje wspólnego rozliczenia małżonków. Właściciel rozlicza się z żoną w już używanej aplikacji. Zestawienie ma obejmować właściwe dla danego roku i formy opodatkowania wartości firmowe, w tym przychody, koszty tam, gdzie mają zastosowanie, dochód/stratę, zaliczki lub ryczałt należny i faktycznie wpłacony oraz dane o składkach potrzebne do rozliczenia. Dokładne pola ustalić przy weryfikacji przepisów. Wyłączenie pełnego PIT-u nie wyłącza miesięcznych obliczeń, rocznego rozliczenia zdrowotnej ani wymaganych rocznych plików dotyczących ksiąg.

- R32. Aplikacja wstrzymuje zatwierdzenie miesiąca, gdy dotycząca go faktura ma brakujące dane potrzebne do rozliczenia lub nieustalony sposób księgowania. Pokazuje konkretny problem i sposób jego rozwiązania. Właściciel może świadomie oznaczyć dokument jako niezwiązany z firmą; dokument zachowuje ten status i nie jest księgowany. Nie jest to ogólna opcja pominięcia błędów w dokumentach firmowych.
- R33. Jeśli działalność lub świadczenie abonamentu rozpocznie się po 1. dniu miesiąca, pierwsza faktura obejmuje cały miesiąc, bez proporcjonalnego pomniejszenia. Przy ręcznym zatwierdzeniu w tym samym miesiącu obowiązuje bieżąca data wystawienia i termin płatności 7 dni od tej daty.
- R34. Zatwierdzona reguła kosztu może działać automatycznie mimo zmiany kwoty lub zwykłej zmiany daty dokumentu. Zmiana sprzedawcy, kraju, waluty, stawki VAT albo rodzaju usługi zatrzymuje automatyczne księgowanie i wymaga sprawdzenia przez właściciela. Po sprawdzeniu właściciel może zaakceptować wyjątek tylko dla tego dokumentu albo świadomie zmienić regułę na przyszłość.
- R35. Jeśli wersja robocza faktury zostanie zatwierdzona dopiero w kolejnym miesiącu, otrzymuje bieżącą datę wystawienia i termin płatności 7 dni od tej daty, ale nadal dotyczy pierwotnego okresu abonamentu. Aplikacja ostrzega o opóźnieniu, nie zmienia po cichu okresu usługi i nie pomija osobnej faktury za bieżący miesiąc. Przykład: faktura za wrzesień zatwierdzona 2 października ma datę 2 października, termin 9 października i okres usługi obejmujący wrzesień.
- R36. Roczne dokumenty powstają dopiero po osobnym kliknięciu właściciela „Zamknij rok”. Zamknięcie jest dostępne po zatwierdzeniu wszystkich miesięcy danego roku oraz rozwiązaniu wykrytych braków. Przed zatwierdzeniem aplikacja pokazuje roczne podsumowanie przychodów, kosztów, dochodu lub straty, zaliczek i składki zdrowotnej, roczne rozliczenie składki zdrowotnej, wymagany plik dotyczący KPiR oraz zestawienie danych firmowych do zewnętrznego rozliczenia PIT.
- R37. Zestawienie danych firmowych do zewnętrznej aplikacji PIT jest generowane jako PDF. Po zamknięciu roku aplikacja pokazuje przycisk „Pobierz zestawienie roczne PDF”. Kliknięcie rozpoczyna pobieranie przez przeglądarkę; docelowy folder zależy od jej ustawień i zwykle jest to folder „Pobrane”. PDF pozostaje również dostępny przy zamkniętym roku do ponownego pobrania.
- R38. Zamknięty rok jest domyślnie dostępny tylko do odczytu. Poprawkę rozpoczyna osobna czynność „Rozpocznij korektę”, wymagająca krótkiego powodu. Aplikacja zachowuje poprzednią wersję, przelicza dane objęte zmianą i przygotowuje nowe wersje wymaganych dokumentów do ponownego zatwierdzenia. Nie usuwa ani nie nadpisuje wcześniejszych dokumentów i potwierdzeń.
- R39. Powiadomienia na `pawel@sitback.pl` działają według harmonogramu: wersja robocza faktury sprzedaży — 1. dnia miesiąca rano, następnie nie częściej niż raz dziennie do wystawienia; dokumenty i płatności do US/ZUS — 7 dni przed terminem, 2 dni przed terminem oraz w dniu terminu, jeśli nadal nie są oznaczone jako wysłane lub zapłacone; nieopłacona faktura klienta — dzień po terminie płatności; błąd KSeF, odczytu dokumentu lub przygotowania rozliczenia — od razu, następnie nie częściej niż raz dziennie do rozwiązania. Konkretna godzina porannych wiadomości może zostać ustalona w planie.
- R40. Aplikacja ma tworzyć automatyczne, zaszyfrowane kopie danych i przechowywać pięć ostatnich kopii na lokalnym dysku MacBooka właściciela. Starsza kopia jest zastępowana dopiero po poprawnym zapisaniu i sprawdzeniu nowszej. Właściciel ma też możliwość ręcznego uruchomienia kopii oraz sprawdzenia daty ostatniej poprawnej kopii. Ponieważ MacBook może być wyłączony lub uśpiony, niewykonana kopia powinna zostać pobrana przy kolejnej dostępności komputera. Dokładny sposób bezpiecznego przekazywania danych z VPS na MacBooka należy ustalić w planie.
- R41. Każda kopia na MacBooku, zarówno dzienna, jak i okresowa, ma być jednym przenośnym plikiem. Plik zawiera komplet potrzebny do odtworzenia Firemki: bazę danych, faktury i załączniki, dokumenty własne, wygenerowane pliki urzędowe, potwierdzenia oraz informacje o wersji kopii. Po przeniesieniu pliku na inny komputer i użyciu funkcji „Odtwórz z kopii” aplikacja ma przywrócić pełne działanie bez ręcznego układania plików i folderów. Po zamknięciu każdego roku aplikacja tworzy także osobny, trwały plik archiwalny tego roku; nie podlega on rotacji pięciu kopii dziennych.
- R42. Przenośne pliki kopii są szyfrowane i chronione osobnym hasłem odzyskiwania ustalonym przez właściciela. Przy odtwarzaniu na innym komputerze aplikacja wymaga pliku kopii oraz tego hasła. Hasło nie może być zapisane wewnątrz kopii. Aplikacja jasno informuje, że bez niego odtworzenie danych nie będzie możliwe; sposób bezpiecznego zapamiętania hasła na MacBooku można ustalić w planie.
- R43. Dostęp do aplikacji ma jedno konto właściciela. Logowanie wymaga hasła oraz jednorazowego kodu z aplikacji uwierzytelniającej na telefonie. Podczas konfiguracji aplikacja wydaje awaryjne kody odzyskiwania, które można pobrać i przechować poza aplikacją. Dane dostępowe do KSeF i dane księgowe nie mogą być dostępne przed poprawnym zalogowaniem.
- R44. Właściciel może oznaczyć swój MacBook i telefon jako zaufane urządzenia na 30 dni. W tym okresie aplikacja nie wymaga kodu z aplikacji uwierzytelniającej przy każdym ponownym logowaniu. Dodatkowy kod jest wymagany po upływie 30 dni, po użyciu funkcji wylogowania wszystkich urządzeń oraz przy logowaniu z nowego urządzenia. Utrata urządzenia musi być możliwa do obsłużenia przez unieważnienie jego zaufania.
- R45. Po zalogowaniu aplikacja otwiera widok „Mój miesiąc”. Pokazuje on najbliższą czynność, status faktury sprzedaży i jej płatności, nowe faktury kosztowe i dokumenty wymagające sprawdzenia, bieżące przewidywane kwoty VAT, PIT i składki zdrowotnej, terminy wysyłki i płatności, status rozliczenia miesiąca oraz datę ostatniej poprawnej kopii na MacBooku. Szczegóły ksiąg i dokumentów są dostępne po wejściu w odpowiednią pozycję.
- R46. Nowa wysokość abonamentu ustawiona od wybranego miesiąca aktualizuje niewystawione wersje robocze dotyczące tego i późniejszych okresów. Nie zmienia wystawionych faktur. Jeżeli właściciel wcześniej ręcznie zmienił wersję roboczą, aplikacja pokazuje różnicę i pyta przed zastąpieniem tej konkretnej kwoty nową stawką.

## Success Criteria

- Znany, powtarzalny koszt trafia do ksiąg bez dodatkowego zatwierdzenia przez właściciela.
- Nierozwiązane braki danych lub sposobu księgowania w fakturach dotyczących miesiąca uniemożliwiają jego zatwierdzenie; użytkownik widzi listę problemów. Świadome oznaczenie dokumentu jako niezwiązanego z firmą wyłącza go z księgowania i zachowuje informację o tej decyzji.
- Przykład domowego ładowania: 10 000 Wh daje 10 kWh i koszt 9,10 zł brutto przy stawce 0,91 zł/kWh.
- Zmiana ceny energii od wybranego miesiąca nie przelicza wcześniejszych rozliczeń według nowej ceny.
- Właściciel otrzymuje na wskazany adres e-mail powiadomienia o wymaganym wystawieniu faktury, nadchodzących terminach oraz błędach pobierania dokumentów.
- Przy imporcie wyciągu jednoznaczne dopasowania uzupełniają płatności automatycznie, a niejednoznaczne pozostają propozycjami do zatwierdzenia.
- Właściciel może sprawdzić sposób zaksięgowania i wprowadzić poprawkę.
- Pierwsza faktura danego rodzaju wymaga zatwierdzenia proponowanego sposobu rozliczenia; następne pasujące faktury nie wymagają ponownego zatwierdzania.
- Poprawienie jednej faktury nie zmienia reguły dla kolejnych bez wyraźnego wyboru użytkownika.
- W ustawieniu początkowym przygotowanie wersji roboczej faktury sprzedaży nie powoduje jej wystawienia ani wysłania do KSeF.
- Upływ kilku miesięcy sam w sobie nie uruchamia automatycznego wystawiania; potrzebne jest włączenie opcji przez właściciela.
- Nowa wysokość abonamentu obowiązuje dla wskazanych przyszłych okresów; poprzednie wystawione faktury zachowują swoje kwoty.
- Dnia 1. każdego miesiąca przygotowywana jest faktura za ten miesiąc z terminem płatności 7 dni; w trybie ręcznym harmonogram nie pomija wymaganego kliknięcia właściciela.
- Zatwierdzenie faktury 3. dnia tego samego miesiąca nadaje jej datę wystawienia z 3. dnia oraz termin płatności 7 dni później; okres abonamentu i kwota za cały miesiąc pozostają bez zmian.
- Właściciel może dodać PDF lub zdjęcie faktury spoza KSeF, sprawdzić odczyt obok dokumentu i poprawić dane przed księgowaniem. Nieczytelny dokument nie blokuje ręcznego uzupełnienia danych.
- Zmiana formy opodatkowania w nowym roku nie przelicza wcześniejszego roku według nowych zasad.
- Pozostałe kryteria, w tym zakres samodzielnego zamknięcia miesiąca, do dopracowania.
- Przed zatwierdzeniem miesiąca użytkownik widzi bieżące wyliczenia, podsumowanie i wykryte braki; ostateczna paczka dokumentów wymaga jego kliknięcia.
- Poprawka księgowania pozostawia historię i pokazuje zmianę kwot. Wymagane korekty wysłanych dokumentów są powiązane z wcześniejszymi wersjami i czekają na zatwierdzenie właściciela.
- Pierwsza faktura rozpoczętego w trakcie miesiąca abonamentu obejmuje cały miesiąc; nie jest automatycznie proporcjonalnie pomniejszana.
- Zmiana samej kwoty znanego kosztu nie wymaga ponownego zatwierdzenia. Zmiana danych wpływających na sposób podatkowego rozliczenia powoduje zatrzymanie dokumentu i pokazanie konkretnej różnicy.
- Opóźnione zatwierdzenie nie powoduje utraty faktury za zaległy okres ani pominięcia faktury za kolejny okres.
- Roku nie można zamknąć przed zatwierdzeniem wszystkich jego miesięcy i rozwiązaniem wykrytych braków.
- Po zamknięciu roku właściciel może pobrać PDF z zestawieniem danych firmowych i pobrać go ponownie później.
- Zwykła edycja zamkniętego roku jest zablokowana. Korekta wymaga świadomego rozpoczęcia, podania powodu i pozostawia pełną historię poprzednich wersji.
- Nierozwiązana sprawa nie powoduje wysyłania więcej niż jednego ponownego przypomnienia dziennie; nowe błędy są zgłaszane od razu.
- Na MacBooku istnieje maksymalnie pięć rotacyjnych kopii, a aplikacja pokazuje datę ostatniej poprawnej kopii. Brak możliwości zapisania kopii nie jest traktowany jako powodzenie i powoduje powiadomienie.
- Poprawna kopia nie ujawnia zawartości bez hasła odzyskiwania; odtworzenie na nowym komputerze wymaga pojedynczego pliku kopii, hasła i zgodnej wersji aplikacji.
- Konto właściciela nie pozwala zalogować się wyłącznie samym hasłem bez dodatkowego kodu lub jednorazowego kodu odzyskiwania.
- Zaufane urządzenie ogranicza częstotliwość podawania dodatkowego kodu maksymalnie przez 30 dni; właściciel może wylogować wszystkie urządzenia i unieważnić utracony sprzęt.
- Każdą kopię można przenieść jako jeden plik na inny komputer i odtworzyć z niej komplet danych oraz dokumentów. Roczne pliki archiwalne pozostają zachowane niezależnie od rotacji kopii dziennych.
- Po zalogowaniu właściciel widzi na jednym ekranie stan bieżącego miesiąca, kwoty, terminy, sprawy wymagające reakcji oraz stan kopii zapasowej.

## Scope Boundaries

- Pierwsza wersja: tylko skala podatkowa. Ryczałt jest rozszerzeniem na później, zapisanym w dokumentacji; nie należy implementować go w ramach pierwszej wersji.
- Pierwsza wersja nie obsługuje rozliczania jednej faktury wieloma płatnościami. Założeniem biznesowym jest regulowanie faktur w całości.

- Roczny PIT: tylko zestawienie danych firmowych do zewnętrznej aplikacji. Pełne zeznanie, dane etatowe i wspólne rozliczenie z żoną są poza zakresem. Wymóg docelowej automatycznej wysyłki do US nie obejmuje pełnego rocznego PIT-u; obejmuje dokumenty urzędowe należące do zakresu Firemki.

- Jedna własna JDG, jedna polska firma jako odbiorca faktury; bez potrzeby obsługi pracowników i wielu firm.
- Obecny etap ustala zachowanie produktu. Nie obejmuje kodowania, commitów, publikacji ani wdrożenia.
- Pierwsza wersja obejmuje eksport dokumentów, instrukcję ręcznej wysyłki i zapis potwierdzeń. Automatyczna wysyłka do US i ZUS jest wymaganiem wersji docelowej, a przygotowanie do niej należy do zakresu planowania pierwszej wersji.

## Key Decisions

- Potwierdzone: zamknięcie roku wymaga osobnego kliknięcia właściciela po zatwierdzeniu miesięcy i rozwiązaniu braków.
- Potwierdzone: roczne zestawienie firmowe powstaje jako PDF dostępny przez przycisk pobierania; miejsce zapisu wybiera przeglądarka zgodnie ze swoimi ustawieniami.
- Potwierdzone: zamknięty rok jest tylko do odczytu; poprawki przechodzą przez osobny tryb korekty z powodem, historią i ponownym zatwierdzeniem dokumentów.
- Potwierdzone: przyjęto harmonogram e-maili dla faktury sprzedaży, terminów US/ZUS, nieopłaconej faktury klienta i błędów, z dziennym ograniczeniem ponowień.
- Potwierdzone: pięć ostatnich zaszyfrowanych kopii jest przechowywanych lokalnie na dysku MacBooka; przy niedostępnym komputerze kopia jest pobierana po odzyskaniu połączenia.
- Potwierdzone: pliki kopii są zabezpieczone osobnym hasłem wymaganym przy odtwarzaniu danych.
- Potwierdzone: kopia jest pojedynczym, przenośnym plikiem zawierającym całą aplikacyjną zawartość potrzebną do odtworzenia. Zamknięty rok otrzymuje dodatkowy trwały plik archiwalny.
- Potwierdzone: jedno konto właściciela jest chronione hasłem i kodem z aplikacji uwierzytelniającej; dostępne są awaryjne kody odzyskiwania.
- Potwierdzone: MacBook i telefon mogą być zaufane przez 30 dni; nowy lub unieważniony sprzęt wymaga ponownego pełnego logowania.
- Potwierdzone: „Mój miesiąc” jest głównym widokiem i zbiera czynności, dokumenty, szacowane należności, terminy oraz status kopii.

- Potwierdzone: obsługa płatności częściowych nie jest potrzebna w pierwszej wersji; klienci przeważnie płacą pełną kwotę.
- Potwierdzone: na start wystarczy skala. Ryczałt pozostaje w dokumentacji jako przyszłe rozszerzenie. Uściśla wcześniejsze wymaganie przygotowania do zmiany formy opodatkowania przy nowym roku.

- Potwierdzone: nierozwiązane braki dokumentów blokują zatwierdzenie miesiąca. Właściciel może oznaczyć dokument jako niezwiązany z firmą.

- Potwierdzone: roczne rozliczenie PIT i wspólne rozliczenie z żoną pozostają w istniejącej aplikacji właściciela; Firemka dostarcza zestawienie firmowe. Zastępuje propozycję pełnego PIT-u rocznego z analizy pomysłów.

- Potwierdzone: koszty AI głównie OpenAI, ewentualnie Anthropic; VPS od OVH.

- Potwierdzone: preferowany najem długoterminowy, leasing pozostaje dopuszczalny; ostateczny wybór zależy od oferty. Zakres produktu uwzględnia oba warianty.

- Potwierdzone: domowe ładowanie jest dokumentowane statystykami z kabla/aplikacji, eksportowanymi do CSV; aplikacja ma przygotować własną tabelę miesięcznego rozliczenia. Ogólna faktura za prąd jest wystawiana prywatnie na właściciela i zgodnie z jego decyzją nie będzie księgowana jako koszt.

- Potwierdzone: cena energii jest edytowalna z miesiącem rozpoczęcia obowiązywania, a wcześniejsze rozliczenia zachowują poprzednie stawki.

- Potwierdzone: domowe ładowanie — wejściowe Wh dzielimy przez 1000 i mnożymy przez 0,91 zł brutto/kWh. Stawka pochodzi od właściciela; nie jest wynikiem weryfikacji taryfy energii.

- Potwierdzone: obsługa domowego ładowania z CSV i sporadycznych faktur za ładowanie na stacjach publicznych należy do zakresu aplikacji.

- Potwierdzone: samochód elektryczny, wykorzystywany firmowo i prywatnie.

- Potwierdzone: powiadomienia e-mail na `pawel@sitback.pl` dla terminów, błędów i spraw wymagających reakcji właściciela.

- Potwierdzone: jednoznaczne dopasowania transakcji z wyciągu są automatyczne, a niejednoznaczne wymagają zatwierdzenia przedstawionej propozycji.

- Potwierdzone: ręczne oznaczanie płatności wystarcza w pierwszej wersji. Wersja docelowa ma odczytywać wgrywany miesięczny wyciąg i automatycznie uzupełniać płatności.

- Potwierdzone: rozliczenia są obliczane na bieżąco, a ostateczne dokumenty miesięczne powstają po zatwierdzeniu miesiąca przez właściciela. Docelowa automatyzacja wysyłki nie znosi tego kroku.

- Potwierdzone: ręczna wysyłka plików urzędowych wystarcza na początek. Docelowo aplikacja ma wysyłać je automatycznie; nie traktować tego jako opcjonalnego pomysłu. Dobór technologii i przygotowanie dokumentów od początku muszą uwzględniać ten kierunek.

- Potwierdzone: automatyczne księgowanie znanych, powtarzalnych kosztów z opcją edycji. Powód: użytkownik oczekuje ograniczenia ręcznych zatwierdzeń.
- Zastępuje wcześniejszą rekomendację zatwierdzania każdej partii powtarzalnych kosztów z dokumentu ideacji.
- Potwierdzone: aplikacja proponuje regułę przy pierwszej fakturze danego rodzaju, a właściciel zatwierdza ją raz przed automatycznym księgowaniem następnych podobnych dokumentów.
- Potwierdzone: edycja pojedynczej faktury jest oddzielona od opcjonalnej zmiany reguły na przyszłość, aby jednorazowy wyjątek nie zmieniał dalszych księgowań.
- Potwierdzone: faktura sprzedaży początkowo czeka na kliknięcie właściciela. Możliwość samodzielnego włączenia automatycznego wystawiania i wysyłania jest wymagana; moment przejścia zależy od oceny właściciela, a nie sztywnego okresu próbnego.
- Potwierdzone: stały abonament niezależny od liczby użytkowników, z możliwością edycji i ustalenia nowej wysokości na kolejne miesiące.
- Potwierdzone: fakturowanie z góry, 1. dnia miesiąca, z terminem płatności 7 dni.
- Potwierdzone: późniejsze ręczne zatwierdzenie w tym samym miesiącu oznacza bieżącą datę wystawienia i 7 dni na płatność od niej, nadal za cały miesiąc.
- Potwierdzone: odczytywanie faktur z PDF i zdjęć należy do pierwszej wersji. Zastępuje wcześniejsze odłożenie OCR w analizie pomysłów. Odczyt służy wprowadzaniu danych; nie jest samodzielnym podejmowaniem decyzji podatkowych.
- Potwierdzone: poprawki zachowują historię, pokazują wpływ na kwoty oraz prowadzą do przygotowania wymaganych korekt wysłanych dokumentów do zatwierdzenia przez właściciela.
- Potwierdzone: pierwsza faktura obejmuje cały miesiąc również przy rozpoczęciu działalności po 1. dniu.
- Potwierdzone: kwota i zwykła zmiana daty nie zatrzymują reguły kosztowej; zmiana sprzedawcy, kraju, waluty, VAT lub rodzaju usługi wymaga sprawdzenia.
- Potwierdzone: faktura zatwierdzona w kolejnym miesiącu zachowuje pierwotny okres abonamentu, ale otrzymuje bieżącą datę wystawienia i nowy 7-dniowy termin płatności; aplikacja ostrzega o opóźnieniu i przygotowuje także fakturę bieżącego miesiąca.

## Dependencies / Assumptions

- Dokładna data założenia, szczegóły umowy samochodu oraz podmioty wystawiające faktury, kraje i waluty zakupów pozostają nieustalone. Znane marki dostawców: OpenAI, Anthropic i OVH.
- „Znany, powtarzalny koszt” wymaga ustalenia reguły rozpoznawania; nie zakładamy, że każda faktura tego samego dostawcy ma być rozliczana identycznie.
- Propozycja: zmiana kwoty przy niezmienionym rodzaju usługi sama nie wymaga ręcznej obsługi. Nowy rodzaj wydatku lub brak potrzebnych danych wymaga wyjaśnienia.

## Open Questions

### Before Planning

- Brak nierozstrzygniętych decyzji produktowych blokujących przygotowanie planu.

### Deferred to Planning

- Technologia, struktura kodu, integracje i szczegółowe wykonanie sprawdzeń.
- Pobranie przykładowego CSV z aplikacji kabla, potwierdzenie kolumn i jednostki oraz źródła stawki 0,91 zł brutto/kWh. Przed automatycznym księgowaniem zweryfikować dopuszczalną dokumentację kosztu domowego ładowania i zasady VAT na aktualnych źródłach urzędowych. Prywatna faktura za energię nie będzie księgowana jako osobny koszt.
- Pobranie przykładowych faktur OpenAI, Anthropic i OVH oraz wybranej oferty lub umowy samochodu przed ustaleniem szczegółowych reguł księgowania tych dokumentów.
- Potwierdzenie dokładnej daty rozpoczęcia działalności i kompletu danych firmy oraz kontrahenta przed uruchomieniem produkcyjnym.
- Mechanizm bezpiecznego pobierania kopii z VPS na MacBooka, zachowanie po uśpieniu komputera, lokalny folder, szyfrowanie, rotacja pięciu kopii oraz okresowy test pełnego odtworzenia.
- Sposób przechowywania hasła kopii na bieżącym MacBooku bez dołączania go do pliku kopii oraz jasny proces odzyskania danych na nowym komputerze.
- Format pojedynczej przenośnej paczki, zgodność kopii ze starszymi i nowszymi wersjami aplikacji, kontrola kompletności przed usunięciem starszej kopii oraz bezpieczny proces odtworzenia bez przypadkowego nadpisania bieżących danych.
- Bank i dostępne formaty eksportu wyciągu; przygotowanie obsługi płatności na import oraz weryfikacja ponownego importu, aby nie powielać płatności ani wcześniej ręcznie zapisanych wpłat. Dopasowania płatności nie mogą tworzyć ponownie zaksięgowanego kosztu lub przychodu z faktury.
- Zweryfikowanie oficjalnych możliwości wysyłki, podpisu i uprawnień osobno dla wymaganych plików JPK i dokumentów ZUS oraz innych dokumentów faktycznie należących do zakresu. Pełny roczny PIT jest wyłączony. Ustalenie technologii umożliwiającej późniejszą automatyzację bez ponownego budowania zasad obliczania i generowania dokumentów.
- Zaplanowanie stanów dokumentu: przygotowany, wyeksportowany, wysłany, przyjęty lub odrzucony; powiązanie potwierdzeń z konkretną wersją, obsługa korekt i ponowień bez przypadkowej podwójnej wysyłki. Eksport nie oznacza przyjęcia przez urząd.

## Next Step

Użyć `$dev-plan`, aby przygotować etapowy plan techniczny z weryfikacją aktualnych zasad KSeF, KPiR, VAT, PIT i ZUS, zakresem pierwszej wersji, kolejnością prac oraz kryteriami odbioru. Ten dokument nie upoważnia do rozpoczęcia implementacji, commitów, publikacji ani wdrożenia.
