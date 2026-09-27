# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Performance
- PNG images are no longer decoded when composed: only the header is read, and
  pixels are decoded once per distinct image when the document is saved. Placing
  the same PNG 40 times is ~20× faster and allocates ~40× less. Deduplication now
  keys on the original file bytes instead of hashing decoded pixels.
  `VectorCanvas.GetImageSizeInPoints` no longer decodes the image either.
- Documents without page-number spans are no longer laid out a second time when
  their page count has a different digit count from the initial estimate (99),
  e.g. every 1–9 page document.
- Text layout computes each word's width, font, and colour once instead of 4–5
  times, and memoises wrapped lines and table row heights for the duration of a
  publish. Text-heavy documents are ~2× faster with about half the allocations.
- Drawing a page slice of a split table only visits that slice's cells instead
  of scanning the whole table.
- Content-stream numbers and escaped text are written straight into the page
  buffer without temporary strings.
- RGB and palette PNGs are embedded still compressed (`/FlateDecode` with
  `/DecodeParms /Predictor 15`, palettes as an `/Indexed` lookup stream): no
  decode and no re-compression. PNGs with an alpha channel are still decoded to
  split out their `/SMask`.
- Consecutive words and spaces in the same font, size and colour are shown by a
  single `Tj` operator instead of one positioned `Tj` per word, making sample
  PDFs up to 14% smaller. Justified lines keep one positioned word at a time.
- Custom-font text with no Devanagari ि or virama skips the reordering and
  conjunct-mapping pipeline when measured and encoded.
- Font subsetting builds the `glyf` table at its exact size and no longer copies
  unchanged tables (≈70% fewer allocations).
- Page content streams are compressed straight from the operator buffer.
- Canvas QR codes are encoded once when recorded rather than on every draw.
- The PNG decoder reads the compressed data in place, decompresses into a single
  buffer of the exact size, undoes the row filters in place, and allocates the
  alpha plane only when a pixel is transparent: ~35% faster, ~70% less memory.
- Much less allocation per text block and table cell: tokens share one
  per-span format object, single-line blocks reuse their token list as the line,
  token lists are sized exactly, rendering contexts are structs, `PdfColor`
  implements `IEquatable<PdfColor>` (colour comparisons no longer box), and
  hex colours and font names are resolved without allocating. Table rows now
  allocate ~8 KB instead of ~26 KB, and text-heavy documents ~80% less.
- QR code generation is 2–6× faster: the symbol is built on flat arrays, each
  candidate mask is applied and undone in place instead of copying the matrix, and
  the four penalty rules are scored bit-parallel on packed rows and columns. The
  generated symbols are identical.
- Content-stream numbers (`F2`/`F4` coordinates and colours) are written by a
  fixed-point formatter instead of the general floating-point one, with
  byte-identical output: vector-heavy pages are ~35% faster.
- PNG images with an alpha channel are converted (decoded and compressed) once
  per process: the resulting streams are kept in a bounded (32 MB) process-wide
  cache keyed by the file's SHA-256, so a logo rendered into every document is
  no longer decoded again for each one. Output is identical.
- Large documents (8 pages or more and at least 512 K characters of content)
  compress their page content streams in parallel. Output is identical.
- New BenchmarkDotNet suite in `benchmarks/TerraPDF.Benchmarks`, and a throughput
  harness (`benchmarks/TerraPDF.Throughput`) that measures pages per second, CPU
  and memory, in or out of a container, and compares two versions (see
  `docs/benchmarks.md`). In a 2-CPU / 1 GB container, with the logo cache warm,
  invoices went from 356 to 10,200 pages/s and a 19-page annual report from
  3,100 to 25,200 pages/s.
- Output-comparison and profiling tools in `tools/pdf-compare` (byte, visual and
  glyph-position comparison, PNG round trip, font-width check against a viewer,
  QR symbol reference, allocation and CPU profiling).

### Fixed
- Built-in font widths now match the Adobe AFM metrics for every WinAnsi
  character. 25 entries were wrong, among them the curly quotes, bullet,
  `‚ „`, `š Ž Þ ß ¡` in Helvetica, `Z`, `Ž`, `š`, `ø`, `ý`, `þ`, `ÿ` in
  Times-Bold, and `„ Œ ™ œ ¡ ¦` in Times-Italic. A test now checks every entry
  against the AFM files.
- Characters with no WinAnsi code (e.g. Cyrillic or CJK in a built-in font) are
  measured as the `?` drawn in their place instead of a flat 500 units, so the
  words after them no longer overlap.
- Line wrapping and alignment can change slightly for text containing the
  characters above, because they are now measured at their real width.


---

## [Unreleased]

### Performance
- PNG images are no longer decoded when composed: only the header is read, and
  pixels are decoded once per distinct image when the document is saved. Placing
  the same PNG 40 times is ~20× faster and allocates ~40× less. Deduplication now
  keys on the original file bytes instead of hashing decoded pixels.
  `VectorCanvas.GetImageSizeInPoints` no longer decodes the image either.
- Documents without page-number spans are no longer laid out a second time when
  their page count has a different digit count from the initial estimate (99),
  e.g. every 1–9 page document.
- Text layout computes each word's width, font, and colour once instead of 4–5
  times, and memoises wrapped lines and table row heights for the duration of a
  publish. Text-heavy documents are ~2× faster with about half the allocations.
- Drawing a page slice of a split table only visits that slice's cells instead
  of scanning the whole table.
- Content-stream numbers and escaped text are written straight into the page
  buffer without temporary strings.
- New BenchmarkDotNet suite in `benchmarks/TerraPDF.Benchmarks` (see
  `docs/benchmarks.md`).

---

## [2.3.0] - 2026-09-26

