# Text & Spans

## Single-String Text

The simplest form — one string, block-level style:

```csharp
container.Text("Hello, world!");
```

Chain style methods on the returned `TextDescriptor` to format the whole block:

```csharp
container.Text("Section Heading")
    .Bold()
    .FontSize(16)
    .FontColor(Color.Blue.Darken2)
    .AlignCenter();
```

### All `TextDescriptor` style methods

| Method | Effect |
|--------|--------|
| `.Bold()` | Bold weight |
| `.SemiBold()` | Semi-bold (mapped to bold in the built-in font set) |
| `.Italic()` | Italic style |
| `.Strikethrough()` | Horizontal strikethrough line |
| `.Underline()` | Underline beneath the text |
| `.FontSize(double)` | Font size in PDF points |
| `.LineHeight(double)` | Line-height multiplier (e.g. `1.0` = tight, `1.4` = default, `2.0` = double-spaced) |
| `.FontColor(string)` | Hex colour, e.g. `"#1a4a8a"` or `Color.Red.Medium` |
| `.AlignLeft()` | Left-align (default) |
| `.AlignCenter()` | Centre-align |
| `.AlignRight()` | Right-align |
| `.Justify()` | Justify all lines except the last |

---

## Multi-Span Text

Use the `Action<TextDescriptor>` overload to compose a text block from multiple
independently styled spans.

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

> **Important:** `t.Span(...)` returns a `SpanDescriptor`, not a `TextDescriptor`.
> Style methods chained after `.Span()` apply **only to that span**. This is intentional —
> it prevents accidental formatting of the whole block.

### `SpanDescriptor` methods

| Method | Effect |
|--------|--------|
| `.Bold()` | Bold weight for this span |
| `.SemiBold()` | Semi-bold for this span |
| `.Italic()` | Italic for this span |
| `.Strikethrough()` | Strikethrough for this span |
| `.Underline()` | Underline for this span |
| `.FontSize(double)` | Font size for this span |
| `.FontColor(string)` | Text colour for this span |

---

## Page Numbers

`CurrentPageNumber()` and `TotalPages()` also return `SpanDescriptor` so they
can be individually styled:

```csharp
page.Footer().AlignCenter().Text(t =>
{
    t.Span("Page ").FontSize(9).FontColor(Color.Grey.Medium);
    t.CurrentPageNumber().FontSize(9).FontColor(Color.Grey.Medium);
    t.Span(" of ").FontSize(9).FontColor(Color.Grey.Medium);
    t.TotalPages().FontSize(9).FontColor(Color.Grey.Medium);
});
```

---

## Mixing Styles in One Block

Because the block's alignment is controlled at the `TextDescriptor` level, you can
combine per-span colour/size with a block-level alignment:

```csharp
container.Text(t =>
{
    t.Span("Status: ").Bold();
    t.Span("Approved").FontColor(Color.Green.Darken2).Bold();
    t.Span("  (June 2025)").FontColor(Color.Grey.Medium).FontSize(9);
})
.AlignRight();
```

---

## Underline

`.Underline()` draws a line beneath the text. It works on both the whole block
(`TextDescriptor`) and on individual spans (`SpanDescriptor`).

```csharp
// Whole block underlined
container.Text("Important notice").Underline().Bold();

// Only one span underlined in a mixed block
container.Text(t =>
{
    t.Span("Visit ");
    t.Span("TerraPDF").Underline().FontColor(Color.Blue.Medium);
    t.Span(" for more info.");
});

// Underline and strikethrough can be combined
container.Text("Deprecated").Underline().Strikethrough().FontColor(Color.Grey.Medium);
```

---

## Line Height

`.LineHeight(double)` sets a multiplier applied to the natural line height.
The default multiplier is approximately `1.4`.

```csharp
container.Text("Tight paragraph.").LineHeight(1.0);
container.Text("Normal paragraph.").LineHeight(1.4);
container.Text("Relaxed paragraph.").LineHeight(1.6);
container.Text("Double-spaced paragraph.").LineHeight(2.0);
```

Line height can also be set page-wide via `DefaultTextStyle`:

```csharp
page.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.5));
```

---

## Default Text Style

A page-wide default style is set on `PageDescriptor` and inherited by all text
unless explicitly overridden at the block or span level:

```csharp
page.DefaultTextStyle(s => s.FontSize(11).FontColor(Color.Grey.Darken2));
```

Style resolution order (highest wins):

```
Span style  >  Block style (TextDescriptor)  >  Page default style  >  Library default (12 pt, black)
```
