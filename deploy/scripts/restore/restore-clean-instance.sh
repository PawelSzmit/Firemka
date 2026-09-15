#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Użycie: $0 /folder/po-restore-extract /pusty/folder-plików /pusty/folder-kluczy" >&2
  exit 2
fi
if [[ -z "${PGHOST:-}" || -z "${PGDATABASE:-}" || -z "${PGUSER:-}" || -z "${PGPASSFILE:-}" ]]; then
  echo "Ustaw PGHOST, PGDATABASE, PGUSER i PGPASSFILE. Hasła nie podawaj w argumentach." >&2
  exit 2
fi
if [[ -z "${FIREMKA_WEB_DLL:-}" || "$FIREMKA_WEB_DLL" != /* || ! -f "$FIREMKA_WEB_DLL" ]]; then
  echo "Ustaw FIREMKA_WEB_DLL na pełną ścieżkę do Firemka.Web.dll z odtwarzanej wersji aplikacji." >&2
  exit 2
fi
if [[ -z "${ConnectionStrings__Firemka:-}" ]]; then
  echo "Ustaw ConnectionStrings__Firemka dla migratora bieżącej wersji aplikacji." >&2
  exit 2
fi

extracted="$1"
files_target="$2"
keys_target="$3"
dump="$extracted/database/firemka.dump"
manifest="$extracted/manifest.json"
[[ -f "$dump" && -f "$manifest" ]] || { echo "Brakuje zweryfikowanego zrzutu lub manifestu." >&2; exit 2; }
command -v psql >/dev/null
command -v pg_restore >/dev/null
command -v jq >/dev/null
command -v dotnet >/dev/null
if command -v sha256sum >/dev/null; then
  calculate_sha256() { sha256sum "$1" | awk '{print toupper($1)}'; }
elif command -v shasum >/dev/null; then
  calculate_sha256() { shasum -a 256 "$1" | awk '{print toupper($1)}'; }
else
  echo "Brakuje narzędzia sha256sum albo shasum." >&2
  exit 2
fi

jq -e '.formatVersion == 1 and (.entries | type == "array") and (.tableRecordCounts | type == "object")' "$manifest" >/dev/null
while IFS=$'\t' read -r archived_path expected_hash; do
  [[ "$archived_path" != /* && "$archived_path" != *".."* && "$archived_path" != *\\* ]] \
    || { echo "Niebezpieczna ścieżka w manifeście." >&2; exit 1; }
  source_path="$extracted/$archived_path"
  [[ -f "$source_path" ]] || { echo "Brakuje pliku $archived_path." >&2; exit 1; }
  actual_hash="$(calculate_sha256 "$source_path")"
  [[ "$actual_hash" == "$expected_hash" ]] || { echo "Niezgodny skrót pliku $archived_path." >&2; exit 1; }
done < <(jq -r '.entries[] | [.path, .sha256] | @tsv' "$manifest")

while IFS=$'\t' read -r table expected; do
  [[ "$table" =~ ^[A-Za-z0-9_]+$ && "$expected" =~ ^[0-9]+$ ]] \
    || { echo "Nieprawidłowa tabela albo liczba rekordów w manifeście." >&2; exit 1; }
done < <(jq -r '.tableRecordCounts | to_entries[] | [.key, (.value|tostring)] | @tsv' "$manifest")

mkdir -p "$files_target" "$keys_target"
if find "$files_target" -mindepth 1 -print -quit | grep -q . \
  || find "$keys_target" -mindepth 1 -print -quit | grep -q .; then
  echo "Folder plików albo kluczy nie jest pusty. Odtworzenie przerwane." >&2
  exit 1
fi

user_tables="$(psql -X -Atqc "SELECT count(*) FROM pg_tables WHERE schemaname='public'")"
if [[ "$user_tables" != "0" ]]; then
  echo "Docelowa baza nie jest pusta. Odtworzenie przerwane bez zmian." >&2
  exit 1
fi

pg_restore --exit-on-error --no-owner --no-privileges --dbname "$PGDATABASE" "$dump"
if [[ -d "$extracted/private-files" ]]; then
  cp -R "$extracted/private-files/." "$files_target/"
fi
cp -R "$extracted/data-protection-keys/." "$keys_target/"

dotnet "$FIREMKA_WEB_DLL" --migrate

while IFS=$'\t' read -r archived_path expected_hash; do
  case "$archived_path" in
    private-files/*) restored_path="$files_target/${archived_path#private-files/}" ;;
    data-protection-keys/*) restored_path="$keys_target/${archived_path#data-protection-keys/}" ;;
    *) continue ;;
  esac
  actual_hash="$(calculate_sha256 "$restored_path")"
  [[ "$actual_hash" == "$expected_hash" ]] || { echo "Niezgodny skrót odtworzonego pliku $archived_path." >&2; exit 1; }
done < <(jq -r '.entries[] | [.path, .sha256] | @tsv' "$manifest")

while IFS=$'\t' read -r table expected; do
  [[ "$table" =~ ^[A-Za-z0-9_]+$ ]] || { echo "Niebezpieczna nazwa tabeli w manifeście." >&2; exit 1; }
  actual="$(psql -X -Atqc "SELECT count(*) FROM \"$table\"")"
  [[ "$actual" == "$expected" ]] || { echo "Niezgodna liczba rekordów tabeli $table." >&2; exit 1; }
done < <(jq -r '.tableRecordCounts | to_entries[] | [.key, (.value|tostring)] | @tsv' "$manifest")
echo "Odtworzenie zakończone. Liczby rekordów odpowiadają manifestowi. Przed przełączeniem ruchu uruchom health check aplikacji."