### Added
- Dash patterns and phases on canvas ellipses (`StrokeEllipse`, `DrawEllipse`),
  rounded rectangles (`StrokeRoundedRect`, `DrawRoundedRect`), pie sectors
  (`StrokePie`, `DrawPie`), and arbitrary paths via `PathDescriptor.Dash(...)`,
  with the same validation and q/Q scoping as dashed lines and rectangles.
- `PathDescriptor.RoundedRect(...)` subpath; the radius is clamped to half the
  shorter side so the corners never overlap.
- Linear and radial two-stop gradient fills on canvas paths
  (`PathDescriptor.FillLinearGradient`, `PathDescriptor.FillRadialGradient`),
  written as PDF axial (type 2) and radial (type 3) shadings clipped to the
  path. The gradient spans the path's bounding box; outline strokes, even-odd
  fill, opacity, and dashes still apply.
- Canvas hyperlinks (`VectorCanvas.Link`), in-document links
  (`VectorCanvas.InternalLink`, with an optional scroll position), and outline
  entries (`VectorCanvas.Bookmark`, nestable by parent title) placed at
  absolute canvas positions.
- Canvas QR codes (`VectorCanvas.QrCode`) drawn as one filled vector path of
  merged module runs, with an optional background and a configurable quiet
  zone. Data too large for the chosen error-correction level fails at the call
  site rather than at render time.
- Canvas extras showcase sample (`19_CanvasExtrasShowcase.cs`) covering dashed
  shapes and paths, gradients, links, bookmarks, and QR codes.
- AI-agent toolset:
  - **`skills/terrapdf`**: an Agent Skill for coding assistants (GitHub Copilot,
    Claude Code, and others) with rules, a verified API reference, compiling
    recipes, and troubleshooting.
  - **`TerraPDF.Agents`** (new package): `create_pdf` and
    `get_pdf_document_format` tools that render a validated JSON document
    (headings, paragraphs, lists, tables, key/value, callouts, columns, charts,
    images, barcodes, and QR codes). They are exposed as `AIFunction`s for the
    Microsoft Agent Framework, Semantic Kernel, and `IChatClient`. Errors return
    JSON paths so models can self-correct. File output is sandboxed.
  - **`TerraPDF.Mcp`** (new package, dotnet tool `terrapdf-mcp`): an MCP server
    hosting the same tools plus `get_terrapdf_csharp_guide`, for Copilot,
    Cursor, Claude, and LangChain.
  - `AGENTS.md` and `docs/ai-agents.md`.

### Fixed
- `VectorCanvas.Grid()` drew nothing. The canvas callback runs before layout,
  when the canvas size is still zero, so the grid is now recorded as a command
  and sized when the canvas is drawn. It also keeps its place in the draw order.
  `Grid` now rejects a non-positive `cellHeight` or `lineWidth`.
- `ShowIf(false)` did not hide anything: the element chained after it replaced
  the empty placeholder. Everything chained after `ShowIf(false)` is now
  discarded, including headings, so they no longer reach the table of contents.

---

## [2.2.0] - 2026-09-20

### Added
- Positioned PNG/JPEG images on `VectorCanvas` from file paths, byte arrays,
  and streams, with `Stretch`, `Contain`, centred or top-left `Cover`, and
  natural-size top-left crop modes. Image clipping is isolated from subsequent
  canvas commands.
- Optional clockwise text rotation around the canvas text baseline point for
  standard and registered custom fonts.
- Elliptical arcs and closed sectors through `PathDescriptor`, plus
  `FillPie`, `StrokePie`, and `DrawPie` canvas conveniences.
- Native dash patterns and phases for canvas lines and stroked rectangles.
- Canvas media showcase sample (`18_CanvasMediaShowcase.cs`) covering every
  image fit mode, PNG soft-mask transparency, constant-alpha layering, image
  sources and natural sizing, dash phases, pie sectors, elliptical arcs, and
  rotated text.

### Fixed
- PNG decoding now supports 8-bit grayscale images with alpha (colour type 4).
- Elliptical arcs and pies reject non-finite angles instead of subdividing
  forever; a slice of a zero total (`360 * value / total`) previously hung.
- Rotated canvas text emits full-precision text-matrix coefficients. Small
  angles were previously distorted (0.3° rendered as 0.57°) or dropped
  entirely below ~0.3°, and matrix rounding rescaled glyphs by up to ~0.5%.
- Canvas dash phases must be nonnegative, as the PDF specification requires.
- Canvas images with a zero pixel dimension are skipped instead of writing
  `NaN` operands into the content stream.
- `VectorCanvas.GetImageSizeInPoints` throws the documented `ArgumentException`
  for unsupported data instead of `NotSupportedException`, matching
  `VectorCanvas.Image(byte[], …)`.

### Changed
- A canvas image is decoded once and reused on every page the canvas is drawn
  on, rather than re-decoded per page.
- Updated `Microsoft.SourceLink.GitHub` to `10.0.401` to remove the vulnerable
  transitive `Microsoft.Build.Tasks.Git` 8.0.0 dependency.

---

## [2.1.0] - 2026-09-03

Three additions, all backward compatible: font embedding now subsets
automatically, the vector canvas gained real translucency, and the vector
canvas can place text. No public API was removed or changed — existing calls
keep behaving exactly as before.

### Added (automatic font subsetting)
- **`FontFamily.Register(...)` now embeds only the glyphs a document actually
  shows**, instead of the whole font file. Stage 1: every glyph a document
  never draws is blanked out of the font's `glyf` table — glyph IDs are never
  renumbered, so `cmap`, `hmtx`, `GSUB`, and `Identity-H`/`CIDToGIDMap` all
  stay valid with no other change. Fully automatic; no new API.
- **Composite-glyph closure.** Accented Latin letters and Devanagari
  conjuncts are commonly composite glyphs — built from component glyphs no
  content stream ever references by ID directly. Blanking now computes the
  closure of shown glyphs under their composite references first, so an
  accent or conjunct never loses a component that happens to be otherwise
  unused.
