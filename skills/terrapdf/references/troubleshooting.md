# Troubleshooting TerraPDF code

## Mistakes that break generated code

| Wrong | Right | Why |
|-------|-------|-----|
| Two `.Text(...)` on one container | Wrap in `Column`, use `col.Item()` | A container holds one child; the second replaces the first |
| `.Save(path)` / `.Generate()` | `.PublishPdf(path)` | No other output method exists |
| `Color.Orange.Lighten3` | `Color.Orange.Medium` | Most families only have `Medium` and `Darken2` |
| `Color.FromHex("#FF0000")` | `"#FF0000"` | Colour parameters are plain strings |
| `page.Size(PageSize.A4.Landscape)` | `page.Size(PageSize.Landscape(PageSize.A4))` | `PageSize` members are tuples, not objects |
| `canvas.Text(...)` positioned by top-left | Pass the **baseline** y | `Text` is the one baseline-anchored primitive |
| `FillPie(cx, cy, rx, ry, ...)` | `FillPie(x, y, width, height, ...)` | Pies take a bounding box; only `Arc`/`Sector` take centre + radii |
| Cyrillic/Devanagari in a standard font | `FontFamily.Register(...)` then `.FontFamily("Name")` | Standard-14 fonts are WinAnsiEncoding only |
| `doc.Encrypt(...)` after `doc.Page(...)` | Call `Encrypt` first | Encryption must be configured before pages |
| `Text("Chapter 1")` expecting a TOC entry | `H1("Chapter 1")` | Only `H1`–`H6` are collected |
| `col.Item().ShowIf(cond).Text(...)` | `if (cond) col.Item().Text(...);` | Before 2.3.0, `ShowIf(false)` was overwritten by the element chained after it, so the content still rendered |
| `canvas.Grid(20)` inside `Canvas(...)` | Draw grid lines with `Line(...)` using widths you computed | Before 2.3.0 the draw callback ran before layout, so `Grid` saw a zero-sized canvas and drew nothing |
| `canvas.StrokeCircle(..., dashPattern: [4, 2])` | `canvas.StrokeEllipse(cx, cy, r, r, ..., dashPattern: [4, 2])` | Circle methods have no dash parameters; ellipses do **[2.3+]** |
| `p.Dash([4, 2]).Fill(...)` with no `Stroke` | Add `.Stroke(color, width)` | `Dash` only affects the stroke **[2.3+]** |
| `.FillLinearGradient(...).Fill("#FFF")` expecting both | Keep one | `Fill` and the gradient methods replace each other; the last call wins **[2.3+]** |
| `canvas.Link(...)` expecting a visible button | Paint the shape and text first, then `Link` over it | Links are invisible annotations **[2.3+]** |
| `row.Item()` | `row.RelativeItem()` / `ConstantItem()` / `AutoItem()` | `Item()` exists on `ColumnDescriptor` only |

## Compiler errors

| Error | Cause | Fix |
|---|---|---|
| `CS0117: 'Color.X' does not contain a definition for 'Lighten3'` | That shade does not exist for this colour family | Use `Medium`/`Darken2`, a full-palette family (`Red`, `Blue`, `Green`, `Grey`), or a hex string |
| `CS1061: 'IContainer' does not contain a definition for 'Text'` | Missing `using TerraPDF.Core;` (the fluent API is extension methods) | Add the using |
| `CS0246: 'PageSize' / 'Color' / 'Unit' could not be found` | Missing `using TerraPDF.Helpers;` | Add the using |
| `CS0246: 'IComponent' / 'IDocument' could not be found` | Missing `using TerraPDF.Infra;` | Add the using |
| `CS0246: 'QrErrorCorrectionLevel' could not be found` | Missing `using TerraPDF.Barcodes;` | Add the using |
| `CS1061: 'RowDescriptor' does not contain a definition for 'Item'` | `Item()` is Column-only | `RelativeItem()`, `ConstantItem(w)`, or `AutoItem()` |
| `CS1061: 'TextDescriptor' does not contain a definition for 'Padding'` | Decorators go **before** the element | `container.Padding(8).Text("x")`, not `container.Text("x").Padding(8)` |
| `CS1061: '...' does not contain a definition for 'GeneratePdf' / 'Save'` | Wrong output method | `PublishPdf(path)`, `PublishPdf()` or `PublishPdf(stream)` |
| `CS1503: cannot convert from 'string' to 'Unit'` in `Margin` | `Margin(2, "cm")` | `Margin(2, Unit.Centimetre)` |

## Runtime symptoms

| Symptom | Cause | Fix |
|---|---|---|
| Only the last of several items appears | Several elements assigned to one container | Put them in a `Column` |
| `?` instead of characters | Text outside WinAnsi in a standard font | `FontFamily.Register(...)` a `.ttf` that covers the script, then `.FontFamily(name)` |
| `NotSupportedException` on `FontFamily.Register` | CFF-flavoured `.otf` or `.ttc` collection | Use a TrueType-outline `.ttf` |
| `NotSupportedException` from `Barcode` | Code128 input outside printable ASCII | Strip or transliterate, or use `QrCode` (UTF-8) |
| `NotSupportedException` from `QrCode` | Data too long for any QR version at that error-correction level | Shorten, or lower the level (`H` → `M` → `L`) |
| Canvas drawing clipped or overlapping following content | Drawing exceeds the canvas height; canvases never paginate | Increase `Canvas(height, ...)` or scale the drawing |
| Blank area where a `PageBreak()` was expected to move content | A break at the very top of a page is skipped by design | Nothing to fix |
| Empty table-of-contents page | No `H1()`-`H6()` headings | Use heading methods for section titles |
| TOC or bookmark page numbers off by the TOC page | Manual `doc.Bookmark(title, page)` numbers are absolute, 1-based | Prefer anchored `container.Bookmark("Title")`, which resolves automatically |
| `InvalidOperationException` at `PublishPdf` mentioning a page | A canvas `InternalLink` targets a page beyond the document's last page | Use a page number that exists (1-based, physical pages including a TOC page) **[2.3+]** |
| `NotSupportedException` from `canvas.QrCode` | Data too long for any QR version at that level; thrown at the call, not at render | Shorten it or lower the level **[2.3+]** |
| `InvalidDataException` at `PublishPdf` | A PNG is truncated or corrupt. From 2.4.0 its pixel data is read when the document is saved, so the error surfaces there rather than at `Image(...)` | Re-export the PNG, or check it opens in an image viewer before adding it |
| A canvas bookmark appears once although the canvas repeats on every page | A repeated (title, parent) pair is recorded once by design | Include something unique, such as the page, in the title **[2.3+]** |
| `CS1061` for `Link`, `QrCode`, `Bookmark` on `VectorCanvas`, or `FillLinearGradient`/`Dash`/`RoundedRect` on `PathDescriptor` | The project references TerraPDF older than 2.3.0; these are **[2.3+]** members | Upgrade TerraPDF, or use the layout-level `Hyperlink`, `QrCode`, and `Bookmark` decorators |
| Image stretched across the page | `Image(path)` fills the available width | `Image(path, widthPt)`, wrapped in `AlignCenter()` or `AlignRight()` to position it |
