#!/usr/bin/env bash
# Generates every sample PDF into a folder (Release build of the current tree).
#   tools/pdf-compare/generate-samples.sh <output-dir>
set -euo pipefail
OUT="${1:?output directory}"
ROOT="$(git rev-parse --show-toplevel)"
dotnet build "$ROOT/samples/TerraPDF.Sample" -c Release >/dev/null
rm -rf "$OUT" && mkdir -p "$OUT"
dotnet run -c Release --no-build --project "$ROOT/samples/TerraPDF.Sample" -- "$OUT" >/dev/null
echo "$(ls "$OUT"/*.pdf | wc -l | tr -d ' ') PDFs in $OUT"