- Measured, not assumed: a custom-font sample with mixed Latin/Cyrillic/Greek
  text dropped from 718KB to 320KB (55% smaller); a Devanagari report using
  two font variants (regular + bold) dropped from 152KB to 77KB (49%
  smaller). In both cases `glyf` itself shrank by over 99%; the realistic
  whole-document win is capped below that because tables sized per glyph
  regardless of usage (`hmtx`, `loca`, `cmap`, `GSUB`/`GPOS`, `post`, `name`)
  are not yet trimmed — see "Known limitations."

### Added (graphics state / constant alpha)
- **`/ExtGState` and the `gs` operator** — the first transparency mechanism
  in the writer beyond per-pixel image `/SMask`. Distinct opacity values used
  anywhere in a document are deduplicated into shared `/ExtGState` resources,
  the same way repeated images and fonts already are.
- Every `VectorCanvas` fill/stroke primitive (`Line`, `FillRect`/`StrokeRect`/
  `DrawRect`, the rounded-rectangle and ellipse/circle families, `Path`) takes
  a trailing `opacity` parameter (`1` = fully opaque, the default — omitting
  it costs nothing, no `/ExtGState` is emitted at all). `PathDescriptor`
  gained a matching `.Opacity(...)` fluent setter.

### Added (text on the vector canvas)
- **`VectorCanvas.Text(text, x, y, ...)`** places one line of text with its
  baseline at `(x, y)` — the one canvas primitive that isn't top-left
  anchored, since a baseline is what lets a label sit flush against an axis
  line or the shape it annotates. Renders through a registered custom font
  when `fontFamily` names one, otherwise the standard-14 families — the same
  resolution every other TerraPDF text API uses. Supports `opacity` like
  every other primitive (ghosted/watermark-style canvas text).
- **`VectorCanvas.MeasureTextWidth(...)`** (`static`) — measures a label in
  the same font `Text` would render it in, for centering or right-aligning
  before placing it.

### Fixed (samples)
- `10_VectorGraphicsShowcase.cs`: four shapes (a concentric-circle group, a
  Bézier teardrop, a star polygon, a diamond ring) were positioned wider than
  their panel's actual available width and bled past the page margin, one of
  them almost to the physical page edge. The donut chart's legend was drawn
  twice — bare colour swatches on the canvas with no labels (canvas text
  didn't exist yet when this sample was written), plus a second, separately
  laid-out text list stacked *below* the chart instead of beside it, wrapping
  awkwardly. Retightened the overflowing layouts and rebuilt the legend as a
  single swatch-plus-label pass using the new `VectorCanvas.Text`.

### Known limitations
- Font subsetting stage 1 does not renumber glyph IDs or shrink tables sized
  per glyph regardless of usage (`hmtx`, `loca`, `cmap`, `GSUB`/`GPOS`,
  `post`, `name`), so the size win on a full font is substantial but well
  short of what `glyf` alone shrinking by over 99% would suggest. Full
  re-indexed subsetting may follow in a future version.
- `/ExtGState` opacity is wired through `VectorCanvas` and canvas text only;
  `DrawImage`, flowed text (`TextBlock`), and `Background()`/border colours
  do not yet take an opacity parameter.

---

## [2.0.1] - 2026-08-28

A table-correctness release. Every fix below addresses a case that produced a
valid PDF with visibly wrong geometry — cells drawn on top of one another, or
rows running off the bottom of the page — rather than an error. No public API
changed; documents that do not use table spans and do not overflow a page are
byte-for-byte identical to 2.0.0.

### Added
- New sample: `16_table_spans_showcase.pdf` — a seven-page document covering
  every fix below. Pages 1-2 demonstrate `columnSpan`, `rowSpan`, the two
  together, and a spanned cell growing the rows it covers, each next to the
  code that produced it. Pages 3-5 are a header-less ledger whose accounts are
  joined by row spans, split across three pages to show the spans surviving the
  breaks intact. Pages 6-7 are a table placed straight into the content slot
  with no `Column` wrapper, paginating with its grouped two-column header
  repeated on both pages.

### Fixed (table column and row spans)
- **`Cell(columnSpan:)` no longer overlaps the cell that follows it.** The row
  cursor advanced by one column regardless of the span just placed, so in a
  three-column table a `columnSpan: 2` cell followed by a normal cell put that
  cell in column 2 — inside the span — and left column 3 empty. Cells are now
  placed in the first column not already covered by an earlier cell.
- **`Cell(rowSpan:)` now reserves its columns in the rows below it.** Each row
  started its cursor at column 1 with no record of what the previous rows had
  spanned, so the row after a `rowSpan: 2` cell drew its first cell at the same
  origin, on top of the spanned cell.
- **Spanned cells are measured and grow the rows they cover.** `GetRowHeights`
  skipped every cell with `RowSpan > 1`, so a spanned cell contributed no height:
  content taller than the rows it covered overflowed past the table, and a row
  containing only spanned cells collapsed to zero height. Row heights are now
  computed in two passes, the second distributing any shortfall evenly across
  the rows a spanned cell covers.
- Spans below `1` are treated as `1` rather than producing a zero-width or
  zero-height cell.

All three defects produced valid PDFs that silently drew cells on top of each
other, and none of them were covered by the test suite. New
`TableSpanTests` asserts on the rectangles actually emitted into the content
stream — placement, width, height, and pairwise non-overlap — including a
grouped header span repeated across a multi-page table.

### Fixed (table pagination)
- **A table with no header row now splits across pages.** Only tables declaring
  a `HeaderRow` were split between rows; a header-less table taller than the
  page was placed whole and its overflowing rows simply ran off the bottom.
  Header-less tables are now split when — and only when — they cannot fit a
  page, so a short one is still drawn as an ordinary item and the decorators
  wrapped around it (background, border) keep painting.
- **A table placed directly in the content slot now paginates.** The layout pass
  looked for a top-level `Column` and sent anything else down a single-page
  path, so `page.Content().Table(…)` overflowed instead of splitting. Such a
  table is now wrapped in a synthetic one-item column and takes the same
  row-splitting path. Content that fits on one page is unaffected and still
  receives the whole content box.
- **A row span is no longer cut in half at a page break.** The splitter moved one
  row at a time, so a break landing inside a `rowSpan` left a truncated cell on
  the first page with nothing continuing it overleaf. Data rows are now grouped
  into the smallest runs that no row span crosses, and a group is never divided
  between pages. A group taller than a whole page is still forced out so layout
  always makes progress.

`TablePaginationTests` covers each case, including the row-span break at seven
different page offsets — the defect only appeared at offsets where the break
happened to fall inside a spanned pair.

### Known limitations
- A row span that starts in a *header* row and extends into data rows renders
  correctly on the first page, but is truncated to the header rows on every
  continuation page: the header block repeats while the data rows it also
  covers stay behind on the page before. Row spans that start in a data row are
  unaffected — those are grouped and never split. Keep header rows
  self-contained if a table is expected to paginate.

---

## [2.0.0] - 2026-07-23

### Added (custom font embedding)
- **`FontFamily.Register(...)`** — embeds a TrueType font (`.ttf`, or `.otf`
  with a `glyf`/`loca` table) as a regular, bold, italic, and/or bold-italic
  variant, registered process-wide by name and used via the existing
  `TextStyle.FontFamily(...)`/`.Bold()`/`.Italic()` API — no new call sites
  at the document-composition layer. File path, `byte[]`, and `Stream`
  overloads are provided; registration is thread-safe and cached, so
  registering once at startup and reusing across every document (including
  concurrently, in a long-lived server process) is the intended usage.
- Registered fonts are embedded as `Type0`/`CIDFontType2` composite fonts
  with `Identity-H` encoding, giving full Unicode text support (Cyrillic,
  Greek, and any other script the font covers) — not just the
  WinAnsiEncoding range the three standard-14 fonts are limited to. A
  `ToUnicode` CMap is emitted so copy/paste and text extraction recover the
  correct Unicode text.
- A font is embedded once per document and shared across every page and
  every use, mirroring the existing image-deduplication behaviour — a
  registered font used throughout a 200-page report still costs one
  embedded copy. `/W` glyph widths and the `ToUnicode` CMap only cover the
  glyphs the document actually shows.
- Requesting a style that wasn't registered (e.g. `.Bold()` on a family with
  only a regular file registered) falls back to the closest registered
  variant instead of throwing, matching the library's existing
  graceful-fallback behaviour for unmappable glyphs.
