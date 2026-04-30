using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

public sealed class RoundedBorderTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).GeneratePdf();

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    // ── RoundedBorder (stroke only) ───────────────────────────────────────

    [Fact]
    public void RoundedBorderDefaultRadiusRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().RoundedBorder().Padding(12).Text("Default radius border");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void RoundedBorderCustomRadiusAndColorRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content()
             .RoundedBorder(radius: 16, lineWidth: 2, hexColor: Color.Blue.Darken2)
             .Padding(16)
             .Text("Custom rounded border");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void RoundedBorderLargeRadiusIsClamped()
    {
        // radius larger than half the box — must not crash
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content()
             .RoundedBorder(radius: 9999)
             .Padding(8)
             .Text("Clamped radius");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── RoundedBox (fill + stroke) ────────────────────────────────────────

    [Fact]
    public void RoundedBoxDefaultColorsRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().RoundedBox().Padding(12).Text("Filled rounded box");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void RoundedBoxCustomColorsRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content()
             .RoundedBox(radius: 12, fillHexColor: Color.Blue.Lighten5,
                         borderHexColor: Color.Blue.Darken2, lineWidth: 1.5)
             .Padding(16)
             .Text("Custom filled rounded box").FontColor(Color.Blue.Darken2);
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Chaining with other decorators ────────────────────────────────────

    [Fact]
    public void RoundedBorderWithMarginAndPaddingRenders()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content()
             .Margin(10)
             .RoundedBorder(radius: 10, lineWidth: 1, hexColor: Color.Grey.Darken1)
             .Padding(14)
             .Text("Card with rounded border");
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void MultipleRoundedBoxesInColumnRender()
    {
        byte[] bytes = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Spacing(12);

                col.Item()
                   .RoundedBox(radius: 8, fillHexColor: Color.Green.Lighten5,
                               borderHexColor: Color.Green.Darken2)
                   .Padding(10)
                   .Text("Success").Bold().FontColor(Color.Green.Darken2);

                col.Item()
                   .RoundedBox(radius: 8, fillHexColor: Color.Red.Lighten5,
                               borderHexColor: Color.Red.Darken2)
                   .Padding(10)
                   .Text("Error").Bold().FontColor(Color.Red.Darken2);

                col.Item()
                   .RoundedBox(radius: 8, fillHexColor: Color.Amber.Medium,
                               borderHexColor: Color.Orange.Darken2)
                   .Padding(10)
                   .Text("Warning").Bold();
            });
        }));

        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public void RoundedBorderZeroRadiusThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().RoundedBorder(radius: 0).Text("x");
            })));

    [Fact]
    public void RoundedBorderNegativeLineWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().RoundedBorder(lineWidth: -1).Text("x");
            })));

    [Fact]
    public void RoundedBorderNullColorThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().RoundedBorder(hexColor: null!).Text("x");
            })));

    [Fact]
    public void RoundedBoxNullFillColorThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().RoundedBox(fillHexColor: null!).Text("x");
            })));
}
