# Components & Templates

TerraPDF provides two interfaces for structuring and reusing document content:

| Interface | Scope | Purpose |
|-----------|-------|---------|
| `IComponent` | Container slot | Reusable content block injected anywhere in a layout |
| `IDocument` | Whole document | Reusable, self-contained document template |

Both live in the `TerraPDF.Infra` namespace.

---

## IComponent — Reusable Content Blocks

`IComponent` encapsulates a piece of content that can be composed into any
`IContainer` slot. Ideal for repeated UI elements like header cards, badges,
callout boxes, or address blocks.

### Interface

```csharp
namespace TerraPDF.Infra;

public interface IComponent
{
    void Compose(IContainer container);
}
```

### Example — Callout box

```csharp
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

public class CalloutBox : IComponent
{
    private readonly string _text;
    private readonly string _color;

    public CalloutBox(string text, string color = "#E3F2FD")
    {
        _text  = text;
        _color = color;
    }

    public void Compose(IContainer container) =>
        container
            .Margin(6)
            .Background(_color)
            .Border(1, Color.Blue.Lighten2)
            .Padding(10)
            .Text(_text).Italic().FontColor(Color.Blue.Darken2);
}
```

### Usage

```csharp
col.Item().Component(new CalloutBox("Note: prices exclude VAT."));
col.Item().Component(new CalloutBox("Warning: read before proceeding.", Color.Orange.Medium));
```

### Example — Address block

```csharp
public class AddressBlock : IComponent
{
    private readonly string _name;
    private readonly string[] _lines;

    public AddressBlock(string name, params string[] lines)
    {
        _name  = name;
        _lines = lines;
    }

    public void Compose(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(2);
            col.Item().Text(_name).Bold();
            foreach (var line in _lines)
                col.Item().Text(line).FontColor(Color.Grey.Darken1);
        });
    }
}

// Usage
row.RelativeItem().Component(new AddressBlock(
    "Acme Corp.",
    "88 Commerce Blvd, Floor 12",
    "New York, NY 10001",
    "billing@acme.example"
));
```

---

## IDocument — Reusable Document Templates

`IDocument` encapsulates an entire multi-page document. Use it to separate
document structure from data and to enable unit testing.

### Interface

```csharp
namespace TerraPDF.Infra;

public interface IDocument
{
    void Compose(IDocumentContainer container);
}
```

### Example — Invoice template

```csharp
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

public record InvoiceData(string Number, string ClientName, decimal Total);

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
            page.DefaultTextStyle(s => s.FontSize(11));

            page.Header().Column(col =>
            {
                col.Item()
                   .Background(Color.Blue.Darken2)
                   .Padding(12)
                   .Text($"INVOICE  #{_data.Number}")
                   .Bold().FontSize(18).FontColor(Color.White);
            });

            page.Content().Column(col =>
            {
                col.Spacing(10);
                col.Item().Text($"Bill To: {_data.ClientName}").Bold();
                col.Item().Text($"Total Due: ${_data.Total:N2}").FontSize(14);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontSize(9).FontColor(Color.Grey.Medium);
                t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
            });
        });
    }
}
```

### Generating the document

```csharp
var data = new InvoiceData("2025-042", "Acme Corp.", 14_250.00m);

// To file
Document.Create(new InvoiceDocument(data)).GeneratePdf("invoice.pdf");

// To byte array
byte[] pdf = Document.Create(new InvoiceDocument(data)).GeneratePdf();

// To stream
Document.Create(new InvoiceDocument(data)).GeneratePdf(responseStream);
```

---

## Combining IDocument with IComponent

Components can be used freely inside `IDocument.Compose`:

```csharp
public class ReportDocument : IDocument
{
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(2, Unit.Centimetre);

            page.Content().Column(col =>
            {
                col.Spacing(12);
                col.Item().Component(new CalloutBox("This report is confidential."));
                col.Item().Text("Report body...").Justify();
            });
        });
    }
}
```