- New [`docs/custom-fonts.md`](docs/custom-fonts.md) guide.

### Added (Devanagari-aware text rendering)
- Custom-font text through a Devanagari (Hindi/Marathi/Sanskrit, …) font now
  renders through automatic, pure-C# corrections applied transparently at
  measurement and draw time — no new public API, no external dependency:
  - **Matra reordering** — the vowel sign ि (U+093F) is moved to before the
    consonant cluster it attaches to, matching its visual position (Unicode
    stores it after the consonant).
  - **Conjunct ligatures** — reads the font's own `GSUB` table (`half`,
    `akhn`, `cjct` features) and substitutes the ligature glyphs the font
    defines, so conjuncts like स्व, स्थ, क्ष, ज्ञ render as proper joined
    forms instead of separate glyphs with a visible ् mark.
  - **Reph** (र् at the start of a cluster, e.g. धर्म, वर्तमान, दुर्बलता) —
    reordered to the end of its cluster and substituted via `GSUB`'s `rphf`
    feature, so it renders as the correct hook above the following
    consonant instead of a loose र + ् pair.
  - **Below-base/post-base 'ra'** (प्र, क्र, त्र, ष्ट्र, …) — substituted
    via `GSUB`'s `rkrf` feature into the font's single designed glyph.
  - None of this requires a native shaping engine (no HarfBuzz, no other
    native dependency) — `src/TerraPDF` remains pure managed C#.
- Long single words (a URL, or a run of connected-script text with no space
  to wrap at) that exceed the available line width now break at character
  boundaries instead of overflowing the container.
- New sample: `15_child_nutrition_india_report.pdf` — a full multi-page
  Hindi-language report demonstrating the above.

### Known limitations
- CFF-flavoured OpenType (`OTTO`) fonts and TrueType Collections (`.ttc`)
  are not yet supported and throw `NotSupportedException` on registration.
- Fonts are embedded in full; glyph subsetting is not yet implemented.
- No synthetic (faux) bold/italic — only registered variants are used.
- Devanagari support is scoped, font-data-driven substitution, not a general
  OpenType shaping engine: no GPOS mark positioning, and `GSUB`'s `blwf`
  feature (below-base forms for consonants other than र) uses contextual
  lookups that aren't parsed, so those specific forms may still draw as
  separate glyphs. See [`docs/custom-fonts.md`](docs/custom-fonts.md)'s
  "Known limitations" for the full picture.

### Changed
- Bumped to a major version because of the scope of the new font-embedding
  subsystem, not because of any breaking change: every existing public API
  and every previously-generated byte for standard-font documents is
  unchanged (verified by the full pre-existing test suite passing
  unmodified).

---

## [1.5.1] - 2026-07-10

### Added
- **.NET 10 target** — the library now multi-targets `net8.0`, `net9.0` and
  `net10.0` (the current LTS). No API or behaviour changes; existing .NET 8/9
  consumers are unaffected.

### Changed
- Test suite now runs once per supported runtime (`net8.0`, `net9.0`,
  `net10.0`); the sample app moved to `net10.0`.
- CI workflows install the .NET 8/9/10 SDKs; `global.json` now requires the
  .NET 10 SDK (with `rollForward: latestMajor`).
