# Getting Started with TerraPDF

## Installation

```sh
dotnet add package TerraPDF
```

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `TerraPDF.Core` | Fluent API entry points, descriptors, extension methods |
| `TerraPDF.Infra` | `IContainer`, `IDocument`, `IComponent` interfaces |
| `TerraPDF.Helpers` | `Color`, `PageSize`, `Unit`, `TextStyle` |

---

## Minimal Example

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

        page.Header().Text("My First PDF").Bold().FontSize(20);

        page.Content().Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Hello, TerraPDF!");
            col.Item().Text("A second paragraph.").Italic();
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    });
})
.PublishPdf("output.pdf");
```

---

## Document Entry Points

```csharp
// Inline callback
Document.Create(container => { ... }).PublishPdf("output.pdf");

// Reusable IDocument class
Document.Create(new MyReport(data)).PublishPdf("output.pdf");
```

---

## Page Configuration

Every page is configured through `PageDescriptor`:

```csharp
container.Page(page =>
{
    // Size
    page.Size(PageSize.A4);                        // standard size
    page.Size(PageSize.Landscape(PageSize.A4));    // landscape
    page.Size(210, 297, Unit.Millimetre);          // explicit dimensions

    // Margins
    page.Margin(2, Unit.Centimetre);               // all sides
    page.MarginVertical(1, Unit.Centimetre);       // top + bottom
    page.MarginHorizontal(1.5, Unit.Centimetre);   // left + right
    page.Margin(top: 72, right: 54, bottom: 72, left: 54); // individual (points)

    // Appearance
    page.PageColor(Color.White);
    page.DefaultTextStyle(s => s.FontSize(11).FontColor(Color.Grey.Darken2));

    // Layout slots
    page.Header()   // IContainer — drawn above content on every page
    page.Content()  // IContainer — main scrollable area
    page.Footer()   // IContainer — drawn below content on every page
});
```

---

## Output Methods

```csharp
var composer = Document.Create(...);

// Write to file
composer.PublishPdf("report.pdf");

// Return as byte array (API responses, email attachments)
byte[] bytes = composer.PublishPdf();

// Write to any stream
using var stream = new MemoryStream();
composer.PublishPdf(stream);
```

---

## Next Steps

- [Text & Spans](text-and-spans.md) — styling, underline, line-height, multi-span, page numbers
- [Layout](layout.md) — Column, Row, Table
- [Decorators](decorators.md) — Padding, Margin, Background, Border, Rounded Border, Per-Edge Borders, Alignment, Lines, PageBreak, Hyperlink, ShowIf
- [Images](images.md) — PNG and JPEG embedding
- [Bookmarks](bookmarks.md) — PDF outlines / hierarchical navigation tree
- [Colors](colors.md) — full built-in palette reference
- [Page Sizes & Units](page-sizes-and-units.md) — all standard sizes and unit conversions
- [Components & Templates](components-and-templates.md) — reusable `IComponent` and `IDocument`
- [Row & Column Layout](row-and-column-layout.md) — deep dive with diagrams
