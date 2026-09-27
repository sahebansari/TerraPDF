# Benchmark analysis — 2026-09-26

First run of the `TerraPDF.Benchmarks` suite (see [docs/benchmarks.md](../docs/benchmarks.md)),
the hotspots it revealed, their root causes in the code, a prioritised plan to fix them, the
results of Phases 1 and 2 and of the follow-up work (plans A, B and C, then QR codes and vector
content), and what it means for a service: pages per second in a container.

## Status summary

| Item | Topic | Status |
|---|---|---|
| Phase 0 | Baseline, 5/150-page variants, output-equivalence check | Partly done: equivalence checks were run with ad-hoc scripts (see [Verification tools](#verification-tools)); the page-count variants and a committed baseline are still to do |
| 1 | PNG decoded once per placement | Done (Phase 1) |
| 2 | Double layout pass | Done (Phase 1) |
| 3 | Token widths recomputed | Done (Phase 1) |
| 4 | Layout recomputed on every measure/draw | Done (Phase 1) |
| 5 | Per-operand string allocations | Done (Phase 1) |
| 6 | PNG decoder buffers | Done ([plan C](#c-png-decoder-buffers-item-6)) |
| 7 | PNG passthrough | Done (Phase 2) |
| 8 | Quadratic table slice drawing | Done (Phase 1) |
| 9 | One `Tj` per run | Done (Phase 2; ASCII-only limit for built-in fonts lifted by [plan A](#a-built-in-font-width-tables)) |
| 10 | Custom-font fast path | Done (Phase 2) |
| 11 | Subsetting buffers | Done (Phase 2) |
| 12 | Content-stream copies | Done (Phase 2) |
| 13 | QR codes | Done (Phase 2); the per-module finding turned out to be wrong, see Findings |
| – | Built-in font width tables (found in Phase 2) | Done ([plan A](#a-built-in-font-width-tables)); changes output on purpose |
| – | Table allocations / GC pressure | Done ([plan B](#b-table-allocations-and-gc-pressure)); scaling at 10,000 rows limited by the document model, see [Progress — plans A, B, C](#progress--plans-a-b-c-2026-09-26) |
| – | QR code generation (found profiling QR documents) | Done, see [QR codes and vector content](#progress--qr-codes-and-vector-content-2026-09-26) |
| – | Number formatting in content streams | Done, same section |
| – | Throughput in a container (pages per second) | Measured, see [Throughput in a container](#throughput-in-a-container-pages-per-second) |
| – | Converted images cached across documents | Done, see [Image cache, parallel compression, tools](#progress--image-cache-parallel-compression-tools-2026-09-26) |
| Phase 3 | D1 parallel compression | Done, same section |
| Phase 3 | D2 streaming output, D3 compression option, D4 parallel rendering | Open, see [Still open](#still-open) |
| – | Verification scripts committed (`tools/pdf-compare`) | Done |

### Headline results (baseline → now)

In a container limited to 2 CPUs and 1 GB (Docker, Linux arm64, .NET 10, Server GC), with
the same logo in every document as in a real service:

| Document | Pages per second | Pages per minute | CPU per page | Allocated per page |
|---|---:|---:|---:|---:|
| Invoice (1 page: logo, table, QR code) | 356 → **10,209** (×29) | 21,300 → **612,600** | 4.80 → 0.19 ms | 9.4 MB → 328 KB |
| Annual report (19 pages: text, tables, charts) | 3,097 → **25,156** (×8.1) | 185,800 → **1,509,300** | 0.62 → 0.08 ms | 1.2 MB → 143 KB |

With a different image in every document (cold image cache), the figures stay close to the
previous step: about 670 invoice pages/s and 8,800 report pages/s.

Single-operation benchmarks (BenchmarkDotNet):

| Benchmark | Time | Allocated |
|---|---:|---:|
| Invoice30Lines | 0.81 → 0.14 ms (−83%) | 1.9 MB → 390 KB |
| LongText 500 pages | 422 → 59 ms (−86%) | 1,186 → 89 MB |
| PlainTable 10,000 rows | 283 → 82 ms (−71%) | 645 → 79 MB |
| TableWithSpans 10,000 rows | 187 → 36 ms (−81%) | 461 → 40 MB |
| LatinDocument10Pages (Lato) | 15.1 → 6.9 ms (−54%) | 28.5 → 3.1 MB |
| PngDocument ×40 | 93.9 → 3.6 ms (−96%) | 323 → 2.6 MB |
| Unencrypted 20 pages | 13.8 → 1.3 ms (−91%) | 38.3 → 3.6 MB |
| Document100QrCodes | 25.0 → 10.7 ms (−57%) | 9.2 → 4.0 MB |
| DenseCanvas ×100 | 26.2 → 5.0 ms (−81%) | 21.0 → 7.4 MB |

## Environment

- Commit: `61f5c50` (branch `users/pgo/improvements`)
- BenchmarkDotNet v0.15.8, macOS 27.2, Apple M5 (10 cores), .NET 10.0.12 Arm64 RyuJIT
- Job: `ShortRun` (3 iterations, 3 warmups, 1 launch) — indicative only; the
  invoice results have a very wide error margin and should be re-measured with the default job.

## Baseline results

### End to end

| Benchmark | Mean | Allocated |
|---|---:|---:|
| HelloWorld | 10.5 µs | 90 KB |
| Invoice30Lines | 0.81 ms | 1.9 MB |
| Invoice300Lines | 6.9 ms | 13.8 MB |

### Text (built-in fonts)

| Benchmark | Pages | Mean | Allocated |
|---|---:|---:|---:|
| LongText | 10 | 6.8 ms | 19.2 MB |
| LongText | 100 | 90.2 ms | 237 MB |
| LongText | 500 | 422 ms | 1,186 MB |
| RichSpans | 10 | 8.7 ms | 23.1 MB |
| RichSpans | 100 | 109 ms | 283 MB |
| RichSpans | 500 | 560 ms | 1,415 MB |
| UnicodeWithBuiltInFont* | 10 | 2.8 ms | 7.5 MB |
| UnicodeWithBuiltInFont* | 100 | 23.2 ms | 60 MB |
| UnicodeWithBuiltInFont* | 500 | 140 ms | 372 MB |

\* Uses a shorter paragraph than `LongText`, so it is not directly comparable.

### Tables

| Benchmark | Rows | Mean | Allocated |
|---|---:|---:|---:|
| PlainTable | 100 | 2.4 ms | 6.5 MB |
| PlainTable | 1,000 | 20.7 ms | 48.4 MB |
| PlainTable | 10,000 | 283 ms | 645 MB |
| TableWithSpans | 100 | 1.6 ms | 4.7 MB |
| TableWithSpans | 1,000 | 13.2 ms | 33.9 MB |
| TableWithSpans | 10,000 | 187 ms | 461 MB |

### Fonts

| Benchmark | Mean | Allocated |
|---|---:|---:|
| ParseLato | 18.7 µs | 131 KB |
| ParseDevanagari | 5.9 µs | 43 KB |
| SubsetLato | 115 µs | 938 KB (mostly LOH) |
| LatinDocument10Pages (Lato) | 15.1 ms | 28.5 MB |
| DevanagariDocument10Pages | 5.7 ms | 15.7 MB |

### Images

| Benchmark | Count | Mean | Allocated |
|---|---:|---:|---:|
| DecodePng (header logo) | – | 2.1 ms | 8.1 MB |
| DecodeAlphaPng | – | 0.27 ms | 1.1 MB |
| PngDocument | 1 | 12.1 ms | 32.4 MB |
| PngDocument | 40 | 93.9 ms | 323 MB |
| AlphaPngDocument | 1 | 2.8 ms | 4.5 MB |
| AlphaPngDocument | 40 | 14.9 ms | 43.6 MB |
| JpegDocument | 1 | 18 µs | 95 KB |
| JpegDocument | 40 | 98 µs | 174 KB |

### Encryption (20-page document)

| Benchmark | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| Unencrypted | 13.8 ms | 38.3 MB | 1.00 |
| Aes128 | 14.3 ms | 38.4 MB | 1.03 |
| Aes256 | 15.5 ms | 41.2 MB | 1.12 |

### Barcodes and canvas

| Benchmark | Mean | Allocated |
|---|---:|---:|
| QrShort (L / H) | 63 / 109 µs | 13 / 27 KB |
| QrLong (L / H) | 1.7 / 4.4 ms | 149 / 449 KB |
| Code128 | 0.33 µs | 1.9 KB |
| Document100QrCodes | 25.0 ms | 9.2 MB |
| DenseCanvas ×10 | 2.5 ms | 2.2 MB |
| DenseCanvas ×100 | 26.2 ms | 21.0 MB |

## Findings

| Area | Signal | Root cause |
|---|---|---|
| PNG images | 40× the same PNG = 323 MB / 94 ms, exactly 10× one placement. JPEG is cheap (174 KB for 40). | Every `ImageElement` decodes its PNG in its constructor; document-wide deduplication only happens at write time, after all decodes. Dedup key is a SHA-256 over the *decoded* pixels. |
| Text | ~2.4 MB allocated per page of plain paragraphs. | Word widths recomputed 4–5× per token; line layout recomputed on every `Measure` and on `Draw`; per-token string/`StringBuilder` allocations when emitting PDF operators. |
| Tables | ~64 KB per row; time grows ×8.8 (100→1k rows) then ×13.7 (1k→10k rows). | Row heights computed up to 3×; `DrawRows` scans *all* cells of the table for every page slice (quadratic in rows × pages). |
| Custom fonts | Lato document 2× slower and 50% more allocation than built-in font. `SubsetLato` allocates 938 KB, mostly LOH. | `CustomFontVariant.MeasureWidth` always runs Devanagari reordering + conjunct mapping (list allocations), multiplied by the repeated width measurement. Subsetting grows buffers instead of sizing them. |
| QR codes | ~250 µs / 94 KB per QR inside a document vs ~62 µs to generate. | ~~One rectangle operator per module.~~ *Correction (Phase 2):* `QrCodeElement` already merges module runs and encodes once. Only canvas QR codes re-encoded on every draw. The rest of the per-QR cost is ordinary layout and drawing (row of five cells per QR row). |
| Layout passes | (not directly benchmarked yet) | The initial page-count hint is 99, so any document with 1–9 or ≥100 pages is laid out a second time — even without page-number spans. |
| Encryption | +3% (AES-128), +12% (AES-256). | Acceptable; no action needed. |

## Improvement plan

### Phase 0 — Reliable baseline (½ day) — *partly done*

- Re-run the full suite with the default job and store the JSON under `perf/baseline/`.
- Add `LongText` variants at **5** and **150** pages to expose the double-layout issue (item 2).
- Add an output-equivalence check (content streams decompressed, ignoring `/ID` and dates, or
  `pdftoppm` rendering diff) so optimisations can be proven not to change output.

### Phase 1 — Quick wins (low risk, high impact) — *done except item 6*

| # | Problem | Fix | Where | Expected gain |
|---|---|---|---|---|
| 1 | PNG decoded once per placement; SHA-256 over decoded pixels per page | Document-level decode cache keyed by the input bytes; lazy decode (layout only needs the IHDR size); hash the source bytes instead of pixels | `src/TerraPDF/Elements/ImageElement.cs:62`, `src/TerraPDF/Drawing/PdfDocument.cs:141` | `PngDocument(40)` 323 MB → ~10 MB |
| 2 | Double layout for 1–9 or ≥100 pages | Track whether page-number / total-pages spans are used; skip the stabilisation re-layout when they are not | `src/TerraPDF/Core/DocumentComposer.cs:824`, `src/TerraPDF/Elements/Element.cs:12` | ~2× on typical 1–9 page invoices |
| 3 | Token widths computed 4–5× (`BuildLines`, `Measure` Max+Sum, `DrawLines` Sum + advance, `DrawToken`) plus a font lookup each time | Compute width and resolved font once in `Tokenize`, store them on `TextToken` | `src/TerraPDF/Elements/TextBlock.cs:100` | text −30–50% time |
| 4 | `LayoutLines` re-run on every `Measure` and `Draw`; `Row` measures auto items twice; `Table.GetRowHeights` runs 3× | Memoise the last layout per `(width, style, hint)` on `TextBlock` and `Table` | `src/TerraPDF/Elements/TextBlock.cs:228`, `src/TerraPDF/Elements/Table.cs:115` | tables / nested layouts −40%+ |
| 5 | `F()` / `C()` format through `ToString("F2")`; `EscapeForPdfString` allocates a `StringBuilder` per token; `PdfColor.FromHex` per token | Format numbers directly into the content buffer (`ISpanFormattable`); escape straight into `_ops`; parse colours once per style | `src/TerraPDF/Drawing/PdfPage.cs:734`, `src/TerraPDF/Drawing/PdfPage.cs:768` | fewer allocations on every document |
| 6 | `PngDecoder` allocates a new row buffer per scanline, concatenates IDAT chunks via `List<byte[]>`, decompresses via `MemoryStream.ToArray()` | Reuse two row buffers; decompress into a pre-sized buffer (`height × stride`) | `src/TerraPDF/Drawing/PngDecoder.cs:125` | `DecodePng` 8.1 MB → ~3 MB |

### Phase 2 — Structural (medium risk) — *done*

7. **PNG passthrough**: for non-alpha RGB / gray / indexed PNGs, embed the IDAT stream as-is with
   `/DecodeParms << /Predictor 15 /Colors n /BitsPerComponent 8 /Columns w >>` (and an `/Indexed`
   colour space for palettes). No decode and no recompression. `PngDocument(1)` 12 ms → < 0.5 ms.
   Alpha images keep the decode path.
8. **Table slice drawing is quadratic**: `DrawRows` iterates every cell for every slice and builds a
   `Dictionary` each time. Index cells by row once so each slice only visits its own rows
   (`src/TerraPDF/Elements/Table.cs:215`).
9. **One `Tj` per line instead of per word**: every word currently emits its own `Td` + `Tj`. Merge
   consecutive same-style tokens; use `Tw` or `TJ` for justification. Content streams 2–3× smaller,
   so compression and encryption get faster too.
10. **Custom-font fast path**: skip Devanagari reordering and conjunct mapping when the text has no
    U+0900–U+097F characters (`src/TerraPDF/Drawing/TrueType/CustomFontVariant.cs:47`).
11. **Subsetting buffers**: size output buffers exactly or rent them from `ArrayPool` to stay off the LOH.
12. **Content stream copies**: `StringBuilder` → `string` → `byte[]` → `Compress` → `ToArray` makes
    three full copies; encode and compress directly into a pooled buffer.
13. **QR codes**: merge horizontal module runs into single rectangles; cache the generated QR matrix
    on the element.

### Phase 3 — Optional — *open, see [plan D](#d-phase-3)*

- Parallel per-page content building and compression after layout (requires auditing shared state:
  image alias counter, custom-font glyph usage merging).
- Stream PDF objects to the output instead of accumulating `binaryObjects` (lower peak memory on
  large documents).
- Expose a compression-level option (`Optimal` today everywhere; `Fastest` for bulk generation).

## Suggested order

1. Items 2, 1, 3, 4 — biggest gains for the least code.
2. Items 8, 5.
3. Items 7, 9 — change PDF output, so they need the Phase 0 equivalence check plus a visual diff.
4. Items 10–13.
5. Phase 3 only if benchmarks still justify it.

Each change should be its own PR, with a before/after table from the relevant benchmark filter
(e.g. `--filter '*TableBenchmarks*'`), the full test suite green, and output equivalence verified
(except for items 7 and 9).

## Progress — 2026-09-26

Applied steps 1 and 2 of the suggested order: items **2, 1, 3, 4, 8, 5**.
Same machine and `ShortRun` job as the baseline. All tests pass on net8.0, net9.0 and
net10.0, and every non-encrypted sample PDF is byte-identical to the one produced before
the changes.

| Item | Change |
|---|---|
| 2 | `LayoutAllStable` skips the re-layout when no page-number span was laid out (`TextBlock.PageCountDependentLayouts`). |
| 1 | New `ImageSource`: header-only parse at compose time; PNG decoded once per distinct content key (SHA-256 of the file bytes) in `PdfDocument`. |
| 3 | `TextToken` carries its resolved font, colour and width, computed once per span/token. |
| 4 | `TextBlock.LayoutLines` and `Table.GetRowHeights` memoise their last result per publish (`LayoutPass`). |
| 8 | `Table.DrawRows` visits only the slice's cells through a per-row cell index. |
| 5 | Number operands formatted in place (`{value:F2}`), text escaped straight into the content buffer, colour parsed once per span. |

| Benchmark | Before | After | Δ time | Alloc before | Alloc after | Δ alloc |
|---|---:|---:|---:|---:|---:|---:|
| Invoice30Lines | 0.81 ms | 0.34 ms | −58% | 1.9 MB | 1.1 MB | −42% |
| Invoice300Lines | 6.92 ms | 3.90 ms | −44% | 13.8 MB | 8.9 MB | −35% |
| LongText 100 pages | 90.2 ms | 38.3 ms | −58% | 237 MB | 98 MB | −59% |
| LongText 500 pages | 422 ms | 198 ms | −53% | 1,186 MB | 490 MB | −59% |
| RichSpans 500 pages | 560 ms | 364 ms | −35% | 1,415 MB | 697 MB | −51% |
| PlainTable 1,000 rows | 20.7 ms | 16.0 ms | −22% | 48.4 MB | 25.8 MB | −47% |
| PlainTable 10,000 rows | 283 ms | 212 ms | −25% | 645 MB | 259 MB | −60% |
| TableWithSpans 10,000 rows | 187 ms | 101 ms | −46% | 461 MB | 147 MB | −68% |
| LatinDocument10Pages (Lato) | 15.1 ms | 11.0 ms | −27% | 28.5 MB | 17.8 MB | −37% |
| DevanagariDocument10Pages | 5.7 ms | 3.3 ms | −41% | 15.7 MB | 6.9 MB | −56% |
| PngDocument ×1 | 12.1 ms | 4.2 ms | −65% | 32.4 MB | 8.2 MB | −75% |
| PngDocument ×40 | 93.9 ms | 4.3 ms | −95% | 323 MB | 8.2 MB | −97% |
| AlphaPngDocument ×40 | 14.9 ms | 2.0 ms | −87% | 43.6 MB | 1.3 MB | −97% |
| Unencrypted 20 pages | 13.8 ms | 6.5 ms | −53% | 38.3 MB | 19.6 MB | −49% |
| Document100QrCodes | 25.0 ms | 24.5 ms | −2% | 9.2 MB | 6.2 MB | −32% |
| DenseCanvas ×100 | 26.2 ms | 25.3 ms | −3% | 21.0 MB | 13.6 MB | −36% |

Micro-benchmarks of untouched code (PNG decoder, font parsing/subsetting, QR and Code128
encoding) are unchanged, as expected.

Still open at that point: items 6, 7, 9–13 and Phase 3 (7 and 9–13 were done in Phase 2).

## Progress — Phase 2 (2026-09-26)

Applied the rest of Phase 2: items **7, 9, 10, 11, 12, 13** (item 8 was done with Phase 1).
Same machine and `ShortRun` job. All 569 tests pass on net8.0, net9.0 and net10.0.

| Item | Change |
|---|---|
| 7 | RGB (type 2) and indexed (type 3) PNGs embedded without decoding: IDAT stream as `/FlateDecode` + `/DecodeParms << /Predictor 15 … >>`, palette as a separate `/Indexed` lookup stream (so encryption covers it). |
| 9 | Same-font/size/colour tokens on a left/centre/right-aligned line share one `Tj`. Justified lines stay per word. Built-in-font runs only join printable-ASCII tokens (see finding below). |
| 10 | `CustomFontVariant.MeasureWidth` and `PdfPage.AppendIdentityHHex` skip Devanagari shaping when the text has neither ि (U+093F) nor a virama (U+094D). |
| 11 | Subset `glyf` table sized in a first pass and block-copied; unchanged tables referenced in place instead of copied. |
| 12 | Content streams encoded chunk by chunk from the `StringBuilder` straight into the compressor (no `string`, no intermediate `byte[]`). |
| 13 | Canvas QR symbol cached on the draw command. (`QrCodeElement` already merged module runs and encoded once.) |

### Verification

- Items 10–13 produce byte-identical PDFs for every non-encrypted sample.
- Item 7 was checked with purpose-built RGB and indexed PNGs using all five PNG row filters
  and several IDAT chunks, plain and encrypted: the image MuPDF decodes from the PDF is
  pixel-identical to the source.
- Item 9 changes the content streams. Across all samples, the extracted text is identical
  and every glyph origin is within 0.01 pt of the previous output, except custom-font
  (Lato) lines, which drift by at most 0.15 pt over a full line (viewer glyph-advance
  rounding). Sample PDFs are 0.2–14% smaller.

### Results (vs the original baseline)

| Benchmark | Baseline | Phase 1 | Phase 2 | Alloc baseline → now |
|---|---:|---:|---:|---:|
| Invoice30Lines | 0.81 ms | 0.34 ms | 0.28 ms | 1.9 → 1.0 MB |
| Invoice300Lines | 6.92 ms | 3.90 ms | 3.48 ms | 13.8 → 8.2 MB |
| LongText 100 pages | 90.2 ms | 38.3 ms | 28.8 ms | 237 → 86 MB |
| LongText 500 pages | 422 ms | 198 ms | 143 ms | 1,186 → 432 MB |
| RichSpans 500 pages | 560 ms | 364 ms | 347 ms | 1,415 → 643 MB |
| PlainTable 10,000 rows | 283 ms | 212 ms | 196 ms | 645 → 248 MB |
| TableWithSpans 10,000 rows | 187 ms | 101 ms | 87 ms | 461 → 133 MB |
| LatinDocument10Pages (Lato) | 15.1 ms | 11.0 ms | 8.5 ms | 28.5 → 10.2 MB |
| DevanagariDocument10Pages | 5.7 ms | 3.3 ms | 2.7 ms | 15.7 → 4.8 MB |
| SubsetLato | 115 µs | 114 µs | 83 µs | 938 → 302 KB |
| Unencrypted 20 pages | 13.8 ms | 6.5 ms | 4.4 ms | 38.3 → 17.6 MB |
| Aes256 20 pages | 15.5 ms | 8.1 ms | 6.1 ms | 41.2 → 20.4 MB |
| RgbPngDocument ×1 (new) | – | – | 18 µs | 96 KB |
| PngDocument ×1 (RGBA) | 12.1 ms | 4.2 ms | 4.2 ms | 32.4 → 8.2 MB |
| Document100QrCodes | 25.0 ms | 24.5 ms | 24.5 ms | 9.2 → 4.5 MB |
| DenseCanvas ×100 | 26.2 ms | 25.3 ms | 24.0 ms | 21.0 → 8.9 MB |

An RGB PNG the size of the header logo now costs 18 µs to embed, against ~4 ms for the
same image as RGBA, which still needs a decode and re-compression.

### New finding — built-in font width tables

Merging words into one `Tj` exposed a bug that already existed in `FontMetrics`: several WinAnsi
widths above 0x7E do not match the Adobe AFM values. For Helvetica, `‘ ’` are listed as
278 instead of 222, `“ ”` as 556 instead of 333, `•` as 278 instead of 350, and `š`/`Ž`
are also off. In addition, characters with no WinAnsi code are measured as 500 units but
drawn as `?` (556 in Helvetica). Positioning each word separately hid these errors
between words, but they already skew line wrapping and the width of words that contain
such characters. Fixing the tables changes line breaks in existing documents, so it is
left as a separate decision. Until then, item 9 keeps such tokens on their own show op.

## Plan for the remaining work

### Table allocations: diagnosis

GC columns of `TableBenchmarks` after Phase 2:

| Benchmark | Rows | Mean | Gen0 | Gen1 | Gen2 | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| PlainTable | 100 | 0.94 ms | 321 | 143 | – | 2.6 MB |
| PlainTable | 1,000 | 15.2 ms | 3,406 | 1,313 | 547 | 24.8 MB |
| PlainTable | 10,000 | 196 ms | 32,000 | 13,000 | 2,000 | 248 MB |

(GC counts are per 1,000 operations.) Allocation grows linearly at ≈25 KB per row (≈6 KB
per cell), but time grows faster: from 1,000 rows, a large share of that garbage survives
into Gen1/Gen2 because the element tree and its cached layouts stay alive for the whole
publish. The remaining table cost is therefore allocation volume and GC promotion, not
an algorithm that scales badly.

### A. Built-in font width tables

The WinAnsi widths above 0x7E are wrong for several glyphs, and unmappable characters are
measured differently from the `?` drawn in their place (see the Phase 2 finding).

1. Generate the six width arrays in `src/TerraPDF/Drawing/FontMetrics.cs` from the official
   Core-14 AFM files with a script (WinAnsi code → glyph name → `WX`), kept under `tools/` so
   the tables can be regenerated rather than hand-edited.
2. In `FontMetrics.MeasureWidth`, measure characters without a WinAnsi code as `?` in the
   current font, since that is what is drawn. Do the same for DEL and the undefined codes
   0x81, 0x8D, 0x8F, 0x90 and 0x9D.
3. Add a test that checks every table entry against the AFM data.
4. Remove the printable-ASCII gate in `TextBlock.CanJoinRun`, so built-in-font runs join all
   characters.
5. Verify every sample, including 11 (Unicode) and 14 (the `???` lines), with the
   glyph-position check: ≤ 0.01 pt shift. Where the corrected widths change line wrapping,
   check the new line breaks visually.
6. CHANGELOG "Fixed" entry saying line wrapping may change for text with curly quotes,
   bullets, dashes, `€` or unmappable characters. Ship it in a minor version bump.

Effort ½ day. Output changes on purpose.

### B. Table allocations and GC pressure

1. Profile before changing anything: `[EventPipeProfiler(EventPipeProfile.GcVerbose)]` on a
   temporary copy of `TableBenchmarks`, or `dotnet-trace collect --profile gc-verbose`, to
   get the top allocation sites per row. Suspects:
   - `TextStyle.MergeWith` allocates a new style per span per layout, even when the
     override adds nothing.
   - Each cell keeps its decorator chain (`Background` → `Padding` → alignment → `TextBlock`),
     a `List<WrappedLine>`, one `List<TextToken>` per line, and a layout cache entry, all
     alive for the whole publish.
   - `Table._occupied` holds a `HashSet<int>` per row.
   - `TextToken` has grown to seven fields (text, style, two flags, font, colour, width).
2. Candidate fixes, applied in the order the profile ranks them:
   - `MergeWith` returns `this` when the override is empty; cache merged styles per
     (base, override) pair.
   - Replace `_occupied` with a per-row "next free column" array; keep a set only for rows a
     row span reaches into.
   - Store a line as a (start, count) slice of the block's token array instead of its own
     list, with a fast path for single-line blocks (most cells).
   - Move style, font and colour into a shared per-span record, so a token is text, width
     and a span index.
3. Targets: under 10 KB per `PlainTable` row, no Gen2 collections at 1,000 rows, and
   1,000 → 10,000 rows scaling at ×10.5 or better.

Effort 1–2 days. Output stays byte-identical.

### C. PNG decoder buffers (item 6)

Only matters now for PNGs with an alpha channel; opaque RGB and palette PNGs skip decoding.

1. Decompress through a small stream that walks the IDAT chunks in place, instead of
   concatenating them first.
2. Decompress into one buffer of the exact size (`height × (1 + width × bpp)`) with
   `ReadExactly`, instead of `MemoryStream` + `ToArray`.
3. Undo the row filters in place in that buffer (the previous row sits just above), removing
   the per-row arrays.
4. Split colour and alpha in one pass; allocate the alpha array only when some pixel is
   transparent.
5. Target: `DecodePng` 8.1 MB → ≈2.9 MB and ≈30% faster.

Effort ½ day. Output byte-identical; covered by the existing alpha tests and the
filter-coverage PNGs described below.

### D. Phase 3

1. **Parallel compression.** In `PdfDocument.Save`, compress page content streams and
   decoded PNGs in parallel (`Parallel.For`), then write objects in their original order.
   Object ids are still assigned sequentially, so output stays byte-identical. Encryption
   stays per object, after compression. Effort ½ day.
2. **Streaming output.** Write each object to the output as soon as it is built and record its
   offset for the xref table, instead of collecting everything in `binaryObjects` first. Lower
   peak memory on large documents; byte-identical output. Effort ½–1 day.
3. **Compression-level option.** Public API, e.g. `doc.Compression(PdfCompression.Fastest)`,
   defaulting to today's `Optimal` so existing output does not change. Needs validation, XML
   docs, tests, a docs page and a CHANGELOG "Added" entry. Effort ½ day.
4. **Parallel page rendering** (last, only if benchmarks still justify it). Blockers:
   thread-static state (`LayoutPass.Current`, `TextBlock.PageCountDependentLayouts`) would
   have to flow to worker threads; the bookmark and heading recorders depend on order and
   need per-page buffers merged afterwards; header and footer elements would be drawn by
   several pages at once (their caches are replaced atomically, but this needs checking).
   Effort 1–2 days, high risk.

### Order

1. **C**: small, no output change.
2. **B**: the largest remaining time and memory gain.
3. **A**: changes output on purpose; ship with a version bump and release note.
4. **D1 → D2 → D3.**
5. **D4** only if benchmarks still justify it.

Each step: tests on net8.0/net9.0/net10.0; before/after benchmarks with the same job for the
affected classes; byte-identical non-encrypted samples for B, C, D1 and D2; the glyph-position
and visual checks for A.

## Progress — plans A, B, C (2026-09-26)

Applied in the order C → B → A. Same machine and `ShortRun` job. All 581 tests pass on
net8.0, net9.0 and net10.0.

### C. PNG decoder buffers

`PngDecoder.Decode` now works on the file bytes: IDAT chunks are read in place through a
small stream, decompressed with `ReadAtLeast` into one buffer of the exact size, unfiltered
in place, and converted to RGB in one pass; the alpha plane is only allocated when a pixel is
transparent. The now-unused stream overload was removed.

- Output: byte-identical samples. RGBA and gray+alpha test PNGs using all five row filters
  (plain and encrypted) decode pixel-exactly, colour and alpha.
- `DecodePng` 2.11 → 1.40 ms, 8.1 → 2.4 MB (target was ≈2.9 MB). `PngDocument` −17% time,
  −69% allocation.

### B. Table allocations and GC pressure

Profiled first, with an in-process `EventListener` on GC allocation ticks (ranking by type)
and `dotnet-trace` + TraceEvent (allocation stacks). Fixes, in the order the profile ranked
them:

| Cause found | Fix |
|---|---|
| `PdfColor` had no `IEquatable`, so `Equals` went through reflection and boxed every `double` (≈15% of allocations) | `PdfColor : IEquatable<PdfColor>` with `==`/`!=` (public API addition) |
| `TextToken` carried style, font and a 3-double colour (~72 bytes per word), held in growing lists | Tokens share one `TextFormat` per span; token lists sized exactly; single-line blocks use their token list as the line; `TrimTrailing` trims in place |
| A `DrawingContext` object per decorator level per cell | `DrawingContext` is a `readonly struct` |
| `TextStyle.Default` built a new object on every access (and `DrawingContext` read it on every `At`) | One shared instance (the type is immutable) |
| LINQ `Sum`/`Max`/`Where` over lists (boxed enumerators), `SplitWords` iterator, font fallback iterator, name normalisation, `FromHex` substrings, decoration list, page-number placeholder | Loops, a list-filling splitter, a static style list, cached normalised names, allocation-free hex parsing, lazily created lists |
| Cached lines kept alive until the end of the publish | A block's cached layout is released once drawn |

A remaining `System.Double` boxing in `StringBuilder.AppendInterpolatedStringHandler.AppendFormatted<double>`
only appears before the JIT's tier-1 recompilation, so it only affects the first few
documents in a process, not steady state.

Results (steady state):

| Target | Result |
|---|---|
| Under 10 KB per `PlainTable` row | **8.1 KB** (2.5 compose + 5.6 publish), from 26.4 KB |
| No Gen2 collections at 1,000 rows | **Met** (0 Gen2) |
| 1,000 → 10,000 rows at ×10.5 or better | **Not met: ×15.7** (7.15 → 112 ms) |

The remaining super-linear cost at 10,000 rows is garbage collection over the live document
tree: the whole element tree (≈25 MB for 10,000 rows) stays alive from composition until the
publish ends, so Gen1/Gen2 collections get more expensive as the document grows. Reducing
that further needs a change to the document model (for example, releasing or streaming
content that has been rendered), which is outside this plan. Absolute times still improved:
`PlainTable` 10,000 rows 196 → 112 ms after Phase 2.

Output: byte-identical samples.

### A. Built-in font width tables

- Compared every entry of the six width tables against the Adobe Core-14 AFM files (WinAnsi
  code → glyph name → `WX`): 25 entries were wrong (12 in Helvetica, 7 in Times-Bold including
  ASCII `Z`, 6 in Times-Italic; Helvetica-Bold, Times-Roman and Times-BoldItalic were already
  exact). They were patched in place from the AFM data.
- Characters with no WinAnsi code are measured as `?`, the glyph drawn in their place.
- `AfmWidthTableTests` checks every WinAnsi glyph of the nine AFM font variants against the
  AFM files, which are committed unmodified under `tests/TerraPDF.Tests/TestAssets/Afm/`
  together with Adobe's `MustRead.html`, as its terms require. A committed generator script was
  not needed: the test pins the tables.
- The printable-ASCII limit on text-run merging (Phase 2, item 9) is removed.

Verification:

- A PDF with every WinAnsi character plus two unmappable characters, drawn as one run in each
  of the 12 built-in variants: every glyph that MuPDF draws lands within **0.001 pt** of the
  position computed from the tables.
- Samples: extracted text is identical and no line breaks changed. Glyphs after a previously
  mismeasured character move to their correct place: up to 26 pt on sample 14's `???` lines,
  where words used to overlap, and 5.6 pt on sample 11's curly-quote line, which had a visible
  extra gap. 4 samples are byte-identical; the others shrink by up to 6% thanks to the longer
  text runs.
- Documents whose text contains the corrected characters can wrap differently; this is noted
  under "Fixed" in the CHANGELOG.

### Results (vs the original baseline)

| Benchmark | Baseline | Phase 2 | Now | Alloc baseline → now |
|---|---:|---:|---:|---:|
| Invoice30Lines | 0.81 ms | 0.28 ms | 0.20 ms | 1.9 MB → 390 KB |
| Invoice300Lines | 6.92 ms | 3.48 ms | 2.01 ms | 13.8 → 2.8 MB |
| LongText 100 pages | 90.2 ms | 28.8 ms | 9.3 ms | 237 → 17.8 MB |
| LongText 500 pages | 422 ms | 143 ms | 67 ms | 1,186 → 89 MB |
| RichSpans 500 pages | 560 ms | 347 ms | 254 ms | 1,415 → 246 MB |
| UnicodeWithBuiltInFont 500 pages | 140 ms | 67 ms | 24 ms | 372 → 37 MB |
| PlainTable 1,000 rows | 20.7 ms | 15.2 ms | 7.2 ms | 48.4 → 7.9 MB |
| PlainTable 10,000 rows | 283 ms | 196 ms | 112 ms | 645 → 79 MB |
| TableWithSpans 10,000 rows | 187 ms | 87 ms | 39 ms | 461 → 40 MB |
| LatinDocument10Pages (Lato) | 15.1 ms | 8.5 ms | 6.9 ms | 28.5 → 3.1 MB |
| DevanagariDocument10Pages | 5.7 ms | 2.7 ms | 2.6 ms | 15.7 → 2.4 MB |
| PngDocument ×1 (RGBA) | 12.1 ms | 4.2 ms | 3.5 ms | 32.4 → 2.5 MB |
| PngDocument ×40 (RGBA) | 93.9 ms | 4.3 ms | 3.6 ms | 323 → 2.6 MB |
| DecodePng | 2.1 ms | 2.1 ms | 1.4 ms | 8.1 → 2.4 MB |
| Unencrypted 20 pages | 13.8 ms | 4.4 ms | 1.5 ms | 38.3 → 3.6 MB |
| Aes256 20 pages | 15.5 ms | 6.1 ms | 2.9 ms | 41.2 → 6.4 MB |
| Document100QrCodes | 25.0 ms | 24.5 ms | 24.6 ms | 9.2 → 4.5 MB |
| DenseCanvas ×100 | 26.2 ms | 24.0 ms | 24.0 ms | 21.0 → 7.2 MB |

### Still open at that point (superseded by [Still open](#still-open) at the end)

- Phase 3 ([plan D](#d-phase-3)): parallel compression, streaming output, a compression-level
  option, parallel rendering.
- Table scaling at very large sizes (document-model change, see B above).
- `Document100QrCodes` and `DenseCanvas` time is unchanged since the baseline. Neither was
  profiled; they are the next candidates if QR- or canvas-heavy documents matter.
- The verification scripts below are still scratch scripts; commit them under
  `tools/pdf-compare/`.

## Progress — QR codes and vector content (2026-09-26)

`Document100QrCodes` and `DenseCanvas` had not moved since the baseline, so they were profiled
(thread-time sampling with `dotnet-trace`, stacks aggregated with TraceEvent):

- **QR documents:** 40–55% of the time was QR generation. Most of it was mask selection:
  `SelectBestMask` cloned the `bool[,]` matrix for each of the 8 masks, and the four penalty
  rules scanned it module by module. The rest was drawing and output: `AddFilledRects` 20%,
  Deflate 13%, number formatting 6%.
- **Canvas documents:** almost nothing was geometry. Time went into the amount of content-stream
  text: Deflate 37%, buffer allocation 34%, `FormatFloat` 13% (a circle is 26 numbers).

### Q1. QR generation

- `QrMatrixBuilder` works on flat row-major arrays. Each candidate mask is applied with an XOR,
  scored, and undone with a second XOR, so the matrix is never copied.
- Mask conditions are computed from per-row and per-column residues instead of divisions per
  module.
- The four penalty rules (ISO/IEC 18004 §7.8.3) are scored bit-parallel. Every row and column is
  packed into 192 bits, then:
  - **runs:** a run of length L costs L − 2 = (5-module windows in the run) + 2 × (run starts);
  - **2×2 blocks:** horizontal equality on both rows AND vertical equality;
  - **finder-like patterns:** 11 shifted ANDs;
  - **balance:** one popcount.
- Verification: a dump of 458 symbols (4 error-correction levels, versions 1 to 40) is
  identical before and after, bit for bit, and the samples are byte-identical.

| Benchmark | Baseline | Now |
|---|---:|---:|
| QrShort L / H | 62.6 / 109 µs | 27.9 / 42.4 µs (−55% / −61%) |
| QrLong L / H | 1.68 / 4.42 ms | 0.26 / 0.68 ms (−84% / −85%) |
| Document100QrCodes | 25.0 ms | 10.7 ms (−57%) |

### N1. Number formatting

- New `PdfReal`, a small `ISpanFormattable` used as `{PdfReal.F2(x)}` in the content-stream
  interpolations, so `StringBuilder` formats it without boxing.
- It scales and rounds in integer arithmetic. Below 1e7 the scaled double is within half an ulp
  (under 1e-9) of the exact value, so away from a rounding tie it rounds exactly like the exact
  decimal expansion.
- Within 1e-7 of a tie, for large values and for NaN/infinity it uses `double.TryFormat("F2"/"F4")`.
  `-0.00` is preserved.
- Verification: 20 million values (coordinates, colours, near-ties, arbitrary bit patterns) give
  exactly the same text as `ToString("F2"/"F4")`. A unit test keeps a sample of 800,000. The
  samples are byte-identical.
- Effect: `DenseCanvas` −37%, and every document with vector content or tables gains
  (Invoice300Lines −30% after this step alone).

## Throughput in a container (pages per second)

`benchmarks/TerraPDF.Throughput` (see [docs/benchmarks.md](../docs/benchmarks.md)) generates
documents in parallel for a fixed time and reports pages per second and per minute, CPU per page,
allocation, GC, memory and latency.

`run-comparison.sh` builds the same harness against the base commit `61f5c50` (in a temporary
git worktree) and against the current tree. It runs both images one after the other with
`--cpus=2 --memory=1g`, for 30 s per scenario after a 10 s warm-up. That is Docker Desktop's
Linux VM (arm64, 4 CPUs) on an Apple M5, with .NET 10.0.12 and Server GC. Two runs agreed within
2%; the figures are from the second.

| | Invoice, before | Invoice, after | Report, before | Report, after |
|---|---:|---:|---:|---:|
| **Pages per second** | 361 | **674** (×1.87) | 3,025 | **8,778** (×2.90) |
| **Pages per minute** | 21,600 | **40,500** | 181,500 | **526,700** |
| CPU per page | 4.81 ms | 2.41 ms (−50%) | 0.63 ms | 0.21 ms (−67%) |
| Allocated per page | 9.4 MB | 2.8 MB (−70%) | 1.2 MB | 276 KB (−77%) |
| Peak working set | 97 MB | 86 MB | 121 MB | 108 MB |
| Latency p50 per document | 4.6 ms | 2.1 ms | 11.4 ms | 3.6 ms |

The invoice is a 1-page document; the report has 19 pages.

What this means for a service:

- **Same hardware, more output.** A 2-CPU instance produces about twice as many invoices and
  almost three times as many report pages. Put differently, the same load needs one half to one
  third of the CPU.
- **Memory.** The working set of a busy 2-CPU container stays around 90–110 MB. It was already
  modest, and it drops by about 10%. The large gain is in allocation, 70–77% less per page. That
  means less GC work, which is part of the CPU saving, and more headroom under a tight memory
  limit.
- **Invoices still decode their logo every time.** The invoice uses the RGBA header logo, which
  every document decodes again (about 2.4 MB of pixels, on the large object heap). That is why
  its GC is dominated by Gen2 collections and why it does not use the full 2 CPUs (81%). Caching
  decoded images across documents, or pooling the decode buffers, is the next gain for this kind
  of document (see "Still open").

## Current results vs the original baseline

Full BenchmarkDotNet suite, `ShortRun` job, same machine as the baseline:

| Benchmark | Baseline | Now | Δ time | Alloc baseline → now |
|---|---:|---:|---:|---:|
| HelloWorld | 10.5 µs | 8.8 µs | −16% | 90 → 84 KB |
| Invoice30Lines | 0.81 ms | 0.14 ms | −83% | 1.9 MB → 390 KB |
| Invoice300Lines | 6.92 ms | 1.42 ms | −79% | 13.8 → 2.8 MB |
| LongText 100 pages | 90.2 ms | 8.8 ms | −90% | 237 → 17.8 MB |
| LongText 500 pages | 422 ms | 64 ms | −85% | 1,186 → 89 MB |
| RichSpans 500 pages | 560 ms | 240 ms | −57% | 1,415 → 246 MB |
| UnicodeWithBuiltInFont 500 pages | 140 ms | 27 ms | −81% | 372 → 37 MB |
| PlainTable 1,000 rows | 20.7 ms | 5.2 ms | −75% | 48.4 → 7.9 MB |
| PlainTable 10,000 rows | 283 ms | 99 ms | −65% | 645 → 79 MB |
| TableWithSpans 10,000 rows | 187 ms | 36 ms | −81% | 461 → 40 MB |
| LatinDocument10Pages (Lato) | 15.1 ms | 6.9 ms | −54% | 28.5 → 3.1 MB |
| DevanagariDocument10Pages | 5.7 ms | 2.3 ms | −60% | 15.7 → 2.4 MB |
| SubsetLato | 115 µs | 83 µs | −28% | 938 → 302 KB |
| DecodePng | 2.1 ms | 1.4 ms | −33% | 8.1 → 2.4 MB |
| PngDocument ×40 (RGBA) | 93.9 ms | 3.6 ms | −96% | 323 → 2.6 MB |
| RgbPngDocument ×40 (new) | – | 89 µs | – | 139 KB |
| JpegDocument ×40 | 98 µs | 89 µs | −9% | 174 → 135 KB |
| Unencrypted 20 pages | 13.8 ms | 1.3 ms | −91% | 38.3 → 3.6 MB |
| Aes256 20 pages | 15.5 ms | 2.7 ms | −82% | 41.2 → 6.4 MB |
| QrShort L | 62.6 µs | 27.9 µs | −55% | 13 → 8 KB |
| QrLong H | 4.4 ms | 0.68 ms | −85% | 449 → 325 KB |
| Document100QrCodes | 25.0 ms | 10.7 ms | −57% | 9.2 → 4.0 MB |
| DenseCanvas ×100 | 26.2 ms | 15.1 ms | −42% | 21.0 → 7.2 MB |

All 598 tests pass on net8.0, net9.0 and net10.0. Output is byte-identical to the baseline
except for three deliberate changes, each verified visually and on extracted text:

- merged text runs (item 9);
- PNG passthrough (item 7);
- corrected font widths (plan A).

## Progress — image cache, parallel compression, tools (2026-09-26)

### Converted images cached across documents

The throughput runs showed that the invoice spent most of its CPU on its logo. Every document
decoded the RGBA PNG (about 2.4 MB of pixels on the large object heap) and compressed it
again. That also caused 8,700 Gen2 collections in 30 s.

- `EncodedImageCache` keeps the **finished streams** (compressed RGB plus compressed alpha)
  in a process-wide LRU bounded to 32 MB, keyed by the SHA-256 of the file. Images larger than
  a quarter of the budget are not cached. A hit skips both decoding and Deflate.
- The streams are cached before encryption, which is still applied per object, and compression
  is deterministic, so a cached entry gives exactly the bytes of a fresh conversion. Tests check
  this and the budget (`EncodedImageCacheTests`); samples are byte-identical.
- `ImageBenchmarks.PngDocument` now measures the warm cache: 3.5 ms → **20 µs**, 2.6 MB → 90 KB.
  `PngDocumentColdCache` keeps the first-document cost visible (3.5 ms).

### D1. Parallel compression of page content

Documents with at least 8 pages and 512 K characters of content compress their page streams
with `Parallel.For`; results are placed by page index, so output is identical (checked by
`ParallelCompressionTests`, which compares with the sequential path). Smaller documents stay
sequential, so invoices and short reports, and a service already using every core, are not
affected.

| Benchmark | Before D1 | After D1 |
|---|---:|---:|
| DenseCanvas ×100 | 15.1 ms | 5.0 ms (−67%) |
| PlainTable 1,000 rows | 5.2 ms | 3.4 ms (−35%) |
| PlainTable 10,000 rows | 99 ms | 82 ms (−17%) |
| LongText 500 pages | 64 ms | 59 ms (−7%) |

### Throughput in a container, after these steps

Same setup as before (`run-comparison.sh 61f5c50 2 1g 30`):

| | Invoice, before | Invoice, after | Report, before | Report, after |
|---|---:|---:|---:|---:|
| **Pages per second** | 356 | **10,209** | 3,097 | **25,156** |
| **Pages per minute** | 21,300 | **612,600** | 185,800 | **1,509,300** |
| CPU per page | 4.80 ms | 0.19 ms | 0.62 ms | 0.08 ms |
| Allocated per page | 9.4 MB | 328 KB | 1.2 MB | 143 KB |
| Gen2 collections in 30 s | 8,708 | 3 | 4,522 | 60 |
| Peak working set | 101 MB | 71 MB | 117 MB | 103 MB |
| Latency p50 per document | 4.6 ms | 0.17 ms | 11.3 ms | 1.5 ms |

The report gains too, because its cover uses the same RGBA logo: converting it was about
3.5 ms of the 3.9 ms of CPU each report took after the previous step. These figures assume
images are reused across documents. With a different image in every document, expect the
previous step's figures (about 670 and 8,800 pages/s).

### Verification tools committed

The scratch scripts are now in [`tools/pdf-compare`](../tools/pdf-compare/README.md), with
repository-relative paths, and each was run from there:
- sample generation and byte comparison;
- visual and text comparison, glyph positions;
- PNG round trip;
- font widths against a viewer;
- the QR symbol reference (`reference-symbols.txt`, produced by the original encoder);
- the allocation and CPU profiling helpers.

## Still open

| Item | Expected gain | Notes |
|---|---|---|
| D2: stream objects to the output instead of collecting them in `binaryObjects` | Lower peak memory on large documents | Output identical |
| D3: compression-level option (`Fastest` / `Optimal`) | Faster saves for bulk generation | Public API addition; `Optimal` stays the default |
| C1: content buffer in bytes instead of UTF-16 | Content memory halved, no encoding step | Mechanical refactor of `PdfPage` |
| N2 / Q2 / C2: shorter numbers, merged QR rectangles, graphics-state caching | Smaller content streams | Change output; need visual checks |
| Table scaling at very large sizes | GC over the live document tree | Needs a document-model change |
| Phase 0 leftovers: `LongText` 5- and 150-page variants; a committed baseline measured with the default job | Better coverage, less noisy comparisons | — |
| D4: parallel page rendering | Latency of very large documents | High risk; only if benchmarks justify it |

## Verification tools

The equivalence checks used for Phases 1 and 2 were run with throwaway scripts. They should
be committed under `tools/pdf-compare/` so later work can be checked the same way:

| Check | How it works | Used for |
|---|---|---|
| Byte comparison | Generate all samples (`samples/TerraPDF.Sample` with an output folder argument) before and after; compare files byte for byte. Encrypted samples (`12*`) are skipped because their file id is random. | Every change meant to keep output identical |
| Visual and text comparison | PyMuPDF renders every page at 110 dpi and counts pixels differing by more than 24 per channel; also compares the extracted text of every page. | Changes that alter content streams (item 9) |
| Glyph positions | PyMuPDF `rawdict` glyph origins, compared line by line; reports the largest shift per file. | Text-run merging (item 9), width tables (plan A) |
| Width tables vs viewer | Every WinAnsi character drawn as one run per built-in variant; MuPDF glyph origins compared with cumulative AFM widths. | Width tables (plan A) |
| (all tools below) | Committed under [`tools/pdf-compare`](../tools/pdf-compare/README.md). | — |
| QR symbol dump | 458 symbols (4 levels × lengths covering versions 1–40) hashed module by module, compared before and after. | QR generation (Q1) |
| Fixed-point formatter | 20 million values compared with `ToString("F2"/"F4")`; a sample is kept as a unit test (`PdfRealTests`). | Number formatting (N1) |
| CPU profile | `dnx dotnet-trace collect --profile dotnet-common,dotnet-sampled-thread-time` on a small harness, stacks aggregated with TraceEvent; helpers temporarily marked `NoInlining` to split inlined time. | QR and canvas (Q1, N1) |
| Allocation profile | In-process `EventListener` on `GCAllocationTick` events (bytes by type, split into compose and publish), then `dnx dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x1:5` and a TraceEvent script for allocation stacks. | Table allocations (plan B) |
| PNG round trip | A Python script writes RGB, indexed, RGBA and gray+alpha PNGs that use all five row filters and several IDAT chunks; a file-based C# app embeds them (plain and encrypted); PyMuPDF extracts each image and its soft mask and compares them pixel by pixel with the source. | PNG embedding (item 7), PNG decoder (plan C) |