- Consolidated the two overlapping CI workflows (`ci.yml` + `dotnet.yml`) into
  a single `ci.yml` triggered on `master` (the old one targeted `main` and
  never ran).
- Migrated the solution to the XML-based `.slnx` format (`TerraPDF.slnx`,
  replaces `TerraPDF.sln`); also fixed a stale mapping that built the library
  in Release for Debug|Any CPU solution builds.

---

## [1.5.0] - 2026-07-06

### Added (barcodes & QR codes)
- **`container.Barcode(data, ...)`** — Code128 (Subset B) barcode generation,
  encoding printable ASCII (0x20-0x7E). Supports an explicit or auto-fill
  width, custom module/background colour, an optional human-readable caption
  rendered below the bars, and a configurable quiet zone.
- **`container.QrCode(data, ...)`** — from-scratch ISO/IEC 18004 QR code
  generator: byte-mode encoding, automatic version selection (1-40), all four
  error correction levels (`QrErrorCorrectionLevel.L/M/Q/H`), Reed-Solomon
  error correction, and full mask-pattern penalty scoring. Supports an
  explicit or auto-fill size, custom module/background colour, and a
  configurable quiet zone.
- Both render as **vector-filled rectangles** (one rect per bar / per
  contiguous run of dark QR modules), matching the existing `VectorCanvas`
  rendering style — crisp at any zoom, no raster image pipeline, and
  placeable anywhere an `IContainer` is exposed (`Column`, `Row`, `Table`
  cell, header, footer).
- New `PdfPage.AddFilledRects` batch primitive: emits one colour operator
  followed by many `re` ops and a single trailing fill, avoiding a redundant
  colour-set/fill pair per module on symbols with thousands of modules.

---

## [1.4.0] - 2026-07-04

### Added (encryption)
- **AES-256 encryption (Standard Security Handler Revision 6)** — now the
  **default** algorithm. SHA-2 based key derivation (algorithm 2.B) with random
  salts, 48-byte /O and /U verifiers, /OE + /UE key wrapping, and an encrypted
  /Perms entry; documents are emitted as PDF 2.0 and open in every mainstream
  viewer since ~2008 (Acrobat 9+, Chrome, Edge, Firefox, Preview, …).
- `EncryptionOptions.Algorithm` (`EncryptionAlgorithm.Aes256` |
  `EncryptionAlgorithm.Aes128`). **Behavioral note:** existing callers now get
  AES-256 by default; set `Algorithm = EncryptionAlgorithm.Aes128` to keep the
  legacy Revision 4 output for very old viewers.

### Added (content features)
- **Images from bytes and streams** — `container.Image(byte[])` and
  `container.Image(Stream)` overloads (with optional width), so images can come
  from embedded resources, databases, or generated data. The format (PNG/JPEG)
  is now detected from the data's magic bytes rather than the file extension.
- **PNG transparency** — RGBA PNGs keep their alpha channel, emitted as a
  `/SMask` soft mask (fully opaque images skip the mask). Indexed-transparency
  (tRNS) PNGs still render opaque.
- **Image deduplication** — identical image data used on multiple pages is
  embedded once and shared document-wide.
- **Anchor-based bookmarks** — `container.Bookmark("Title")` (optionally with a
  `parentTitle` for nesting) marks its content as an outline destination; the
  page number and vertical position are resolved automatically during render.
  The page-number-based `Bookmark(title, pageNumber)` API remains available.
- **Paragraph splitting across pages** — a text block taller than the remaining
  page now flows onto the next page, split between wrapped lines, instead of
  overflowing off the bottom. Applies to plain/decorated text items; content
  wrapped in hyperlinks and headings intentionally keeps the previous behaviour.

### Fixed (content features)
- **Bookmark destinations keep the reader's zoom and land accurately.**
  Bookmarks previously emitted `/Fit`/`/FitH` destinations (which force a zoom
  change) with an un-flipped Y coordinate (top-origin written where PDF expects
  bottom-origin), so clicking an entry zoomed the page and scrolled to the
  wrong position. Destinations are now `/XYZ null top null` — zoom-retaining —
  with the Y correctly converted to PDF coordinates.
- **Height-constrained images keep their aspect ratio** — previously only the
  height was clamped, horizontally squashing tall images.
- **TOC heading scan traverses wrappers** — headings inside decorators
  (`Padding`, `Background`, …), hyperlinks, or bookmark anchors were invisible
  to the Table of Contents scan but still recorded during render, which could
  crash TOC generation with mismatched entry lists.

### Changed (output size & memory)
- **Content streams are Flate-compressed** (`/Filter /FlateDecode`), matching how
  PNG image data was already stored. Typical multi-page text documents shrink
  substantially; combined with encryption the stream is compressed first, then
  encrypted (PDF §7.6.1).
- **Streamed serialization.** The writer no longer buffers the whole file in a
  `MemoryStream` to compute xref offsets — it writes directly to the output
  stream (buffered, non-seekable-safe) while counting bytes, so peak memory is
  no longer ~2× the file size.
- **Per-line text objects.** Text is emitted as one `BT…ET` block per line with
  font/colour set only when they change, instead of a full text object per
  word — significantly smaller content streams, no visual change.

### Changed (layout engine)
- **Fragment-based layout engine.** Pagination decisions are now made once, in a
  single layout pass that produces per-page fragments consumed by both page
  counting and rendering — the previous duplicated count/render implementations
  (which could drift and disagree) have been removed. No visible output change
  except the decorator fix below.
- **Decorators now render on every page of a split column.** A `Background`,
  `Border`, `RoundedBorder`/`RoundedBox`, or per-edge border wrapped around
  paginating content previously drew on no page at all; it now draws its chrome
  on each page, covering that page's content area.
- **Thread safety.** Concurrent document generation is now safe: the static
  heading recorder used by the Table of Contents pass has been replaced with
  per-render state, so parallel `PublishPdf` calls no longer cross-contaminate
  TOC entries.

