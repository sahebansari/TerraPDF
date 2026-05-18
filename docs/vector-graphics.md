# Vector Graphics

TerraPDF provides a fluent **Canvas API** for drawing vector graphics directly inside
any layout container. You can render lines, rectangles, circles, ellipses, rounded
rectangles, arbitrary Bézier paths, polygons, and grids — all without any external
dependencies.

---

## Adding a canvas

Call `container.Canvas(height, draw)` anywhere a container slot is available. The canvas
occupies the full available width and the exact height you specify.

```csharp
container.Canvas(120, c =>
{
    c.FillRect(0, 0, 200, 80, Color.Blue.Lighten4);
    c.StrokeRect(0, 0, 200, 80, Color.Blue.Darken2, 1.5);
    c.Line(0, 40, 200, 40, Color.Blue.Medium, 0.5);
});
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `height` | `double` | Canvas height in PDF points (must be > 0) |
| `draw` | `Action<VectorCanvas>` | Callback that issues drawing commands |

### Coordinate system

All coordinates are in **PDF points** with a **top-left origin** (0, 0) at the
upper-left corner of the canvas element — consistent with TerraPDF's layout
coordinate system.

```
(0,0) ──────────────────────► x
  │
  │      canvas area
  │
  ▼
  y
```

---

## VectorCanvas primitives

Every method returns `this` so calls can be chained.

---

### Lines

```csharp
canvas.Line(x1, y1, x2, y2, hexColor = "#000000", lineWidth = 1);
```

Draws a straight line from `(x1, y1)` to `(x2, y2)`.

```csharp
c.Line(0, 20, 300, 20, "#CCCCCC", 0.5);   // thin grey rule
c.Line(0,  0, 150, 80, Color.Red.Medium, 2);
```

---

### Rectangles

Three variants give you fill-only, stroke-only, or both:

```csharp
// Filled rectangle
canvas.FillRect(x, y, width, height, hexColor = "#000000");

// Stroked (outline) rectangle
canvas.StrokeRect(x, y, width, height, hexColor = "#000000", lineWidth = 1);

// Filled + stroked rectangle
canvas.DrawRect(x, y, width, height,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1);
```

```csharp
c.FillRect  (  0, 0, 80, 50, Color.Blue.Lighten3);
c.StrokeRect(100, 0, 80, 50, Color.Blue.Darken2, 1.5);
c.DrawRect  (200, 0, 80, 50, Color.Blue.Lighten5, Color.Blue.Darken2, 1);
```

---

### Rounded rectangles

Identical variants to the rectangle API, but with a `radius` parameter for
the corner curve:

```csharp
canvas.FillRoundedRect  (x, y, w, h, radius, hexColor = "#000000");
canvas.StrokeRoundedRect(x, y, w, h, radius, hexColor = "#000000", lineWidth = 1);
canvas.DrawRoundedRect  (x, y, w, h, radius,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1);
```

```csharp
c.FillRoundedRect  (  0, 0, 90, 50,  6, "#2E6DA4");      // r=6 badge
c.StrokeRoundedRect(110, 0, 90, 50, 12, "#E87722", 2);   // r=12 outline
c.DrawRoundedRect  (220, 10, 90, 30, 15, "#FFF", "#1A3C5E", 1); // pill
```

---

### Circles

```csharp
canvas.FillCircle  (cx, cy, radius, hexColor = "#000000");
canvas.StrokeCircle(cx, cy, radius, hexColor = "#000000", lineWidth = 1);
canvas.DrawCircle  (cx, cy, radius,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1);
```

`(cx, cy)` is the centre of the circle.

```csharp
c.FillCircle  ( 40, 40, 30, Color.Blue.Medium);
c.StrokeCircle(120, 40, 30, Color.Orange.Medium, 2);
c.DrawCircle  (200, 40, 30, Color.Green.Lighten4, Color.Green.Darken2, 1.5);
```

---

### Ellipses

Same variants as circles, but with independent horizontal (`rx`) and vertical
(`ry`) radii:

```csharp
canvas.FillEllipse  (cx, cy, rx, ry, hexColor = "#000000");
canvas.StrokeEllipse(cx, cy, rx, ry, hexColor = "#000000", lineWidth = 1);
canvas.DrawEllipse  (cx, cy, rx, ry,
    fillHex = "#FFFFFF", strokeHex = "#000000", lineWidth = 1);
