#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repository_root"
dotnet_bin="${DOTNET_BIN:-dotnet}"

"$dotnet_bin" restore Firemka.sln --locked-mode
"$dotnet_bin" format Firemka.sln --no-restore --verify-no-changes
"$dotnet_bin" build Firemka.sln --no-restore --configuration Release --warnaserror
"$dotnet_bin" test Firemka.sln --no-restore --configuration Release
command -v jq >/dev/null || { echo "Brakuje jq wymaganego do oceny skanu zależności." >&2; exit 1; }
vulnerability_report="$("$dotnet_bin" list Firemka.sln package \
  --vulnerable --include-transitive --no-restore --format json --output-version 1)"
printf '%s\n' "$vulnerability_report"
jq -e \
  '[.projects[].frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?)] | length == 0' \
  <<<"$vulnerability_report" >/dev/null \
  || { echo "Skan zależności wykrył pakiet ze znaną podatnością." >&2; exit 1; }
"$dotnet_bin" ef migrations has-pending-model-changes \
  --project src/Firemka.Infrastructure --startup-project src/Firemka.Web \
  --no-build --configuration Release

bash docs/research/contracts/validate-fixtures.sh
bash -n deploy/scripts/restore/restore-clean-instance.sh
bash -n deploy/scripts/operations/audit-vps.sh
bash -n deploy/scripts/operations/monitor-health.sh
bash -n deploy/macos/install-backup-client.sh
if command -v plutil >/dev/null; then
  plutil -lint deploy/macos/pl.firemka.backup.plist.template
fi

command -v docker >/dev/null || { echo "Brakuje Dockera wymaganego do kontroli obrazow." >&2; exit 1; }
docker scout version >/dev/null \
  || { echo "Brakuje Docker Scout wymaganego do skanu obrazow." >&2; exit 1; }
docker compose -f compose.yaml -f deploy/compose.production.yaml \
  --env-file .env.example config --quiet
docker build --file deploy/Dockerfile --target web --tag firemka-phase12-gate:web .
docker build --file deploy/Dockerfile --target worker --tag firemka-phase12-gate:worker .
docker build --file deploy/Caddy.Dockerfile --tag firemka-phase12-gate:caddy .
docker build --file deploy/Postgres.Dockerfile --tag firemka-phase12-gate:postgres .
for image in web worker caddy postgres; do
  docker scout cves --only-severity critical,high --exit-code \
    "firemka-phase12-gate:${image}"
done
docker run --rm --entrypoint /bin/sh firemka-phase12-gate:web \
  -c 'pg_dump --version && jq --version && test "$(id -u)" = 10001 && test -f /app/Firemka.Web.dll'
docker run --rm --entrypoint /bin/sh firemka-phase12-gate:worker \
  -c 'pg_dump --version && pdftotext -v 2>&1 | head -1 && tesseract --version | head -1 && test "$(id -u)" = 10001 && test -f /app/Firemka.Worker.dll'
docker run --rm --entrypoint caddy \
  --env FIREMKA_DOMAIN=localhost --env CADDY_EMAIL=local@example.test \
  firemka-phase12-gate:caddy validate --config /etc/caddy/Caddyfile
docker run --rm --entrypoint /bin/sh firemka-phase12-gate:caddy \
  -c 'test "$(id -u)" = 10002 && test "$XDG_DATA_HOME" = /data && test "$(caddy version)" = v2.11.4'
docker run --rm --entrypoint /bin/sh firemka-phase12-gate:postgres \
  -c 'test "$(id -u)" != 0 && test ! -e /usr/local/bin/gosu && postgres --version'

if [[ -n "${FIREMKA_TEST_POSTGRES:-}" ]]; then
  "$dotnet_bin" test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj \
    --no-restore --configuration Release --filter FullyQualifiedName~Postgres
elif [[ "${PHASE12_REQUIRE_POSTGRES:-0}" == "1" ]]; then
  echo "Brakuje FIREMKA_TEST_POSTGRES wymaganego dla pełnej bramki." >&2
  exit 1
else
  echo "UWAGA: testy PostgreSQL pominięte. Pełna bramka wymaga FIREMKA_TEST_POSTGRES."
fi

echo "Lokalna bramka Fazy 12 zakończona poprawnie. To nie jest zgoda na wdrożenie."
