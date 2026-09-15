#!/usr/bin/env bash
set -euo pipefail

contracts_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export XML_CATALOG_FILES="$contracts_dir/catalog.xml"
export SGML_CATALOG_FILES=""

validate() {
  local schema="$1"
  local fixture="$2"

  xmllint --catalogs --nonet --noout \
    --schema "$contracts_dir/$schema" \
    "$contracts_dir/$fixture"
}

validate "fa3-2026-09-09.xsd" "fixtures/fa3-synthetic-from-ksef-sdk.xml"
validate "jpk-v7m3-2026-09-09.xsd" "fixtures/jpk-v7m3-official-person.xml"
validate "jpk-pkpir3-2026-09-09.xsd" "fixtures/jpk-pkpir3-synthetic.xml"
validate "kedu-2.27-2026-09-09.xsd" "fixtures/kedu-2.27-official-dra.xml"
