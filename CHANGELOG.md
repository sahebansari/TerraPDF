# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.2.3] - 2026-05-18

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