```

```csharp
c.FillEllipse(100, 40, 80, 30, Color.Purple.Lighten3);   // wide, flat ellipse
```

---

### Grid helper

Draws a full-canvas grid of evenly spaced vertical and horizontal lines:

```csharp
canvas.Grid(cellWidth, cellHeight = null, hexColor = "#CCCCCC", lineWidth = 0.5);
```

When `cellHeight` is `null`, square cells are used (`cellHeight = cellWidth`).

```csharp
c.Grid(20);                          // 20 × 20 pt square grid, light grey
c.Grid(30, 20, "#E0E0E0", 0.3);     // 30 × 20 pt rectangular grid
```

> **Note:** `Grid` reads the canvas's allocated width and height to fill the area,
> so it must be called inside the `Canvas(height, draw)` callback (not stored and
> called later).

---

## Arbitrary paths with `PathDescriptor`

`canvas.Path(p => ...)` gives you full control via a fluent `PathDescriptor`. Use it
for triangles, custom polygons, Bézier curves, compound shapes, and shapes with holes.

### Move and line commands

```csharp
canvas.Path(p => p
    .MoveTo(50, 10)     // lift pen, move to (50, 10)
    .LineTo(90, 80)     // line to (90, 80)
    .LineTo(10, 80)     // line to (10, 80)
    .Close()            // close subpath back to (50, 10)
    .Fill(Color.Blue.Lighten3)
    .Stroke(Color.Blue.Darken2, 1.5));
```

### Cubic Bézier curves

```csharp
p.CurveTo(cx1, cy1, cx2, cy2, x, y)
```

Draws a cubic Bézier curve from the current point to `(x, y)`, using `(cx1, cy1)`
and `(cx2, cy2)` as control points.

```csharp
canvas.Path(p => p
    .MoveTo(10, 60)
    .CurveTo(30, 10, 70, 10, 90, 60)   // smooth arch
    .Stroke("#1A3C5E", 2));
```

### Convenience shapes on PathDescriptor

These helpers append subpaths to the current descriptor:

| Method | Description |
|--------|-------------|
| `Rect(x, y, width, height)` | Rectangular subpath |
| `Ellipse(cx, cy, rx, ry)` | Ellipse subpath (cubic Bézier approximation) |
| `Circle(cx, cy, radius)` | Circle subpath |
| `Polyline((x,y)[] points)` | Open polyline through 2+ points |
| `Polygon((x,y)[] points)` | Closed polygon through 3+ points |

```csharp
// Star-of-David using two overlapping triangles
canvas.Path(p => p
    .Polygon((50,10), (90,80), (10,80))
    .Fill(Color.Blue.Lighten4)
    .Stroke(Color.Blue.Darken2, 1));

canvas.Path(p => p
    .Polygon((50,80), (10,10), (90,10))
    .Fill(Color.Blue.Lighten4)
    .Stroke(Color.Blue.Darken2, 1));
```

### Paint methods

| Method | Description |
|--------|-------------|
| `.Fill(hexColor)` | Fill the path with the given colour |
| `.Stroke(hexColor, lineWidth = 1)` | Stroke the path outline |
| `.UseEvenOddFill()` | Use even-odd rule (for shapes with holes, e.g. donuts) |

You can call both `.Fill()` and `.Stroke()` on the same path to fill and stroke it.

### Shapes with holes (even-odd fill)

```csharp
// Donut: outer circle + inner circle, even-odd fill creates the hole
canvas.Path(p => p
    .Circle(100, 60, 50)   // outer
    .Circle(100, 60, 25)   // inner (becomes a hole)
    .Fill(Color.Orange.Medium)
    .UseEvenOddFill());
