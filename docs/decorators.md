# Decorators

Decorators wrap a container slot and modify how its content is drawn.
They are chainable and compose from the **outside in**:

```
.Margin()  →  .Background()  →  .Border()  →  .Padding()  →  content
```

Each decorator method returns a new inner `IContainer` so the chain continues
into the decorated area.

---

## Padding

Adds space **inside** the element's box, between the background/border edge and
the content. The background and border cover the padded region.

```csharp
container.Padding(10)                         // all sides, in points
container.Padding(0.5, Unit.Centimetre)       // all sides, with unit
container.PaddingVertical(8)                  // top + bottom
container.PaddingVertical(4, Unit.Millimetre)
container.PaddingHorizontal(12)               // left + right
container.PaddingTop(4)
container.PaddingBottom(4)
container.PaddingLeft(6)
container.PaddingRight(6)
```

---

## Margin

Adds space **outside** the element's box, between the element and its surroundings.
The margin region is always transparent — background and border start after it.

```csharp
container.Margin(10)                          // all sides, in points
container.Margin(0.5, Unit.Centimetre)        // all sides, with unit
container.MarginVertical(8)                   // top + bottom
container.MarginVertical(4, Unit.Millimetre)
container.MarginHorizontal(12)               // left + right
container.MarginTop(4)
container.MarginBottom(4)
container.MarginLeft(6)
container.MarginRight(6)
```

### Padding vs Margin illustrated

```
┌─────────────────────────────────────────┐  ← outer slot
│           margin (transparent)          │
│   ┌─────────────────────────────────┐   │
│   │       background / border       │   │
│   │   ┌─────────────────────────┐   │   │
│   │   │        padding          │   │   │
│   │   │   ┌─────────────────┐   │   │   │
│   │   │   │    content      │   │   │   │
│   │   │   └─────────────────┘   │   │   │
│   │   └─────────────────────────┘   │   │
│   └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

```csharp
// The red box is inset by 10 pt; text is inset 5 pt inside the red edge.
container
    .Margin(10)
    .Background(Color.Red.Lighten4)
    .Padding(5)
    .Text("Inside the box");
```

---

## Background

Fills the element's area with a solid colour.

```csharp
container.Background(Color.Blue.Lighten5)
container.Background("#1a4a8a")
```

---

## Border

Draws a rectangular border around the element's area.

```csharp
container.Border(1.5, "#1a4a8a")   // line width in points + hex colour
container.Border(1)                 // black, 1 pt (default colour)
```

---

## Rounded Border

Draws a border with rounded corners. Optionally fills the interior to create a
"rounded box" in a single call.

```csharp
// Stroke-only rounded border
container.RoundedBorder()                                         // 8 pt radius, 1 pt black
container.RoundedBorder(radius: 12, lineWidth: 1.5, hexColor: Color.Blue.Darken2)

// Filled rounded box (background fill + rounded border in one call)
container.RoundedBox()                                            // white fill, 8 pt radius, 1 pt black border
container.RoundedBox(radius: 10, fillHexColor: Color.Blue.Lighten5, borderHexColor: Color.Blue.Darken2)
```

### Rounded card example

```csharp
col.Item()
   .Margin(6)
   .RoundedBox(radius: 10, fillHexColor: Color.Grey.Lighten5, borderHexColor: Color.Grey.Lighten2)
   .Padding(12)
   .Column(card =>
   {
       card.Item().Text("Card Title").Bold().FontSize(13);
       card.Item().PaddingTop(4).Text("Card body text.").FontColor(Color.Grey.Darken1);
   });
```

---

## Per-Edge Borders

Draw a border on individual sides only, each with its own width and colour.
Useful for table-cell separators or left-accent quote blocks.

```csharp
container.BorderTop(1.5, Color.Grey.Darken2)       // top only
container.BorderBottom(1)                           // bottom only, black
container.BorderLeft(3, Color.Blue.Darken2)         // left accent
container.BorderRight(0.5, "#cccccc")               // right only
```

All four methods accept an optional `hexColor` (defaults to `"#000000"`).

### Table column-separator pattern

```csharp
table.Row(row =>
{
    row.Cell().BorderBottom(1.5, Color.Grey.Darken2).Padding(6).Text("Column A").Bold();
    row.Cell().BorderBottom(1.5, Color.Grey.Darken2).Padding(6).Text("Column B").Bold();
});
```

### Left-accent callout block

```csharp
col.Item()
   .BorderLeft(4, Color.Blue.Darken2)
   .PaddingLeft(10).PaddingVertical(6)
   .Text("Note: this is important information.").Italic();
