using TerraPDF.Elements;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Fluent API for styling a single text span inside a multi-span <see cref="TextBlock"/>.
/// Returned by <c>TextDescriptor.Span(…)</c>, <c>TextDescriptor.CurrentPageNumber()</c>
/// and <c>TextDescriptor.TotalPages()</c>.
/// All methods here modify <em>only</em> the individual span — never the whole block.
/// </summary>
public sealed class SpanDescriptor
{
    private readonly TextSpan _span;

    internal SpanDescriptor(TextSpan span) => _span = span;

    private SpanDescriptor Apply(Func<TextStyle, TextStyle> fn)
    {
        _span.Style = fn(_span.Style ?? new TextStyle());
        return this;
    }

    /// <summary>Renders this span in bold.</summary>
    public SpanDescriptor Bold() => Apply(s => s.Bold());

    /// <summary>Renders this span in semi-bold.</summary>
    public SpanDescriptor SemiBold() => Apply(s => s.SemiBold());

    /// <summary>Renders this span in italic.</summary>
    public SpanDescriptor Italic() => Apply(s => s.Italic());

    /// <summary>Renders this span with a strikethrough line.</summary>
    public SpanDescriptor Strikethrough() => Apply(s => s.Strikethrough());

    /// <summary>Renders this span with an underline.</summary>
    public SpanDescriptor Underline() => Apply(s => s.Underline());

    /// <summary>Sets the font size for this span in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is zero or negative.</exception>
    public SpanDescriptor FontSize(double size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return Apply(s => s.FontSize(size));
    }

    /// <summary>Sets the text colour for this span using a hex string.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public SpanDescriptor FontColor(string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        return Apply(s => s.FontColor(hexColor));
    }
}
