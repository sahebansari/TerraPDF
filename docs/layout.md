# Layout

TerraPDF provides three layout elements: **Column**, **Row**, and **Table**.
They are all accessed through extension methods on `IContainer`.

For a visual explanation of how Column and Row relate to each other see
[Row & Column Layout](row-and-column-layout.md).

---

## Column

`Column` stacks its children **vertically** (top → bottom).

```csharp
container.Column(col =>
{
    col.Spacing(8);          // vertical gap between items in points

    col.Item().Text("First paragraph");
    col.Item().Text("Second paragraph");
    col.Item().Background(Color.Grey.Lighten4).Padding(10).Text("Highlighted box");
});
```

### Column alignment

Each `Item()` can be aligned independently by calling an `AlignItems*` method
**before** the `Item()` call it should affect:

```csharp
col.AlignItemsLeft();           // default
col.Item().Text("Left");

col.AlignItemsCenter();
col.Item().Text("Centred");

col.AlignItemsRight();
col.Item().Text("Right");
```

### `ColumnDescriptor` API

| Method | Description |
|--------|-------------|
| `Spacing(double)` | Vertical gap between items in points |
| `AlignItemsLeft()` | Left-align subsequent items (default) |
| `AlignItemsCenter()` | Centre-align subsequent items |
| `AlignItemsRight()` | Right-align subsequent items |
| `Item()` | Adds an item slot; returns `IContainer` |

---

## Row

`Row` arranges its children **horizontally** (left → right).

```csharp
container.Row(row =>
{
    row.Spacing(6);   // horizontal gap between items in points

    row.RelativeItem(2).Text("Takes 2x the share");
    row.RelativeItem(1).Text("Takes 1x the share");
    row.AutoItem().Text("Natural content width");
    row.ConstantItem(80).Text("Always 80 pt wide");
});
```

### Item sizing

| Method | Width |
|--------|-------|
| `RelativeItem(weight = 1)` | Proportional share of remaining space after constant/auto items |
| `AutoItem()` | Measured natural content width |
| `ConstantItem(points)` | Fixed number of PDF points |

Width calculation order:
1. Subtract spacing from available width.
2. Measure all `ConstantItem` widths and `AutoItem` widths (content measurement pass).
3. Distribute remaining space among `RelativeItem` slots proportionally by weight.

### `RowDescriptor` API

| Method | Returns | Description |
|--------|---------|-------------|
| `Spacing(double)` | `RowDescriptor` | Horizontal gap in points |
| `RelativeItem(double weight = 1)` | `IContainer` | Proportional slot |
| `AutoItem()` | `IContainer` | Auto-sized slot |
| `ConstantItem(double pts)` | `IContainer` | Fixed-width slot |

---

## Table

`Table` renders a grid with fixed or proportional columns. Header rows are
automatically repeated on every continuation page when the table spans multiple pages.

```csharp
container.Table(table =>
{
    // 1. Define columns
    table.ColumnsDefinition(cols =>
    {
        cols.RelativeColumn(4);    // proportional
        cols.RelativeColumn(1);
        cols.ConstantColumn(70);   // fixed width in points
    });

    // 2. Header row (repeated on continuation pages)
    table.HeaderRow(row =>
    {
        row.Cell().Background("#1a4a8a").Padding(6)
           .Text("Description").Bold().FontColor(Color.White);
        row.Cell().Background("#1a4a8a").Padding(6).AlignCenter()
           .Text("Qty").Bold().FontColor(Color.White);
        row.Cell().Background("#1a4a8a").Padding(6).AlignRight()
           .Text("Price").Bold().FontColor(Color.White);
    });

    // 3. Data rows
    bool shade = false;
    foreach (var item in lineItems)
    {
        string bg = shade ? Color.Grey.Lighten4 : Color.White;
        table.Row(row =>
        {
            row.Cell().Background(bg).Padding(6).Text(item.Name);
            row.Cell().Background(bg).Padding(6).AlignCenter().Text(item.Qty.ToString());
            row.Cell().Background(bg).Padding(6).AlignRight().Text($"${item.Price:N2}");
        });
        shade = !shade;
    }
});
```

### Column definitions

| Method | Description |
|--------|-------------|
| `RelativeColumn(weight = 1)` | Proportional share of available width |
| `ConstantColumn(points)` | Fixed width in PDF points |

### `TableDescriptor` API

| Method | Description |
|--------|-------------|
| `ColumnsDefinition(Action<ColumnsDefinitionDescriptor>)` | Define all columns |
| `HeaderRow(Action<TableRowDescriptor>)` | Add a header row (repeats on new pages) |
| `Row(Action<TableRowDescriptor>)` | Add a data row |

### `TableRowDescriptor` API

| Method | Returns | Description |
|--------|---------|-------------|
| `Cell()` | `IContainer` | Next cell slot (left to right); supports all decorators |

> **Tip:** Cells support the full decorator chain:
> `row.Cell().Background(bg).Padding(6).AlignRight().Text("value")`

---

## Nesting Layouts

`Column`, `Row`, and `Table` nest freely inside each other.

### Two-column page layout

```csharp
container.Row(row =>
{
    row.RelativeItem().Column(left =>
    {
        left.Spacing(6);
        left.Item().Text("Left heading").Bold();
        left.Item().Text("Left body text.");
    });

    row.ConstantItem(1).Background(Color.Grey.Lighten2);  // divider

    row.RelativeItem().PaddingLeft(12).Column(right =>
    {
        right.Spacing(6);
        right.Item().Text("Right heading").Bold();
        right.Item().Text("Right body text.");
    });
});
```

### Header / body / footer page structure

```csharp
container.Column(page =>
{
    page.Spacing(10);

    // Header row
    page.Item().Row(header =>
    {
        header.RelativeItem().Text("Logo").Bold();
        header.AutoItem().Text("Page 1").FontColor(Color.Grey.Medium);
    });

    // Body
    page.Item().Text("Main content goes here.");

    // Footer row
    page.Item().Row(footer =>
    {
        footer.RelativeItem().Text("Company Name");
        footer.AutoItem().Text("Confidential").Italic();
    });
});
```
