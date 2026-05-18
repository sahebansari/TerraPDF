namespace TerraPDF.Sample.Samples;

using TerraPDF.Core;
using TerraPDF.Helpers;

internal static class VectorGraphicsShowcase
{
    internal static void Generate(string path)
    {
        // ── Palette ───────────────────────────────────────────────────────────────
        const string brand      = "#1A3C5E";   // deep navy
        const string brandLight = "#2E6DA4";   // medium blue
        const string accent     = "#E87722";   // vivid orange
        const string grid       = "#E0E6ED";   // light grid lines
        const string panelBg    = "#F4F7FA";   // page panel background
        const string muted      = "#7A8A99";   // subdued text
        const string white      = "#FFFFFF";

        // Chart data
        (string Label, double Value)[] barData =
        [
            ("Q1", 38), ("Q2", 52), ("Q3", 71), ("Q4", 64),
        ];
        double[] donutData   = [42, 28, 18, 12];   // percentages (sum = 100)
        string[] donutColors = [brandLight, accent, "#27AE60", "#9B59B6"];
        string[] donutLabels = ["Product A", "Product B", "Product C", "Product D"];
        double[] lineData    = [22, 35, 29, 48, 41, 60, 55, 73, 68, 82, 77, 90];

        // Helper: caption under a canvas
        void Caption(TerraPDF.Infra.IContainer container, string text) =>
            container.PaddingTop(4).AlignCenter()
                     .Text(text).FontSize(9).FontColor(muted).Italic();

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF – Vector Graphics Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Demonstrates every Canvas drawing primitive in TerraPDF");
            doc.MetadataKeywords("pdf; vector; canvas; charts; shapes; bezier");
            doc.MetadataCreator("TerraPDF Sample Generator v1.0");

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 1 — Primitives reference sheet
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                // ── Header ───────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Row(hdr =>
                       {
                           hdr.RelativeItem().AlignMiddle()
                              .Text("TerraPDF — Vector Graphics Showcase")
                              .Bold().FontSize(17).FontColor(white);
                           hdr.AutoItem().AlignRight().AlignMiddle()
                              .Text("Primitives Reference").FontSize(10).FontColor(grid);
                       });
                    col.Item().Background(accent).Canvas(3, _ => { });   // thin accent stripe
                });

                // ── Content ──────────────────────────────────────────────────────
                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(16);

                    // ── 1. Lines ─────────────────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "1  Lines"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(80, c =>
                       {
                           // Solid lines at different weights and colours
                           c.Line(0,  10, 200,  10, brand, 0.5);
                           c.Line(0,  25, 200,  25, brand, 1.5);
                           c.Line(0,  40, 200,  40, brandLight, 3);
                           c.Line(0,  55, 200,  55, accent, 2);
                           // Diagonal cross
                           c.Line(220,  0, 290, 80, Color.Grey.Darken1, 1);
                           c.Line(290,  0, 220, 80, Color.Grey.Darken1, 1);
                       });
                    Caption(col.Item(), "Lines: 0.5 pt, 1.5 pt, 3 pt weights + diagonal cross");

                    // ── 2. Rectangles ────────────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "2  Rectangles"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(70, c =>
                       {
                           // Filled
                           c.FillRect(0,   0, 80, 60, brandLight);
                           // Stroked
                           c.StrokeRect(100, 0, 80, 60, accent, 2);
                           // Filled + stroked
                           c.DrawRect(200,  0, 80, 60, Color.Indigo.Lighten5, brand, 1.5);
                           // Nested rectangles to show layering
                           c.FillRect(290,  5, 70, 50, Color.Grey.Lighten3);
                           c.FillRect(300, 15, 50, 30, Color.Grey.Lighten1);
                           c.FillRect(310, 22, 30, 16, Color.Grey.Medium);
                       });
                    Caption(col.Item(), "Rectangles: filled · stroked · filled+stroked · nested");

                    // ── 3. Rounded rectangles ────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "3  Rounded Rectangles"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(70, c =>
                       {
                           c.FillRoundedRect(0,   0, 90, 60,  6, brandLight);
                           c.StrokeRoundedRect(110, 0, 90, 60, 12, accent, 2);
                           c.DrawRoundedRect(220,  0, 90, 60, 20, Color.Indigo.Lighten5, brand, 1.5);
                           // Large radius (pill shape)
                           c.DrawRoundedRect(330,  15, 80, 30, 15, accent, white, 1);
                       });
                    Caption(col.Item(), "Rounded rects: r=6 (filled) · r=12 (stroked) · r=20 (filled+stroked) · pill");

                    // ── 4. Circles & Ellipses ────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "4  Circles & Ellipses"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(80, c =>
                       {
                           // Circles
                           c.FillCircle(40,  40, 36, brandLight);
                           c.StrokeCircle(120, 40, 36, accent, 2);
                           c.DrawCircle(200, 40, 36, Color.Indigo.Lighten5, brand, 1.5);
                           // Ellipses
                           c.FillEllipse(310, 40, 60, 30, Color.Green.Medium);
                           c.StrokeEllipse(410, 40, 30, 38, Color.Purple.Medium, 2);
                           // Concentric circles
                           c.FillCircle(490,  40, 38, Color.Blue.Lighten4);
                           c.FillCircle(490,  40, 26, Color.Blue.Lighten2);
                           c.FillCircle(490,  40, 14, Color.Blue.Medium);
                       });
                    Caption(col.Item(), "Circles: filled · stroked · filled+stroked  |  Ellipses: filled · stroked  |  Concentric");
                });

                // ── Footer ───────────────────────────────────────────────────────
                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5, grid);
                    f.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text("TerraPDF — Vector Graphics Showcase")
                           .FontSize(8).FontColor(muted);
                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(muted);
                            t.CurrentPageNumber().FontSize(8).FontColor(brand);
                            t.Span(" / ").FontSize(8).FontColor(muted);
                            t.TotalPages().FontSize(8).FontColor(brand);
                        });
                    });
                });
            });

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 2 — Paths & composition
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Row(hdr =>
                       {
                           hdr.RelativeItem().AlignMiddle()
                              .Text("TerraPDF — Vector Graphics Showcase")
                              .Bold().FontSize(17).FontColor(white);
                           hdr.AutoItem().AlignRight().AlignMiddle()
                              .Text("Paths & Composition").FontSize(10).FontColor(grid);
                       });
                    col.Item().Background(accent).Canvas(3, _ => { });
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(16);

                    // ── 5. Cubic Bézier paths ────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "5  Cubic Bézier Paths"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(100, c =>
                       {
                           // Smooth S-curve
                           c.Path(p => p
                               .MoveTo(0, 80)
                               .CurveTo(40, 80, 60, 0, 100, 0)
                               .Stroke(brandLight, 2));

                           // Closed petal / leaf shape
                           c.Path(p => p
                               .MoveTo(160, 50)
                               .CurveTo(160, 10, 220, 10, 220, 50)
                               .CurveTo(220, 90, 160, 90, 160, 50)
                               .Close()
                               .Fill(Color.Green.Lighten3)
                               .Stroke(Color.Green.Darken2, 1.5));

                           // Wave shape
                           c.Path(p => p
                               .MoveTo(250, 50)
                               .CurveTo(270, 10, 290, 10, 310, 50)
                               .CurveTo(330, 90, 350, 90, 370, 50)
                               .CurveTo(390, 10, 410, 10, 430, 50)
                               .Stroke(accent, 2.5));

                           // Drop / teardrop
                           c.Path(p => p
                               .MoveTo(490, 10)
                               .CurveTo(530, 10, 530, 70, 490, 90)
                               .CurveTo(450, 70, 450, 10, 490, 10)
                               .Close()
                               .Fill(Color.Blue.Lighten3)
                               .Stroke(brand, 1.5));
                       });
                    Caption(col.Item(), "Cubic Bézier: S-curve · leaf · wave · teardrop");

                    // ── 6. Polylines & Polygons ──────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "6  Polylines & Polygons"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(100, c =>
                       {
                           // Open polyline (zigzag)
                           c.Path(p => p
                               .Polyline((0, 90), (30, 10), (60, 70), (90, 10), (120, 50))
                               .Stroke(brandLight, 2));

                           // Triangle
                           c.Path(p => p
                               .Polygon((180, 90), (230, 10), (280, 90))
                               .Fill("#FFE0B2")
                               .Stroke(accent, 1.5));

                           // Pentagon
                           c.Path(p => p
                               .Polygon(
                                   (370, 10), (420, 46), (401, 90),
                                   (339, 90), (320, 46))
                               .Fill(Color.Indigo.Lighten5)
                               .Stroke(brand, 1.5));

                           // Star (two interlocked triangles)
                           c.Path(p => p
                               .Polygon((490, 10), (503, 46), (540, 46),
                                        (510, 68), (521, 100), (490, 80),
                                        (459, 100), (470, 68), (440, 46),
                                        (477, 46))
                               .Fill(accent)
                               .Stroke(Color.Orange.Darken2, 1));
                       });
                    Caption(col.Item(), "Polyline (zigzag) · Triangle · Pentagon · Star polygon");

                    // ── 7. Even-Odd fill rule (shapes with holes) ────────────────
                    col.Item().Component(new SectionHeader(brand, "7  Even-Odd Fill Rule — Shapes with Holes"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(100, c =>
                       {
                           // Donut via even-odd
                           c.Path(p => p
                               .Circle(55, 50, 48)
                               .Circle(55, 50, 28)
                               .Fill(brandLight)
                               .Stroke(brand, 1.5)
                               .UseEvenOddFill());

                           // Frame with inner cutout
                           c.Path(p => p
                               .Rect(140, 5, 90, 90)
                               .Rect(158, 22, 54, 56)
                               .Fill("#FFE0B2")
                               .Stroke(accent, 1)
                               .UseEvenOddFill());

                           // Nested squares with alternating fill
                           c.Path(p => p
                               .Rect(268, 5, 84, 84)
                               .Rect(282, 19, 56, 56)
                               .Rect(296, 33, 28, 28)
                               .Fill(Color.Green.Medium)
                               .UseEvenOddFill());

                           // Diamond ring
                           c.Path(p => p
                               .Polygon((440, 10), (490, 50), (440, 90), (390, 50))  // outer diamond
                               .Polygon((440, 28), (472, 50), (440, 72), (408, 50))  // inner diamond
                               .Fill(Color.Purple.Medium)
                               .UseEvenOddFill());
                       });
                    Caption(col.Item(), "Even-odd rule: donut · frame+cutout · nested squares · diamond ring");

                    // ── 8. Composition & layering ────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "8  Grid Helper & Composition"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(10)
                       .Canvas(120, c =>
                       {
                           // Background grid
                           c.FillRect(0, 0, 250, 120, panelBg);
                           c.Grid(20, null, grid, 0.5);

                           // Layered shapes on top of grid
                           c.FillRoundedRect(10, 10, 100, 100, 10, Color.Blue.Lighten4);
                           c.FillEllipse(60, 60, 42, 42, Color.Blue.Lighten2);
                           c.FillCircle(60, 60, 22, brandLight);
                           c.FillCircle(60, 60, 8, white);

                           // Intersecting shapes
                           c.FillCircle(175, 50, 40, "#FFE0B2");
                           c.FillCircle(215, 50, 40, Color.Green.Lighten4);
                           c.FillCircle(195, 80, 40, "#E1BEE7");
                           // Labels via lines
                           c.StrokeCircle(175, 50, 40, accent,              1);
                           c.StrokeCircle(215, 50, 40, Color.Green.Darken2, 1);
                           c.StrokeCircle(195, 80, 40, Color.Purple.Darken2, 1);
                       });
                    Caption(col.Item(), "Grid helper + layered shapes + overlapping translucent circles (Venn-style)");
                });

                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5, grid);
                    f.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text("TerraPDF — Vector Graphics Showcase")
                           .FontSize(8).FontColor(muted);
                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(muted);
                            t.CurrentPageNumber().FontSize(8).FontColor(brand);
                            t.Span(" / ").FontSize(8).FontColor(muted);
                            t.TotalPages().FontSize(8).FontColor(brand);
                        });
                    });
                });
            });

            // ══════════════════════════════════════════════════════════════════════
            //  PAGE 3 — Data-driven charts
            // ══════════════════════════════════════════════════════════════════════
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14)
                       .Row(hdr =>
                       {
                           hdr.RelativeItem().AlignMiddle()
                              .Text("TerraPDF — Vector Graphics Showcase")
                              .Bold().FontSize(17).FontColor(white);
                           hdr.AutoItem().AlignRight().AlignMiddle()
                              .Text("Data-Driven Charts").FontSize(10).FontColor(grid);
                       });
                    col.Item().Background(accent).Canvas(3, _ => { });
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(16);

                    // ── 9. Bar chart ─────────────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "9  Bar Chart — Quarterly Revenue ($M)"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(14)
                       .Canvas(160, c =>
                       {
                           const double chartW  = 420;
                           const double chartH  = 130;
                           const double originX = 40;
                           const double originY = 130;
                           const double maxVal  = 80;

                           // Background grid (horizontal lines)
                           for (int g = 0; g <= 4; g++)
                           {
                               double gy = originY - g * chartH / 4;
                               c.Line(originX, gy, originX + chartW, gy, grid, 0.5);
                           }

                           // Axes
                           c.Line(originX, 0,       originX,         originY, brand, 1);  // Y axis
                           c.Line(originX, originY, originX + chartW, originY, brand, 1); // X axis

                           // Bars
                           double barW   = chartW / (barData.Length * 2.0);
                           double barGap = barW * 0.6;
                           for (int i = 0; i < barData.Length; i++)
                           {
                               double bx = originX + i * (barW + barGap) * 2 + barGap;
                               double bh = barData[i].Value / maxVal * chartH;
                               double by = originY - bh;

                               // Bar shadow
                               c.FillRect(bx + 3, by + 3, barW, bh, Color.Grey.Lighten2);
                               // Bar fill with gradient effect (two rects)
                               c.FillRect(bx, by, barW,        bh, brandLight);
                               c.FillRect(bx, by, barW * 0.35, bh, brand);
                               // Bar top accent
                               c.FillRect(bx, by, barW, 3, accent);
                           }

                           // Value tick marks on Y axis
                           c.Line(originX - 4, originY - chartH * 0.25, originX, originY - chartH * 0.25, brand, 0.75);
                           c.Line(originX - 4, originY - chartH * 0.5,  originX, originY - chartH * 0.5,  brand, 0.75);
                           c.Line(originX - 4, originY - chartH * 0.75, originX, originY - chartH * 0.75, brand, 0.75);
                           c.Line(originX - 4, originY - chartH,        originX, originY - chartH,        brand, 0.75);
                       });
                    // Bar labels row
                    col.Item().PaddingLeft(54).Row(lrow =>
                    {
                        foreach (var (label, _) in barData)
                            lrow.RelativeItem().AlignCenter().Text(label).FontSize(9).FontColor(brand).Bold();
                    });
                    Caption(col.Item(), "Bar chart built entirely with Canvas primitives — no charting library");

                    // ── 10. Line chart ───────────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "10  Line Chart — Monthly Active Users (K)"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(14)
                       .Canvas(160, c =>
                       {
                           const double chartW  = 440;
                           const double chartH  = 130;
                           const double originX = 20;
                           const double originY = 130;
                           double maxV = lineData.Max();
                           int    n    = lineData.Length;

                           double StepX(int i) => originX + i * chartW / (n - 1);
                           double StepY(double v) => originY - v / maxV * chartH;

                           // Grid
                           for (int g = 1; g <= 4; g++)
                           {
                               double gy = originY - g * chartH / 4;
                               c.Line(originX, gy, originX + chartW, gy, grid, 0.5);
                           }

                           // Shaded area under the line (manual polygon via path)
                           c.Path(p =>
                           {
                               p.MoveTo(StepX(0), originY);
                               for (int i = 0; i < n; i++) p.LineTo(StepX(i), StepY(lineData[i]));
                               p.LineTo(StepX(n - 1), originY);
                               p.Close();
                               p.Fill(Color.Blue.Lighten5);
                           });

                           // Line
                           c.Path(p =>
                           {
                               p.MoveTo(StepX(0), StepY(lineData[0]));
                               for (int i = 1; i < n; i++)
                               {
                                   // Smoothed via cardinal-spline-style control points
                                   double x0 = StepX(i - 1), y0 = StepY(lineData[i - 1]);
                                   double x1 = StepX(i),     y1 = StepY(lineData[i]);
                                   double tension = (x1 - x0) * 0.4;
                                   p.CurveTo(x0 + tension, y0, x1 - tension, y1, x1, y1);
                               }
                               p.Stroke(brandLight, 2);
                           });

                           // Data point dots
                           for (int i = 0; i < n; i++)
                           {
                               c.FillCircle(StepX(i), StepY(lineData[i]), 4, white);
                               c.StrokeCircle(StepX(i), StepY(lineData[i]), 4, brandLight, 1.5);
                           }

                           // Axes
                           c.Line(originX, 0,       originX,         originY, brand, 1);
                           c.Line(originX, originY, originX + chartW, originY, brand, 1);
                       });
                    Caption(col.Item(), "Smoothed line chart with shaded area — cubic Bézier interpolation");

                    // ── 11. Donut chart ──────────────────────────────────────────
                    col.Item().Component(new SectionHeader(brand, "11  Donut Chart — Revenue Mix by Product"));
                    col.Item().Background(panelBg).Border(0.5, grid).Padding(14)
                       .Canvas(150, c =>
                       {
                           const double cx    = 90;
                           const double cy    = 75;
                           const double outer = 65;
                           const double inner = 35;
                           const double tau   = 2 * Math.PI;

                           double startAngle = -Math.PI / 2;   // start at 12 o'clock

                           for (int i = 0; i < donutData.Length; i++)
                           {
                               double sweep = donutData[i] / 100.0 * tau;
                               double end   = startAngle + sweep;

                               // Approximate arc with 8 cubic Bézier segments per slice
                               int    segs  = Math.Max(1, (int)Math.Ceiling(sweep / (Math.PI / 4)));
                               double step  = sweep / segs;
                               // Control-point length for circular arc approximation
                               double kArc  = 4.0 / 3.0 * Math.Tan(step / 4);

                               c.Path(p =>
                               {
                                   // Outer arc start
                                   p.MoveTo(cx + outer * Math.Cos(startAngle),
                                            cy + outer * Math.Sin(startAngle));
                                   // Outer arc segments
                                   double a = startAngle;
                                   for (int s = 0; s < segs; s++, a += step)
                                   {
                                       double ae   = a + step;
                                       double cp1x = cx + outer * (Math.Cos(a)  - kArc * Math.Sin(a));
                                       double cp1y = cy + outer * (Math.Sin(a)  + kArc * Math.Cos(a));
                                       double cp2x = cx + outer * (Math.Cos(ae) + kArc * Math.Sin(ae));
                                       double cp2y = cy + outer * (Math.Sin(ae) - kArc * Math.Cos(ae));
                                       p.CurveTo(cp1x, cp1y, cp2x, cp2y,
                                                 cx + outer * Math.Cos(ae),
                                                 cy + outer * Math.Sin(ae));
                                   }
                                   // Line to inner arc end point
                                   p.LineTo(cx + inner * Math.Cos(end),
                                            cy + inner * Math.Sin(end));
                                   // Inner arc segments (reverse direction)
                                   a = end;
                                   for (int s = 0; s < segs; s++, a -= step)
                                   {
                                       double ae   = a - step;
                                       double cp1x = cx + inner * (Math.Cos(a)  + kArc * Math.Sin(a));
                                       double cp1y = cy + inner * (Math.Sin(a)  - kArc * Math.Cos(a));
                                       double cp2x = cx + inner * (Math.Cos(ae) - kArc * Math.Sin(ae));
                                       double cp2y = cy + inner * (Math.Sin(ae) + kArc * Math.Cos(ae));
                                       p.CurveTo(cp1x, cp1y, cp2x, cp2y,
                                                 cx + inner * Math.Cos(ae),
                                                 cy + inner * Math.Sin(ae));
                                   }
                                   p.Close()
                                    .Fill(donutColors[i])
                                    .Stroke(white, 1.5);
                               });

                               startAngle = end;
                           }

                           // Centre label
                           c.FillCircle(cx, cy, inner - 4, white);
                           c.StrokeCircle(cx, cy, inner - 4, grid, 0.5);

                           // Legend boxes (right of donut)
                           for (int i = 0; i < donutLabels.Length; i++)
                           {
                               double ly = 20 + i * 28;
                               c.FillRoundedRect(185, ly, 16, 16, 3, donutColors[i]);
                               c.StrokeRoundedRect(185, ly, 16, 16, 3, white, 0.5);
                           }
                       });
                    // Legend text
                    col.Item().PaddingLeft(200).Column(legend =>
                    {
                        legend.Spacing(4);
                        for (int i = 0; i < donutLabels.Length; i++)
                        {
                            int idx = i;
                            legend.Item().Row(r =>
                            {
                                r.ConstantItem(20).Canvas(12, lc =>
                                    lc.FillRoundedRect(0, 0, 14, 12, 2, donutColors[idx]));
                                r.AutoItem().PaddingLeft(4)
                                 .Text($"{donutLabels[idx]}  {donutData[idx]:0}%")
                                 .FontSize(9).FontColor(brand);
                            });
                        }
                    });
                    Caption(col.Item(), "Donut chart — Bézier arc slices with inner hole via lineTo + reverse arc");
                });

                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5, grid);
                    f.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text("TerraPDF — Vector Graphics Showcase")
                           .FontSize(8).FontColor(muted);
                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(muted);
                            t.CurrentPageNumber().FontSize(8).FontColor(brand);
                            t.Span(" / ").FontSize(8).FontColor(muted);
                            t.TotalPages().FontSize(8).FontColor(brand);
                        });
                    });
                });
            });

        }).PublishPdf(path);

        Console.WriteLine($"  [10] Vector graphics showcase -> {path}");
    }
}

// ---------------------------------------------------------------------------
//  Reusable component: coloured section header strip
// ---------------------------------------------------------------------------
internal sealed class SectionHeader(string hexColor, string title) : TerraPDF.Infra.IComponent
{
    public void Compose(TerraPDF.Infra.IContainer container) =>
        container.Background(hexColor).PaddingVertical(5).PaddingHorizontal(10)
                 .Text(title).Bold().FontSize(10).FontColor(Color.White);
}
