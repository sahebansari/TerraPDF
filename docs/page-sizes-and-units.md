# Page Sizes & Units

---

## Page Sizes

All constants live in `TerraPDF.Helpers.PageSize` and are expressed as
`(double Width, double Height)` tuples in **PDF points** (1 pt = 1/72 inch).

### ISO A-Series

| Constant | Width (pt) | Height (pt) | Approx. mm |
|----------|-----------|------------|------------|
| `PageSize.A0` | 2383.94 | 3370.39 | 841 × 1189 |
| `PageSize.A1` | 1683.78 | 2383.94 | 594 × 841 |
| `PageSize.A2` | 1190.55 | 1683.78 | 420 × 594 |
| `PageSize.A3` | 841.89 | 1190.55 | 297 × 420 |
| `PageSize.A4` | 595.28 | 841.89 | 210 × 297 |
| `PageSize.A5` | 419.53 | 595.28 | 148 × 210 |
| `PageSize.A6` | 297.64 | 419.53 | 105 × 148 |

### North American

| Constant | Width (pt) | Height (pt) | Approx. inches |
|----------|-----------|------------|----------------|
| `PageSize.Letter` | 612.00 | 792.00 | 8.5 × 11 |
| `PageSize.Legal` | 612.00 | 1008.00 | 8.5 × 14 |
| `PageSize.Tabloid` | 792.00 | 1224.00 | 11 × 17 |
| `PageSize.Executive` | 521.86 | 756.00 | 7.25 × 10.5 |

### Landscape Variant

Use `PageSize.Landscape()` to swap width and height for any size:

```csharp
page.Size(PageSize.Landscape(PageSize.A4));      // 841.89 × 595.28 pt
page.Size(PageSize.Landscape(PageSize.Letter));   // 792.00 × 612.00 pt
```

### Custom Size

Pass explicit dimensions with an optional unit:

```csharp
page.Size(148, 210, Unit.Millimetre);    // A5 in millimetres
page.Size(6, 4, Unit.Inch);             // 6 × 4 inch card
page.Size(300, 500);                    // raw points
```

---

## Units

All API methods that accept a measurement also accept an optional `Unit` parameter.
Without a unit the value is interpreted as **PDF points**.

| `Unit` value | Description | Conversion |
|--------------|-------------|------------|
| `Unit.Point` | PDF native unit (default) | 1 pt = 1/72 inch |
| `Unit.Millimetre` | Millimetres | 1 mm ≈ 2.835 pt |
| `Unit.Centimetre` | Centimetres | 1 cm ≈ 28.35 pt |
| `Unit.Inch` | Inches | 1 in = 72 pt |

### Methods that accept a Unit

```csharp
// Page margin
page.Margin(2, Unit.Centimetre);
page.MarginVertical(10, Unit.Millimetre);
page.MarginHorizontal(0.75, Unit.Inch);

// Container padding
container.Padding(0.5, Unit.Centimetre);
container.PaddingTop(5, Unit.Millimetre);

// Container margin
container.Margin(0.25, Unit.Inch);
container.MarginLeft(8, Unit.Millimetre);
```

### Manual conversion

Use `UnitConversion.ToPoints()` when you need to convert a value yourself:

```csharp
using TerraPDF.Helpers;

double pts = UnitConversion.ToPoints(2.5, Unit.Centimetre);  // ≈ 70.87 pt
```