### Added
- **`FontFamily(string)`** on `TextDescriptor`, `SpanDescriptor`, and `TextStyle` now
  actually selects a font (it was previously a silent no-op). Supported families are the
  standard-14 sets **Helvetica**, **Times**, and **Courier**; common aliases
  (`"Arial"`, `"Times New Roman"`, `"Courier New"`) are accepted and unknown names fall
  back to Helvetica. All 12 family/weight/slant variants (F1–F12) are registered in
  every document.
- Accurate Adobe AFM width tables for **Helvetica-Bold**, **Times-Roman**, and
  **Times-BoldItalic**; Courier measured at its fixed 600-unit advance.

### Fixed
- **Bold/italic no longer switch typeface.** `Bold()` previously rendered Times-Bold and
  `Italic()` Times-Italic even in Helvetica text; they now select the bold/oblique
  variant of the *current* family (e.g. Helvetica → Helvetica-Bold). Documents render
  visibly different (correct) from 1.3.0.
- **Document metadata now shows in viewers.** The Info dictionary is referenced from the
  PDF *trailer* (`/Info`, per spec §7.5.5) instead of the Catalog, where conforming
  readers never looked for it.
- **Encrypted documents no longer leak or corrupt strings.** Metadata values, bookmark
  titles, and hyperlink URIs are now AES-encrypted with their owning object's key,
  matching the declared `/StrF /StdCF` crypt filter. Previously they were written in
  plaintext, which conforming viewers would "decrypt" into garbage — and which leaked
  the plaintext in a supposedly encrypted file.
- **Pagination works through all decorators.** Columns/tables wrapped in `Margin`,
  `RoundedBorder`, `RoundedBox`, or per-edge borders (`BorderTop` …) now split across
  pages; previously these wrappers silently disabled pagination and content overflowed
  a single page. Decorator traversal is now driven by the element model
  (`Element.PassthroughChild`), so new decorators participate automatically.
- **Page-number footers in 100+ page documents.** `CurrentPageNumber()`/`TotalPages()`
  spans are measured using a placeholder sized from the real page count (previously a
  fixed 2-digit `"00"`), so footers no longer wrap taller than the space reserved for
  them in long documents.
- **Outline (bookmark) spec compliance.** Outline items no longer carry a spurious
  `/Type /Outlines` entry, and `/Count` now reports all open descendants instead of
  direct children only.
- Missing negative-value validation added to the single-side `Padding*`/`Margin*`
  overloads (`PaddingTop`, `MarginLeft`, `PaddingHorizontal`, …).

### Changed
- **BREAKING:** `TextDescriptor.Span(string, Action<TextStyle>)` is now
  `Span(string, Func<TextStyle, TextStyle>)`. `TextStyle` is immutable, so the old
  `Action` overload discarded every style it built — code using it compiled but had no
  effect. Return the configured style instead: `t.Span("hi", s => s.Bold())`.
- `Text(string)` accepts empty/whitespace strings (renders an empty block) instead of
  throwing, so dynamic data no longer needs caller-side guards. `null` still throws.

---

## [1.3.0] - 2026-05-19

### Added
- **AES-128 PDF Encryption**
  documents with the PDF Standard Security Handler (Revision 4, AES-128 CBC).
  - `UserPassword` — password required to open the document (omit for no-password encryption).
  - `OwnerPassword` — password granting full access, bypassing all restrictions.
  - `Permissions` — `PdfPermissions` flags enum controlling print, copy, edit, fill forms, etc.
  - `PdfPermissions` — `[Flags]` enum with `Print`, `CopyText`, `ModifyContents`,
    `ModifyAnnotations`, `FillForms`, `ExtractForAccessibility`, `AssembleDocument`,
    `PrintLowResolution`, `All`, `None`.
  - Per-object AES-128 CBC encryption of all content streams and image XObjects.
  - Standard /O and /U verifier entries computed via the PDF key-derivation algorithms
    (MD5 key expansion + RC4 for O/U; AES-128 for content).
  - Encrypted documents are emitted as PDF 1.6 (minimum required for AES).
  - No external packages — implemented entirely with `System.Security.Cryptography`.
      `EncryptionTests.cs` — 26 tests covering all password combinations, every permission
      flag, multi-page documents, metadata + encryption, null-guard, and output-size sanity.

  ### Fixed
  - **Incorrect password error** — encrypted PDFs now correctly write the random `/ID` array
    to the PDF trailer and pass it into all key-derivation steps, so viewers can reproduce
    the exact file encryption key.
  - **"Error on this page"** — removed spurious `/Filter /Crypt` from content streams and
    JPEG image dictionaries; encryption is transparent to stream filters per PDF §7.6.1.
  - **Corrupted decrypted content** — `EncryptBytes` now uses PKCS#7 padding instead of
    zero-padding so viewers can correctly strip the padding block after AES-128 CBC decryption.
  - **`SamplePDF` folder** — sample project now calls `Directory.CreateDirectory` at startup
    so the output folder is always created automatically if it does not already exist.
  - **Tick/cross badges** — `EncryptionShowcase` permission badges replaced `✓`/`✗`
    (U+2713/U+2717, outside WinAnsi) with ASCII `+`/`x` to prevent `?` rendering.

  ---

  ## [1.2.3]

