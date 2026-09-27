# Benchmarks

TerraPDF ships a [BenchmarkDotNet](https://benchmarkdotnet.org/) suite in
`benchmarks/TerraPDF.Benchmarks/`. It measures execution time **and** managed
allocations of the main pipelines, so performance regressions and hotspots can be
tracked from one change to the next.

Every document benchmark composes, lays out and serializes a PDF into a reused
in-memory stream: disk I/O is never part of the numbers.

## What is measured

| Class | Scenarios |
|-------|-----------|
| `EndToEndBenchmarks` | `HelloWorld` (fixed per-document cost), invoice with 30 and 300 line items (header, footer, logo, table, totals) |
| `TextBenchmarks` | 10 / 100 / 500 pages of wrapped paragraphs, the same volume as per-word styled spans, and non-WinAnsi text with a built-in font |
| `TableBenchmarks` | 100 / 1 000 / 10 000 rows: plain table with a repeating header, and a table with row and column spans |
| `FontBenchmarks` | TrueType parsing (Lato, Noto Sans Devanagari), glyph subsetting, 10-page documents in an embedded Latin font and in Devanagari (GSUB shaping) |
| `ImageBenchmarks` | PNG and alpha PNG decoding; documents placing the same RGBA PNG (warm and cold image cache), RGB PNG (embedded without decoding), alpha PNG and JPEG 1 and 40 times |
| `EncryptionBenchmarks` | 20-page document unencrypted vs AES-128 vs AES-256 |
| `QrCodeBenchmarks` | QR code generation (short/long payload, ECC level L and H) |
| `BarcodeBenchmarks` | Code128 encoding, a document with 100 QR codes |
| `CanvasBenchmarks` | 10 / 100 dense vector canvases (dashed lines, pies, gradients, rotated text) |

The benchmark project can access the library's internals (`InternalsVisibleTo`),
so decoders and encoders are also measured in isolation.

## Running

Always run in **Release**, on AC power, with other heavy applications closed.

```sh
# Everything (takes a while)
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*'

# One class or one method
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*TableBenchmarks*'
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*FontBenchmarks.SubsetLato'

# Faster, less precise run while iterating
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*Text*' --job short

# Smoke test: run each benchmark once to check nothing throws
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*' --job dry

# List benchmarks without running them
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --list flat
```

To compare runtimes, first change `<TargetFramework>` to
`<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>` in the benchmark
project, then:

```sh
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -f net10.0 -- --filter '*' --runtimes net8.0 net10.0
```

## Reading the results

Results are written to `BenchmarkDotNet.Artifacts/results/` (git-ignored) as a
GitHub-flavoured Markdown table and a full JSON report per class.

- **Mean** — average time per operation.
- **Allocated** — managed memory allocated per operation. Often the first sign of
  a hotspot: allocations that grow faster than the input (rows, pages) point to
  quadratic work or needless copies.
- **Gen0 / Gen1 / Gen2** — garbage collections per 1 000 operations. Gen2
  collections on a single document usually mean large temporary buffers.
- **Ratio** — relative to the baseline method of the class, where one is marked.

For scaling benchmarks (`Pages`, `Rows`, `Count`), check that time and allocations
grow roughly linearly with the parameter.

## Tracking a change

1. On `master`, run the relevant benchmarks and keep the JSON reports:
   ```sh
   dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*Table*' --artifacts ./perf/before
   ```
2. On your branch, run the same filter into another folder:
   ```sh
   dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*Table*' --artifacts ./perf/after
   ```
3. Compare the two Markdown tables, or diff the JSON reports with the
   [ResultsComparer](https://github.com/dotnet/performance/tree/main/src/tools/ResultsComparer)
   tool from `dotnet/performance`.

Benchmarks are not run in CI: shared runners are too noisy for reliable numbers.
CI still builds the project, so the suite always compiles.

## Throughput: pages per second in a container

BenchmarkDotNet measures single operations on an idle machine. To see what a service can
deliver, `benchmarks/TerraPDF.Throughput` generates documents in parallel for a fixed time
and reports:

- **pages per second and per minute**, the headline figure;
- **CPU time per page**, and the share of the available CPUs used;
- **allocated memory per page**, GC counts and GC pause share;
- **peak working set** and GC heap size;
- **latency** per document (p50, p95).

It uses Server GC, like an ASP.NET Core service, and one worker per CPU visible to the process.
Two documents are measured:

| Scenario | Content |
|---|---|
| `invoice` | 1 page: RGBA PNG logo, addresses, 12-line table, totals, payment QR code, "Page x of y" footer |
| `report` | 19 pages: cover with logo, 6 chapters of justified text, bar and pie charts (vector canvas), 18-row financial tables, running header with photo, "Page x of y" footer |

Run it directly:

```sh
dotnet run -c Release --project benchmarks/TerraPDF.Throughput -- --scenario all --seconds 30
```

Or compare two versions of the library in containers with the same CPU and memory limits
(Docker required). The script builds the same harness against a base commit (in a temporary
git worktree) and against your working tree:

```sh
# base ref, CPUs, memory, seconds per scenario
benchmarks/TerraPDF.Throughput/run-comparison.sh 61f5c50 2 1g 30
```

The scenarios reuse the same logo in every document, like a real service, so after the
first document the converted image comes from TerraPDF's process-wide image cache. A
workload with a different image in every document behaves like the cold case
(`ImageBenchmarks.PngDocumentColdCache`).

Results are printed and appended as JSON lines to
`BenchmarkDotNet.Artifacts/throughput/results.jsonl`. The harness only uses public API that
exists in every compared version; if you add to it, keep it that way.

## Checking that output did not change

Performance changes should normally leave the PDFs byte-identical. `tools/pdf-compare`
has the scripts to check it (sample byte comparison, visual and text comparison, glyph
positions, PNG round trip, font widths against a viewer, QR symbols) and the profiling
helpers; see [tools/pdf-compare/README.md](../tools/pdf-compare/README.md).

## Adding a benchmark

- Put reusable document builders in `Scenarios/Docs.cs` and test assets in
  `Scenarios/Assets.cs` (fonts and images are linked from `tests/` and `samples/`).
- Derive document benchmarks from `PdfBenchmarkBase` and return
  `Publish(document)`.
- Load input bytes in a `[GlobalSetup]` method, never inside the benchmark.
- Custom fonts are registered process-wide: call `Assets.RegisterFonts()` from
  `[GlobalSetup]`.
- Use `[Params]` to show how cost scales with input size.
