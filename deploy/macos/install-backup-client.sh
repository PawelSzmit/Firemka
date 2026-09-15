#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Użycie: $0 https://adres-firemki /pełna/ścieżka/folderu-kopii" >&2
  exit 2
fi

server_url="$1"
backup_folder="$2"
if [[ "$server_url" != https://* || "$backup_folder" != /* ]]; then
  echo "Adres musi używać HTTPS, a folder musi być pełną ścieżką." >&2
  exit 2
fi

case "$(uname -m)" in
  arm64) runtime="osx-arm64" ;;
  x86_64) runtime="osx-x64" ;;
  *) echo "Nieobsługiwana architektura Maca." >&2; exit 2 ;;
esac

app_dir="$HOME/Library/Application Support/Firemka/BackupClient"
config_dir="$HOME/Library/Application Support/Firemka"
log_dir="$HOME/Library/Logs/Firemka"
launch_agents="$HOME/Library/LaunchAgents"
config_path="$config_dir/backup-client.json"
plist_path="$launch_agents/pl.firemka.backup.plist"
mkdir -p "$app_dir" "$config_dir" "$log_dir" "$launch_agents" "$backup_folder"

publish_lock_dir="$(mktemp -d "${TMPDIR:-/tmp}/firemka-backup-publish-locks.XXXXXX")"
cleanup_publish_locks() { rm -rf -- "$publish_lock_dir"; }
trap cleanup_publish_locks EXIT

dotnet publish src/Firemka.BackupClient/Firemka.BackupClient.csproj \
  --configuration Release --runtime "$runtime" --self-contained true \
  --output "$app_dir" /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:FiremkaIsolatedLockDirectory="$publish_lock_dir"

escaped_server="${server_url//\\/\\\\}"
escaped_server="${escaped_server//\"/\\\"}"
escaped_folder="${backup_folder//\\/\\\\}"
escaped_folder="${escaped_folder//\"/\\\"}"
printf '{"serverUrl":"%s","backupFolder":"%s"}\n' "$escaped_server" "$escaped_folder" > "$config_path"
chmod 600 "$config_path"

client_path="$app_dir/Firemka.BackupClient"
cp deploy/macos/pl.firemka.backup.plist.template "$plist_path"
plutil -replace ProgramArguments.0 -string "$client_path" "$plist_path"
plutil -replace ProgramArguments.2 -string "$config_path" "$plist_path"
plutil -replace StandardOutPath -string "$log_dir/backup.log" "$plist_path"
plutil -replace StandardErrorPath -string "$log_dir/backup-error.log" "$plist_path"
chmod 600 "$plist_path"

if ! security find-generic-password -s pl.firemka.backup.token -a "$USER" >/dev/null 2>&1 \
  || ! security find-generic-password -s pl.firemka.backup.recovery -a "$USER" >/dev/null 2>&1; then
  echo "Instalacja przygotowana, ale nie uruchomiona. Dodaj w aplikacji Dostęp do pęku kluczy dwa hasła internetowe/ogólne zgodnie z instrukcją:" >&2
  echo "  pl.firemka.backup.token oraz pl.firemka.backup.recovery; konto: $USER" >&2
  echo "Potem uruchom ponownie ten instalator. Sekretów nie podawaj jako argumentów terminala." >&2
  exit 3
fi

"$client_path" run --config "$config_path"
launchctl bootout "gui/$(id -u)/pl.firemka.backup" >/dev/null 2>&1 || true
launchctl bootstrap "gui/$(id -u)" "$plist_path"
echo "Klient kopii został sprawdzony i uruchomiony."
