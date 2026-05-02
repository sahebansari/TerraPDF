# TerraPDF

![TerraPDF Logo](logo.png)

[![NuGet](https://img.shields.io/nuget/v/TerraPDF.svg)](https://www.nuget.org/packages/TerraPDF)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

📚 **Documentation:** https://github.com/sahebansari/TerraPDF/tree/master/docs

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
- Conditional rendering via `ShowIf`
- Reusable components via `IComponent`
- Headers, footers, and page numbers
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

## API Overview

### Document entry points

| Method | Description |
|--------|-------------|
| `Document.Create(Action<IDocumentContainer>)` | Inline composition callback |
| `Document.Create(IDocument)` | Reusable `IDocument` class |

### Page configuration

```csharp
page.Size(PageSize.A4);
page.Margin(2, Unit.Centimetre);   // page margin
page.PageColor(Color.White);
page.DefaultTextStyle(s => s.FontSize(11));
page.Header()   // returns IContainer
page.Content()  // returns IContainer
page.Footer()   // returns IContainer
```

---

## Text

### Single-string text

```csharp
container.Text("Hello, world!")
    .Bold()
    .SemiBold()
    .Italic()
    .Strikethrough()
    .Underline()
    .FontSize(18)
    .LineHeight(1.5)  // line-height multiplier (default ≈ 1.4)
    .FontColor(Color.Blue.Darken2)
    .AlignLeft()      // default
    .AlignCenter()
    .AlignRight()
    .Justify();
```

### Multi-span text

Each `Span()`, `CurrentPageNumber()`, and `TotalPages()` returns a `SpanDescriptor`
so formatting applies **only to that span**, not to the whole block.

```csharp
container.Text(t =>
{
    t.Span("Normal  ");
    t.Span("Bold  ").Bold();
    t.Span("Italic  ").Italic();
    t.Span("Struck  ").Strikethrough();
    t.Span("Coloured  ").FontColor(Color.Red.Medium);
    t.Span("Large").FontSize(16).FontColor("#1a4a8a");
});
```

### Page numbers in footers

```csharp
container.Text(t =>
{
    t.Span("Page ").FontSize(9).FontColor(Color.Grey.Medium);
    t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
    t.Span(" / ").FontSize(9);
    t.TotalPages().FontSize(9);
});
```

---

## Layout

### Column — stacks children vertically

```csharp
container.Column(col =>
{
    col.Spacing(6);                       // gap between items in points
    col.AlignItemsLeft();                 // default
    col.AlignItemsCenter();
    col.AlignItemsRight();

    col.Item().Text("First");
    col.Item().Text("Second");
});
```

### Row — arranges children horizontally

```csharp
container.Row(row =>
{
    row.Spacing(8);                       // gap between items in points

    row.RelativeItem(2).Text("2x wide"); // proportional share of remaining space
    row.RelativeItem(1).Text("1x wide");
    row.AutoItem().Text("Auto width");   // natural content width
    row.ConstantItem(80).Text("80 pt");  // fixed width in points
});
```

### Table — grid with repeating header rows

```csharp
container.Table(table =>
{
    table.ColumnsDefinition(cols =>
    {
        cols.RelativeColumn(4);   // proportional
        cols.RelativeColumn(1);
        cols.ConstantColumn(60);  // fixed width in points
    });

    // HeaderRow repeats on every continuation page
    table.HeaderRow(row =>
    {
        row.Cell().Background("#1a4a8a").Padding(6).Text("Description").Bold().FontColor(Color.White);
        row.Cell().Background("#1a4a8a").Padding(6).Text("Qty").Bold().FontColor(Color.White);
        row.Cell().Background("#1a4a8a").Padding(6).AlignRight().Text("Amount").Bold().FontColor(Color.White);
    });

    table.Row(row =>
    {
        row.Cell().Padding(6).Text("Web Development");
        row.Cell().Padding(6).Text("1");
        row.Cell().Padding(6).AlignRight().Text("$2,500.00");
    });
});
```

---

## Decorators

Decorators are chainable and compose from the outside in:
`.Margin()` → `.Background()` → `.Padding()` → content

### Padding — inner spacing (inside background / border)

```csharp
container.Padding(10)                    // all sides
container.PaddingVertical(8)             // top + bottom
container.PaddingHorizontal(12)          // left + right
container.PaddingTop(4)
container.PaddingBottom(4)
container.PaddingLeft(6)
container.PaddingRight(6)

// All padding methods also accept a Unit:
container.Padding(0.5, Unit.Centimetre)
```

### Margin — outer spacing (outside background / border)

```csharp
container.Margin(10)                     // all sides
container.MarginVertical(8)              // top + bottom
container.MarginHorizontal(12)           // left + right
container.MarginTop(4)
container.MarginBottom(4)
container.MarginLeft(6)
container.MarginRight(6)

// All margin methods also accept a Unit:
container.Margin(0.5, Unit.Centimetre)
```

> **Padding vs Margin**
> `.Margin(10).Background("red").Padding(5).Text("Hi")`
> — the red background starts *after* the 10 pt margin gap;
> the text is inset 5 pt from the red edge.

### Background and Border

```csharp
container.Background("#1a4a8a")
container.Border(1.5, "#1a4a8a")        // line width + colour
container.Border(1)                      // black by default

// Rounded-corner border
container.RoundedBorder(radius: 8, lineWidth: 1, hexColor: "#1a4a8a")

// Filled rounded box (background + border)
container.RoundedBox(radius: 8, fillHexColor: "#E3F2FD", borderHexColor: "#1a4a8a")

// Per-edge borders
container.BorderTop(1.5, "#1a4a8a")
container.BorderBottom(1)
container.BorderLeft(3, Color.Red.Medium)
container.BorderRight(1, Color.Grey.Lighten2)
```

### Page Break

```csharp
// Force a new page at this position inside a Column
container.PageBreak();
```

### Hyperlink

```csharp
// Wrap any content in a clickable URI annotation
container.Hyperlink("https://example.com").Text("Click here");
container.Hyperlink("https://example.com").Image("logo.png", 120);
```

### Alignment

```csharp
// Horizontal
container.AlignLeft()
container.AlignCenter()
container.AlignRight()

// Vertical
container.AlignMiddle()
container.AlignBottom()
```

### Lines

```csharp
container.LineHorizontal(1.5, "#1a4a8a")   // horizontal rule
container.LineVertical(1)                   // vertical rule (black)
```

### Conditional rendering

```csharp
container.ShowIf(isAdmin).Text("Admin panel");
```

---

## Images

```csharp
// Fill available width, preserve aspect ratio (PNG or JPEG)
container.Image("path/to/image.png");
container.Image("path/to/photo.jpg");

// Fixed width in points, can be positioned with alignment
container.AlignCenter().Image("logo.png", 120);
```

---

## Reusable Components

```csharp
public class CalloutBox : IComponent
{
    private readonly string _text;
    public CalloutBox(string text) => _text = text;

    public void Compose(IContainer container) =>
        container
            .Margin(6).Background(Color.Blue.Lighten4).Padding(10)
            .Text(_text).Italic();
}

// Usage
container.Component(new CalloutBox("Note: this is important."));
```
---
## Reusable Document Template
```csharp
public class InvoiceDocument : IDocument
{
    private readonly InvoiceData _data;
    public InvoiceDocument(InvoiceData data) => _data = data;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.Content().Text($"Invoice #{_data.Number}").Bold().FontSize(24);
        });
    }
}
// Usage
Document.Create(new InvoiceDocument(data)).PublishPdf("invoice.pdf");
```
---
## Output Methods
```csharp
var composer = Document.Create(...);
// To file
composer.PublishPdf("output.pdf");
// To byte array (HTTP responses, email attachments, etc.)
byte[] bytes = composer.PublishPdf();
// To any writable stream
composer.PublishPdf(stream);
```

---

## Reusable Document Template

```csharp
public class InvoiceDocument : IDocument
{
    private readonly InvoiceData _data;
    public InvoiceDocument(InvoiceData data) => _data = data;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);
            page.Content().Text($"Invoice #{_data.Number}").Bold().FontSize(24);
        });
    }
}

// Usage
Document.Create(new InvoiceDocument(data)).PublishPdf("invoice.pdf");
```

---

## Output Methods

```csharp
var composer = Document.Create(...);

// To file
composer.PublishPdf("output.pdf");

// To byte array (HTTP responses, email attachments, etc.)
byte[] bytes = composer.PublishPdf();

// To any writable stream
composer.PublishPdf(stream);
```

---

## Built-in Page Sizes

| Constant | Width × Height (pt) |
|----------|---------------------|
| `PageSize.A0` | 2383.94 × 3370.39 |
| `PageSize.A1` | 1683.78 × 2383.94 |
| `PageSize.A2` | 1190.55 × 1683.78 |
| `PageSize.A3` | 841.89 × 1190.55 |
| `PageSize.A4` | 595.28 × 841.89 |
| `PageSize.A5` | 419.53 × 595.28 |
| `PageSize.A6` | 297.64 × 419.53 |
| `PageSize.Letter` | 612.00 × 792.00 |
| `PageSize.Legal` | 612.00 × 1008.00 |
| `PageSize.Tabloid` | 792.00 × 1224.00 |
| `PageSize.Executive` | 521.86 × 756.00 |

```csharp
// Landscape variant of any size
page.Size(PageSize.Landscape(PageSize.A4));
```

---

## Built-in Units

| Enum value | Description |
|------------|-------------|
| `Unit.Point` | PDF native unit (1 pt = 1/72 inch) — default |
| `Unit.Millimetre` | 1 mm ≈ 2.835 pt |
| `Unit.Centimetre` | 1 cm ≈ 28.35 pt |
| `Unit.Inch` | 1 in = 72 pt |

---

## Built-in Colours

```csharp
// Named shades (Material Design palette)
Color.Red.Darken2      Color.Red.Darken1
Color.Red.Medium       Color.Red.Lighten1  ...  Color.Red.Lighten4
Color.Blue.Darken2     Color.Blue.Medium   ...
Color.Green.Darken2    Color.Green.Medium  ...
Color.Grey.Darken2     Color.Grey.Medium   Color.Grey.Lighten1 ...
Color.Orange.Medium
Color.Purple.Medium
// Convenience constants
Color.White            // "#FFFFFF"
Color.Black            // "#000000"
```

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

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for coding standards, project structure, how to run tests, and the pull-request process.

---

## Changelog

All notable changes are documented in [CHANGELOG.md](CHANGELOG.md), following the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

---

## Security

To report a vulnerability, please follow the responsible-disclosure process described in [SECURITY.md](SECURITY.md). **Do not open a public issue for security problems.**

---

## License

MIT — see [LICENSE](LICENSE) for details.
