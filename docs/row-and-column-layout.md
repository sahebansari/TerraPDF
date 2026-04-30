# Row & Column Layout in TerraPDF

## Core Concept

The name describes **how children are arranged**, not the shape of the container itself.

| Element | Direction | Axis |
|---------|-----------|------|
| `Column` | Top → Bottom | Vertical |
| `Row` | Left → Right | Horizontal |

---

## Column

A `Column` stacks its children **vertically**, one below the other — like a column of text in a newspaper.

```
┌──────────────────┐
│     Item 1       │
├──────────────────┤
│     Item 2       │
├──────────────────┤
│     Item 3       │
└──────────────────┘
```

### Fluent API

```csharp
container.Column(col =>
{
    col.Spacing(10); // vertical gap between items in points

    col.Item().Text("First line");
    col.Item().Text("Second line");
    col.Item().Text("Third line");
});
```

### What happens internally

- `Measure()` accumulates **height** for each item and tracks the **maximum width**.
- `Draw()` advances the cursor **downward** (`curY`) after each item.

---

## Row

A `Row` arranges its children **horizontally**, side by side — like cells in a table row.

```
┌──────────┬──────────┬──────────┐
│  Item 1  │  Item 2  │  Item 3  │
└──────────┴──────────┴──────────┘
```

### Fluent API

```csharp
container.Row(row =>
{
    row.Spacing(5); // horizontal gap between items in points

    row.AutoItem().Text("Auto-sized");        // takes its natural content width
    row.RelativeItem(2).Text("2x wide");      // takes 2x the share of remaining space
    row.RelativeItem(1).Text("1x wide");      // takes 1x the share of remaining space
    row.ConstantItem(80).Text("Fixed 80pt");  // always exactly 80 points wide
});
```

### Item sizing options

| Method | Behaviour |
|--------|-----------|
| `AutoItem()` | Width = natural content width (measured first) |
| `RelativeItem(weight)` | Width = proportional share of remaining space after auto/constant items |
| `ConstantItem(pts)` | Width = fixed number of PDF points, always |

### What happens internally

- `CalculateWidths()` resolves all item widths from available space.
- `Measure()` accumulates **widths** and tracks the **maximum height**.
- `Draw()` advances the cursor **to the right** (`curX`) after each item.

---

## Combining Row & Column

`Row` and `Column` are designed to be **nested** freely to build any layout.

### Example: Two-column page layout

```csharp
// Side-by-side columns, each containing stacked content
container.Row(row =>
{
    row.RelativeItem().Column(left =>
    {
        left.Spacing(8);
        left.Item().Text("Left heading");
        left.Item().Text("Left body text...");
    });

    row.ConstantItem(20); // spacer

    row.RelativeItem().Column(right =>
    {
        right.Spacing(8);
        right.Item().Text("Right heading");
        right.Item().Text("Right body text...");
    });
});
```

Result:

```
┌─────────────────────┬────┬─────────────────────┐
│  Left heading       │    │  Right heading       │
│  Left body text...  │    │  Right body text...  │
└─────────────────────┴────┴─────────────────────┘
```

### Example: Header + body + footer (Column wrapping Rows)

```csharp
container.Column(page =>
{
    page.Spacing(12);

    // Header row
    page.Item().Row(header =>
    {
        header.RelativeItem().Text("Logo");
        header.RelativeItem().Text("Title");
        header.AutoItem().Text("Page 1");
    });

    // Body content
    page.Item().Text("Main body paragraph text goes here...");

    // Footer row
    page.Item().Row(footer =>
    {
        footer.RelativeItem().Text("Company Name");
        footer.AutoItem().Text("Confidential");
    });
});
```

Result:

```
┌──────────────────────────────────────────┐
│  Logo          Title            Page 1   │  ← Row (header)
├──────────────────────────────────────────┤
│  Main body paragraph text goes here...   │  ← Column item
├──────────────────────────────────────────┤
│  Company Name                Confidential│  ← Row (footer)
└──────────────────────────────────────────┘
```

---

## Quick Reference

```
Column  =  vertical stacking   (think: stack of pancakes)
Row     =  horizontal stacking (think: seats in a cinema row)
```

> **Tip:** This is the same convention used by CSS Flexbox (`flex-direction: column` / `row`),
> Flutter (`Column` / `Row` widgets) — so the mental model transfers directly.
