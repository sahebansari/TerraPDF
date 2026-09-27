#!/usr/bin/env bash
# Byte-for-byte comparison of two sample folders. Encrypted samples (12*) are skipped:
# their file identifier is random, so they differ on every run.
#   tools/pdf-compare/compare-bytes.sh <before-dir> <after-dir>
set -uo pipefail
A="${1:?before directory}"; B="${2:?after directory}"
same=0; diff=0
for f in "$A"/*.pdf; do
  name="$(basename "$f")"
  case "$name" in 12*) continue;; esac
  if cmp -s "$f" "$B/$name"; then same=$((same + 1)); else diff=$((diff + 1)); echo "DIFF: $name"; fi
done
echo "identical: $same, different: $diff"
[ "$diff" -eq 0 ]
