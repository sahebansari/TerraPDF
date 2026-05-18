using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

// =============================================================================
//  Vector-graphics (Canvas) tests
//  Every test verifies that the PDF bytes are structurally valid ("%PDF-" header).
//  Shape-level correctness is validated by exercising each code path without
//  throwing and confirming the output is a well-formed PDF document.
// =============================================================================
public sealed class VectorGraphicsTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    private static byte[] CanvasPage(double height, Action<VectorCanvas> draw) =>
        Build(doc => doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(1, Unit.Centimetre);
            page.Content().Canvas(height, draw);
        }));

    // ── Basic wiring ─────────────────────────────────────────────────────────

    [Fact]
    public void EmptyCanvasProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, _ => { });
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Line ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LineProducesValidPdf()
    {
        byte[] bytes = CanvasPage(50, c =>
            c.Line(0, 0, 100, 50, Color.Black, 1));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void LineDefaultColorProducesValidPdf()
    {
        byte[] bytes = CanvasPage(50, c => c.Line(10, 10, 90, 10));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Rectangle ────────────────────────────────────────────────────────────

    [Fact]
    public void FillRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.FillRect(10, 10, 60, 40, Color.Blue.Medium));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void StrokeRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.StrokeRect(10, 10, 60, 40, Color.Red.Medium, 2));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void DrawRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.DrawRect(10, 10, 60, 40, Color.Blue.Lighten5, Color.Blue.Darken2, 1.5));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Rounded rectangle ────────────────────────────────────────────────────

    [Fact]
    public void FillRoundedRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.FillRoundedRect(10, 10, 80, 50, 8, Color.Green.Medium));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void StrokeRoundedRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.StrokeRoundedRect(10, 10, 80, 50, 8, Color.Grey.Darken2, 1));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void DrawRoundedRectProducesValidPdf()
    {
        byte[] bytes = CanvasPage(80, c =>
            c.DrawRoundedRect(10, 10, 80, 50, 8, Color.Blue.Lighten5, Color.Blue.Darken2));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Circle ───────────────────────────────────────────────────────────────

    [Fact]
    public void FillCircleProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.FillCircle(50, 50, 40, Color.Red.Medium));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void StrokeCircleProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.StrokeCircle(50, 50, 40, Color.Blue.Medium, 2));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void DrawCircleProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.DrawCircle(50, 50, 40, Color.Blue.Lighten5, Color.Blue.Darken2, 1));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Ellipse ──────────────────────────────────────────────────────────────

    [Fact]
    public void FillEllipseProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.FillEllipse(80, 50, 70, 40, Color.Purple.Medium));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void StrokeEllipseProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.StrokeEllipse(80, 50, 70, 40, Color.Orange.Medium, 1.5));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void DrawEllipseProducesValidPdf()
    {
        byte[] bytes = CanvasPage(100, c =>
            c.DrawEllipse(80, 50, 70, 40, Color.Yellow.Medium, Color.Orange.Darken2));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Arbitrary path ───────────────────────────────────────────────────────

    [Fact]
    public void PathWithFillAndStrokeProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .MoveTo(10, 10)
            .LineTo(90, 10)
            .LineTo(50, 80)
            .Close()
            .Fill(Color.Blue.Lighten4)
            .Stroke(Color.Blue.Darken2, 1.5)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathFillOnlyProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .MoveTo(10, 10)
            .LineTo(100, 10)
            .LineTo(100, 80)
            .LineTo(10, 80)
            .Close()
            .Fill(Color.Green.Lighten4)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathStrokeOnlyProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .MoveTo(10, 60)
            .CurveTo(40, 10, 80, 10, 110, 60)
            .Stroke(Color.Red.Medium, 2)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathEvenOddFillProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .Rect(10, 10, 80, 80)
            .Rect(30, 30, 40, 40)
            .Fill(Color.Teal.Medium)
            .UseEvenOddFill()));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathWithNoCommandsProducesValidPdf()
    {
        byte[] bytes = CanvasPage(50, c => c.Path(_ => { }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── PathDescriptor convenience shapes ────────────────────────────────────

    [Fact]
    public void PathEllipseShortcutProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .Ellipse(60, 60, 50, 30)
            .Fill(Color.Indigo.Medium)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathCircleShortcutProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .Circle(60, 60, 40)
            .Stroke(Color.Black, 1)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathPolylineShortcutProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .Polyline((10, 100), (50, 10), (90, 100))
            .Stroke(Color.Red.Darken2, 2)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PathPolygonShortcutProducesValidPdf()
    {
        byte[] bytes = CanvasPage(120, c => c.Path(p => p
            .Polygon((10, 100), (60, 10), (110, 100))
            .Fill(Color.Green.Medium)
            .Stroke(Color.Green.Darken2, 1)));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Grid helper ──────────────────────────────────────────────────────────

    [Fact]
    public void GridProducesValidPdf()
    {
        byte[] bytes = CanvasPage(200, c =>
        {
            c.Grid(20, 20, Color.Grey.Lighten3, 0.5);
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void GridSquareCellsProducesValidPdf()
    {
        byte[] bytes = CanvasPage(200, c => c.Grid(25));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Composition ──────────────────────────────────────────────────────────

    [Fact]
    public void MultipleShapesOnCanvasProducesValidPdf()
    {
        byte[] bytes = CanvasPage(200, c =>
        {
            c.FillRect(0, 0, 200, 200, Color.Grey.Lighten5);
            c.Grid(20, null, Color.Grey.Lighten3);
            c.FillCircle(100, 100, 60, Color.Blue.Lighten4);
            c.StrokeCircle(100, 100, 60, Color.Blue.Darken2, 2);
            c.Line(40, 100, 160, 100, Color.Red.Medium, 1);
            c.Line(100, 40, 100, 160, Color.Red.Medium, 1);
            c.Path(p => p
                .Polygon((100, 30), (160, 150), (40, 150))
                .Stroke(Color.Orange.Darken2, 2));
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public void CanvasZeroHeightThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(doc => doc.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Canvas(0, _ => { });
            })));

    [Fact]
    public void LineZeroWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanvasPage(50, c => c.Line(0, 0, 50, 0, Color.Black, 0)));

    [Fact]
    public void FillRectNullColorThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            CanvasPage(50, c => c.FillRect(0, 0, 50, 50, null!)));

    [Fact]
    public void FillCircleNegativeRadiusThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanvasPage(50, c => c.FillCircle(25, 25, -5)));

    [Fact]
    public void PathPolylineTooFewPointsThrows() =>
        Assert.Throws<ArgumentException>(() =>
            CanvasPage(50, c => c.Path(p => p.Polyline((10, 10)))));

    [Fact]
    public void PathPolygonTooFewPointsThrows() =>
        Assert.Throws<ArgumentException>(() =>
            CanvasPage(50, c => c.Path(p => p.Polygon((10, 10), (50, 10)))));

    [Fact]
    public void CanvasInsideColumnProducesValidPdf()
    {
        byte[] bytes = Build(doc => doc.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(1, Unit.Centimetre);
            page.Content().Column(col =>
            {
                col.Item().Text("Chart title").Bold().FontSize(14);
                col.Item().Canvas(150, c =>
                {
                    c.FillRect(0, 0, 200, 150, Color.BlueGrey.Lighten5);
                    c.FillCircle(75, 75, 50, Color.Blue.Medium);
                    c.FillCircle(175, 75, 50, Color.Red.Medium);
                });
                col.Item().Text("Figure 1").Italic();
            });
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }
}
