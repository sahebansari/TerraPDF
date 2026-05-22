# TerraPDF 

A free, pure C# library designed for fast and reliable PDF generation.

![TerraPDF](https://raw.githubusercontent.com/sahebansari/TerraPDF/master/logo.png)

[![NuGet](https://img.shields.io/nuget/v/TerraPDF.svg)](https://www.nuget.org/packages/TerraPDF)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

📚 **To know more about it and documentation and sample codes. Please visit its website** 
[https://terrapdf.com/](https://terrapdf.com/)


> **New in 1.3.0:** 
[Encryption & Password Protection](https://github.com/sahebansari/TerraPDF/blob/master/docs/encryption.md) guide — AES-128 encryption, user and owner passwords, fine-grained permission flags, and secure document protection.

**TerraPDF** is a lightweight, zero-dependency, pure C# library for generating professional PDF 1.7 documents programmatically. 
It provides a fluent, composable API that covers the full document-authoring lifecycle — from page layout and 
rich text to tables, images, hyperlinks, and multi-page pagination — with no native binaries, no third-party 
runtime packages, and no licensing restrictions.

- No native dependencies, no third-party packages
- Targets **.NET 8** and **.NET 9**
- Text styling — bold, italic, bold-italic, strikethrough, underline, font size, colour
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
 - **AES-128 PDF encryption** — user password, owner password, and fine-grained permission flags (`PdfPermissions`) via `container.Encrypt()`
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

- **[Text & Typography](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Single-span and multi-span text, styling, page numbers
- **[Layout Containers](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Column, Row, and Table layouts with alignment and spacing
- **[Page Configuration](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Page sizes, margins, headers, footers, and page colors
- **[Decorators](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Padding, margin, backgrounds, borders, and styling
- **[Hyperlinks & Navigation](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — URI links, internal links, bookmarks, Table of Contents
- **[Images](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — PNG and JPEG embedding with sizing and alignment
- **[Headings](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — H1–H6 section headings with customizable styles
- **[Encryption & Security](https://github.com/sahebansari/TerraPDF/blob/master/docs/encryption.md)** — AES-128 protection with permission flags
- **[Vector Graphics](https://github.com/sahebansari/TerraPDF/blob/master/docs/vector-graphics.md)** — Canvas API, shapes, paths, grids, and charts
- **[Page Sizes & Units](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Built-in page sizes and unit conversions
- **[Colors](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Material Design color palette with shades
- **[Reusable Components](https://github.com/sahebansari/TerraPDF/tree/master/docs)** — Creating custom components and document templates
- **[Unicode & Character Encoding](https://github.com/sahebansari/TerraPDF/blob/master/docs/unicode-and-encoding.md)** — WinAnsiEncoding, character coverage
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
