using TerraPDF.Elements;
using TerraPDF.Infra;
using TerraPDF.Helpers;

namespace TerraPDF.Core;

/// <summary>
/// Extension methods on <see cref="IContainer"/> that mirror the fluent API.
/// Every method creates an element, attaches it to the container, and returns either
/// a new <see cref="IContainer"/> (for further decoration) or a specific descriptor.
/// </summary>
public static class ContainerExtensions
{
    // -- Internal helpers ------------------------------------------

    // Casts IContainer to the concrete Container type; throws if the caller
    // passed a foreign IContainer implementation not produced by this library.
    private static Container Slot(this IContainer c) =>
        c as Container
            ?? throw new InvalidOperationException(
                $"IContainer implementation {c.GetType().Name} is not a Container. " +
                "Only containers returned by the TerraPDF API support chaining.");

    // Attaches `element` as the container's child and returns `inner` as the next
    // chaining target (decorator pattern: outer wraps inner, caller writes to inner).
    private static Container SetAndReturnInner(this IContainer container, Element element, Container inner)
    {
        container.Slot().Child = element;
        return inner;
    }

    // -- Text ------------------------------------------------------

    /// <summary>Places a text element with a single literal string.</summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or whitespace.</exception>
    public static TextDescriptor Text(this IContainer container, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var element = new TextBlock(text);
        container.Slot().Child = element;
        return new TextDescriptor(element);
    }

    /// <summary>Places a text element composed from multiple spans.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="compose"/> is <c>null</c>.</exception>
    public static TextDescriptor Text(this IContainer container, Action<TextDescriptor> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        var element    = new TextBlock();
        var descriptor = new TextDescriptor(element);
        compose(descriptor);
        container.Slot().Child = element;
        return descriptor;
    }

    // -- Layout ----------------------------------------------------

