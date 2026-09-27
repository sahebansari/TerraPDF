using System.Globalization;
using TerraPDF.Drawing;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// <see cref="PdfReal"/> must write exactly what <c>double.ToString("F2"/"F4", InvariantCulture)</c>
/// writes, since content streams used to be produced with those format strings.
/// </summary>
public sealed class PdfRealTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.005)]
    [InlineData(-0.005)]
    [InlineData(0.125)]          // exact binary tie: rounds away from zero
    [InlineData(2.675)]          // just below a decimal tie
    [InlineData(1.005)]
    [InlineData(-0.001)]         // rounds to "-0.00"
    [InlineData(99999.995)]
    [InlineData(100000.0)]       // at the fast-path limit
    [InlineData(1e15)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(841.89)]
    [InlineData(0.3333333333333333)]
    public void MatchesFixedPointFormatting(double value) => AssertSame(value);

    [Fact]
    public void NegativeZeroKeepsItsSign() => AssertSame(-0.0);   // "-0.00", like F2

    [Fact]
    public void MatchesFixedPointFormattingOnRandomValues()
    {
        var random = new Random(20260926);
        for (int i = 0; i < 200_000; i++)
        {
            AssertSame((random.NextDouble() - 0.5) * 2000);                                  // coordinates
            AssertSame(random.NextDouble());                                                  // colours
            AssertSame(Math.Round((random.NextDouble() - 0.5) * 20000) / 1000 + 5e-4);        // near ties
            AssertSame(BitConverter.Int64BitsToDouble(random.NextInt64()));                   // any bit pattern
        }
    }

    private static void AssertSame(double value)
    {
        Assert.Equal(value.ToString("F2", CultureInfo.InvariantCulture), PdfReal.F2(value).ToString());
        Assert.Equal(value.ToString("F4", CultureInfo.InvariantCulture), PdfReal.F4(value).ToString());
    }
}