```

---

## Page Break

Forces the document engine to start a new PDF page at the current position.
A `PageBreak()` inside a `Column` is silently ignored when it falls at the very
bottom of one page (i.e. the next page would start anyway).

```csharp
container.Column(col =>
{
    col.Item().Text("Chapter 1").Bold().FontSize(18);
    col.Item().Text("Body text for chapter one...");

    col.Item().PageBreak();   // ← forces new page here

    col.Item().Text("Chapter 2").Bold().FontSize(18);
    col.Item().Text("Body text for chapter two...");
});
```

---

## Hyperlink

Wraps any child content in a clickable PDF URI annotation.
Clicking the rendered area in a conforming PDF viewer opens the URL in a browser.

```csharp
// Wrap a text span
container.Hyperlink("https://example.com").Text("Visit Example");

// Wrap styled text
container.Hyperlink("https://example.com")
         .Text("Click here").Underline().FontColor(Color.Blue.Medium);

// Wrap an image (clickable logo)
container.AlignCenter()
         .Hyperlink("https://example.com")
         .Image("logo.png", 120);
```

Hyperlink participates in the full decorator chain — chain it after alignment
and margin decorators and before the content:

```csharp
col.Item()
   .Margin(4)
   .Hyperlink("https://docs.example.com")
   .Text("Read the documentation").Underline().FontColor(Color.Blue.Darken2);
```

---

### Internal Link

Creates a clickable internal link (GoTo action) that navigates to a specific page
within the same PDF document. This is the mechanism used by the automatic Table
of Contents feature to make entries clickable.

```csharp
// Jump to page 5, at the default vertical position (top of page)
container.InternalLink(5).Text("See Chapter 5");

// Jump to page 3 at a specific Y coordinate (e.g. 150 pt from top)
container.InternalLink(3, 150).Text("Back to top");
```

Multiple internal links can be combined with other decorators. The target page
must exist when the PDF is rendered; otherwise an `InvalidOperationException`
is thrown during saving.

---

### Horizontal alignment

Positions the child within the available width without changing available height.

```csharp
container.AlignLeft()      // default
container.AlignCenter()    // centres child horizontally
container.AlignRight()     // pushes child to the right edge
```

### Vertical alignment

Positions the child within the available height.

```csharp
container.AlignMiddle()    // centres child vertically
container.AlignBottom()    // pushes child to the bottom edge
```

### Combining horizontal and vertical

```csharp
container.AlignCenter().AlignMiddle().Text("Centred both axes");
```

---

## Lines

Rule lines that span the full available width or height of their container.

```csharp
// Horizontal rule
container.LineHorizontal()                          // 1 pt, black
container.LineHorizontal(2, Color.Blue.Darken2)     // 2 pt, coloured

// Vertical rule
container.LineVertical()                            // 1 pt, black
container.LineVertical(1.5, "#cccccc")
```

> **Tip:** Use a `ConstantItem` inside a `Row` as a thin vertical divider:
> ```csharp
> row.ConstantItem(1).Background(Color.Grey.Lighten2);
> ```

---

## ShowIf — Conditional Rendering

Renders child content only when a condition is `true`. When `false` the slot is
replaced with a zero-size no-op, so surrounding layout is unaffected.

```csharp
container.ShowIf(isAdmin).Text("Admin panel");
container.ShowIf(invoice.IsPaid).Background(Color.Green.Lighten4).Padding(6).Text("PAID");
```

---

## Chaining Examples

### Card with coloured header

```csharp
col.Item()
   .Margin(6)
   .Border(1, Color.Grey.Lighten2)
   .Column(card =>
   {
       card.Item()
           .Background(Color.Blue.Darken2)
           .Padding(8)
           .Text("Card Title").Bold().FontColor(Color.White);

       card.Item()
           .Padding(10)
           .Text("Card body text goes here.");
   });
```

### Right-aligned badge

```csharp
row.AutoItem()
   .Margin(4)
   .Background(Color.Green.Lighten4)
   .Border(1, Color.Green.Darken1)
   .Padding(4)
   .AlignCenter()
   .Text("NEW").Bold().FontSize(9).FontColor(Color.Green.Darken2);
```
