using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for TextBlock self-splitting: paragraphs taller than the remaining
/// page flow across pages, split between wrapped lines by the fragment engine.
/// </summary>
public sealed class TextSplittingTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static int CountPages(byte[] pdf)
    {
        string text = System.Text.Encoding.Latin1.GetString(pdf);
        int count = 0, idx = 0;
        while ((idx = text.IndexOf("/Type /Page /", idx, StringComparison.Ordinal)) >= 0) { count++; idx++; }
        return count;
    }

    /// <summary>A paragraph long enough to overflow at least two A4 pages.</summary>
    private static string LongParagraph(out string first, out string last)
    {
        first = "STARTSENTINEL";
        last  = "ENDSENTINEL";
        var words = new System.Text.StringBuilder(first);
        for (int i = 0; i < 1200; i++)
            words.Append(" filler").Append(i);
        words.Append(' ').Append(last);
        return words.ToString();
    }

    [Fact]
    public void LongParagraphFlowsAcrossPages()
    {
        string para = LongParagraph(out string first, out string last);

        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text(para);
            });
        }));

        Assert.True(CountPages(pdf) >= 2,
            $"A ~1200-word paragraph must span multiple pages, got {CountPages(pdf)}.");

        // Every word survives the split.
        string content = PdfTestUtils.InflatedText(pdf);
        Assert.Contains(first, content);
        Assert.Contains(last, content);
        Assert.Contains("filler600", content);   // middle of the paragraph
    }

    [Fact]
    public void SplitContentIsDistributedNotDuplicated()
    {
        string para = LongParagraph(out string first, out string last);

        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col => col.Item().Text(para));
        }));

        string content = PdfTestUtils.InflatedText(pdf);

        // First and last sentinels must live in different content streams
        // (an "endstream" boundary sits between them), and each appears once.
        int idxFirst = content.IndexOf(first, StringComparison.Ordinal);
        int idxLast  = content.IndexOf(last, StringComparison.Ordinal);
        Assert.True(idxFirst >= 0 && idxLast > idxFirst);
        Assert.True(content.IndexOf("endstream", idxFirst, StringComparison.Ordinal) < idxLast,
            "Start and end of the paragraph must be on different pages.");

        Assert.Equal(idxFirst, content.LastIndexOf(first, StringComparison.Ordinal));
        Assert.Equal(idxLast,  content.LastIndexOf(last,  StringComparison.Ordinal));
    }

    [Fact]
    public void PaddedLongParagraphStillSplits()
    {
        string para = LongParagraph(out string first, out string last);

        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Padding(20).Text(para);
            });
        }));

        Assert.True(CountPages(pdf) >= 2);
        string content = PdfTestUtils.InflatedText(pdf);
        Assert.Contains(first, content);
        Assert.Contains(last, content);
    }

    [Fact]
    public void HyperlinkWrappedLongTextDoesNotSplit()
    {
        // Link is opaque to the walker (splitting would bypass Link.Draw and
        // drop the annotation), so the legacy single-page overflow behaviour
        // is preserved and the URI annotation survives.
        string para = LongParagraph(out _, out _);

        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Hyperlink("https://example.com/long").Text(para);
            });
        }));

        Assert.Equal(1, CountPages(pdf));
        Assert.Contains("/URI", System.Text.Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void ShortParagraphsAreUnaffected()
    {
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("A perfectly ordinary paragraph.");
                col.Item().Text("And another one.");
            });
        }));

        Assert.Equal(1, CountPages(pdf));
    }

    [Fact]
    public void SplitParagraphFollowedByMoreItemsKeepsOrder()
    {
        string para = LongParagraph(out _, out string last);

        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text(para);
                col.Item().Text("AFTERSENTINEL");
            });
        }));

        string content = PdfTestUtils.InflatedText(pdf);
        int idxLast  = content.IndexOf(last, StringComparison.Ordinal);
        int idxAfter = content.IndexOf("AFTERSENTINEL", StringComparison.Ordinal);
        Assert.True(idxLast >= 0 && idxAfter > idxLast,
            "The item after a split paragraph must render after its final slice.");
    }
}
