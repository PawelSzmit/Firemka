#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Użycie: $0 https://adres-firemki /pełna/ścieżka/projektu" >&2
  exit 2
fi

base_url="${1%/}"
project_dir="$2"
allowed_ports=" ${FIREMKA_ALLOWED_PUBLIC_PORTS:-22 80 443} "
failures=0

[[ "$base_url" == https://* && "$project_dir" == /* ]] \
  || { echo "Adres musi używać HTTPS, a katalog projektu musi być pełną ścieżką." >&2; exit 2; }
[[ "$(uname -s)" == "Linux" ]] || { echo "Audyt VPS działa wyłącznie na Linuxie." >&2; exit 2; }

for command_name in docker curl openssl df ss awk; do
  command -v "$command_name" >/dev/null || { echo "BRAK: $command_name" >&2; failures=$((failures + 1)); }
done
(( failures == 0 )) || exit 1

echo "System: $(. /etc/os-release && printf '%s %s' "$NAME" "$VERSION_ID")"
echo "Docker: $(docker version --format '{{.Server.Version}}')"

if command -v ufw >/dev/null; then
  ufw status | sed -n '1,12p'
elif command -v firewall-cmd >/dev/null; then
  firewall-cmd --state
  firewall-cmd --list-ports
else
  echo "BRAK: nie wykryto ufw ani firewalld." >&2
  failures=$((failures + 1))
fi

while read -r endpoint; do
  port="${endpoint##*:}"
  [[ "$port" =~ ^[0-9]+$ ]] || continue
  if [[ "$allowed_ports" != *" $port "* ]]; then
    echo "NIEOCZEKIWANY PORT PUBLICZNY: $endpoint" >&2
    failures=$((failures + 1))
  fi
done < <(ss -ltnH | awk '$4 !~ /127\.0\.0\.1:|\[::1\]:/ {print $4}')

compose_images="$({
  cd "$project_dir"
  docker compose -f compose.yaml -f deploy/compose.production.yaml config --quiet
  docker compose -f compose.yaml -f deploy/compose.production.yaml config --images
})"
printf 'Obrazy z konfiguracji:\n%s\n' "$compose_images"
while read -r image; do
  [[ -n "$image" ]] || continue
  if [[ "$image" == *:local || "$image" == *:latest ]]; then
    echo "NIEDOZWOLONY OBRAZ PRODUKCYJNY: $image" >&2
    failures=$((failures + 1))
  fi
done <<<"$compose_images"

(
  cd "$project_dir"
  docker compose -f compose.yaml -f deploy/compose.production.yaml ps
  docker compose -f compose.yaml -f deploy/compose.production.yaml images
)

headers="$(curl --fail --silent --show-error --head --max-time 20 "$base_url/Account/Login")"
grep -qi '^strict-transport-security:' <<<"$headers" \
  || { echo "BRAK: nagłówek HSTS." >&2; failures=$((failures + 1)); }
grep -qi '^x-content-type-options: nosniff' <<<"$headers" \
  || { echo "BRAK: X-Content-Type-Options." >&2; failures=$((failures + 1)); }

"$project_dir/deploy/scripts/operations/monitor-health.sh" "$base_url"

if command -v apt-get >/dev/null; then
  pending_updates="$(apt-get -s upgrade 2>/dev/null | awk '/^Inst / {count++} END {print count+0}')"
  echo "Oczekujące aktualizacje pakietów: $pending_updates (audyt niczego nie instaluje)."
fi

(( failures == 0 )) || { echo "Audyt zakończony z liczbą problemów: $failures" >&2; exit 1; }
echo "Audyt odczytowy VPS zakończony bez wykrytych problemów."
