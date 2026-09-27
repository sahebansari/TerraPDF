# TerraPDF - Free C# PDF Library and PDF Generator for .NET

**TerraPDF is a free, open-source (MIT) C# PDF library and PDF generator for .NET**. Create invoices, reports, receipts, statements, labels, certificates, and other PDF documents from C# with a fluent, composable API. It is 100% managed C#, has zero runtime dependencies, and is free for personal **and commercial** use: no watermarks, page limits, or paid tiers.

![TerraPDF](https://raw.githubusercontent.com/sahebansari/TerraPDF/master/logo.png)

[![NuGet](https://img.shields.io/nuget/v/TerraPDF.svg)](https://www.nuget.org/packages/TerraPDF)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TerraPDF.svg)](https://www.nuget.org/packages/TerraPDF)
[![.NET 8, 9 and 10](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/sahebansari/TerraPDF/blob/master/LICENSE)

[Website](https://terrapdf.com/) | [Documentation](https://github.com/sahebansari/TerraPDF/tree/master/docs) | [Samples](https://github.com/sahebansari/TerraPDF/tree/master/samples)

> **Highlights:** Custom font embedding — `FontFamily.Register(...)` loads a TrueType font
(brand typefaces, or scripts beyond WinAnsiEncoding like Cyrillic and Greek) and uses it via the
same `TextStyle.FontFamily(...)` API as the built-in families — including automatic, pure-C#
Devanagari-aware rendering (conjunct ligatures, reph, and below-base 'ra' forms, no native
shaping engine) and automatic glyph subsetting, so only the glyphs a document actually uses are
embedded. See [Custom Fonts](https://github.com/sahebansari/TerraPDF/blob/master/docs/custom-fonts.md).
>
> Also: Code128 barcodes and QR codes (ISO/IEC 18004, versions 1-40, all four error correction levels) via `container.Barcode(...)` and `container.QrCode(...)` — rendered as vector-filled rectangles, no raster image pipeline, placeable anywhere a `Column`, `Row`, or `Table` cell can go. The vector canvas (`container.Canvas(...)`) supports positioned and clipped images, rotated text, arcs and pie sectors, dashed strokes on every shape and path, linear and radial gradient fills, clickable links and bookmarks, QR codes, and constant-alpha transparency. TerraPDF also multi-targets **.NET 10 (LTS)** alongside .NET 8 and 9.

TerraPDF is a lightweight, zero-dependency, pure C# PDF 1.7 writer for programmatic document generation.
Its fluent API covers the full document-authoring lifecycle: page layout, rich text, tables, images,
hyperlinks, bookmarks, encryption, and multi-page pagination. There are no native binaries or third-party
runtime packages.

## At a glance

- **Package:** [`TerraPDF`](https://www.nuget.org/packages/TerraPDF)
- **Targets:** .NET 8, .NET 9, and .NET 10
- **License:** MIT, including commercial use
- **Runtime:** 100% managed C# with zero runtime dependencies
- **Output:** PDF 1.7 documents generated directly from code

---

## Why TerraPDF?

- **Truly free** — MIT licensed, free for commercial use. No royalties, no watermarks, no page limits, no "community edition" restrictions, no revenue caps.
- **Pure C#, zero dependencies** — no native binaries (no `libgdiplus`, no Chromium, no wkhtmltopdf), no third-party NuGet packages. The entire PDF writer is managed code.
- **Cross-platform** — runs anywhere .NET runs: Windows, Linux, macOS, Docker containers, Azure Functions, AWS Lambda, ASP.NET Core web apps, console apps, and background services.
- **Modern .NET** — targets .NET 8 (LTS), .NET 9, and .NET 10 (LTS).
- **Code-first, not HTML-to-PDF** — documents are composed from typed C# layout primitives (`Column`, `Row`, `Table`), so output is deterministic and fast — no browser engine to install or babysit.
- **High throughput, low memory** — in a 2-CPU / 1 GB container, one-page invoices render at about 10,000 pages per second and a 19-page report at about 25,000. Layout results are reused within each render, and images are converted once per process. See [Benchmarks](https://github.com/sahebansari/TerraPDF/blob/master/docs/benchmarks.md).
- **Batteries included** — text styling, tables, images, hyperlinks, bookmarks, table of contents, headers/footers, page numbers, vector graphics, Code128 barcodes, QR codes, custom embedded fonts, and AES-256 encryption.

## Common use cases

- Generate **invoice PDFs** in C# / ASP.NET Core
- Export **reports** and **statements** to PDF from .NET applications
- Create **receipts**, **delivery notes**, and **order confirmations**
- Print **shipping labels** with Code128 barcodes and QR codes
- Produce **certificates**, **letters**, and **contracts** from templates
- Server-side PDF generation in **Docker**, **Azure**, and **AWS** — no native dependencies to install

## Features

- No native dependencies, no third-party packages
- Targets **.NET 8**, **.NET 9** and **.NET 10**
- Text styling — bold, italic, bold-italic, strikethrough, underline, font size, colour
- Three built-in font families — Helvetica, Times, Courier — via `FontFamily()`, no embedding needed
- Configurable line-height multiplier per text block
- Per-span formatting inside mixed-style text blocks
- Margin and padding decorators with full unit support
- Background fills and borders (full, rounded, and per-edge)
- Rounded-corner borders and filled rounded boxes
- Per-edge borders — `BorderTop`, `BorderBottom`, `BorderLeft`, `BorderRight`
- Horizontal and vertical alignment
- Column, Row, and Table layouts — with column- and row-spanning table cells
- PNG and JPEG image embedding
- Horizontal and vertical rule lines
- Explicit page breaks via `PageBreak()`
- Clickable hyperlink (URI) annotations via `Hyperlink()`
- Internal document links (GoTo) via `InternalLink()`
- Automatic Table of Contents generation from H1-H6 headings
- PDF bookmarks / outlines with hierarchical nesting
- Document metadata (Title, Author, Subject, Keywords, Creator)
- Conditional rendering via `ShowIf`
- Reusable components via `IComponent`
- Headers, footers, and page numbers
- **AES-256 PDF encryption by default** - user password, owner password, and fine-grained permission flags (`PdfPermissions`) via `container.Encrypt()`; AES-128 remains available for compatibility
- **Images from bytes and streams** — `Image(byte[])` / `Image(Stream)` with transparency and deduplication
- **Anchor-based bookmarks** — bookmark content directly to rendered elements and keep destinations accurate
- Full **WinAnsiEncoding** character coverage
- **Vector graphics canvas** - lines, dashed strokes, rectangles, rounded rectangles, circles, ellipses, arbitrary Bezier paths, arcs, pie sectors, rotated text, positioned images with fit/crop modes, and grid helpers via `container.Canvas()`
- **Gradient fills** - linear (any angle) and radial two-color gradients on canvas paths, written as native PDF shadings
- **Canvas links, bookmarks, and QR codes** - clickable URI and in-document links, outline entries, and vector QR codes placed at absolute canvas positions
- **Constant-alpha transparency** - translucent fills, strokes, and text on the vector canvas via `/ExtGState`, with `opacity` on every canvas primitive
- **Code128 barcodes** - `container.Barcode(...)`, with optional human-readable caption, custom colours, and quiet zone
- **QR codes** - `container.QrCode(...)`, full ISO/IEC 18004 generator (versions 1-40, error correction levels L/M/Q/H), rendered as vector rectangles
- **Custom font embedding with automatic subsetting** - `FontFamily.Register(...)` embeds a TrueType font (brand typefaces, Cyrillic, Greek, and beyond WinAnsiEncoding) used via the same `FontFamily()` API as the built-in families, embedding only the glyphs each document actually uses
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

## Using TerraPDF with AI agents

- **Coding assistants** (GitHub Copilot, Cursor, Claude Code, Codex): copy
  [`skills/terrapdf`](https://github.com/sahebansari/TerraPDF/tree/master/skills/terrapdf)
  into `.github/skills/` or `.claude/skills/`. It gives the assistant the
  verified API, the rules that prevent common compile errors, and compiling
  recipes.
- **Agents that create PDFs at runtime**: `dotnet add package TerraPDF.Agents`
  provides `create_pdf` tools, taking a validated JSON document, for the
  Microsoft Agent Framework, Semantic Kernel, and `IChatClient`.
- **Any MCP client** (Copilot, Cursor, Claude, LangChain): run
  `dnx TerraPDF.Mcp --yes`.

See [AI Agents](https://github.com/sahebansari/TerraPDF/blob/master/docs/ai-agents.md) for setup.

---

## Full Documentation

For complete API reference and detailed guides, visit the [docs](https://github.com/sahebansari/TerraPDF/tree/master/docs/) directory:

- **[Getting Started](https://github.com/sahebansari/TerraPDF/blob/master/docs/getting-started.md)** — Installation, Quick Start, and basic usage
- **[Text & Spans](https://github.com/sahebansari/TerraPDF/blob/master/docs/text-and-spans.md)** — Single-span and multi-span text, styling, page numbers
- **[Layout](https://github.com/sahebansari/TerraPDF/blob/master/docs/layout.md)** — Column, Row, and Table layouts with alignment, spacing, and cell spans
- **[Row and Column Layout](https://github.com/sahebansari/TerraPDF/blob/master/docs/row-and-column-layout.md)** — Detailed Row and Column layout examples
- **[Decorators](https://github.com/sahebansari/TerraPDF/blob/master/docs/decorators.md)** — Padding, margin, backgrounds, borders, and styling
- **[Images](https://github.com/sahebansari/TerraPDF/blob/master/docs/images.md)** — PNG and JPEG embedding, positioned canvas images, fit modes, clipping, sizing, and alignment
- **[Page Sizes & Units](https://github.com/sahebansari/TerraPDF/blob/master/docs/page-sizes-and-units.md)** — Built-in page sizes and unit conversions
- **[Colors](https://github.com/sahebansari/TerraPDF/blob/master/docs/colors.md)** — Material Design color palette with shades
- **[Encryption & Security](https://github.com/sahebansari/TerraPDF/blob/master/docs/encryption.md)** — AES-256 by default, with AES-128 compatibility mode and permission flags
- **[Custom Fonts](https://github.com/sahebansari/TerraPDF/blob/master/docs/custom-fonts.md)** — embed TrueType fonts for brand typefaces and full Unicode (Cyrillic, Greek, and beyond)
- **[Vector Graphics](https://github.com/sahebansari/TerraPDF/blob/master/docs/vector-graphics.md)** — Canvas API, images, rotated text, dashed strokes, arcs, sectors, gradients, links, QR codes, transparency, grids, and charts
- **[Table of Contents](https://github.com/sahebansari/TerraPDF/blob/master/docs/table-of-contents.md)** — Automatic TOC generation from headings
- **[Bookmarks](https://github.com/sahebansari/TerraPDF/blob/master/docs/bookmarks.md)** — PDF bookmarks and outlines
- **[Components & Templates](https://github.com/sahebansari/TerraPDF/blob/master/docs/components-and-templates.md)** — Reusable components and document templates
- **[Metadata](https://github.com/sahebansari/TerraPDF/blob/master/docs/metadata.md)** — Document metadata (Title, Author, Subject, Keywords, Creator)
- **[Unicode & Character Encoding](https://github.com/sahebansari/TerraPDF/blob/master/docs/unicode-and-encoding.md)** — WinAnsiEncoding and character coverage
- **[AI Agents](https://github.com/sahebansari/TerraPDF/blob/master/docs/ai-agents.md)** — Skill for coding assistants, agent tools, and MCP server
- **[Benchmarks](https://github.com/sahebansari/TerraPDF/blob/master/docs/benchmarks.md)** — Running the BenchmarkDotNet suite and the throughput harness, and reading the results
- **[Samples](https://github.com/sahebansari/TerraPDF/tree/master/samples)** — Complete working examples demonstrating all features

---

## FAQ

**Is TerraPDF really free for commercial use?**
Yes. TerraPDF is released under the [MIT license](https://github.com/sahebansari/TerraPDF/blob/master/LICENSE) — you can use it in closed-source and commercial products at no cost, with no revenue caps, royalties, watermarks, or feature-limited tiers.

**How does TerraPDF compare to iTextSharp, QuestPDF, or PDFsharp?**
iText (iTextSharp) is AGPL-licensed, which requires a commercial license for most closed-source use. QuestPDF's Community license is free only below a company-revenue threshold. PDFsharp is MIT like TerraPDF, but uses an imperative drawing model. TerraPDF offers a fluent, composable, code-first API under a plain MIT license with zero runtime dependencies.

**Does TerraPDF convert HTML to PDF?**
No. TerraPDF is a code-first PDF generator: you compose documents from C# layout primitives (`Column`, `Row`, `Table`, `Text`, `Image`). That means no headless browser, deterministic output, and much faster rendering — but if your source content is HTML, an HTML-to-PDF converter is a better fit.

**Does it run on Linux, macOS, and in Docker?**
Yes. TerraPDF is 100% managed C# with no native binaries, so it runs on any platform supported by .NET 8/9/10 — including Alpine-based Docker images, Azure Functions, and AWS Lambda — with nothing extra to install.

**Can it create password-protected PDFs?**
Yes. Documents can be encrypted with AES-256 (default) or AES-128, with user/owner passwords and fine-grained permission flags (printing, copying, editing, etc.).

---

## Building from Source

```sh
git clone https://github.com/sahebansari/TerraPDF.git
cd TerraPDF
dotnet build -c Release
./tests/TerraPDF.Tests/bin/Release/net10.0/TerraPDF.Tests
./tests/TerraPDF.Agents.Tests/bin/Release/net10.0/TerraPDF.Agents.Tests
```

Requires the **.NET 10 SDK** (builds all targets), plus the .NET 8 and .NET 9
runtimes to execute the full multi-framework test suite. The tests are xUnit v3
executables: run them directly (add `.exe` on Windows, and swap `net10.0` for
`net8.0` or `net9.0`), because `dotnet test` cannot drive them on the .NET 10 SDK.

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
