#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Użycie: $0 https://adres-firemki [ścieżka-danych]" >&2
  exit 2
fi

base_url="${1%/}"
data_path="${2:-/var/lib/docker}"
minimum_free_percent="${FIREMKA_MINIMUM_FREE_PERCENT:-20}"
certificate_days="${FIREMKA_CERTIFICATE_WARNING_DAYS:-14}"

[[ "$base_url" == https://* ]] || { echo "Adres musi używać HTTPS." >&2; exit 2; }
[[ "$minimum_free_percent" =~ ^[0-9]+$ && "$certificate_days" =~ ^[0-9]+$ ]] \
  || { echo "Progi monitoringu muszą być liczbami całkowitymi." >&2; exit 2; }

host="${base_url#https://}"
host="${host%%/*}"
host="${host%%:*}"
[[ -n "$host" ]] || { echo "Nie udało się odczytać nazwy hosta." >&2; exit 2; }

used_percent="$(df -P "$data_path" | awk 'NR==2 {gsub(/%/, "", $5); print $5}')"
free_percent="$((100 - used_percent))"
if (( free_percent < minimum_free_percent )); then
  echo "ALARM: wolne miejsce ${free_percent}% jest poniżej progu ${minimum_free_percent}%." >&2
  exit 1
fi

if ! openssl s_client -connect "$host:443" -servername "$host" </dev/null 2>/dev/null \
  | openssl x509 -checkend "$((certificate_days * 86400))" -noout >/dev/null; then
  echo "ALARM: certyfikat jest nieważny albo wygaśnie w ciągu ${certificate_days} dni." >&2
  exit 1
fi

health="$(curl --fail --silent --show-error --max-time 20 "$base_url/health")"
[[ "$health" == "Healthy" ]] || { echo "ALARM: /health nie zwrócił Healthy." >&2; exit 1; }

echo "OK: HTTPS, certyfikat, /health i wolne miejsce są poprawne. Wolne: ${free_percent}%."