```

---

## All `VectorCanvas` methods at a glance

| Method | Description |
|--------|-------------|
| `Line(x1,y1, x2,y2, color, lw)` | Straight line |
| `FillRect(x,y,w,h, color)` | Filled rectangle |
| `StrokeRect(x,y,w,h, color, lw)` | Stroked rectangle |
| `DrawRect(x,y,w,h, fill, stroke, lw)` | Filled + stroked rectangle |
| `FillRoundedRect(x,y,w,h, r, color)` | Filled rounded rectangle |
| `StrokeRoundedRect(x,y,w,h, r, color, lw)` | Stroked rounded rectangle |
| `DrawRoundedRect(x,y,w,h, r, fill, stroke, lw)` | Filled + stroked rounded rect |
| `FillCircle(cx,cy, r, color)` | Filled circle |
| `StrokeCircle(cx,cy, r, color, lw)` | Stroked circle |
| `DrawCircle(cx,cy, r, fill, stroke, lw)` | Filled + stroked circle |
| `FillEllipse(cx,cy, rx,ry, color)` | Filled ellipse |
| `StrokeEllipse(cx,cy, rx,ry, color, lw)` | Stroked ellipse |
| `DrawEllipse(cx,cy, rx,ry, fill, stroke, lw)` | Filled + stroked ellipse |
| `Path(Action<PathDescriptor>)` | Arbitrary path with full Bézier support |
| `Grid(cw, ch?, color, lw)` | Full-canvas rectangular grid |

---

## All `PathDescriptor` methods at a glance

| Method | Description |
|--------|-------------|
| `MoveTo(x, y)` | Move current point without drawing |
| `LineTo(x, y)` | Straight line to point |
| `CurveTo(cx1,cy1, cx2,cy2, x,y)` | Cubic Bézier curve |
| `Close()` | Close subpath to its start |
| `Rect(x,y,w,h)` | Append rectangular subpath |
| `Ellipse(cx,cy,rx,ry)` | Append ellipse subpath |
| `Circle(cx,cy,r)` | Append circle subpath |
| `Polyline(points[])` | Append open polyline (≥ 2 points) |
| `Polygon(points[])` | Append closed polygon (≥ 3 points) |
| `Fill(hexColor)` | Set fill paint |
| `Stroke(hexColor, lw)` | Set stroke paint and width |
| `UseEvenOddFill()` | Switch to even-odd fill rule |

---

## Practical examples

### Simple bar chart

```csharp
(string Label, double Value)[] data = [("Q1", 38), ("Q2", 52), ("Q3", 71), ("Q4", 64)];
double maxValue = 80;
double canvasHeight = 120;
double barWidth = 40;
double gap = 20;

container.Canvas(canvasHeight, c =>
{
    for (int i = 0; i < data.Length; i++)
    {
        double barH = data[i].Value / maxValue * (canvasHeight - 20);
        double x = i * (barWidth + gap);
        double y = canvasHeight - 20 - barH;
        c.FillRoundedRect(x, y, barWidth, barH, 3, Color.Blue.Medium);
    }
    // baseline
    c.Line(0, canvasHeight - 20, data.Length * (barWidth + gap), canvasHeight - 20,
           Color.Grey.Lighten2, 0.5);
});
```

### Donut chart segment

```csharp
// Draw a filled donut wedge using Path + UseEvenOddFill
container.Canvas(160, c =>
{
    // Full outer circle minus inner circle
    c.Path(p => p
        .Circle(80, 80, 60)   // outer radius
        .Circle(80, 80, 35)   // inner radius (hole)
        .Fill(Color.Blue.Lighten3)
        .UseEvenOddFill());

    c.Path(p => p
        .Circle(80, 80, 60)
        .Stroke(Color.White, 2));
});
```

### Sparkline

```csharp
double[] values = [22, 35, 29, 48, 41, 60, 55, 73];
double canvasH = 60;
double stepX = 40;

container.Canvas(canvasH, c =>
{
    // Shaded area under the line
    c.Path(p =>
    {
        p.MoveTo(0, canvasH);
        for (int i = 0; i < values.Length; i++)
            p.LineTo(i * stepX, canvasH - (values[i] / 80.0 * canvasH));
        p.LineTo((values.Length - 1) * stepX, canvasH);
        p.Close();
        p.Fill(Color.Blue.Lighten5);
    });

    // Line on top
    c.Path(p =>
    {
        p.MoveTo(0, canvasH - (values[0] / 80.0 * canvasH));
        for (int i = 1; i < values.Length; i++)
            p.LineTo(i * stepX, canvasH - (values[i] / 80.0 * canvasH));
        p.Stroke(Color.Blue.Darken2, 1.5);
    });
});
```

---

## Sample

The `10_VectorGraphicsShowcase.cs` sample in `samples/TerraPDF.Sample/Samples/`
generates a 4-page PDF demonstrating every primitive and three chart types:

```
Page 1  Primitives reference sheet (lines, rects, rounded rects, circles, ellipses)
Page 2  Arbitrary paths — triangles, polygons, Bézier curves, compound shapes
Page 3  Data visualisation — bar chart, donut chart, line/sparkline chart
Page 4  Design patterns — badges, progress bars, callout boxes, icon grid
```

Run it with:

```sh
cd samples/TerraPDF.Sample
dotnet run
# Output: 10_vector_graphics_showcase.pdf
```
