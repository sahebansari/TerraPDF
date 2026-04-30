using TerraPDF.Elements;
using TerraPDF.Infra;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Configures a single page: its size, margins, background, and the three layout slots
/// (Header, Content, Footer).
/// </summary>
public sealed class PageDescriptor
{
    // -- Internal state --------------------------------------------

    internal double PageWidth  { get; private set; } = 595.28;   // A4 default
    internal double PageHeight { get; private set; } = 841.89;

    internal double MarginTop    { get; private set; }
    internal double MarginRight  { get; private set; }
    internal double MarginBottom { get; private set; }
    internal double MarginLeft   { get; private set; }

    internal PdfColor?   BackgroundColor    { get; private set; }
    internal TextStyle   DefaultStyle   { get; private set; } = TextStyle.Default;

    // The three slots
    internal Container HeaderSlot  { get; } = new();
    internal Container ContentSlot { get; } = new();
    internal Container FooterSlot  { get; } = new();

    /// <summary>
    /// When <c>true</c> the header is drawn only on the first PDF page produced by
    /// this descriptor; continuation pages leave the header area blank and use the
    /// extra vertical space for content.
    /// </summary>
    internal bool HeaderFirstPageOnly { get; private set; }

    // -- Page size -------------------------------------------------

    /// <summary>Sets the page dimensions using a standard size tuple (e.g. <c>PageSize.A4</c>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    public PageDescriptor Size((double Width, double Height) size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width,  nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height, nameof(size));
        PageWidth  = size.Width;
        PageHeight = size.Height;
        return this;
    }

    /// <summary>Sets the page dimensions explicitly in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    public PageDescriptor Size(double widthPt, double heightPt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPt,  nameof(widthPt));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightPt, nameof(heightPt));
        PageWidth  = widthPt;
        PageHeight = heightPt;
        return this;
    }

    /// <summary>Sets the page dimensions with an explicit unit.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    public PageDescriptor Size(double width, double height, Unit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width,  nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height, nameof(height));
        PageWidth  = UnitConversion.ToPoints(width,  unit);
        PageHeight = UnitConversion.ToPoints(height, unit);
        return this;
    }

    // -- Margins ---------------------------------------------------

    /// <summary>Sets equal margins on all four sides (in PDF points).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public PageDescriptor Margin(double value) =>
        Margin(value, value, value, value);

    /// <summary>Sets margins using the specified unit.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public PageDescriptor Margin(double value, Unit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        double pt = UnitConversion.ToPoints(value, unit);
        return Margin(pt, pt, pt, pt);
    }

    /// <summary>Sets vertical (top + bottom) and horizontal (left + right) margins in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Any value is negative.</exception>
    public PageDescriptor MarginVertical(double vertical)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(vertical);
        return Margin(vertical, MarginRight, vertical, MarginLeft);
    }

    /// <exception cref="ArgumentOutOfRangeException">Any value is negative.</exception>
    public PageDescriptor MarginHorizontal(double horizontal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horizontal);
        return Margin(MarginTop, horizontal, MarginBottom, horizontal);
    }

    /// <summary>Sets all four margins individually in PDF points.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Any margin value is negative.</exception>
    public PageDescriptor Margin(double top, double right, double bottom, double left)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(top,    nameof(top));
        ArgumentOutOfRangeException.ThrowIfNegative(right,  nameof(right));
        ArgumentOutOfRangeException.ThrowIfNegative(bottom, nameof(bottom));
        ArgumentOutOfRangeException.ThrowIfNegative(left,   nameof(left));
        MarginTop    = top;
        MarginRight  = right;
        MarginBottom = bottom;
        MarginLeft   = left;
        return this;
    }

    // -- Page colour -----------------------------------------------

    /// <summary>Fills the page background with the given hex colour.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public PageDescriptor PageColor(string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        BackgroundColor = PdfColor.FromHex(hexColor);
        return this;
    }

    // -- Default text style ----------------------------------------

    /// <summary>Applies a transformation to the page-wide default text style.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public PageDescriptor DefaultTextStyle(Func<TextStyle, TextStyle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        DefaultStyle = configure(DefaultStyle);
        return this;
    }

    // -- Layout slots ----------------------------------------------

    /// <summary>Returns the header container slot.</summary>
    public IContainer Header()  => HeaderSlot;

    /// <summary>Returns the main content container slot.</summary>
    public IContainer Content() => ContentSlot;

    /// <summary>Returns the footer container slot.</summary>
    public IContainer Footer()  => FooterSlot;

    /// <summary>
    /// Restricts the header to the first PDF page of this descriptor only.
    /// Continuation pages produced by content overflow or explicit page-breaks
    /// will not draw the header, and the space it would have occupied becomes
    /// available for content.
    /// </summary>
    public PageDescriptor HeaderOnFirstPageOnly()
    {
        HeaderFirstPageOnly = true;
        return this;
    }
}