### Added
- **Vector Graphics / Canvas API** — `container.Canvas(height, draw)` places a
  fixed-height drawing surface that fills the available width. The callback receives
  a `VectorCanvas` with a full set of fluent primitives:
  - **Lines** — `Line(x1, y1, x2, y2, hexColor, lineWidth)`
  - **Rectangles** — `FillRect`, `StrokeRect`, `DrawRect` (filled + stroked)
  - **Rounded rectangles** — `FillRoundedRect`, `StrokeRoundedRect`, `DrawRoundedRect`
  - **Circles** — `FillCircle`, `StrokeCircle`, `DrawCircle`
  - **Ellipses** — `FillEllipse`, `StrokeEllipse`, `DrawEllipse`
  - **Arbitrary paths** — `Path(p => ...)` using `PathDescriptor` with `MoveTo`,
    `LineTo`, `CurveTo` (cubic Bézier), `Close`, convenience shapes
    (`Rect`, `Ellipse`, `Circle`, `Polyline`, `Polygon`), paint setters
    (`Fill`, `Stroke`), and even-odd fill rule (`UseEvenOddFill`)
  - **Grid helper** — `Grid(cellWidth, cellHeight?, hexColor, lineWidth)` draws a
    full-canvas rectangular grid
  - All coordinates use a **top-left origin** in PDF points, consistent with every
    other TerraPDF layout API
- **`VectorGraphicsTests.cs`** — test suite exercising every `VectorCanvas` primitive,
  every `PathDescriptor` command, validation guards, and compound shapes
- **`10_VectorGraphicsShowcase.cs`** sample — 4-page showcase PDF demonstrating all
  primitives (lines, rectangles, rounded rectangles, circles, ellipses, arbitrary
  paths, polygons) plus data-visualisation examples (bar chart, donut chart,
  line/sparkline chart) built entirely with the Canvas API
- **`docs/vector-graphics.md`** — new documentation guide covering the Canvas API,
  all `VectorCanvas` methods, `PathDescriptor` usage, coordinate system, and
  practical chart/diagram examples
- **Unicode & WinAnsiEncoding showcase sample** (`11_UnicodeShowcase.cs`) — a 5-page
  reference PDF demonstrating TerraPDF's full WinAnsiEncoding character coverage:
  - Page 1: Introduction and an 18-language European language sample table showing
    correct glyph rendering and line-wrapping across French, German, Spanish,
    Portuguese, Italian, Dutch, Romanian, Hungarian, Norwegian, Swedish, Finnish,
    Polish, Czech, Turkish, Catalan, Danish, Welsh, and English.
  - Page 2: Windows-1252 typographic specials (byte range 0x80–0x9F) with Unicode
    code points, byte values, rendered glyphs, names, and in-context examples;
    plus Latin-1 Supplement symbol groups (U+00A0–U+00FF).
  - Page 3: Complete WinAnsiEncoding byte-to-glyph reference grid (0x20–0xFF),
    16 columns wide, with row/column labels and greyed-out undefined positions.
  - Page 4: Multi-font typographic comparison showing Helvetica (sans-serif),
    Times (serif), and Courier (monospace) rendering the same paragraph.
  - Page 5: Font metrics deep-dive — advance-width heat-map table and a
    justified paragraph demonstrating word-spacing and glyph-width accuracy.
- **`docs/unicode-and-encoding.md`** — new documentation guide covering
  WinAnsiEncoding coverage, Windows-1252 specials, Latin-1 Supplement characters,
  octal content-stream encoding, safe character ranges, and the built-in AFM
  glyph-width tables.

### Fixed
- Language sample sentences for Polish, Czech, Turkish, Romanian, and Hungarian
  replaced with WinAnsiEncoding-safe equivalents — characters in the U+0100+ range
  that lack a WinAnsiEncoding glyph (e.g. Ł, ż, Č, ř, Ş, ğ, ı, ă) previously
  rendered as `?` in the output PDF.
- Win-1252 Specials showcase table converted from `ConstantColumn` widths
  (52 pt + 40 pt + 28 pt) to proportional `RelativeColumn` definitions so the
  table always fits within the page's content area without overflowing.

---

## [1.2.2] - 2026-05-04

### Added
- **Table of Contents generation** — `container.TableOfContents()` creates a TOC page populated with headings collected from `.H1()`–`.H6()`, with hierarchical numbering (e.g. 1, 1.1, 1.1.1) and clickable internal links.
- **Internal links (GoTo)** — `container.InternalLink(pageNumber, top?)` creates intra-document navigation that preserves current zoom level and scrolls to the target heading.
- **Section headings** — `.H1()` through `.H6()` methods with sensible default styles (size + weight), each returning `TextDescriptor` for further customisation.

### Fixed
- Internal link zoom issue — `/FitH` replaced with `/XYZ` so clicking TOC entries no longer resets zoom.
- Page number display in TOC now excludes TOC page count (TOC treated as page zero), while links still point to correct physical pages.
- `HeadingRecorder` propagation through `DrawingContext.At` fixed so TOC entries are correctly collected.

---

## [1.2.1] - 2026-05-03

### Documentation
- Corrected all `GeneratePdf()` → `PublishPdf()` method references throughout README and all documentation files
- Fixed `Color.Blue` hex values: Darken2 (`#1976D2`), added missing Darken3 (`#1565C0`) and Darken4 (`#0D47A1`)
- Added `PageBreak()` to `ColumnDescriptor` API table in layout guide
- Documented `HeaderOnFirstPageOnly()` page method for first-page-only headers
- Clarified `RelativeItem()` default weight = 1 in row-and-column layout guide
- Added `FontFamily(string)` to `TextDescriptor` methods table in text-and-spans guide

---

## [1.2.0] - 2025-07-14

### Added
- `Underline()` style method on `TextDescriptor` and `SpanDescriptor`. Draws an
  underline beneath the text. Can be combined with `Strikethrough()`.
- `LineHeight(double)` style method on `TextDescriptor`. Sets a line-height
  multiplier (e.g. `1.0` = tight, `1.4` = default, `2.0` = double-spaced).
  Also accepted by `DefaultTextStyle` for page-wide control.
  `TextStyle.LineHeightMultiplier` property exposed for custom render logic.
- `RoundedBorder(radius, lineWidth, hexColor)` decorator — draws a rounded-corner
  stroke border around child content. Corner radius is automatically clamped to
  half the shorter dimension.
