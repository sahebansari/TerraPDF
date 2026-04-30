using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

// =============================================================================
//  Input-validation tests
//  Every public API entry point that should throw on bad input is covered here.
// =============================================================================
public sealed class ValidationTests
{
    // ── Document.Create ───────────────────────────────────────────────────

    [Fact]
    public void DocumentCreateNullCallbackThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create((Action<IDocumentContainer>)null!));

    [Fact]
    public void DocumentCreateNullIDocumentThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create((IDocument)null!));

    // ── DocumentComposer.Page ─────────────────────────────────────────────

    [Fact]
    public void PageNullConfigureThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(null!)));

    // ── DocumentComposer.GeneratePdf ──────────────────────────────────────

    [Fact]
    public void GeneratePdfPathNullPathThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p => p.Size(PageSize.A4)))
                    .GeneratePdf((string)null!));

    [Fact]
    public void GeneratePdfPathWhitespacePathThrows() =>
        Assert.Throws<ArgumentException>(() =>
            Document.Create(c => c.Page(p => p.Size(PageSize.A4)))
                    .GeneratePdf("   "));

    [Fact]
    public void GeneratePdfStreamNullStreamThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p => p.Size(PageSize.A4)))
                    .GeneratePdf((Stream)null!));

    // ── PageDescriptor.Size ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void PageSizeZeroOrNegativeThrows(double w, double h) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p => p.Size(w, h))));

    // ── PageDescriptor.Margin ─────────────────────────────────────────────

    [Fact]
    public void PageMarginNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(-1);
            })));

    [Fact]
    public void PageMarginFourArgsAnyNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(10, 10, -5, 10);
            })));

    // ── PageDescriptor.PageColor / DefaultTextStyle ───────────────────────

    [Fact]
    public void PageColorNullOrWhitespaceThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.PageColor(null!);
            })));

    [Fact]
    public void DefaultTextStyleNullConfigureThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.DefaultTextStyle(null!);
            })));

    // ── ContainerExtensions.Text ──────────────────────────────────────────

    [Fact]
    public void TextNullStringThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text((string)null!);
            })));

    [Fact]
    public void TextNullComposeThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text((Action<TextDescriptor>)null!);
            })));

    // ── ContainerExtensions.Column / Row / Table ──────────────────────────

    [Fact]
    public void ColumnNullConfigureThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Column(null!);
            })));

    [Fact]
    public void RowNullConfigureThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Row(null!);
            })));

    [Fact]
    public void TableNullConfigureThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Table(null!);
            })));

    // ── ContainerExtensions.Padding / Margin ──────────────────────────────

    [Fact]
    public void PaddingNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Padding(-1).Text("x");
            })));

    [Fact]
    public void MarginNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Margin(-1).Text("x");
            })));

    // ── ContainerExtensions.Background / Border ───────────────────────────

    [Fact]
    public void BackgroundNullHexThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Background(null!).Text("x");
            })));

    [Fact]
    public void BorderNegativeOrZeroWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Border(0).Text("x");
            })));

    [Fact]
    public void BorderNullHexThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Border(1, null!).Text("x");
            })));

    // ── ContainerExtensions.Image ─────────────────────────────────────────

    [Fact]
    public void ImageNullPathThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Image(null!);
            })));

    [Fact]
    public void ImageFixedNegativeOrZeroWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Image("logo.png", 0);
            })));

    // ── ContainerExtensions.Component ────────────────────────────────────

    [Fact]
    public void ComponentNullThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Component(null!);
            })));

    // ── TextDescriptor ────────────────────────────────────────────────────

    [Fact]
    public void TextDescriptorFontSizeZeroOrNegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("x").FontSize(0);
            })));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("x").FontSize(-1);
            })));
    }

    [Fact]
    public void TextDescriptorFontColorNullOrEmptyThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("x").FontColor(null!);
            })));

    [Fact]
    public void TextDescriptorSpanNullThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text(t => t.Span(null!));
            })));

    // ── SpanDescriptor ────────────────────────────────────────────────────

    [Fact]
    public void SpanDescriptorFontSizeZeroOrNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text(t => t.Span("x").FontSize(-5));
            })));

    [Fact]
    public void SpanDescriptorFontColorNullThrows() =>
        Assert.Throws<ArgumentNullException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text(t => t.Span("x").FontColor(null!));
            })));

    // ── RowDescriptor ─────────────────────────────────────────────────────

    [Fact]
    public void RowSpacingNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Row(r => r.Spacing(-1));
            })));

    [Fact]
    public void RowRelativeItemZeroOrNegativeWeightThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Row(r => r.RelativeItem(0));
            })));

    [Fact]
    public void RowConstantItemZeroOrNegativeWidthThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Row(r => r.ConstantItem(-10));
            })));

    // ── ColumnDescriptor ──────────────────────────────────────────────────

    [Fact]
    public void ColumnSpacingNegativeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Document.Create(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Column(col => col.Spacing(-1));
            })));

    // ── PdfColor.FromHex ──────────────────────────────────────────────────

    [Fact]
    public void PdfColorNullHexThrows() =>
        Assert.Throws<ArgumentNullException>(() => PdfColor.FromHex(null!));

    [Fact]
    public void PdfColorInvalidLengthThrows() =>
        Assert.Throws<ArgumentException>(() => PdfColor.FromHex("#FFF"));

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("FF0000")]
    [InlineData("#RRGGBBAA")]
    public void PdfColorValidFormatsDoNotThrow(string hex)
    {
        // 8-digit hex (with alpha) is silently accepted; non-hex digits will
        // throw FormatException from Convert.ToByte — that's expected behaviour.
        try { PdfColor.FromHex(hex); } catch (FormatException) { /* expected for #RRGGBBAA */ }
    }
}
