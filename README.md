# TerraPDF 

A free, pure C# library designed for fast and reliable PDF generation.

![TerraPDF](https://raw.githubusercontent.com/sahebansari/TerraPDF/master/logo.png)

[![NuGet](https://img.shields.io/nuget/v/TerraPDF.svg)](https://www.nuget.org/packages/TerraPDF)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

📚 **To know more about it and documentation and sample codes. Please visit its website** 
[https://terrapdf.com/](https://terrapdf.com/)


> **New in 1.4.0:** 
[AES-256 encryption by default](https://github.com/sahebansari/TerraPDF/blob/master/docs/encryption.md), plus bytes/stream image sources, anchor-based bookmarks, and the immutable `TextStyle` callback for multi-span text.

**TerraPDF** is a lightweight, zero-dependency, pure C# library for generating professional PDF 1.7 documents programmatically. 
It provides a fluent, composable API that covers the full document-authoring lifecycle — from page layout and 
rich text to tables, images, hyperlinks, and multi-page pagination — with no native binaries, no third-party 
runtime packages, and no licensing restrictions.

- No native dependencies, no third-party packages
- Targets **.NET 8** and **.NET 9**
- Text styling — bold, italic, bold-italic, strikethrough, underline, font size, colour
- Three built-in font families — Helvetica, Times, Courier — via `FontFamily()`, no embedding needed
- Configurable line-height multiplier per text block
- Per-span formatting inside mixed-style text blocks
- Margin and padding decorators with full unit support
- Background fills and borders (full, rounded, and per-edge)
- Rounded-corner borders and filled rounded boxes
- Per-edge borders — `BorderTop`, `BorderBottom`, `BorderLeft`, `BorderRight`
- Horizontal and vertical alignment
- Column, Row, and Table layouts
- PNG and JPEG image embedding
- Horizontal and vertical rule lines
 - Explicit page breaks via `PageBreak()`
 - Clickable hyperlink (URI) annotations via `Hyperlink()`
 - Internal document links (GoTo) via `InternalLink()`
 - Automatic Table of Contents generation from H1–H6 headings
 - PDF bookmarks / outlines with hierarchical nesting
 - Document metadata (Title, Author, Subject, Keywords, Creator)
 - Conditional rendering via `ShowIf`
 - Reusable components via `IComponent`
 - Headers, footers, and page numbers
 - **AES-256 PDF encryption by default** — user password, owner password, and fine-grained permission flags (`PdfPermissions`) via `container.Encrypt()`; AES-128 remains available for compatibility
- **Images from bytes and streams** — `Image(byte[])` / `Image(Stream)` with transparency and deduplication
- **Anchor-based bookmarks** — bookmark content directly to rendered elements and keep destinations accurate
 - Full **WinAnsiEncoding** character coverage
 - **Vector graphics canvas** — lines, rectangles, rounded rectangles, circles, ellipses, arbitrary Bézier paths, polygons, and grid helpers via `container.Canvas()`
 - Fluent, composable API

---

## Installation

```sh
dotnet add package TerraPDF
```

---

## Quick Start

```csharp
using TerraPDF.Core;
using TerraPDF.Helpers;

Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(2, Unit.Centimetre);
        page.PageColor(Color.White);
        page.DefaultTextStyle(s => s.FontSize(11));

        page.Header()
            .Text("My Report")
            .Bold()
            .FontSize(24)
            .FontColor(Color.Blue.Darken2);

        page.Content()
            .Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Hello, TerraPDF!");
                col.Item().Text("This paragraph is italic.").Italic();
                col.Item()
                   .Margin(6).Background(Color.Grey.Lighten4).Padding(10)
                   .Text("Indented callout box").Bold();
            });

        page.Footer()
            .AlignCenter()
            .Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber().FontSize(9);
                t.Span(" / ");
                t.TotalPages().FontSize(9);
            });
    });
})
.PublishPdf("output.pdf");
```

---

## Full Documentation

For complete API reference and detailed guides, visit the [docs](https://github.com/sahebansari/TerraPDF/tree/master/docs/) directory:

- **[Getting Started](https://github.com/sahebansari/TerraPDF/blob/master/docs/getting-started.md)** — Installation, Quick Start, and basic usage
- **[Text & Spans](https://github.com/sahebansari/TerraPDF/blob/master/docs/text-and-spans.md)** — Single-span and multi-span text, styling, page numbers
- **[Layout](https://github.com/sahebansari/TerraPDF/blob/master/docs/layout.md)** — Column, Row, and Table layouts with alignment and spacing
- **[Row and Column Layout](https://github.com/sahebansari/TerraPDF/blob/master/docs/row-and-column-layout.md)** — Detailed Row and Column layout examples
- **[Decorators](https://github.com/sahebansari/TerraPDF/blob/master/docs/decorators.md)** — Padding, margin, backgrounds, borders, and styling
- **[Images](https://github.com/sahebansari/TerraPDF/blob/master/docs/images.md)** — PNG and JPEG embedding with sizing and alignment
- **[Page Sizes & Units](https://github.com/sahebansari/TerraPDF/blob/master/docs/page-sizes-and-units.md)** — Built-in page sizes and unit conversions
- **[Colors](https://github.com/sahebansari/TerraPDF/blob/master/docs/colors.md)** — Material Design color palette with shades
- **[Encryption & Security](https://github.com/sahebansari/TerraPDF/blob/master/docs/encryption.md)** — AES-256 by default, with AES-128 compatibility mode and permission flags
- **[Vector Graphics](https://github.com/sahebansari/TerraPDF/blob/master/docs/vector-graphics.md)** — Canvas API, shapes, paths, grids, and charts
- **[Table of Contents](https://github.com/sahebansari/TerraPDF/blob/master/docs/table-of-contents.md)** — Automatic TOC generation from headings
- **[Bookmarks](https://github.com/sahebansari/TerraPDF/blob/master/docs/bookmarks.md)** — PDF bookmarks and outlines
- **[Components & Templates](https://github.com/sahebansari/TerraPDF/blob/master/docs/components-and-templates.md)** — Reusable components and document templates
- **[Metadata](https://github.com/sahebansari/TerraPDF/blob/master/docs/metadata.md)** — Document metadata (Title, Author, Subject, Keywords, Creator)
- **[Unicode & Character Encoding](https://github.com/sahebansari/TerraPDF/blob/master/docs/unicode-and-encoding.md)** — WinAnsiEncoding and character coverage
- **[Samples](https://github.com/sahebansari/TerraPDF/tree/master/samples)** — Complete working examples demonstrating all features

---



## Building from Source

```sh
git clone https://github.com/sahebansari/TerraPDF.git
cd TerraPDF
dotnet build
dotnet test
```

Requires **.NET 8 SDK** or later.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](https://github.com/sahebansari/TerraPDF/blob/master/CONTRIBUTING.md) for coding standards, project structure, how to run tests, and the pull-request process.

---

## Changelog

All notable changes are documented in [CHANGELOG.md](https://github.com/sahebansari/TerraPDF/blob/master/CHANGELOG.md), following the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

---

## Security

To report a vulnerability, please follow the responsible-disclosure process described in [SECURITY.md](https://github.com/sahebansari/TerraPDF/blob/master/SECURITY.md). **Do not open a public issue for security problems.**

---

## License

MIT — see [LICENSE](https://github.com/sahebansari/TerraPDF/blob/master/LICENSE) for details.