- `RoundedBox(radius, fillHexColor, borderHexColor, lineWidth)` decorator —
  fills the area with `fillHexColor` and draws a rounded-corner border in one
  call. Equivalent to `Background + RoundedBorder` but rendered as a single path.
- `BorderTop(lineWidth, hexColor)`, `BorderBottom`, `BorderLeft`, `BorderRight`
  per-edge border decorators. Each side is independently configurable with its
  own width and colour. The `hexColor` parameter defaults to `"#000000"`.
- `PageBreak()` container extension — inserts an explicit page-break marker
  inside a `Column`. Silently skipped when it falls at the very start of a page.
- `Hyperlink(url)` container extension — wraps child content in a clickable PDF
  URI annotation (`/Annots` with `/URI` action). Clicking the area in a
  conforming PDF viewer navigates to the given URL.
- `HighPriorityFeatureTests.cs` — 20 new tests covering all five features above
  (underline, hyperlink, per-edge borders, line height, and their combinations).
- `RoundedBorderTests.cs` — dedicated tests for `RoundedBorder` and `RoundedBox`
  geometry, clamping behaviour, and validation.
- `PageBreakTests.cs` — tests for explicit page-break positioning.
- `HeaderFirstPageOnlyTests.cs` — tests verifying that the header slot can be
  conditionally rendered only on the first page using `ShowIf`.

### Fixed
- **Breaking (behaviour):** `TextDescriptor.Span().Bold()` / `.Italic()` /
  `.Strikethrough()` etc. previously mutated the whole block's `SpanStyle`
  instead of the individual span's style. Now correctly isolated.

### Changed
- Folder `Fluent` renamed to `Core` (`TerraPDF.Core` namespace).
- Folder `Infrastructure` renamed to `Infra` (`TerraPDF.Infra` namespace).

---

## [1.1.0] - 2025-06-01

### Added
- `Margin`, `MarginVertical`, `MarginHorizontal`, `MarginTop`, `MarginBottom`,
  `MarginLeft`, `MarginRight` decorator methods with full `Unit` overloads.
  Margin is outer spacing — the margin region stays transparent, background
  and border start after the gap.
- `SpanDescriptor` — per-span fluent builder returned by `TextDescriptor.Span()`,
  `CurrentPageNumber()`, and `TotalPages()`. Style methods chained after
  `.Span(...)` now apply **only to that span**, not the whole `TextBlock`.
- Complete documentation suite under `docs/`:
  `getting-started.md`, `text-and-spans.md`, `layout.md`, `decorators.md`,
  `images.md`, `colors.md`, `page-sizes-and-units.md`,
  `components-and-templates.md`, `row-and-column-layout.md`.
- `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`.
- Input validation (using `ArgumentNullException.ThrowIfNull`,
  `ArgumentException.ThrowIfNullOrWhiteSpace`,
  `ArgumentOutOfRangeException.ThrowIfNegative/ThrowIfNegativeOrZero`)
  on every public API entry point.
- 62 new tests across `ValidationTests` and `BehaviourTests` (92 total).

### Fixed
- **Breaking (behaviour):** `TextDescriptor.Span().Bold()` / `.Italic()` /
  `.Strikethrough()` etc. previously mutated the whole block's `SpanStyle`
  instead of the individual span's style. Now correctly isolated.

### Changed
- Folder `Fluent` renamed to `Core` (`TerraPDF.Core` namespace).
- Folder `Infrastructure` renamed to `Infra` (`TerraPDF.Infra` namespace).

---

## [1.0.0] - 2025-01-01

### Added
- Initial release.
- PDF 1.7 generation with zero native dependencies.
- Text styling: bold, italic, bold-italic, font size, colour, strikethrough.
- Multi-span text blocks with mixed styles.
- `Column` (vertical stacking) and `Row` (horizontal layout) with
  `RelativeItem`, `AutoItem`, and `ConstantItem` sizing.
- `Table` with relative and constant columns, `HeaderRow` (repeats on
  continuation pages), alternating-row support.
- `Padding` with all side variants and `Unit` overloads.
- `Background`, `Border`, `Alignment` (horizontal + vertical), `ShowIf`.
- Horizontal and vertical rule lines.
- PNG and JPEG image embedding.
- `IComponent` reusable content blocks.
- `IDocument` reusable document templates.
- Headers, footers, page numbers (`CurrentPageNumber`, `TotalPages`).
- Multi-page documents with automatic table pagination.
- Full Material Design colour palette (`Color.*`).
- Standard page sizes including ISO A-series, Letter, Legal, Tabloid,
  Executive, and `Landscape()` helper.
- `Unit` system: Point, Millimetre, Centimetre, Inch.
- Targets .NET 8 and .NET 9.
- CI workflow (GitHub Actions): build, test, coverage.
- Publish workflow (GitHub Actions): NuGet + symbols on release tag.

[Unreleased]: https://github.com/sahebansari/TerraPDF/compare/v2.3.0...HEAD
[2.3.0]: https://github.com/sahebansari/TerraPDF/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/sahebansari/TerraPDF/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/sahebansari/TerraPDF/compare/v2.0.1...v2.1.0
[2.0.1]: https://github.com/sahebansari/TerraPDF/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/sahebansari/TerraPDF/compare/v1.5.1...v2.0.0
[1.5.1]: https://github.com/sahebansari/TerraPDF/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/sahebansari/TerraPDF/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/sahebansari/TerraPDF/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/sahebansari/TerraPDF/compare/v1.2.3...v1.3.0
[1.2.3]: https://github.com/sahebansari/TerraPDF/compare/v1.2.2...v1.2.3
[1.2.2]: https://github.com/sahebansari/TerraPDF/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/sahebansari/TerraPDF/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/sahebansari/TerraPDF/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/sahebansari/TerraPDF/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/sahebansari/TerraPDF/releases/tag/v1.0.0
