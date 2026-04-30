using System.Globalization;
using TerraPDF.Drawing;
using TerraPDF.Helpers;

namespace TerraPDF.Elements;

// --- Span types ---------------------------------------------------------------

internal abstract class TextSpan
{
    internal TextStyle? Style { get; set; }
}

internal sealed class LiteralSpan : TextSpan
{
    internal required string Text { get; init; }
}

internal sealed class PageNumberSpan : TextSpan { }
internal sealed class TotalPagesSpan : TextSpan { }

// --- Text element -------------------------------------------------------------

/// <summary>Renders one or more styled text spans with automatic word-wrapping.</summary>
internal sealed class TextBlock : Element
{
    internal List<TextSpan> Spans     { get; } = [];
    internal TextStyle?     SpanStyle { get; set; }

    internal TextBlock(string text) => Spans.Add(new LiteralSpan { Text = text });
    internal TextBlock() { }

    private TextStyle Resolve(DrawingContext ctx) => ctx.DefaultTextStyle.MergeWith(SpanStyle);

    // -- Tokenisation ------------------------------------------------------

    /// <summary>
    /// A single word-level unit ready for line-packing.
    /// <c>Text</c> is pre-resolved; page-number spans carry a placeholder during measure
    /// and the real value during draw.
    /// </summary>
    private readonly record struct TextToken(
        string    Text,
        TextStyle Style,
        bool      IsPageNumber,
        bool      IsTotalPages);

