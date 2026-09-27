#!/usr/bin/env bash
# Compares page throughput, CPU and memory of two TerraPDF versions inside containers
# with the same resource limits. The harness in this folder is copied unchanged into a
# temporary worktree of the base version, so both images run exactly the same program.
#
#   benchmarks/TerraPDF.Throughput/run-comparison.sh [base-ref] [cpus] [memory] [seconds]
#   e.g. benchmarks/TerraPDF.Throughput/run-comparison.sh 61f5c50 2 1g 30
#
# Results: printed, and appended as JSON lines to BenchmarkDotNet.Artifacts/throughput/results.jsonl
set -euo pipefail

BASE_REF="${1:-61f5c50}"
CPUS="${2:-2}"
MEMORY="${3:-1g}"
SECONDS_TO_RUN="${4:-30}"

ROOT="$(git rev-parse --show-toplevel)"
HARNESS="benchmarks/TerraPDF.Throughput"
OUT="$ROOT/BenchmarkDotNet.Artifacts/throughput"
WORKTREE="$(mktemp -d)/terrapdf-base"
mkdir -p "$OUT"

cleanup() { git -C "$ROOT" worktree remove --force "$WORKTREE" >/dev/null 2>&1 || true; }
trap cleanup EXIT

echo "== Preparing base version $BASE_REF"
git -C "$ROOT" worktree add --detach "$WORKTREE" "$BASE_REF" >/dev/null
mkdir -p "$WORKTREE/$HARNESS"
cp "$ROOT/$HARNESS"/*.cs "$ROOT/$HARNESS"/*.csproj "$ROOT/$HARNESS/Dockerfile" "$WORKTREE/$HARNESS/"
cp "$ROOT/.dockerignore" "$WORKTREE/"

echo "== Building images"
docker build -q -f "$HARNESS/Dockerfile" -t terrapdf-throughput:base "$WORKTREE" >/dev/null
docker build -q -f "$HARNESS/Dockerfile" -t terrapdf-throughput:current "$ROOT" >/dev/null

run() {
  local tag="$1" label="$2"
  echo
  echo "== $label (--cpus=$CPUS --memory=$MEMORY)"
  docker run --rm --cpus="$CPUS" --memory="$MEMORY" -v "$OUT:/out" "terrapdf-throughput:$tag" \
    --seconds "$SECONDS_TO_RUN" --warmup 10 --label "$label" --json /out/results.jsonl
}

run base    "before ($BASE_REF)"
run current "after ($(git -C "$ROOT" rev-parse --short HEAD)+working tree)"
