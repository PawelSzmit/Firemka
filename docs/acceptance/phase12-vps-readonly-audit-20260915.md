# Odczytowy audyt VPS-a przed Firemką

Data: 2026-09-15  
Zakres: wyłącznie odczyt; bez instalacji, zmian konfiguracji, restartów i deploya  
Połączenie: zapisane `moj-vps` → `debian@57.128.200.20`  
Host: `vps-249debb9`

## Wynik skrócony

**Miejsce na dysku jest wystarczające dla Firemki, ale serwer nie jest jeszcze gotowy do bezpiecznego uruchomienia aplikacji.** Najważniejsze blokady to zajęte porty 80/443 przez doradca.cloud, brak domeny wymaganej przez obecny wariant Caddy oraz ograniczony zapas pamięci bez swapu przy równoczesnej pracy istniejących usług.

## Zebrane dane

- system: Debian 12, Linux `x86_64`, 4 vCPU;
- dysk główny `/dev/sda1`: 74 GB łącznie, 43 GB zajęte, 28 GB wolne (61%); inode: 30% zajęte;
- pamięć: 7,6 GiB łącznie, 3,9 GiB dostępne w chwili kontroli, swap nie skonfigurowano;
- Docker: 29.3.1, Compose v5.1.1, 10 uruchomionych kontenerów i 12 obrazów;
- Docker raportuje 33,97 GB obrazów oraz 28,36 GB cache budowania. Z tego 30,16 GB obrazów i 27,64 GB cache jest oznaczone jako możliwe do odzyskania, ale niczego nie usuwano;
- aktywne usługi obejmują kontenery doradca.cloud, n8n, Qdrant i Umami. Porty 80 i 443 są publikowane przez `posrednik-frontend-1`;
- UFW jest aktywny: domyślnie odrzuca ruch przychodzący, a jawnie dopuszcza 22/tcp, 80/tcp i 443/tcp. Działa też fail2ban i unattended-upgrades;
- symulacja aktualizacji wykazała sześć dostępnych aktualizacji pakietów Docker/containerd/Buildx/Compose. Nie instalowano ich;
- wszystkie obecne kontenery używają `json-file` bez indywidualnie ustawionego limitu rotacji logów. Firemka ma limity w swoim pliku produkcyjnym, lecz nie została uruchomiona.

## Czy Firemka się zmieści?

Tak pod względem samego miejsca na dysku. Cztery obrazy przygotowane dla Firemki mają łącznie około 1,7 GB:

- WWW: 504 MB;
- worker: 572 MB;
- Caddy: 163 MB;
- PostgreSQL: 439 MB.

Przy 28 GB wolnego miejsca pozostaje zapas na obrazy, początkową bazę i pliki aplikacji. Nie oznacza to zgody na czyszczenie istniejącego cache ani gwarancji pojemności dla nieograniczonego magazynu dokumentów.

## Co blokuje gotowość

1. **Porty:** doradca.cloud już zajmuje 80/443. Firemka nie może otrzymać tych portów bez zmian w istniejącym reverse proxy albo użycia innego portu.
2. **Adres IP zamiast domeny:** obecny produkcyjny Compose wymaga `FIREMKA_DOMAIN` i konfiguracji Caddy dla HTTPS. Caddy nie może wystawić zwykłego publicznego certyfikatu dla nieprzygotowanej domeny; dostęp po samym IP wymaga osobno zaprojektowanego wariantu dostępu i nie został wdrożony.
3. **Pamięć:** limity Firemki wynoszą łącznie około 3,5 GiB i 6,5 vCPU dla pięciu usług. Przy obecnych usługach oraz 7,6 GiB RAM i braku swapu jest to możliwe dla małego pilotażu, ale nie ma bezpiecznego zapasu na skok obciążenia. Przed uruchomieniem trzeba potwierdzić wariant zasobów albo zwiększyć serwer.
4. **A3–A5:** audyt potwierdził system, dysk, UFW i działającego Dockera, ale nie potwierdza jeszcze bezpiecznej współpracy z istniejącym reverse proxy, publicznego HTTPS dla Firemki, monitoringu Firemki ani rotacji logów jej kontenerów.

## Granica audytu

Nie wykonano `docker compose up`, budowania, migracji, zmian UFW, aktualizacji pakietów, zmian Caddy, DNS, certyfikatów, konfiguracji doradca.cloud ani żadnego deploya. Wynik zapisuje stan zastany i pozostawia A3–A5 niezaznaczone.
