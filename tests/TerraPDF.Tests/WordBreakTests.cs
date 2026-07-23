using TerraPDF.Drawing;
using TerraPDF.Elements;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for character-level breaking of words wider than the available width:
/// no line produced by <see cref="TextBlock"/> may exceed the container, so
/// unbreakable words can never paint into padding or borders.
/// </summary>
public sealed class WordBreakTests
{
    private static double LineWidth(TextBlock.WrappedLine line) =>
        line.Tokens.Sum(t => FontMetrics.MeasureWidth(
            t.Text, t.Style.Size ?? 12, PdfFonts.Resolve(t.Style.Family),
            t.Style.IsBold ?? false, t.Style.IsItalic ?? false));

    private static string Concatenate(IEnumerable<TextBlock.WrappedLine> lines) =>
        string.Concat(lines.SelectMany(l => l.Tokens).Select(t => t.Text));

    [Fact]
    public void OversizedWordIsBrokenAcrossLines()
    {
        const string word = "Supercalifragilisticexpialidocious";
        const double width = 50;

        var block = new TextBlock(word);
        var (lines, _, _) = block.LayoutLines(width, null, 1);

        Assert.True(lines.Count > 1, "A word wider than the line must span multiple lines.");
        Assert.All(lines, l => Assert.True(LineWidth(l) <= width,
            $"Line \"{Concatenate([l])}\" is {LineWidth(l):F2}pt wide, exceeding {width}pt."));
        Assert.Equal(word, Concatenate(lines));
    }

    [Fact]
    public void FollowingWordPacksAfterLastFragment()
    {
        // The trailing short word must not be forced onto its own line when it
        // fits after the oversized word's final fragment.
        const string text  = "Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa on";
        const double width = 100;

        var block = new TextBlock(text);
        var (lines, _, _) = block.LayoutLines(width, null, 1);

        Assert.All(lines, l => Assert.True(LineWidth(l) <= width));
        Assert.Contains("on", lines[^1].Tokens[^1].Text);
        Assert.True(lines[^1].Tokens.Count > 1,
            "The short trailing word should share a line with the last fragment.");
    }

    [Fact]
    public void MeasureNeverExceedsAvailableWidth()
    {
        const double width = 60;
        var block = new TextBlock("Donaudampfschifffahrtsgesellschaftskapitän short words");
        var size = block.Measure(width, 800);

        Assert.True(size.Width <= width,
            $"Measured width {size.Width:F2}pt exceeds the available {width}pt.");
    }

    [Fact]
    public void WidthNarrowerThanOneGlyphStillMakesProgress()
    {
        // Each fragment must carry at least one character, so a pathologically
        // narrow line yields one character per line rather than looping forever.
        const string word = "abcdef";

        var block = new TextBlock(word);
        var (lines, _, _) = block.LayoutLines(1, null, 1);

        Assert.Equal(word.Length, lines.Count);
        Assert.Equal(word, Concatenate(lines));
    }

    [Fact]
    public void NormalWrappingKeepsWholeWords()
    {
        var block = new TextBlock("alpha beta gamma delta epsilon");
        var (lines, _, _) = block.LayoutLines(80, null, 1);

        var words = lines.SelectMany(l => l.Tokens)
                         .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                         .Select(t => t.Text);
        Assert.Equal(["alpha", "beta", "gamma", "delta", "epsilon"], words);
    }
}
