using TerraPDF.Barcodes;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders a Code128 (Subset B) barcode as vector-filled bars, with an
/// optional human-readable caption centred beneath it.
/// </summary>
internal sealed class BarcodeElement : Element
{
    private const double CaptionFontSize = 9;
    private const double CaptionGap      = 4;

    private readonly bool[]  _modules; // true = bar (dark), false = space (light)
    private readonly string  _text;
    private readonly double? _explicitWidth;
    private readonly double  _barHeight;
    private readonly PdfColor _fillColor;
    private readonly PdfColor _backgroundColor;
    private readonly bool     _showCaption;
    private readonly double   _quietZoneModules;

    internal BarcodeElement(string text, double? width, double barHeight,
        PdfColor fillColor, PdfColor backgroundColor, bool showCaption, double quietZoneModules)
    {
        _modules          = Code128Encoder.Encode(text);
        _text             = text;
        _explicitWidth    = width;
        _barHeight        = barHeight;
        _fillColor        = fillColor;
        _backgroundColor  = backgroundColor;
        _showCaption      = showCaption;
        _quietZoneModules = quietZoneModules;
    }

    // -- Sizing ------------------------------------------------------

    private double TotalModules => _modules.Length + 2 * _quietZoneModules;

    private double ModuleWidth(double availableWidth)
    {
        double targetWidth = _explicitWidth ?? availableWidth;
        return targetWidth / TotalModules;
    }

    private double CaptionHeight => _showCaption ? CaptionGap + CaptionFontSize : 0;

    // -- Measure -------------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        double width = _explicitWidth ?? w;
        return new ElementSize(width, _barHeight + CaptionHeight);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        if (ctx.Width <= 0 || _barHeight <= 0) return;

        double moduleW = ModuleWidth(ctx.Width);

        ctx.Page.AddFilledRect(ctx.X, ctx.Y, ctx.Width, _barHeight, _backgroundColor);

        var rects = new List<(double, double, double, double)>();
        double x = ctx.X + _quietZoneModules * moduleW;
        foreach (bool dark in _modules)
        {
            if (dark) rects.Add((x, ctx.Y, moduleW, _barHeight));
            x += moduleW;
        }
        ctx.Page.AddFilledRects(rects, _fillColor);

        if (_showCaption)
        {
            double textWidth = FontMetrics.MeasureWidth(_text, CaptionFontSize, PdfFontFamily.Helvetica, false, false);
            double textX = ctx.X + (ctx.Width - textWidth) / 2;
            double baselineY = ctx.Y + _barHeight + CaptionGap + CaptionFontSize;
            ctx.Page.BeginTextObject();
            ctx.Page.ShowTextAt(_text, textX, baselineY, CaptionFontSize, _fillColor);
            ctx.Page.EndTextObject();
        }
    }
}
