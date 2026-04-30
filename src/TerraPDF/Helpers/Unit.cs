namespace TerraPDF.Helpers;

/// <summary>Measurement units supported by the layout API.</summary>
public enum Unit
{
    /// <summary>PDF points - the native unit (1 pt = 1/72 inch).</summary>
    Point,
    /// <summary>Millimetres (1 mm ≈ 2.835 pt).</summary>
    Millimetre,
    /// <summary>Centimetres (1 cm ≈ 28.35 pt).</summary>
    Centimetre,
    /// <summary>Inches (1 in = 72 pt).</summary>
    Inch,
}

/// <summary>Conversion helpers for <see cref="Unit"/>.</summary>
public static class UnitConversion
{
    /// <summary>Converts a value from the given unit to PDF points.</summary>
    public static double ToPoints(double value, Unit unit) => unit switch
    {
        Unit.Point      => value,
        Unit.Millimetre => value * 2.8346,
        Unit.Centimetre => value * 28.346,
        Unit.Inch       => value * 72.0,
        _               => value,
    };
}
