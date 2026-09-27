using System.Globalization;

namespace TerraPDF.Drawing;

/// <summary>
/// A PDF real number written with a fixed number of decimals, producing exactly the text of
/// <c>value.ToString("F2")</c> / <c>"F4"</c> with the invariant culture, but without the
/// general-purpose floating-point formatter in the common case.
/// </summary>
/// <remarks>
/// Content streams hold thousands of coordinates, and formatting them was a large share of
/// rendering time. The value is scaled and rounded in integer arithmetic. Below 1e7 the scaled
/// double is within half an ulp (under 1e-9) of the exact product, so whenever it is not within
/// 1e-7 of a rounding tie (x.5) it rounds exactly as the exact decimal expansion does; near a
/// tie, from 1e7 up (|x| ≥ 100,000 for F2, ≥ 1,000 for F4) and for NaN/infinity it defers to <see cref="double.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>.
/// Used as <c>{PdfReal.F2(x)}</c> inside <c>StringBuilder.Append(CultureInfo.InvariantCulture, $"…")</c>,
/// which calls <see cref="ISpanFormattable.TryFormat"/> directly without boxing.
/// </remarks>
internal readonly struct PdfReal : ISpanFormattable
{
    private readonly double _value;
    private readonly int _decimals;

    private PdfReal(double value, int decimals)
    {
        _value = value;
        _decimals = decimals;
    }

    /// <summary>Two decimals, as used for coordinates and sizes ("72.00").</summary>
    internal static PdfReal F2(double value) => new(value, 2);

    /// <summary>Four decimals, as used for colour components ("0.5020").</summary>
    internal static PdfReal F4(double value) => new(value, 4);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        long scale = _decimals == 2 ? 100 : 10_000;
        double scaled = Math.Abs(_value) * scale;

        // Fast path only where rounding the scaled double provably matches rounding the exact value.
        if (!(scaled < 1e7) || Math.Abs(scaled - Math.Floor(scaled) - 0.5) < 1e-7)
            return _value.TryFormat(destination, out charsWritten, _decimals == 2 ? "F2" : "F4", CultureInfo.InvariantCulture);

        long units = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
        long whole = units / scale;
        long fraction = units % scale;

        // .NET writes a sign for every negative value, including one that rounds to zero ("-0.00").
        bool negative = double.IsNegative(_value);
        int wholeDigits = CountDigits(whole);
        int length = (negative ? 1 : 0) + wholeDigits + 1 + _decimals;
        if (destination.Length < length)
        {
            charsWritten = 0;
            return false;
        }

        int pos = 0;
        if (negative) destination[pos++] = '-';
        for (int i = wholeDigits - 1; i >= 0; i--, whole /= 10)
            destination[pos + i] = (char)('0' + whole % 10);
        pos += wholeDigits;
        destination[pos++] = '.';
        for (int i = _decimals - 1; i >= 0; i--, fraction /= 10)
            destination[pos + i] = (char)('0' + fraction % 10);

        charsWritten = length;
        return true;
    }

    private static int CountDigits(long value)
    {
        int digits = 1;
        while (value >= 10) { value /= 10; digits++; }
        return digits;
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[32];
        return TryFormat(buffer, out int written, default, formatProvider)
            ? new string(buffer[..written])
            : _value.ToString(_decimals == 2 ? "F2" : "F4", CultureInfo.InvariantCulture);
    }

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);
}
