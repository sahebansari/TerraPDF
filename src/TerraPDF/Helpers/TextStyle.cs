namespace TerraPDF.Helpers;

/// <summary>Horizontal alignment for a text block.</summary>
public enum TextAlignment { Left, Center, Right, Justify }

/// <summary>
/// Immutable text style. All mutator methods return a new instance.
/// Null properties inherit from context (page default or parent).
/// </summary>
public sealed class TextStyle
{
    internal double?       Size       { get; private set; }
    internal string?       Color      { get; private set; }
    internal string?       Family     { get; private set; }
    internal bool?         IsBold          { get; private set; }
    internal bool?         IsItalic        { get; private set; }
    internal bool?   IsUnderline         { get; private set; }
    internal bool?   IsStrikethrough { get; private set; }
    internal TextAlignment? Alignment       { get; private set; }
    /// <summary>Line-height multiplier (e.g. 1.4 = 140% of font size). Defaults to 1.4 when null.</summary>
    internal double? LineHeightMultiplier { get; private set; }

    // -- Fluent mutators --------------------------------------------

    public TextStyle FontSize(double size)         => Clone(x => x.Size   = size);
    public TextStyle FontColor(string hexColor)    => Clone(x => x.Color  = hexColor);
    public TextStyle FontFamily(string family)     => Clone(x => x.Family = family);
    public TextStyle Bold()                        => Clone(x => x.IsBold     = true);
    public TextStyle SemiBold()                    => Clone(x => x.IsBold     = true);
    public TextStyle NormalWeight()                => Clone(x => x.IsBold     = false);
    public TextStyle Italic()                      => Clone(x => x.IsItalic        = true);
    public TextStyle NormalStyle()                 => Clone(x => x.IsItalic        = false);
    public TextStyle Strikethrough()               => Clone(x => x.IsStrikethrough = true);
    public TextStyle NoStrikethrough()             => Clone(x => x.IsStrikethrough = false);
    public TextStyle Underline()                   => Clone(x => x.IsUnderline      = true);
    public TextStyle NoUnderline()                 => Clone(x => x.IsUnderline      = false);
    public TextStyle LineHeight(double multiplier) => Clone(x => x.LineHeightMultiplier = multiplier);
    public TextStyle AlignLeft()                   => Clone(x => x.Alignment = TextAlignment.Left);
    public TextStyle AlignCenter()                 => Clone(x => x.Alignment = TextAlignment.Center);
    public TextStyle AlignRight()                  => Clone(x => x.Alignment = TextAlignment.Right);
    public TextStyle Justify()                     => Clone(x => x.Alignment = TextAlignment.Justify);

    // -- Merging ----------------------------------------------------

    /// <summary>Returns a new style with non-null properties of <paramref name="override"/> applied on top.</summary>
    internal TextStyle MergeWith(TextStyle? @override)
    {
        if (@override is null) return this;
        return new TextStyle
        {
            Size     = @override.Size     ?? Size,
            Color    = @override.Color    ?? Color,
            Family   = @override.Family   ?? Family,
            IsBold          = @override.IsBold          ?? IsBold,
            IsItalic        = @override.IsItalic        ?? IsItalic,
            IsStrikethrough = @override.IsStrikethrough ?? IsStrikethrough,
            IsUnderline     = @override.IsUnderline     ?? IsUnderline,
            Alignment       = @override.Alignment       ?? Alignment,
            LineHeightMultiplier = @override.LineHeightMultiplier ?? LineHeightMultiplier,
        };
    }

    // -- Defaults ---------------------------------------------------

    /// <summary>Base default style (Helvetica, 12pt, black).</summary>
    internal static TextStyle Default => new()
    {
        Size     = 12,
        Color    = "#000000",
        Family   = "Helvetica",
        IsBold          = false,
        IsItalic        = false,
        IsStrikethrough = false,
        Alignment       = TextAlignment.Left,
    };

    // -- Helpers ----------------------------------------------------

    private TextStyle Clone(Action<TextStyle> configure)
    {
        // Copy-on-write: copy all fields then apply the single mutation
        var copy = new TextStyle
        {
            Size     = Size,
            Color    = Color,
            Family   = Family,
            IsBold          = IsBold,
            IsItalic        = IsItalic,
            IsStrikethrough = IsStrikethrough,
            IsUnderline     = IsUnderline,
            Alignment       = Alignment,
            LineHeightMultiplier = LineHeightMultiplier,
        };
        configure(copy);
        return copy;
    }
}
