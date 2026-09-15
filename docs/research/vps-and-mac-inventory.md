# Firemka — inwentaryzacja VPS i MacBooka

Status: nie rozpoczęto — brak wskazanego hosta i dostępu odczytowego.  
Zasada: ten dokument nie zawiera haseł, tokenów, adresów prywatnych ani danych klientów.

## Cel audytu VPS

Przed wdrożeniem sprawdzić tylko odczytowo:

| Obszar | Dowód do zebrania | Status |
|---|---|---|
| system i architektura | wersja systemu, CPU, RAM, dysk, swap | brak dostępu |
| kontenery | Docker/Compose, uruchomione usługi, wolumeny, limity zasobów | brak dostępu |
| sieć | domena, DNS, reverse proxy, certyfikat TLS, otwarte porty | brak dostępu |
| poczta | dostępny SMTP, domena nadawcy, limity i sposób przechowywania sekretu | brak dostępu |
| kopie | wolne miejsce, snapshot VPS, procedura odtworzenia bez nadpisania | brak dostępu |
| monitoring | health check, alarm dysku, certyfikatu i nieudanej kopii | brak dostępu |

## Bezpieczne polecenia odczytowe po wskazaniu hosta

```text
uname -a
cat /etc/os-release
nproc
free -h
df -h
docker version
docker compose version
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'
ss -tulpn
```

Wynik ma zostać zanonimizowany przed zapisaniem do dokumentacji. Nie należy wklejać `docker inspect`, całych plików środowiskowych ani list sekretów do czatu lub repozytorium.

## MacBook — wymagania dla kopii

| Obszar | Ustalony kierunek | Dane do potwierdzenia |
|---|---|---|
| folder | domyślnie `~/Documents/Firemka/Backups` | właściciel potwierdza lub wybiera inny katalog |
| uruchamianie | klient macOS przy logowaniu i po wybudzeniu przez `launchd` | wersja macOS i możliwość instalacji klienta |
| hasło odzyskiwania | przechowywane w Pęku kluczy lokalnie, dodatkowo poza kopią | miejsce dodatkowego bezpiecznego zapisu |
| rotacja | pięć poprawnie zweryfikowanych kopii | wymagane miejsce na dysku po oszacowaniu rozmiaru |
| odtworzenie | tylko do czystej instancji lub po dodatkowej kopii bieżącej | test na drugim środowisku przed produkcją |

## Kryterium zakończenia

Ta inwentaryzacja jest zakończona dopiero, gdy w tabelach pojawią się dowody z właściwego VPS i MacBooka. Do tego czasu żadna decyzja o limicie OCR, sposobie SMTP, wdrożeniu lub harmonogramie kopii nie jest zatwierdzona.

## Źródła

- [Plan techniczny — jednostki 0, 11 i 12](../plans/2026-09-09-jdg-application-plan.md)
- [Kontekst aktywnego zadania](../active/firemka-jdg/firemka-jdg-kontekst.md)