    /// <summary>
    /// Breaks all spans into word-level <see cref="TextToken"/>s with fully resolved styles.
    /// </summary>
    private List<TextToken> Tokenize(TextStyle baseStyle, string pageNum, string totalPages)
    {
        var tokens = new List<TextToken>();
        foreach (var span in Spans)
        {
            TextStyle s = baseStyle.MergeWith(span.Style);
            switch (span)
            {
                case LiteralSpan ls:
                    foreach (string word in SplitWords(ls.Text))
                        tokens.Add(new TextToken(word, s, false, false));
                    break;
                case PageNumberSpan:
                    tokens.Add(new TextToken(pageNum,    s, true,  false));
                    break;
                case TotalPagesSpan:
                    tokens.Add(new TextToken(totalPages, s, false, true));
                    break;
            }
        }
        return tokens;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into alternating non-whitespace word tokens,
    /// whitespace-run tokens, and explicit newline tokens.
    /// </summary>
    private static IEnumerable<string> SplitWords(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                yield return "\n";
                i++;
            }
            else if (char.IsWhiteSpace(text[i]))
            {
                int s = i;
                while (i < text.Length && char.IsWhiteSpace(text[i]) && text[i] != '\n') i++;
                yield return text[s..i];
            }
            else
            {
                int s = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                yield return text[s..i];
            }
        }
    }

    private static double TokenWidth(in TextToken t) =>
        FontMetrics.MeasureWidth(t.Text, t.Style.Size ?? 12,
            t.Style.IsBold ?? false, t.Style.IsItalic ?? false);

    // -- Line building -----------------------------------------------------

    /// <summary>One wrapped line and whether it ends a paragraph (last line / hard-break line).</summary>
    private readonly record struct WrappedLine(List<TextToken> Tokens, bool IsLastInParagraph);

    /// <summary>
    /// Greedily packs tokens into lines no wider than <paramref name="availableWidth"/>.
    /// Returns at least one (possibly empty) line.
    /// </summary>
    private static List<WrappedLine> BuildLines(List<TextToken> tokens, double availableWidth)
    {
        var lines   = new List<WrappedLine>();
        var current = new List<TextToken>();
        double lineW = 0;

        foreach (var token in tokens)
        {
            // Hard line-break
            if (token.Text == "\n")
            {
                lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: true));
                current = [];
                lineW   = 0;
                continue;
            }

            // Skip leading whitespace at the start of a new line
            if (current.Count == 0 && string.IsNullOrWhiteSpace(token.Text))
                continue;

            double tw = TokenWidth(token);

            // Token overflows – wrap unless it is the only token on the line (very long word)
            if (lineW + tw > availableWidth && current.Count > 0)
            {
                lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: false));
                current = [];
                lineW   = 0;

                // Drop the whitespace token that triggered the wrap
                if (string.IsNullOrWhiteSpace(token.Text))
                    continue;
            }

            current.Add(token);
            lineW += tw;
        }

        if (current.Count > 0)
            lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: true));

        return lines.Count > 0 ? lines : [new WrappedLine([], true)];
    }

    private static List<TextToken> TrimTrailing(List<TextToken> line)
    {
        int last = line.Count - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(line[last].Text)) last--;
        return line[..(last + 1)];
    }

    // -- Measure ---------------------------------------------------

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null)
    {
        TextStyle effective = (defaultStyle ?? TextStyle.Default).MergeWith(SpanStyle);
        double    lineH     = (effective.Size ?? 12) * (effective.LineHeightMultiplier ?? 1.4);

        var tokens = Tokenize(effective, "00", "00");
        var lines  = BuildLines(tokens, w);

        // Return the width of the widest line so that container-level alignment elements
        // (AlignCenter, AlignRight) can compute the correct offset.
        double contentW = lines.Count > 0
            ? lines.Max(l => l.Tokens.Sum(t => TokenWidth(t)))
            : 0;

        return new ElementSize(contentW, lines.Count * lineH);
    }

    private static void DrawToken(DrawingContext ctx, in TextToken token, double x, double lineY)
    {
        double sf  = token.Style.Size    ?? 12;
        string sh  = token.Style.Color   ?? "#000000";
        bool   sb  = token.Style.IsBold  ?? false;
        bool   si  = token.Style.IsItalic ?? false;
        var    sc  = PdfColor.FromHex(sh);
        var    sf2 = (sb, si) switch
        {
            (true,  true)  => StandardFont.TimesBoldItalic,
            (true,  false) => StandardFont.TimesBold,
            (false, true)  => StandardFont.TimesItalic,
            _              => StandardFont.Helvetica,
        };

        double bl = lineY + sf;
        double tw = FontMetrics.MeasureWidth(token.Text, sf, sb, si);

        ctx.Page.AddText(token.Text, x, bl, sf, sc, sf2);

        if (token.Style.IsStrikethrough ?? false)
        {
            double strikeY = bl - sf * 0.35;
            ctx.Page.AddLine(x, strikeY, x + tw, strikeY, sc, lineWidth: sf * 0.07);
        }

        if (token.Style.IsUnderline ?? false)
        {
            double underlineY = bl + sf * 0.12;
            ctx.Page.AddLine(x, underlineY, x + tw, underlineY, sc, lineWidth: sf * 0.07);
        }
    }

    // -- Draw ------------------------------------------------------

    internal override void Draw(DrawingContext ctx)
    {
        TextStyle resolved  = Resolve(ctx);
        double    lineH     = (resolved.Size ?? 12) * (resolved.LineHeightMultiplier ?? 1.4);
        var       alignment = resolved.Alignment ?? TextAlignment.Left;

        string pageNum    = ctx.PageNumber.ToString(CultureInfo.InvariantCulture);
        string totalPages = ctx.TotalPages.ToString(CultureInfo.InvariantCulture);

        var tokens = Tokenize(resolved, pageNum, totalPages);
        var lines  = BuildLines(tokens, ctx.Width);

        double curY = ctx.Y;
        foreach (var (lineTokens, isLastInParagraph) in lines)
        {
            // Determine the effective alignment for this line:
            // the last line of a justified paragraph falls back to left.
            var lineAlignment = (alignment == TextAlignment.Justify && isLastInParagraph)
                ? TextAlignment.Left
                : alignment;

            if (lineAlignment == TextAlignment.Justify)
            {
                // Justify: skip whitespace tokens, distribute extra space between word gaps.
                var    words    = lineTokens.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList();
                double wordsW   = words.Sum(t => TokenWidth(t));
                int    gapCount = words.Count - 1;
                double extra    = gapCount > 0 ? (ctx.Width - wordsW) / gapCount : 0;

                double curX    = ctx.X;
                int    wordIdx = 0;
                foreach (var token in words)
                {
                    DrawToken(ctx, token, curX, curY);
                    curX += TokenWidth(token);
                    if (wordIdx < gapCount)
                        curX += extra;
                    wordIdx++;
                }
            }
            else
            {
                // Left / Center / Right: preserve natural whitespace widths.
                double totalLineW = lineTokens.Sum(t => TokenWidth(t));
                double curX = lineAlignment switch
                {
                    TextAlignment.Right  => ctx.X + ctx.Width - totalLineW,
                    TextAlignment.Center => ctx.X + (ctx.Width - totalLineW) / 2,
                    _                    => ctx.X,
                };

                foreach (var token in lineTokens)
                {
                    if (!string.IsNullOrEmpty(token.Text))
                        DrawToken(ctx, token, curX, curY);
                    curX += TokenWidth(token);
                }
            }

            curY += lineH;
        }
    }
}