    /// <summary>Places a vertical column layout.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public static IContainer Column(this IContainer container, Action<ColumnDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var element    = new Column();
        var descriptor = new ColumnDescriptor(element);
        configure(descriptor);
        container.Slot().Child = element;
        return container;
    }

    /// <summary>Places a horizontal row layout.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public static IContainer Row(this IContainer container, Action<RowDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var element    = new Row();
        var descriptor = new RowDescriptor(element);
        configure(descriptor);
        container.Slot().Child = element;
        return container;
    }

    /// <summary>Places a table layout.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public static IContainer Table(this IContainer container, Action<TableDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var element    = new Table();
        var descriptor = new TableDescriptor(element);
        configure(descriptor);
        container.Slot().Child = element;
        return container;
    }

    // -- Decorators (return new inner slot for chaining) -----------

    /// <summary>Adds equal padding on all sides.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer Padding(this IContainer container, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var element = new Padding { Top = value, Right = value, Bottom = value, Left = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds padding using a specified unit.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer Padding(this IContainer container, double value, Unit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        double pt = UnitConversion.ToPoints(value, unit);
        return container.Padding(pt);
    }

    /// <summary>Adds vertical (top + bottom) padding.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer PaddingVertical(this IContainer container, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var element = new Padding { Top = value, Bottom = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds vertical padding using a specified unit.</summary>
    public static IContainer PaddingVertical(this IContainer container, double value, Unit unit) =>
        container.PaddingVertical(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds horizontal (left + right) padding.</summary>
    public static IContainer PaddingHorizontal(this IContainer container, double value)
    {
        var element = new Padding { Left = value, Right = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds horizontal padding using a specified unit.</summary>
    public static IContainer PaddingHorizontal(this IContainer container, double value, Unit unit) =>
        container.PaddingHorizontal(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds padding only to the top.</summary>
    public static IContainer PaddingTop(this IContainer container, double value)
    {
        var element = new Padding { Top = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds padding only to the bottom.</summary>
    public static IContainer PaddingBottom(this IContainer container, double value)
    {
        var element = new Padding { Bottom = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds padding only to the left.</summary>
    public static IContainer PaddingLeft(this IContainer container, double value)
    {
        var element = new Padding { Left = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds padding only to the right.</summary>
    public static IContainer PaddingRight(this IContainer container, double value)
    {
        var element = new Padding { Right = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    // -- Margin ----------------------------------------------------

    /// <summary>
    /// Adds equal margin on all sides.
    /// Margin is <em>outer</em> spacing: any Background or Border applied after this
    /// call renders inside the already-reduced area, leaving the margin region transparent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer Margin(this IContainer container, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var element = new Margin { Top = value, Right = value, Bottom = value, Left = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds equal margin on all sides using a specified unit.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer Margin(this IContainer container, double value, Unit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        double pt = UnitConversion.ToPoints(value, unit);
        return container.Margin(pt);
    }

    /// <summary>Adds vertical (top + bottom) margin.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static IContainer MarginVertical(this IContainer container, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var element = new Margin { Top = value, Bottom = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds vertical margin using a specified unit.</summary>
    public static IContainer MarginVertical(this IContainer container, double value, Unit unit) =>
        container.MarginVertical(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds horizontal (left + right) margin.</summary>
    public static IContainer MarginHorizontal(this IContainer container, double value)
    {
        var element = new Margin { Left = value, Right = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds horizontal margin using a specified unit.</summary>
    public static IContainer MarginHorizontal(this IContainer container, double value, Unit unit) =>
        container.MarginHorizontal(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds margin only to the top.</summary>
    public static IContainer MarginTop(this IContainer container, double value)
    {
        var element = new Margin { Top = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds margin only to the top using a specified unit.</summary>
    public static IContainer MarginTop(this IContainer container, double value, Unit unit) =>
        container.MarginTop(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds margin only to the bottom.</summary>
    public static IContainer MarginBottom(this IContainer container, double value)
    {
        var element = new Margin { Bottom = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds margin only to the bottom using a specified unit.</summary>
    public static IContainer MarginBottom(this IContainer container, double value, Unit unit) =>
        container.MarginBottom(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds margin only to the left.</summary>
    public static IContainer MarginLeft(this IContainer container, double value)
    {
        var element = new Margin { Left = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds margin only to the left using a specified unit.</summary>
    public static IContainer MarginLeft(this IContainer container, double value, Unit unit) =>
        container.MarginLeft(UnitConversion.ToPoints(value, unit));

    /// <summary>Adds margin only to the right.</summary>
    public static IContainer MarginRight(this IContainer container, double value)
    {
        var element = new Margin { Right = value };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Adds margin only to the right using a specified unit.</summary>
    public static IContainer MarginRight(this IContainer container, double value, Unit unit) =>
        container.MarginRight(UnitConversion.ToPoints(value, unit));

    /// <summary>Fills the background with the given hex colour.</summary>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer Background(this IContainer container, string hexColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new Background { Color = PdfColor.FromHex(hexColor) };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Draws a border of the given width and colour.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer Border(this IContainer container, double lineWidth, string hexColor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new Border
        {
            LineWidth = lineWidth,
            Color     = PdfColor.FromHex(hexColor),
        };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Draws a border using the default colour (black).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    public static IContainer Border(this IContainer container, double lineWidth = 1) =>
        container.Border(lineWidth, "#000000");

    // -- Rounded border --------------------------------------------

    /// <summary>
    /// Draws a rounded-corner border around the child content.
    /// </summary>
    /// <param name="container">The container slot to decorate.</param>
    /// <param name="radius">Corner radius in PDF points. Clamped to half the shorter side automatically.</param>
    /// <param name="lineWidth">Stroke width in PDF points.</param>
    /// <param name="hexColor">Border colour as a hex string (e.g. <c>"#1a4a8a"</c> or <c>Color.Blue.Darken2</c>). Defaults to black.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer RoundedBorder(this IContainer container,
        double radius = 8, double lineWidth = 1, string hexColor = "#000000")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new RoundedBorder
        {
            Radius    = radius,
            LineWidth = lineWidth,
            Color     = PdfColor.FromHex(hexColor),
        };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>
    /// Draws a filled rounded-corner box (background + border) around the child content.
    /// </summary>
    /// <param name="container">The container slot to decorate.</param>
    /// <param name="radius">Corner radius in PDF points.</param>
    /// <param name="fillHexColor">Fill colour as a hex string.</param>
    /// <param name="borderHexColor">Border colour as a hex string. Defaults to black.</param>
    /// <param name="lineWidth">Stroke width in PDF points.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> or <paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException">Any colour argument is null or whitespace.</exception>
    public static IContainer RoundedBox(this IContainer container,
        double radius = 8, string fillHexColor = "#FFFFFF",
        string borderHexColor = "#000000", double lineWidth = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(fillHexColor);
        ArgumentException.ThrowIfNullOrWhiteSpace(borderHexColor);
        var element = new RoundedBorder
        {
            Radius    = radius,
            LineWidth = lineWidth,
            Color     = PdfColor.FromHex(borderHexColor),
            FillColor = PdfColor.FromHex(fillHexColor),
        };
        return container.SetAndReturnInner(element, element.Inner);
    }

    // -- Partial (per-edge) borders --------------------------------

    /// <summary>Draws a border only on the top edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer BorderTop(this IContainer container, double lineWidth, string hexColor = "#000000")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new PartialBorder { TopWidth = lineWidth, TopColor = PdfColor.FromHex(hexColor) };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Draws a border only on the bottom edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer BorderBottom(this IContainer container, double lineWidth, string hexColor = "#000000")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new PartialBorder { BottomWidth = lineWidth, BottomColor = PdfColor.FromHex(hexColor) };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Draws a border only on the left edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer BorderLeft(this IContainer container, double lineWidth, string hexColor = "#000000")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new PartialBorder { LeftWidth = lineWidth, LeftColor = PdfColor.FromHex(hexColor) };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Draws a border only on the right edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lineWidth"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="hexColor"/> is null or whitespace.</exception>
    public static IContainer BorderRight(this IContainer container, double lineWidth, string hexColor = "#000000")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexColor);
        var element = new PartialBorder { RightWidth = lineWidth, RightColor = PdfColor.FromHex(hexColor) };
        return container.SetAndReturnInner(element, element.Inner);
    }

    // -- Alignment -------------------------------------------------

    /// <summary>Horizontally centres the child within the available width.</summary>
    public static IContainer AlignCenter(this IContainer container)
    {
        var element = new Alignment { Horizontal = HorizontalAlignment.Center };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Aligns the child to the right.</summary>
    public static IContainer AlignRight(this IContainer container)
    {
        var element = new Alignment { Horizontal = HorizontalAlignment.Right };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Aligns the child to the left (default).</summary>
    public static IContainer AlignLeft(this IContainer container)
    {
        var element = new Alignment { Horizontal = HorizontalAlignment.Left };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Vertically centres the child within the available height.</summary>
    public static IContainer AlignMiddle(this IContainer container)
    {
        var element = new Alignment { Vertical = VerticalAlignment.Middle };
        return container.SetAndReturnInner(element, element.Inner);
    }

    /// <summary>Aligns the child to the bottom of the available height.</summary>
    public static IContainer AlignBottom(this IContainer container)
    {
        var element = new Alignment { Vertical = VerticalAlignment.Bottom };
        return container.SetAndReturnInner(element, element.Inner);
    }

    // -- Lines -----------------------------------------------------

    /// <summary>Places a horizontal rule line.</summary>
    public static IContainer LineHorizontal(this IContainer container,
        double lineWidth = 1, string hexColor = "#000000")
    {
        container.Slot().Child = new Line
        {
            Vertical  = false,
            LineWidth = lineWidth,
            Color     = PdfColor.FromHex(hexColor),
        };
        return container;
    }

    /// <summary>Places a vertical rule line.</summary>
    public static IContainer LineVertical(this IContainer container,
        double lineWidth = 1, string hexColor = "#000000")
    {
        container.Slot().Child = new Line
        {
            Vertical  = true,
            LineWidth = lineWidth,
            Color     = PdfColor.FromHex(hexColor),
        };
        return container;
    }

    // -- Component -------------------------------------------------

    /// <summary>Injects a reusable <see cref="IComponent"/> into this slot.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is <c>null</c>.</exception>
    public static IContainer Component(this IContainer container, IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        component.Compose(container);
        return container;
    }

    // -- Image -----------------------------------------------------

    /// <summary>
    /// Places a PNG or JPEG image.
    /// The image scales to fill the available width while preserving its aspect ratio.
    /// Supports .png, .jpg, and .jpeg files.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null or whitespace.</exception>
    public static IContainer Image(this IContainer container, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        container.Slot().Child = new ImageElement(filePath);
        return container;
    }

    /// <summary>
    /// Places a PNG or JPEG image constrained to <paramref name="width"/> PDF points.
    /// The image will not exceed this width; wrap in <c>AlignCenter()</c> or <c>AlignRight()</c>
    /// to position it within the available space.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is zero or negative.</exception>
    public static IContainer Image(this IContainer container, string filePath, double width)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        container.Slot().Child = new ImageElement(filePath, width);
        return container;
    }

    // -- Show-if ---------------------------------------------------

    /// <summary>Renders child content only when <paramref name="condition"/> is <c>true</c>.</summary>
    public static IContainer ShowIf(this IContainer container, bool condition)
    {
        if (!condition)
        {
            container.Slot().Child = new Empty();
        }
        return container;
    }

    // -- Page break ------------------------------------------------

    /// <summary>
    /// Places an explicit page-break marker in this container slot.
    /// When the slot is a direct child of a <c>Column</c>, the pagination engine
    /// starts a new PDF page at this position.
    /// If the break falls at the very start of a page it is silently skipped.
    /// </summary>
    public static IContainer PageBreak(this IContainer container)
    {
        container.Slot().Child = new PageBreak();
        return container;
    }

    // -- Hyperlink -------------------------------------------------

    /// <summary>
    /// Wraps child content in a clickable PDF URI annotation.
    /// Clicking the rendered area in a PDF viewer navigates to <paramref name="url"/>.
    /// </summary>
    /// <param name="container">The container slot to attach the hyperlink to.</param>
    /// <param name="url">The destination URI, e.g. <c>"https://example.com"</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="url"/> is null or whitespace.</exception>
    public static IContainer Hyperlink(this IContainer container, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var element = new Link { Url = url };
        return container.SetAndReturnInner(element, element.Inner);
    }
}

/// <summary>A no-op element used as a placeholder when content is hidden.</summary>
internal sealed class Empty : Element
{
    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) => new(0, 0);
    internal override void Draw(DrawingContext ctx) { }
}
