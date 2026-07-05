using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for anchor-based bookmarks: <c>container.Bookmark("Title")</c> resolves
/// its target page and vertical position automatically during render.
/// </summary>
public sealed class AnchorBookmarkTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string Raw(byte[] b) => System.Text.Encoding.Latin1.GetString(b);

    /// <summary>Object ids of the page objects, in page order.</summary>
    private static List<int> PageObjectIds(string pdf) =>
        Regex.Matches(pdf, @"(\d+) 0 obj\n<< /Type /Page /")
             .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
             .ToList();

    /// <summary>The outline-item dictionary containing the given /Title.</summary>
    private static string OutlineItem(string pdf, string title)
    {
        int idx = pdf.IndexOf($"/Title ({title})", StringComparison.Ordinal);
        Assert.True(idx >= 0, $"Outline item '{title}' not found.");
        int start = pdf.LastIndexOf("obj\n", idx, StringComparison.Ordinal);
        int end   = pdf.IndexOf("endobj", idx, StringComparison.Ordinal);
        return pdf[start..end];
    }

    [Fact]
    public void AnchorResolvesToTheCorrectPage()
    {
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("Page one content");
                col.PageBreak();
                col.Item().Bookmark("Chapter Two").Text("Chapter two heading");
            });
        }));

        string text  = Raw(pdf);
        var pageIds  = PageObjectIds(text);
        Assert.Equal(2, pageIds.Count);

        // The anchor must target the SECOND page's object with a zoom-retaining
        // /XYZ destination carrying its resolved Y position.
        string item = OutlineItem(text, "Chapter Two");
        Assert.Contains($"/Dest [{pageIds[1]} 0 R /XYZ null", item);
    }

    [Fact]
    public void NestedAnchorsBuildAHierarchy()
    {
        byte[] pdf = Build(c => c.Page(p =>
        {
            p.Size(PageSize.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Bookmark("Part One").Text("Part one");
                col.Item().Bookmark("Section A", parentTitle: "Part One").Text("Section A");
            });
        }));

        string text = Raw(pdf);
        Assert.Contains("/Parent", OutlineItem(text, "Section A"));
        string parent = OutlineItem(text, "Part One");
        Assert.Contains("/First", parent);
        Assert.Contains("/Count 1", parent);
    }

    [Fact]
    public void MissingAnchorParentThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Build(c => c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Bookmark("Orphan", parentTitle: "NoSuchParent").Text("x");
            })));
    }

    [Fact]
    public void AnchorsCombineWithManualBookmarks()
    {
        byte[] pdf = Build(c =>
        {
            c.Bookmark("Manual Entry", 1);
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Bookmark("Anchored Entry").Text("content");
            });
        });

        string text = Raw(pdf);
        Assert.Contains("/Title (Manual Entry)", text);
        Assert.Contains("/Title (Anchored Entry)", text);
        Assert.Contains("/Type /Outlines", text);
    }

    [Fact]
    public void AnchorNestsUnderManualBookmarkParent()
    {
        byte[] pdf = Build(c =>
        {
            c.Bookmark("Manual Parent", 1);
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Bookmark("Anchored Child", parentTitle: "Manual Parent").Text("content");
            });
        });

        Assert.Contains("/Parent", OutlineItem(Raw(pdf), "Anchored Child"));
    }

    [Fact]
    public void AnchorsCoexistWithTableOfContents()
    {
        byte[] pdf = Build(c =>
        {
            c.TableOfContents(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(2, Unit.Centimetre);
            });
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Margin(2, Unit.Centimetre);
                p.Content().Column(col =>
                {
                    col.Item().Bookmark("Intro").H1("Introduction");
                });
            });
        });

        string text = Raw(pdf);
        Assert.Contains("/Title (Intro)", text);       // outline from the anchor
        Assert.Contains("/Type /Outlines", text);
        // TOC page produced an internal link for the heading.
        Assert.Contains("/Dest [", text);
    }
}
