using TerraPDF.Barcodes;
using TerraPDF.Barcodes.QrCode;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders a QR code as vector-filled rectangles, one per contiguous run of
/// dark modules within a row (adjacent dark modules are merged into a single
/// wider rect, keeping the content stream compact).
/// </summary>
internal sealed class QrCodeElement : Element
{
    private readonly QrCode    _qr;
    private readonly double?   _explicitSize;
    private readonly PdfColor  _fillColor;
    private readonly PdfColor  _backgroundColor;
    private readonly int       _quietZoneModules;

    internal QrCodeElement(string data, QrErrorCorrectionLevel level, double? size,
        PdfColor fillColor, PdfColor backgroundColor, int quietZoneModules)
    {
        _qr               = QrCodeGenerator.Generate(data, level);
        _explicitSize     = size;
        _fillColor        = fillColor;
        _backgroundColor  = backgroundColor;
        _quietZoneModules = quietZoneModules;
    }

    private int TotalModules => _qr.Size + 2 * _quietZoneModules;

    private double SideLength(double availableWidth, double availableHeight)
    {
        double side = _explicitSize ?? Math.Min(availableWidth, availableHeight);
        return side;
    }

    // -- Measure -------------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        double side = SideLength(w, h);
        return new ElementSize(side, side);
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        double side = SideLength(ctx.Width, ctx.Height);
        if (side <= 0) return;

        double moduleSize = side / TotalModules;
        double originX    = ctx.X + _quietZoneModules * moduleSize;
        double originY    = ctx.Y + _quietZoneModules * moduleSize;

        ctx.Page.AddFilledRect(ctx.X, ctx.Y, side, side, _backgroundColor);

        var rects = new List<(double, double, double, double)>();
        int n = _qr.Size;
        for (int row = 0; row < n; row++)
        {
            int col = 0;
            while (col < n)
            {
                if (!_qr.Modules[row, col]) { col++; continue; }
                int runStart = col;
                while (col < n && _qr.Modules[row, col]) col++;
                int runLength = col - runStart;
                rects.Add((
                    originX + runStart * moduleSize,
                    originY + row * moduleSize,
                    runLength * moduleSize,
                    moduleSize));
            }
        }
        ctx.Page.AddFilledRects(rects, _fillColor);
    }
}
