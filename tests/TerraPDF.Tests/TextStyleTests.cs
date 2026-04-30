using TerraPDF.Helpers;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Unit tests for <see cref="TextStyle"/> merge and fluent mutation behaviour.
/// </summary>
public sealed class TextStyleTests
{
    [Fact]
    public void DefaultHasExpectedValues()
    {
        var style = TextStyle.Default;
        Assert.Equal(12,         style.Size);
        Assert.Equal("#000000",  style.Color);
        Assert.False(style.IsBold);
        Assert.False(style.IsItalic);
    }

    [Fact]
    public void BoldSetsIsBoldTrue()
    {
        var style = TextStyle.Default.Bold();
        Assert.True(style.IsBold);
    }

    [Fact]
    public void ItalicSetsIsItalicTrue()
    {
        var style = TextStyle.Default.Italic();
        Assert.True(style.IsItalic);
    }

    [Fact]
    public void NormalWeightClearsBold()
    {
        var style = TextStyle.Default.Bold().NormalWeight();
        Assert.False(style.IsBold);
    }

    [Fact]
    public void NormalStyleClearsItalic()
    {
        var style = TextStyle.Default.Italic().NormalStyle();
        Assert.False(style.IsItalic);
    }

    [Fact]
    public void FontSizeSetsSize()
    {
        var style = TextStyle.Default.FontSize(18);
        Assert.Equal(18, style.Size);
    }

    [Fact]
    public void FontColorSetsColor()
    {
        var style = TextStyle.Default.FontColor("#FF0000");
        Assert.Equal("#FF0000", style.Color);
    }

    [Fact]
    public void MergeWithNullReturnsOriginal()
    {
        var original = TextStyle.Default.Bold();
        var merged   = original.MergeWith(null);
        Assert.True(merged.IsBold);
        Assert.Equal(original.Size, merged.Size);
    }

    [Fact]
    public void MergeWithOverrideWinsOnNonNullProperties()
    {
        var @base     = TextStyle.Default;                          // size=12, bold=false
        var @override = TextStyle.Default.FontSize(20).Bold();     // size=20, bold=true
        var merged    = @base.MergeWith(@override);
        Assert.Equal(20,  merged.Size);
        Assert.True(merged.IsBold);
    }

    [Fact]
    public void MergeWithNullOverridePropertiesFallBackToBase()
    {
        var @base     = TextStyle.Default.FontSize(16).Bold();  // base has size=16
        var @override = new TextStyle();                        // completely empty
        var merged    = @base.MergeWith(@override);
        Assert.Equal(16,  merged.Size);  // inherited from base
        Assert.True(merged.IsBold);      // inherited from base
    }

    [Fact]
    public void FluentChainDoesNotMutateOriginal()
    {
        var original = TextStyle.Default;
        var modified = original.Bold().Italic().FontSize(24);

        // original must be unchanged (immutability guarantee)
        Assert.False(original.IsBold);
        Assert.False(original.IsItalic);
        Assert.Equal(12, original.Size);

        Assert.True(modified.IsBold);
        Assert.True(modified.IsItalic);
        Assert.Equal(24, modified.Size);
    }
}
