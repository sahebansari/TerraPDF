namespace TerraPDF.Helpers;

/// <summary>
/// Material Design-inspired color palette.
/// Colors are expressed as CSS hex strings compatible with <c>PdfColor.FromHex</c>.
/// </summary>
public static class Color
{
    public static readonly string Transparent = "#00000000";
    public static readonly string Black       = "#000000";
    public static readonly string White       = "#FFFFFF";

    public static class Red
    {
        public static readonly string Lighten5 = "#FFEBEE";
        public static readonly string Lighten4 = "#FFCDD2";
        public static readonly string Lighten3 = "#EF9A9A";
        public static readonly string Lighten2 = "#E57373";
        public static readonly string Lighten1 = "#EF5350";
        public static readonly string Medium   = "#F44336";
        public static readonly string Darken1  = "#E53935";
        public static readonly string Darken2  = "#D32F2F";
        public static readonly string Darken3  = "#C62828";
        public static readonly string Darken4  = "#B71C1C";
    }

    public static class Pink
    {
        public static readonly string Medium  = "#E91E63";
        public static readonly string Darken2 = "#C2185B";
    }

    public static class Purple
    {
        public static readonly string Medium  = "#9C27B0";
        public static readonly string Darken2 = "#7B1FA2";
    }

    public static class DeepPurple
    {
        public static readonly string Medium  = "#673AB7";
        public static readonly string Darken2 = "#512DA8";
    }

    public static class Indigo
    {
        public static readonly string Lighten5 = "#E8EAF6";
        public static readonly string Medium   = "#3F51B5";
        public static readonly string Darken2  = "#283593";
    }

    public static class Blue
    {
        public static readonly string Lighten5 = "#E3F2FD";
        public static readonly string Lighten4 = "#BBDEFB";
        public static readonly string Lighten3 = "#90CAF9";
        public static readonly string Lighten2 = "#64B5F6";
        public static readonly string Lighten1 = "#42A5F5";
        public static readonly string Medium   = "#2196F3";
        public static readonly string Darken1  = "#1E88E5";
        public static readonly string Darken2  = "#1976D2";
        public static readonly string Darken3  = "#1565C0";
        public static readonly string Darken4  = "#0D47A1";
    }

    public static class LightBlue
    {
        public static readonly string Medium  = "#03A9F4";
        public static readonly string Darken2 = "#0288D1";
    }

    public static class Cyan
    {
        public static readonly string Medium  = "#00BCD4";
        public static readonly string Darken2 = "#0097A7";
    }

    public static class Teal
    {
        public static readonly string Medium  = "#009688";
        public static readonly string Darken2 = "#00796B";
    }

    public static class Green
    {
        public static readonly string Lighten5 = "#E8F5E9";
        public static readonly string Lighten4 = "#C8E6C9";
        public static readonly string Lighten3 = "#A5D6A7";
        public static readonly string Lighten2 = "#81C784";
        public static readonly string Lighten1 = "#66BB6A";
        public static readonly string Medium   = "#4CAF50";
        public static readonly string Darken1  = "#43A047";
        public static readonly string Darken2  = "#388E3C";
        public static readonly string Darken3  = "#2E7D32";
        public static readonly string Darken4  = "#1B5E20";
    }

    public static class LightGreen
    {
        public static readonly string Medium  = "#8BC34A";
        public static readonly string Darken2 = "#689F38";
    }

    public static class Lime
    {
        public static readonly string Medium  = "#CDDC39";
        public static readonly string Darken2 = "#AFB42B";
    }

    public static class Yellow
    {
        public static readonly string Medium  = "#FFEB3B";
        public static readonly string Darken2 = "#F9A825";
    }

    public static class Amber
    {
        public static readonly string Medium  = "#FFC107";
        public static readonly string Darken2 = "#FF8F00";
    }

    public static class Orange
    {
        public static readonly string Medium  = "#FF9800";
        public static readonly string Darken2 = "#E65100";
    }

    public static class DeepOrange
    {
        public static readonly string Medium  = "#FF5722";
        public static readonly string Darken2 = "#BF360C";
    }

    public static class Brown
    {
        public static readonly string Lighten5 = "#EFEBE9";
        public static readonly string Medium   = "#795548";
        public static readonly string Darken2  = "#4E342E";
    }

    public static class Grey
    {
        public static readonly string Lighten5 = "#FAFAFA";
        public static readonly string Lighten4 = "#F5F5F5";
        public static readonly string Lighten3 = "#EEEEEE";
        public static readonly string Lighten2 = "#E0E0E0";
        public static readonly string Lighten1 = "#BDBDBD";
        public static readonly string Medium   = "#9E9E9E";
        public static readonly string Darken1  = "#757575";
        public static readonly string Darken2  = "#616161";
        public static readonly string Darken3  = "#424242";
        public static readonly string Darken4  = "#212121";
    }

    public static class BlueGrey
    {
        public static readonly string Lighten5 = "#ECEFF1";
        public static readonly string Lighten4 = "#CFD8DC";
        public static readonly string Medium   = "#607D8B";
        public static readonly string Darken2  = "#37474F";
        public static readonly string Darken4  = "#263238";
    }
}

/// <summary>
/// Internal RGB color value used by the rendering layer.
/// Use <see cref="Color"/> for named color constants, then parse them with <see cref="FromHex"/>.
/// </summary>
public readonly struct PdfColor
{
    public double R { get; init; }
    public double G { get; init; }
    public double B { get; init; }

    public static PdfColor FromRgb(byte r, byte g, byte b) =>
        new() { R = r / 255.0, G = g / 255.0, B = b / 255.0 };

    /// <summary>
    /// Parses a CSS hex color string. Accepts 6-digit (#RRGGBB) and 8-digit (#RRGGBBAA)
    /// formats; the alpha channel is silently ignored when present.
    /// </summary>
    public static PdfColor FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        hex = hex.TrimStart('#');
        if (hex.Length != 6 && hex.Length != 8)
            throw new ArgumentException(
                $"Hex color must be 6 or 8 characters after the '#' prefix (got {hex.Length}).",
                nameof(hex));
        return FromRgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
