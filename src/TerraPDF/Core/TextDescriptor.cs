using TerraPDF.Elements;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for configuring a <see cref="TextBlock"/>.
/// Returned by <c>IContainer.Text(…)</c> extension methods.
/// </summary>
public sealed class TextDescriptor
{
    private readonly TextBlock _element;

    internal TextDescriptor(TextBlock element) => _element = element;

    // -- Style shortcuts -------------------------------------------

    /// <summary>Sets the font size in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is zero or negative.</exception>
    public TextDescriptor FontSize(double size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).FontSize(size);
        return this;
    }

    /// <summary>Sets the text colour using a hex string (e.g. "#1a4a8a" or Colors.Blue.Medium).</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public TextDescriptor FontColor(string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).FontColor(hexColor);
        return this;
    }

    /// <summary>Renders text in bold (Times-Bold).</summary>
    public TextDescriptor Bold()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).Bold();
        return this;
    }

    /// <summary>Renders text in semi-bold (mapped to bold in the built-in font set).</summary>
    public TextDescriptor SemiBold()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).SemiBold();
        return this;
    }

    /// <summary>Renders text in italic.</summary>
    public TextDescriptor Italic()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).Italic();
        return this;
    }

    /// <summary>Renders text with a strikethrough line.</summary>
    public TextDescriptor Strikethrough()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).Strikethrough();
        return this;
    }

    /// <summary>Renders text with an underline.</summary>
    public TextDescriptor Underline()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).Underline();
        return this;
    }

    /// <summary>Sets the line-height multiplier (e.g. 1.0 = tight, 1.4 = default, 2.0 = double-spaced).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiplier"/> is zero or negative.</exception>
    public TextDescriptor LineHeight(double multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).LineHeight(multiplier);
        return this;
    }

    /// <summary>Aligns text to the left edge (default).</summary>
    public TextDescriptor AlignLeft()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).AlignLeft();
        return this;
    }

    /// <summary>Centers text horizontally within the available width.</summary>
    public TextDescriptor AlignCenter()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).AlignCenter();
        return this;
    }

    /// <summary>Aligns text to the right edge.</summary>
    public TextDescriptor AlignRight()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).AlignRight();
        return this;
    }

    /// <summary>
    /// Justifies text so each wrapped line fills the full available width.
    /// The last line of each paragraph remains left-aligned.
    /// </summary>
    public TextDescriptor Justify()
    {
        _element.SpanStyle = (_element.SpanStyle ?? new TextStyle()).Justify();
        return this;
    }

    // -- Multi-span overloads --------------------------------------

    /// <summary>Appends a literal text span with an optional style override.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    public SpanDescriptor Span(string text, Action<TextStyle>? styleAction = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var span = new LiteralSpan { Text = text };
        if (styleAction is not null)
        {
            var s = new TextStyle();
            styleAction(s);
            span.Style = s;
        }
        _element.Spans.Add(span);
        return new SpanDescriptor(span);
    }

    /// <summary>Appends the current page number.</summary>
    public SpanDescriptor CurrentPageNumber()
    {
        var span = new PageNumberSpan();
        _element.Spans.Add(span);
        return new SpanDescriptor(span);
    }

    /// <summary>Appends the total page count.</summary>
    public SpanDescriptor TotalPages()
    {
        var span = new TotalPagesSpan();
        _element.Spans.Add(span);
        return new SpanDescriptor(span);
    }

    private TextDescriptor AddSpan(TextSpan span)
    {
        _element.Spans.Add(span);
        return this;
    }
}
