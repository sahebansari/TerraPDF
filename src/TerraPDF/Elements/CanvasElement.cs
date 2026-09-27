using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>
/// Renders all drawing commands accumulated in a <see cref="VectorCanvas"/>
/// onto the PDF page.  The element occupies the full available width and uses
/// an explicit height supplied via <see cref="CanvasElement(VectorCanvas, double)"/>.
/// </summary>
internal sealed class CanvasElement : Element
{
    private readonly VectorCanvas _canvas;
    private readonly double _height;

    internal CanvasElement(VectorCanvas canvas, double height)
    {
        _canvas = canvas;
        _height = height;
    }

    // ── Measure ─────────────────────────────────────────────────────────────

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        // Inject allocated size so Grid() can use it during the configure callback
        _canvas.AllocatedWidth = w;
        _canvas.AllocatedHeight = _height;
        return new ElementSize(w, Math.Min(_height, h));
    }

    // ── Draw ────────────────────────────────────────────────────────────────

    internal override void Draw(DrawingContext ctx)
    {
        _canvas.AllocatedWidth = ctx.Width;
        _canvas.AllocatedHeight = _height;

        foreach (var cmd in _canvas.Commands)
        {
            switch (cmd)
            {
                case VectorCanvas.DrawLineCmd lc:
                    ctx.Page.AddLine(
                        ctx.X + lc.X1, ctx.Y + lc.Y1,
                        ctx.X + lc.X2, ctx.Y + lc.Y2,
                        PdfColor.FromHex(lc.HexColor), lc.LineWidth, lc.Opacity,
                        lc.DashPattern, lc.DashPhase);
                    break;

                case VectorCanvas.DrawRectCmd rc:
                    DrawRect(ctx, rc);
                    break;

                case VectorCanvas.DrawRoundedRectCmd rr:
                    DrawRoundedRect(ctx, rr);
                    break;

                case VectorCanvas.DrawEllipseCmd ec:
                    DrawEllipse(ctx, ec);
                    break;

                case VectorCanvas.DrawPathCmd pc:
                    DrawPath(ctx, pc.Path);
                    break;

                case VectorCanvas.DrawImageCmd ic:
                    DrawImage(ctx, ic);
                    break;

                case VectorCanvas.DrawTextCmd tc:
                    DrawText(ctx, tc);
                    break;

                case VectorCanvas.DrawLinkCmd lk:
                    ctx.Page.AddLinkAnnotation(ctx.X + lk.X, ctx.Y + lk.Y, lk.W, lk.H, lk.Url);
                    break;

                case VectorCanvas.DrawInternalLinkCmd il:
                    ctx.Page.AddInternalLinkAnnotation(ctx.X + il.X, ctx.Y + il.Y, il.W, il.H, il.PageNumber, il.Top);
                    break;

                case VectorCanvas.DrawBookmarkCmd bm:
                    ctx.BookmarkRecorder?.Invoke(bm.Title, bm.ParentTitle, ctx.PageNumber, ctx.Y + bm.Y);
                    break;

                case VectorCanvas.DrawQrCodeCmd qr:
                    DrawQrCode(ctx, qr);
                    break;

                case VectorCanvas.DrawGridCmd gc:
                    DrawGrid(ctx, gc, ctx.Width, _height);
                    break;
            }
        }
    }

    // ── Primitive renderers ─────────────────────────────────────────────────

    // Interior lines only: the canvas edges are left to the caller, as before.
    private static void DrawGrid(DrawingContext ctx, VectorCanvas.DrawGridCmd gc, double width, double height)
    {
        PdfColor color = PdfColor.FromHex(gc.HexColor);

        for (double x = gc.CellWidth; x < width; x += gc.CellWidth)
            ctx.Page.AddLine(ctx.X + x, ctx.Y, ctx.X + x, ctx.Y + height, color, gc.LineWidth);

        for (double y = gc.CellHeight; y < height; y += gc.CellHeight)
            ctx.Page.AddLine(ctx.X, ctx.Y + y, ctx.X + width, ctx.Y + y, color, gc.LineWidth);
    }

    private static void DrawRect(DrawingContext ctx, VectorCanvas.DrawRectCmd rc)
    {
        double ax = ctx.X + rc.X;
        double ay = ctx.Y + rc.Y;

        if (rc.FillHex is not null && rc.StrokeHex is not null)
            ctx.Page.AddRect(ax, ay, rc.W, rc.H,
                PdfColor.FromHex(rc.FillHex), PdfColor.FromHex(rc.StrokeHex), rc.LineWidth, rc.Opacity,
                rc.DashPattern, rc.DashPhase);
        else if (rc.FillHex is not null)
            ctx.Page.AddFilledRect(ax, ay, rc.W, rc.H, PdfColor.FromHex(rc.FillHex), rc.Opacity);
        else if (rc.StrokeHex is not null)
            ctx.Page.AddStrokedRect(ax, ay, rc.W, rc.H, PdfColor.FromHex(rc.StrokeHex), rc.LineWidth, rc.Opacity,
                rc.DashPattern, rc.DashPhase);
    }

    private static void DrawRoundedRect(DrawingContext ctx, VectorCanvas.DrawRoundedRectCmd rr)
    {
        double ax = ctx.X + rr.X;
        double ay = ctx.Y + rr.Y;

        if (rr.FillHex is not null && rr.StrokeHex is not null)
            ctx.Page.AddFilledAndStrokedRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.FillHex), PdfColor.FromHex(rr.StrokeHex), rr.LineWidth, rr.Opacity,
                rr.DashPattern, rr.DashPhase);
        else if (rr.FillHex is not null)
            ctx.Page.AddFilledRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.FillHex), rr.Opacity);
        else if (rr.StrokeHex is not null)
            ctx.Page.AddRoundedRect(ax, ay, rr.W, rr.H, rr.Radius,
                PdfColor.FromHex(rr.StrokeHex), rr.LineWidth, rr.Opacity,
                rr.DashPattern, rr.DashPhase);
    }

    private static void DrawEllipse(DrawingContext ctx, VectorCanvas.DrawEllipseCmd ec)
    {
        // Translate canvas-relative centre to page-absolute centre
        double cx = ctx.X + ec.Cx;
        double cy = ctx.Y + ec.Cy;

        if (ec.FillHex is not null && ec.StrokeHex is not null)
            ctx.Page.AddFilledAndStrokedEllipse(cx, cy, ec.Rx, ec.Ry,
                PdfColor.FromHex(ec.FillHex), PdfColor.FromHex(ec.StrokeHex), ec.LineWidth, ec.Opacity,
                ec.DashPattern, ec.DashPhase);
        else if (ec.FillHex is not null)
            ctx.Page.AddFilledEllipse(cx, cy, ec.Rx, ec.Ry, PdfColor.FromHex(ec.FillHex), ec.Opacity);
        else if (ec.StrokeHex is not null)
            ctx.Page.AddStrokedEllipse(cx, cy, ec.Rx, ec.Ry,
                PdfColor.FromHex(ec.StrokeHex), ec.LineWidth, ec.Opacity,
                ec.DashPattern, ec.DashPhase);
    }

    private static void DrawPath(DrawingContext ctx, PathDescriptor pd)
    {
        if (pd.Commands.Count == 0) return;

        void EmitPath()
        {
            foreach (var cmd in pd.Commands)
            {
                switch (cmd)
                {
                    case MoveToCmd m:
                        ctx.Page.PathMoveTo(ctx.X + m.X, ctx.Y + m.Y);
                        break;
                    case LineToCmd l:
                        ctx.Page.PathLineTo(ctx.X + l.X, ctx.Y + l.Y);
                        break;
                    case CurveToCmd c:
                        ctx.Page.PathCurveTo(
                            ctx.X + c.Cx1, ctx.Y + c.Cy1,
                            ctx.X + c.Cx2, ctx.Y + c.Cy2,
                            ctx.X + c.X, ctx.Y + c.Y);
                        break;
                    case ClosePathCmd:
                        ctx.Page.PathClose();
                        break;
                }
            }
        }

        bool dashScope = ctx.Page.BeginDashScope(pd.DashPattern, pd.DashPhase);

        if (pd.Gradient is { } gradient && pd.TryGetBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            // A gradient is painted through the path used as a clip; the outline, if any,
            // needs the path a second time because the clip consumed the first one.
            bool gradientOpacity = ctx.Page.BeginOpacityScope(pd.PaintOpacity);
            double w = maxX - minX, h = maxY - minY;
            double cx = ctx.X + minX + w / 2, cy = ctx.Y + minY + h / 2;
            var from = PdfColor.FromHex(gradient.FromHex);
            var to = PdfColor.FromHex(gradient.ToHex);

            ctx.Page.BeginClipScope();
            EmitPath();
            ctx.Page.ClipToPath(pd.EvenOddFill);
            if (gradient.Radial)
            {
                ctx.Page.PaintShading(true, from, to, cx, cy, 0, cx, cy, Math.Max(w, h) / 2);
            }
            else
            {
                // The axis runs through the box centre; its half length is the box projected on
                // the axis, so the two end colors sit exactly on the box corners.
                double rad = gradient.Angle * Math.PI / 180.0;
                double dx = Math.Cos(rad), dy = Math.Sin(rad);
                double half = (Math.Abs(w * dx) + Math.Abs(h * dy)) / 2;
                ctx.Page.PaintShading(false, from, to,
                    cx - dx * half, cy - dy * half, 0, cx + dx * half, cy + dy * half, 0);
            }
            ctx.Page.EndClipScope();

            if (pd.StrokeColor.HasValue)
            {
                ctx.Page.BeginPath(null, pd.StrokeColor, pd.LineWidth, pd.EvenOddFill);
                EmitPath();
                ctx.Page.EndPath(null, pd.StrokeColor, pd.EvenOddFill);
            }
            ctx.Page.EndOpacityScope(gradientOpacity);
        }
        else
        {
            bool opacityScope = ctx.Page.BeginPath(
                pd.FillColor,
                pd.StrokeColor,
                pd.LineWidth,
                pd.EvenOddFill,
                pd.PaintOpacity);
            EmitPath();
            ctx.Page.EndPath(pd.FillColor, pd.StrokeColor, pd.EvenOddFill, opacityScope);
        }

        ctx.Page.EndDashScope(dashScope);
    }

    private static void DrawQrCode(DrawingContext ctx, VectorCanvas.DrawQrCodeCmd qr)
    {
        var symbol = qr.Symbol ?? Barcodes.QrCode.QrCodeGenerator.Generate(qr.Data, qr.Level);
        int n = symbol.Size;
        int totalModules = n + 2 * qr.QuietZoneModules;
        double module = qr.Size / totalModules;
        double left = ctx.X + qr.X;
        double top = ctx.Y + qr.Y;
        double originX = left + qr.QuietZoneModules * module;
        double originY = top + qr.QuietZoneModules * module;

        if (qr.BackgroundHex is not null)
            ctx.Page.AddFilledRect(left, top, qr.Size, qr.Size, PdfColor.FromHex(qr.BackgroundHex));

        // One rectangle per horizontal run of dark modules, all in one path.
        var rects = new List<(double, double, double, double)>();
        for (int row = 0; row < n; row++)
        {
            int col = 0;
            while (col < n)
            {
                if (!symbol.Modules[row, col]) { col++; continue; }
                int runStart = col;
                while (col < n && symbol.Modules[row, col]) col++;
                rects.Add((originX + runStart * module, originY + row * module, (col - runStart) * module, module));
            }
        }
        ctx.Page.AddFilledRects(rects, PdfColor.FromHex(qr.Hex));
    }

    private static void DrawText(DrawingContext ctx, VectorCanvas.DrawTextCmd tc)
    {
        var font = PdfFonts.ResolveFont(tc.FontFamily, tc.Bold, tc.Italic);
        var color = PdfColor.FromHex(tc.HexColor);

        bool opacityScope = ctx.Page.BeginOpacityScope(tc.Opacity);
        ctx.Page.BeginTextObject();
        if (font.IsCustom)
        {
            if (tc.Angle == 0)
                ctx.Page.ShowTextAtCustomFont(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, font.Custom!);
            else
                ctx.Page.ShowTextAtRotated(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, tc.Angle, font.Custom!);
        }
        else
        {
            if (tc.Angle == 0)
                ctx.Page.ShowTextAt(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, font.StandardFamily, tc.Bold, tc.Italic);
            else
                ctx.Page.ShowTextAtRotated(tc.Text, ctx.X + tc.X, ctx.Y + tc.Y, tc.FontSize, color, tc.Angle, font.StandardFamily, tc.Bold, tc.Italic);
        }
        ctx.Page.EndTextObject();
        ctx.Page.EndOpacityScope(opacityScope);
    }

    private static void DrawImage(DrawingContext ctx, VectorCanvas.DrawImageCmd ic)
    {
        // A command is replayed once per page the canvas lands on, so the element
        // (header parse, content key) is cached on the command; its resource alias is
        // registered per page by PdfPage.DrawImage, so sharing one element across
        // pages is safe. PNG pixels are decoded once per distinct image at save time.
        ic.Decoded ??= new ImageElement(ic.Data);
        ic.Decoded.DrawAt(ctx.Page, ctx.X + ic.X, ctx.Y + ic.Y, ic.W, ic.H, ic.Fit);
    }
}
