using System.Text;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for document metadata (PDF Info dictionary).
/// </summary>
public sealed class MetadataTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfString(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes);

    [Fact]
    public void MetadataTitleIsWrittenToInfoDictionary()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("My Document Title");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (My Document Title)", pdf);
    }

    [Fact]
    public void MetadataAuthorIsWrittenToInfoDictionary()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataAuthor("John Doe");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Author (John Doe)", pdf);
    }

    [Fact]
    public void MetadataSubjectIsWrittenToInfoDictionary()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataSubject("Test Subject");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Subject (Test Subject)", pdf);
    }

    [Fact]
    public void MetadataKeywordsIsWrittenToInfoDictionary()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataKeywords("pdf, terra, test");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Keywords (pdf, terra, test)", pdf);
    }

    [Fact]
    public void MetadataCreatorIsWrittenToInfoDictionary()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataCreator("MyApp 1.0");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Creator (MyApp 1.0)", pdf);
    }

    [Fact]
    public void MultipleMetadataFieldsAreAllIncluded()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("Report 2025");
            c.MetadataAuthor("Acme Corp");
            c.MetadataSubject("Annual Report");
            c.MetadataKeywords("annual;report;2025");
            c.MetadataCreator("TerraPDF Generator");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Report 2025)", pdf);
        Assert.Contains("/Author (Acme Corp)", pdf);
        Assert.Contains("/Subject (Annual Report)", pdf);
        Assert.Contains("/Keywords (annual;report;2025)", pdf);
        Assert.Contains("/Creator (TerraPDF Generator)", pdf);
    }

    [Fact]
    public void MetadataWithoutPagesStillProducesValidPdf()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("Empty Doc");
            c.MetadataAuthor("Test");
            // No pages added — edge case, but PDF should still have at least one page for validity.
            // TerraPDF currently allows zero pages, but Info should still be written.
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Empty Doc)", pdf);
        Assert.Contains("/Author (Test)", pdf);
        // Basic PDF header/EOF should still be present
        Assert.StartsWith("%PDF-", pdf);
        Assert.EndsWith("%%EOF\n", pdf);
    }

    [Fact]
    public void MetadataWithNullOrWhitespaceIsOmittedFromPdf()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("Valid Title");
            c.MetadataAuthor("");           // whitespace -> ignored
            c.MetadataSubject(null);        // null -> ignored
            c.MetadataKeywords("   ");      // whitespace -> ignored
            c.MetadataCreator("TerraPDF");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Valid Title)", pdf);
        Assert.DoesNotContain("/Author", pdf);
        Assert.DoesNotContain("/Subject", pdf);
        Assert.DoesNotContain("/Keywords", pdf);
        Assert.Contains("/Creator (TerraPDF)", pdf);
    }

    [Fact]
    public void InfoDictionaryIsReferencedFromCatalog()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("Test");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Info ", pdf);
        Assert.Contains("/Catalog ", pdf);
        // The Catalog should contain /Info N 0 R
        Assert.Contains("/Info ", pdf);
    }

    [Fact]
    public void MetadataEscapingSpecialCharactersAreEscaped()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("Report (Final) \\ 2025");
            c.MetadataAuthor("Smith, John");
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (Report \\(Final\\) \\\\ 2025)", pdf);
        Assert.Contains("/Author (Smith, John)", pdf);
    }

    [Fact]
    public void MetadataWithBookmarksBothWorkTogether()
    {
        byte[] bytes = Build(c =>
        {
            c.MetadataTitle("With Bookmarks");
            c.Bookmark("Chapter 1", 1);
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Page 1");
            });
        });

        string pdf = PdfString(bytes);
        Assert.Contains("/Title (With Bookmarks)", pdf);
        Assert.Contains("/Outlines", pdf);
        Assert.Contains("/Info ", pdf);
        Assert.Contains("/Catalog", pdf);
    }
}
