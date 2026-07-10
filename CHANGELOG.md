# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[Unreleased]: https://github.com/sahebansari/TerraPDF/compare/v1.2.3...HEAD
[1.2.3]: https://github.com/sahebansari/TerraPDF/compare/v1.2.2...v1.2.3
[1.2.2]: https://github.com/sahebansari/TerraPDF/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/sahebansari/TerraPDF/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/sahebansari/TerraPDF/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/sahebansari/TerraPDF/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/sahebansari/TerraPDF/releases/tag/v1.0.0
