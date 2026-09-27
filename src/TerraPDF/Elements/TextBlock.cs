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

    /// <summary>
    /// Counts layouts, on the current thread, whose result depends on the total-page
    /// hint (blocks holding page-number spans). The composer compares it before and
    /// after a layout pass: when unchanged, re-laying out with the real page count
    /// would produce the same result and is skipped.
    /// </summary>
    [ThreadStatic]
    internal static int PageCountDependentLayouts;

    internal TextBlock(string text) => Spans.Add(new LiteralSpan { Text = text });
    internal TextBlock() { }

    // -- Tokenisation ------------------------------------------------------

    /// <summary>
    /// A single word-level unit ready for line-packing.
    /// <c>Text</c> carries the page-count placeholder for page-number spans;
    /// the actual value is substituted at draw time via the flags.
    /// The span's style, font and colour live in a shared <see cref="TextFormat"/>, and the
    /// width is measured once when the token is created (see <see cref="TextToken.Create"/>)
    /// and reused by line breaking, measuring and drawing.
    /// </summary>
    internal readonly record struct TextToken(
        string     Text,
        TextFormat Format,
        bool       IsPageNumber,
        bool       IsTotalPages,
        double     Width)
    {
        internal TextStyle    Style => Format.Style;
        internal ResolvedFont Font  => Format.Font;
        internal PdfColor     Color => Format.Color;

        internal static TextToken Create(string text, TextFormat format,
            bool isPageNumber = false, bool isTotalPages = false) =>
            new(text, format, isPageNumber, isTotalPages, Measure(text, format));

        /// <summary>Same token showing different text (a fragment or a page number), re-measured.</summary>
        internal TextToken WithText(string text) =>
            this with { Text = text, Width = Measure(text, Format) };

        private static double Measure(string text, TextFormat format) =>
            FontMetrics.MeasureWidth(text, format.Style.Size ?? 12, format.Font,
                format.Style.IsBold ?? false, format.Style.IsItalic ?? false);
    }

    /// <summary>
    /// A span's resolved style, font and colour, shared by all of its tokens so each token
    /// only carries its text and width.
    /// </summary>
    internal sealed record TextFormat(TextStyle Style, ResolvedFont Font, PdfColor Color);

    private static ResolvedFont ResolveFont(TextStyle style) =>
        PdfFonts.ResolveFont(style.Family, style.IsBold ?? false, style.IsItalic ?? false);

    /// <summary>
    /// Breaks all spans into word-level <see cref="TextToken"/>s with fully resolved styles.
    /// </summary>
    private List<TextToken> Tokenize(TextStyle baseStyle, string pageNum, string totalPages)
    {
        // Sized exactly up front: token lists are the largest per-block allocation.
        int count = 0;
        foreach (var span in Spans)
            count += span is LiteralSpan literal ? CountWords(literal.Text) : 1;

        var tokens = new List<TextToken>(count);
        foreach (var span in Spans)
        {
            TextStyle s = baseStyle.MergeWith(span.Style);
            var format = new TextFormat(s, ResolveFont(s), PdfColor.FromHex(s.Color ?? "#000000"));
            switch (span)
            {
                case LiteralSpan ls:
                    AddWords(tokens, ls.Text, format);
                    break;
                case PageNumberSpan:
                    tokens.Add(TextToken.Create(pageNum,    format, isPageNumber: true));
                    break;
                case TotalPagesSpan:
                    tokens.Add(TextToken.Create(totalPages, format, isTotalPages: true));
                    break;
            }
        }
        return tokens;
    }

    /// <summary>Number of tokens <see cref="AddWords"/> produces for <paramref name="text"/>.</summary>
    private static int CountWords(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; count++)
            i = NextWordEnd(text, i);
        return count;
    }

    /// <summary>End (exclusive) of the word, whitespace run or newline starting at <paramref name="i"/>.</summary>
    private static int NextWordEnd(string text, int i)
    {
        if (text[i] == '\n')
            return i + 1;
        if (char.IsWhiteSpace(text[i]))
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]) && text[i] != '\n') i++;
            return i;
        }
        while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
        return i;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into alternating non-whitespace word tokens,
    /// whitespace-run tokens, and explicit newline tokens, appended to <paramref name="tokens"/>.
    /// </summary>
    private static void AddWords(List<TextToken> tokens, string text, TextFormat format)
    {
        for (int s = 0; s < text.Length; )
        {
            int end = NextWordEnd(text, s);
            tokens.Add(TextToken.Create(end - s == text.Length ? text : text[s..end], format));
            s = end;
        }
    }

    // -- Line building -----------------------------------------------------

    /// <summary>One wrapped line and whether it ends a paragraph (last line / hard-break line).</summary>
    internal readonly record struct WrappedLine(List<TextToken> Tokens, bool IsLastInParagraph);

    /// <summary>
    /// Greedily packs tokens into lines no wider than <paramref name="availableWidth"/>.
    /// A single word wider than the line is broken at character boundaries so no
    /// line ever exceeds the available width. Returns at least one (possibly empty) line.
    /// </summary>
    private static List<WrappedLine> BuildLines(List<TextToken> tokens, double availableWidth)
    {
        // Fast path: everything fits on one line (most table cells and labels). The greedy
        // loop below would never wrap — each prefix width is at most the total — so the
        // token list itself becomes the line instead of being copied into a new one.
        if (FitsOnOneLine(tokens, availableWidth))
            return [new WrappedLine(TrimTrailing(tokens), IsLastInParagraph: true)];

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

            double tw = token.Width;

            // Token overflows the current line – wrap first
            if (lineW + tw > availableWidth && current.Count > 0)
            {
                lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: false));
                current = [];
                lineW   = 0;

                // Drop the whitespace token that triggered the wrap
                if (string.IsNullOrWhiteSpace(token.Text))
                    continue;
            }

            // The token now starts its line. If it alone is wider than the line,
            // break it at character boundaries; the last fragment stays in
            // `current` so following tokens can pack after it. Page-number
            // tokens are exempt: their text is a placeholder substituted at
            // draw time, so each fragment would redraw the full number.
            var placed = token;
            if (tw > availableWidth && !token.IsPageNumber && !token.IsTotalPages)
            {
                var fragments = BreakOversizedToken(token, availableWidth);
                for (int f = 0; f < fragments.Count - 1; f++)
                    lines.Add(new WrappedLine([fragments[f]], IsLastInParagraph: false));
                placed = fragments[^1];
                tw     = placed.Width;
            }

            current.Add(placed);
            lineW += tw;
        }

        if (current.Count > 0)
            lines.Add(new WrappedLine(TrimTrailing(current), IsLastInParagraph: true));

        return lines.Count > 0 ? lines : [new WrappedLine([], true)];
    }

    /// <summary>
    /// Splits a token wider than <paramref name="availableWidth"/> into fragments
    /// that each fit, keeping surrogate pairs intact. Character advance widths are
    /// additive (<see cref="FontMetrics.MeasureWidth(string, double, PdfFontFamily, bool, bool)"/> applies no kerning), so a
    /// fragment's width equals the sum of its characters'. Every fragment carries
    /// at least one character so layout always makes progress, even when the line
    /// is narrower than a single glyph.
    /// </summary>
    private static List<TextToken> BreakOversizedToken(in TextToken token, double availableWidth)
    {
        var fragments = new List<TextToken>();
        string text = token.Text;
        int start = 0;
        while (start < text.Length)
        {
            double w   = 0;
            int    end = start;
            while (end < text.Length)
            {
                int next = end + (char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1);
                double cw = token.WithText(text[end..next]).Width;
                if (end > start && w + cw > availableWidth)
                    break;
                w   += cw;
                end  = next;
            }
            fragments.Add(token.WithText(text[start..end]));
            start = end;
        }
        return fragments;
    }

    /// <summary>
    /// True when the tokens need no wrapping, no hard break and no leading-whitespace skip,
    /// i.e. <see cref="BuildLines"/> would produce exactly one line holding all of them.
    /// </summary>
    private static bool FitsOnOneLine(List<TextToken> tokens, double availableWidth)
    {
        if (tokens.Count == 0 || string.IsNullOrWhiteSpace(tokens[0].Text))
            return false;

        double lineW = 0;
        foreach (var t in tokens)
        {
            if (t.Text == "\n") return false;
            lineW += t.Width;
            if (lineW > availableWidth) return false;
        }
        return true;
    }

    /// <summary>Removes trailing whitespace tokens in place (each line owns its list).</summary>
    private static List<TextToken> TrimTrailing(List<TextToken> line)
    {
        int last = line.Count - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(line[last].Text)) last--;
        line.RemoveRange(last + 1, line.Count - last - 1);
        return line;
    }

    private static double SumWidths(List<TextToken> tokens)
    {
        double total = 0;
        foreach (var t in tokens) total += t.Width;
        return total;
    }

    // -- Measure / line layout --------------------------------------

    /// <summary>
    /// Tokenizes and wraps this block's text for the given width, using the
    /// document's page-count hint as the placeholder for page-number spans
    /// (the actual values are substituted at draw time).  Shared by
    /// <see cref="Measure"/>, <see cref="Draw"/>, and the pagination engine's
    /// line-splitting path, so all three always agree on line breaks.
    /// </summary>
    /// <remarks>
    /// Within one <see cref="LayoutPass"/> the result of the last call is memoised:
    /// the same block is measured by its parents, by the pagination engine and again
    /// when drawn, usually at the same width. The page-count hint is part of the key
    /// only for blocks that hold page-number spans. The returned lines are shared and
    /// must not be mutated.
    /// </remarks>
    internal (List<WrappedLine> Lines, TextStyle Resolved, double LineHeight) LayoutLines(
        double w, TextStyle? defaultStyle, int totalPagesHint)
    {
        bool hasPageNumbers = Spans.Exists(s => s is PageNumberSpan or TotalPagesSpan);
        if (hasPageNumbers)
            PageCountDependentLayouts++;

        int pass = LayoutPass.Current;
        var cached = _layoutCache;
        if (pass != 0 && cached is not null && cached.Pass == pass && cached.Width == w
            && ReferenceEquals(cached.DefaultStyle, defaultStyle)
            && (!hasPageNumbers || cached.TotalPagesHint == totalPagesHint))
        {
            return cached.Result;
        }

        TextStyle resolved = (defaultStyle ?? TextStyle.Default).MergeWith(SpanStyle);
        double    lineH    = (resolved.Size ?? 12) * (resolved.LineHeightMultiplier ?? 1.4);

        string placeholder = hasPageNumbers ? totalPagesHint.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var tokens = Tokenize(resolved, placeholder, placeholder);
        var result = (BuildLines(tokens, w), resolved, lineH);

        if (pass != 0)
            _layoutCache = new LayoutCacheEntry(pass, w, defaultStyle, totalPagesHint, result);
        return result;
    }

    /// <summary>The last <see cref="LayoutLines"/> result and the inputs it was computed for.</summary>
    private sealed record LayoutCacheEntry(
        int Pass, double Width, TextStyle? DefaultStyle, int TotalPagesHint,
        (List<WrappedLine> Lines, TextStyle Resolved, double LineHeight) Result);

    // Replaced as a whole (never mutated), so concurrent publishes of the same
    // document at worst recompute a layout.
    private LayoutCacheEntry? _layoutCache;

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint)
    {
        var (lines, _, lineH) = LayoutLines(w, defaultStyle, totalPagesHint);

        // Return the width of the widest line so that container-level alignment elements
        // (AlignCenter, AlignRight) can compute the correct offset.
        double contentW = 0;
        foreach (var line in lines)
            contentW = Math.Max(contentW, SumWidths(line.Tokens));

        return new ElementSize(contentW, lines.Count * lineH);
    }

    /// <summary>A pending underline/strikethrough stroke, drawn after the line's text object closes.</summary>
    private readonly record struct DecorationStroke(double X, double Y, double Width, PdfColor Color, double LineWidth);

    /// <summary>
    /// Shows <paramref name="text"/> in <paramref name="token"/>'s font, size and colour
    /// inside the currently open text object, starting at <paramref name="x"/>.
    /// </summary>
    private static void DrawText(DrawingContext ctx, in TextToken token, string text, double x, double lineY)
    {
        double sf = token.Style.Size ?? 12;
        double bl = lineY + sf;

        if (token.Font.IsCustom)
            ctx.Page.ShowTextAtCustomFont(text, x, bl, sf, token.Color, token.Font.Custom!);
        else
            ctx.Page.ShowTextAt(text, x, bl, sf, token.Color, token.Font.StandardFamily,
                token.Style.IsBold ?? false, token.Style.IsItalic ?? false);
    }

    /// <summary>
    /// Queues <paramref name="token"/>'s underline/strikethrough strokes (graphics
    /// operators are illegal inside <c>BT…ET</c>, so they are flushed after the text
    /// object closes).
    /// </summary>
    /// <remarks><paramref name="decorations"/> is created on first use: most text has none.</remarks>
    private static void AddDecorations(in TextToken token, double x, double lineY,
        ref List<DecorationStroke>? decorations)
    {
        bool strike = token.Style.IsStrikethrough ?? false;
        bool underline = token.Style.IsUnderline ?? false;
        if (!strike && !underline) return;

        double sf = token.Style.Size ?? 12;
        double bl = lineY + sf;
        decorations ??= [];

        if (strike)
            decorations.Add(new DecorationStroke(x, bl - sf * 0.35, token.Width, token.Color, sf * 0.07));

        if (underline)
            decorations.Add(new DecorationStroke(x, bl + sf * 0.12, token.Width, token.Color, sf * 0.07));
    }

    /// <summary>
    /// True when <paramref name="next"/> can be appended to a run started by
    /// <paramref name="first"/> and shown by the same Tj. The viewer then advances through
    /// the run by its own glyph widths, which equal the widths measured here: the built-in
    /// font tables match the Adobe AFMs (see <c>AfmWidthTableTests</c>) and custom fonts
    /// embed the measured widths in their <c>/W</c> array.
    /// </summary>
    private static bool CanJoinRun(in TextToken first, in TextToken next) => SameTextState(first, next);

    private static string ConcatTexts(List<TextToken> tokens, int start, int end)
    {
        int length = 0;
        for (int k = start; k < end; k++) length += tokens[k].Text.Length;
        return string.Create(length, (tokens, start, end), static (dest, state) =>
        {
            for (int k = state.start; k < state.end; k++)
            {
                state.tokens[k].Text.AsSpan().CopyTo(dest);
                dest = dest[state.tokens[k].Text.Length..];
            }
        });
    }

    /// <summary>
    /// True when <paramref name="b"/> renders with exactly the same font, size and colour
    /// as <paramref name="a"/>, so both can be shown by one text-showing operator.
    /// </summary>
    private static bool SameTextState(in TextToken a, in TextToken b) =>
        ReferenceEquals(a.Format, b.Format) ||
        a.Font.IsCustom == b.Font.IsCustom
        && (a.Font.IsCustom
            ? ReferenceEquals(a.Font.Custom, b.Font.Custom)
            : a.Font.StandardFamily == b.Font.StandardFamily
              && (a.Style.IsBold ?? false) == (b.Style.IsBold ?? false)
              && (a.Style.IsItalic ?? false) == (b.Style.IsItalic ?? false))
        && (a.Style.Size ?? 12) == (b.Style.Size ?? 12)
        && a.Color.Equals(b.Color);

    // -- Draw ------------------------------------------------------

    /// <summary>
    /// Replaces the layout-time page-number placeholder with the actual value
    /// for the page being drawn.
    /// </summary>
    private static TextToken ResolveDynamicText(in TextToken t, DrawingContext ctx)
    {
        if (t.IsPageNumber)
            return t.WithText(ctx.PageNumber.ToString(CultureInfo.InvariantCulture));
        if (t.IsTotalPages)
            return t.WithText(ctx.TotalPages.ToString(CultureInfo.InvariantCulture));
        return t;
    }

    internal override void Draw(DrawingContext ctx)
    {
        var (lines, resolved, lineH) = LayoutLines(ctx.Width, ctx.DefaultTextStyle, ctx.TotalPages);
        DrawLines(ctx, lines, resolved, lineH);

        // Layout runs for the whole document before anything is drawn, so every block's
        // cached lines would otherwise stay alive until the publish ends and get promoted
        // to Gen2 on large documents. Most blocks are drawn once; the few drawn again
        // (headers, footers, repeated table header rows) simply lay out again.
        _layoutCache = null;
    }

    /// <summary>
    /// Draws pre-wrapped lines starting at <c>ctx.Y</c>, honouring per-line
    /// alignment/justification.  Also used by <see cref="TextBlockSlice"/> to
    /// draw a page-sized subrange of a split paragraph.
    /// </summary>
    internal static void DrawLines(DrawingContext ctx, List<WrappedLine> lines,
        TextStyle resolved, double lineH)
    {
        var alignment = resolved.Alignment ?? TextAlignment.Left;

        double curY = ctx.Y;
        List<DecorationStroke>? decorations = null;

        foreach (var (rawTokens, isLastInParagraph) in lines)
        {
            // Substitute the actual page numbers for placeholder tokens before
            // widths are computed, so alignment uses the drawn text's width.
            var lineTokens = rawTokens;
            for (int t = 0; t < rawTokens.Count; t++)
            {
                if (rawTokens[t].IsPageNumber || rawTokens[t].IsTotalPages)
                {
                    lineTokens = rawTokens.Select(tok => ResolveDynamicText(tok, ctx)).ToList();
                    break;
                }
            }

            // Determine the effective alignment for this line:
            // the last line of a justified paragraph falls back to left.
            var lineAlignment = (alignment == TextAlignment.Justify && isLastInParagraph)
                ? TextAlignment.Left
                : alignment;

            // One text object per line; opened lazily so empty lines emit nothing.
            bool textOpen = false;
            decorations?.Clear();

            void Show(in TextToken token, string text, double x)
            {
                if (!textOpen)
                {
                    ctx.Page.BeginTextObject();
                    textOpen = true;
                }
                DrawText(ctx, token, text, x, curY);
            }

            if (lineAlignment == TextAlignment.Justify)
            {
                // Justify: skip whitespace tokens, distribute extra space between word gaps.
                var    words    = lineTokens.FindAll(t => !string.IsNullOrWhiteSpace(t.Text));
                double wordsW   = SumWidths(words);
                int    gapCount = words.Count - 1;
                double extra    = gapCount > 0 ? (ctx.Width - wordsW) / gapCount : 0;

                double curX    = ctx.X;
                int    wordIdx = 0;
                // Each word is positioned individually: the gaps are wider than a space.
                foreach (var token in words)
                {
                    Show(token, token.Text, curX);
                    AddDecorations(token, curX, curY, ref decorations);
                    curX += token.Width;
                    if (wordIdx < gapCount)
                        curX += extra;
                    wordIdx++;
                }
            }
            else
            {
                // Left / Center / Right: preserve natural whitespace widths.
                double totalLineW = SumWidths(lineTokens);
                double curX = lineAlignment switch
                {
                    TextAlignment.Right  => ctx.X + ctx.Width - totalLineW,
                    TextAlignment.Center => ctx.X + (ctx.Width - totalLineW) / 2,
                    _                    => ctx.X,
                };

                // Consecutive tokens (words and the spaces between them) in the same font,
                // size and colour are shown by a single Tj: the viewer's advance widths equal
                // the widths measured here (see CanJoinRun), so it lays them out exactly as
                // one positioned word at a time would, with far fewer operators.
                int i = 0;
                while (i < lineTokens.Count)
                {
                    var first = lineTokens[i];
                    int end = i + 1;
                    while (end < lineTokens.Count && CanJoinRun(first, lineTokens[end]))
                        end++;

                    double runX = curX;
                    for (int k = i; k < end; k++)
                    {
                        AddDecorations(lineTokens[k], curX, curY, ref decorations);
                        curX += lineTokens[k].Width;
                    }

                    string text = end == i + 1 ? first.Text : ConcatTexts(lineTokens, i, end);
                    if (text.Length > 0)
                        Show(first, text, runX);
                    i = end;
                }
            }

            if (textOpen)
                ctx.Page.EndTextObject();

            // Underline/strikethrough are graphics operators — draw them after
            // the text object closes, at the exact per-token coordinates.
            foreach (var s in decorations ?? [])
                ctx.Page.AddLine(s.X, s.Y, s.X + s.Width, s.Y, s.Color, s.LineWidth);

            curY += lineH;
        }
    }
}

/// <summary>
/// A page-sized subrange of a split <see cref="TextBlock"/>'s wrapped lines,
/// produced by the pagination engine when a paragraph is taller than the
/// remaining page.  Follows the same pattern as <see cref="TableSlice"/>:
/// the lines were wrapped once at layout time, so every slice agrees on
/// line breaks with the measured whole.
/// </summary>
internal sealed class TextBlockSlice : Element
{
    private readonly List<TextBlock.WrappedLine> _lines;
    private readonly TextStyle _resolved;
    private readonly double    _lineHeight;

    internal TextBlockSlice(List<TextBlock.WrappedLine> lines, TextStyle resolved, double lineHeight)
    {
        _lines      = lines;
        _resolved   = resolved;
        _lineHeight = lineHeight;
    }

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null,
        int totalPagesHint = DefaultTotalPagesHint) =>
        new(w, _lines.Count * _lineHeight);

    internal override void Draw(DrawingContext ctx) =>
        TextBlock.DrawLines(ctx, _lines, _resolved, _lineHeight);
}
