using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Integration tests that generate real PDF bytes and verify the output is
/// structurally valid without requiring a full PDF parser.
/// </summary>
public sealed class DocumentGenerationTests
{
    private static byte[] OnePage(string text = "Test") =>
        Document.Create(container =>
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(1, Unit.Centimetre);
                page.Content().Text(text);
            })).GeneratePdf();

    private static string GetHeader(byte[] bytes) =>
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5);

    // ── PDF structure ─────────────────────────────────────────────────────

    [Fact]
    public void GeneratePdfReturnsNonEmptyBytes()
    {
        Assert.NotEmpty(OnePage());
    }

    [Fact]
    public void GeneratePdfStartsWithPdfHeader()
    {
        Assert.Equal("%PDF-", GetHeader(OnePage()));
    }

    [Fact]
    public void GeneratePdfContainsPdf17Version()
    {
        byte[] bytes  = OnePage();
        string header = System.Text.Encoding.ASCII.GetString(bytes, 0, 8);
        Assert.Equal("%PDF-1.7", header);
    }

    [Fact]
    public void GeneratePdfEndsWithEof()
    {
        byte[] bytes = OnePage();
        // PDF ends with "%%EOF\n" - trim trailing whitespace before checking
        string tail  = System.Text.Encoding.ASCII.GetString(bytes, bytes.Length - 6, 6).TrimEnd();
        Assert.Equal("%%EOF", tail);
    }

    // ── Text styles ───────────────────────────────────────────────────────

    [Fact]
    public void TextItalicIsValidPdf()
    {
        byte[] bytes = Document.Create(c =>
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello Italic").Italic();
            })).GeneratePdf();

        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    [Fact]
    public void TextBoldIsValidPdf()
    {
        byte[] bytes = Document.Create(c =>
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Hello Bold").Bold();
            })).GeneratePdf();

        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    [Fact]
    public void TextBoldItalicColumnIsValidPdf()
    {
        byte[] bytes = Document.Create(c =>
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Column(col =>
                {
                    col.Item().Text("Bold Italic").Bold();
                    col.Item().Text("Normal Italic").Italic();
                });
            })).GeneratePdf();

        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    // ── Multi-page ────────────────────────────────────────────────────────

    [Fact]
    public void MultiPageProducesValidPdf()
    {
        byte[] bytes = Document.Create(container =>
        {
            for (int i = 1; i <= 3; i++)
            {
                int capture = i;
                container.Page(p =>
                {
                    p.Size(PageSize.A4);
                    p.Content().Text($"Page {capture}");
                });
            }
        }).GeneratePdf();

        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    // ── Table ─────────────────────────────────────────────────────────────

    [Fact]
    public void TableIsValidPdf()
    {
        byte[] bytes = Document.Create(container =>
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(1, Unit.Centimetre);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                    });
                    table.Row(row =>
                    {
                        row.Cell().Text("Description");
                        row.Cell().Text("Amount");
                    });
                    table.Row(row =>
                    {
                        row.Cell().Text("Service A");
                        row.Cell().Text("$100");
                    });
                });
            })).GeneratePdf();

        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    // ── GeneratePdf(stream) overload ──────────────────────────────────────

    [Fact]
    public void GeneratePdfToStreamWritesValidBytes()
    {
        using var ms = new MemoryStream();
        Document.Create(c =>
            c.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Stream test");
            })).GeneratePdf(ms);

        ms.Position = 0;
        Assert.Equal("%PDF-", GetHeader(ms.ToArray()));
    }

    // ── IDocument interface ───────────────────────────────────────────────

    [Fact]
    public void IDocumentComposeProducesValidPdf()
    {
        byte[] bytes = Document.Create(new SimpleOnePageDocument()).GeneratePdf();
        Assert.Equal("%PDF-", GetHeader(bytes));
    }

    /// <summary>Minimal IDocument implementation used for the interface test.</summary>
    private sealed class SimpleOnePageDocument : IDocument
    {
        public void Compose(IDocumentContainer container) =>
            container.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Content().Text("IDocument test");
            });
    }
}
