using System.Text;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for the bookmarks/outlines feature.
/// </summary>
public sealed class BookmarkTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfString(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes);

    [Fact]
    public void SimpleBookmarkIncludesOutlinesInCatalog()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello world");
            });
            c.Bookmark("Chapter 1", 1);
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Outlines", pdf);
        Assert.Contains("/Type /Outlines", pdf);
        Assert.Contains("/Title (Chapter 1)", pdf);
    }

    [Fact]
    public void BookmarkWithoutTopUsesFitDestination()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
            c.Bookmark("Top Level", 1);
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Dest [", pdf);
        Assert.Contains("/Fit]", pdf);
    }

    [Fact]
    public void BookmarkWithTopUsesFitHDestination()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
            c.Bookmark("With Top", 1, 150.0);
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/FitH 150.00", pdf);
    }

    [Fact]
    public void MultipleTopLevelBookmarksAreLinked()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Content");
            });
            c.Bookmark("First", 1);
            c.Bookmark("Second", 1);
            c.Bookmark("Third", 1);
        });

        string pdf = PdfString(bytes);
        // All three titles present
        Assert.Contains("/Title (First)", pdf);
        Assert.Contains("/Title (Second)", pdf);
        Assert.Contains("/Title (Third)", pdf);
        // Sibling links: Second has /Prev pointing to First
        Assert.Contains("/Prev", pdf);
        Assert.Contains("/Next", pdf);
    }

    [Fact]
    public void NestedBookmarksCreateParentChildStructure()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Content page 1");
            });
            // Parent must be added before children
            c.Bookmark("Chapter 1", 1);
            c.Bookmark("Section 1.1", 1, "Chapter 1");
            c.Bookmark("Section 1.2", 1, "Chapter 1");
            c.Bookmark("Subsection 1.2.1", 1, "Section 1.2");
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Chapter 1)", pdf);
        Assert.Contains("/Title (Section 1.1)", pdf);
        Assert.Contains("/Title (Section 1.2)", pdf);
        Assert.Contains("/Title (Subsection 1.2.1)", pdf);

        // Ensure parent-child references exist
        // Count occurrences of /Parent id patterns - not exact IDs, but ensure present
        Assert.Contains("/Parent", pdf);
        Assert.Contains("/First", pdf); // child parent should have children
        Assert.Contains("/Last", pdf);
        Assert.Contains("/Count", pdf);
    }

    [Fact]
    public void BookmarkTargetsSecondPage()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Page 1");
            });
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Page 2");
            });
            c.Bookmark("Go to page 2", 2);
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Go to page 2)", pdf);
        // The /Dest should reference some page object; ensure it's there
        Assert.Contains("/Dest [", pdf);
    }

    [Fact]
    public void InvalidPageNumberZeroThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(c =>
            {
                c.Page(p => p.Size(PageSize.A4).Content().Text("Hi"));
                c.Bookmark("Invalid", 0);
            }));
    }

    [Fact]
    public void InvalidParentThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Build(c =>
            {
                c.Page(p => p.Size(PageSize.A4).Content().Text("Hi"));
                c.Bookmark("Child", 1, "NonExistent");
            }));
    }

    [Fact]
    public void BookmarkPageNumberBeyondTotalThrowsInvalidOperationException()
    {
        // Document has only 1 page; bookmark targets page 2
        Assert.Throws<InvalidOperationException>(() =>
            Build(c =>
            {
                c.Page(p => p.Size(PageSize.A4).Content().Text("Only page"));
                c.Bookmark("Missing page", 2);
            }));
    }

    [Fact]
    public void DeeplyNestedBookmarksThreeLevels()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Content");
            });
            c.Bookmark("L1", 1);
            c.Bookmark("L2", 1, "L1");
            c.Bookmark("L3", 1, "L2");
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (L1)", pdf);
        Assert.Contains("/Title (L2)", pdf);
        Assert.Contains("/Title (L3)", pdf);
        // Ensure the child has parent L2 (which has parent L1)
        int countL = pdf.Split("/Title (L").Length - 1; // count titles L1,L2,L3: 3
        Assert.Equal(3, countL);
    }

    [Fact]
    public void BookmarkWithParentAndTopCombined()
    {
        byte[] bytes = Build(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Page content");
            });
            c.Bookmark("Parent", 1);
            c.Bookmark("Child", 1, "Parent", 200.0);
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Parent)", pdf);
        Assert.Contains("/Title (Child)", pdf);
        Assert.Contains("/FitH 200.00", pdf);
    }
}
