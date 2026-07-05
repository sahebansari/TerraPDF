using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

// =============================================================================
//  Encryption tests
//  Verify that encrypted PDFs are structurally valid ("%PDF-" header) and that
//  the /Encrypt dictionary and Standard Security Handler entries are present.
// =============================================================================
public sealed class EncryptionTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string PdfHeader(byte[] b) =>
        System.Text.Encoding.ASCII.GetString(b, 0, 5);

    private static bool ContainsAscii(byte[] pdf, string token)
    {
        string text = System.Text.Encoding.Latin1.GetString(pdf);
        return text.Contains(token, StringComparison.Ordinal);
    }

    private static byte[] SimplePage(Action<IDocumentContainer>? extra = null) =>
        Build(doc =>
        {
            extra?.Invoke(doc);
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Hello, TerraPDF!").Bold().FontSize(18);
            });
        });

    // ── No encryption (baseline) ─────────────────────────────────────────────

    [Fact]
    public void NoEncryptionProducesValidPdf()
    {
        byte[] bytes = SimplePage();
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.False(ContainsAscii(bytes, "/Encrypt"), "Unencrypted PDF must not contain /Encrypt");
    }

    // ── Encrypt dictionary present ───────────────────────────────────────────

    [Fact]
    public void WithEncryptionContainsEncryptDictionary()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword  = "user",
            OwnerPassword = "owner",
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"),     "Encrypted PDF must reference /Encrypt in trailer");
        Assert.True(ContainsAscii(bytes, "/Standard"),    "Must declare /Filter /Standard");
        Assert.True(ContainsAscii(bytes, "/AESV3"),       "Default must use AES-256 (/AESV3) crypt filter");
        Assert.True(ContainsAscii(bytes, "/StdCF"),       "Must define StdCF crypt filter");
        Assert.True(ContainsAscii(bytes, "/StmF /StdCF"), "Streams must use StdCF");
        Assert.True(ContainsAscii(bytes, "/StrF /StdCF"), "Strings must use StdCF");
    }

    // ── /V and /R entries ────────────────────────────────────────────────────

    [Fact]
    public void DefaultEncryptionIsAes256Revision6()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "test",
        }));
        Assert.True(ContainsAscii(bytes, "/V 5"),  "Must set /V 5 (AES-256)");
        Assert.True(ContainsAscii(bytes, "/R 6"),  "Must set /R 6 (revision 6)");
        Assert.True(ContainsAscii(bytes, "/Length 256"), "Key length must be 256 bits");
    }

    [Fact]
    public void Aes128OptInStillProducesRevision4Dictionary()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "test",
            Algorithm    = EncryptionAlgorithm.Aes128,
        }));
        Assert.True(ContainsAscii(bytes, "/V 4"),  "Must set /V 4 (algorithm 4)");
        Assert.True(ContainsAscii(bytes, "/R 4"),  "Must set /R 4 (revision 4)");
        Assert.True(ContainsAscii(bytes, "/Length 128"), "Key length must be 128 bits");
        Assert.True(ContainsAscii(bytes, "/AESV2"), "Must use AES-128 (/AESV2) crypt filter");
        Assert.StartsWith("%PDF-1.6", System.Text.Encoding.ASCII.GetString(bytes, 0, 8));
    }

    // ── Empty user password (open without password) ──────────────────────────

    [Fact]
    public void EmptyUserPasswordProducesValidEncryptedPdf()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword  = "",            // open without password
            OwnerPassword = "admin",
            Permissions   = PdfPermissions.Print,
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── Null passwords (both optional) ───────────────────────────────────────

    [Fact]
    public void NullPasswordsProducesValidEncryptedPdf()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions()));
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── Permission flags ─────────────────────────────────────────────────────

    [Fact]
    public void AllPermissionsProducesValidPdf()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "pwd",
            Permissions  = PdfPermissions.All,
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void NoPermissionsProducesValidPdf()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "pwd",
            Permissions  = PdfPermissions.None,
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    [Fact]
    public void PrintOnlyPermissionProducesValidPdf()
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "pwd",
            Permissions  = PdfPermissions.Print,
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
    }

    // ── Null options guard ───────────────────────────────────────────────────

    [Fact]
    public void NullOptionsThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Build(doc => doc.Encrypt(null!)));
    }

    // ── Multi-page document ──────────────────────────────────────────────────

    [Fact]
    public void MultiPageEncryptedPdfIsValid()
    {
        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "mp", OwnerPassword = "mpowner" });
            for (int i = 1; i <= 5; i++)
            {
                int pageNum = i;
                doc.Page(page =>
                {
                    page.Size(PageSize.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Page {pageNum}").Bold().FontSize(20);
                        col.Item().Text("Encrypted content on this page.").FontSize(12);
                    });
                });
            }
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── Encryption with metadata ─────────────────────────────────────────────

    [Fact]
    public void EncryptionWithMetadataProducesValidPdf()
    {
        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "meta" });
            doc.MetadataTitle("Encrypted Report");
            doc.MetadataAuthor("TerraPDF");
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Encrypted with metadata.").FontSize(12);
            });
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── Encryption with images ───────────────────────────────────────────────

    [Fact]
    public void EncryptionWithTextTableProducesValidPdf()
    {
        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "table", OwnerPassword = "owner" });
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Table(tbl =>
                {
                    tbl.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });
                    tbl.HeaderRow(row =>
                    {
                        row.Cell().Background(Color.Blue.Darken2).Padding(6)
                           .Text("Item").Bold().FontColor(Color.White);
                        row.Cell().Background(Color.Blue.Darken2).Padding(6)
                           .Text("Value").Bold().FontColor(Color.White);
                    });
                    for (int i = 1; i <= 5; i++)
                    {
                        int row_i = i;
                        tbl.Row(row =>
                        {
                            row.Cell().Padding(6).Text($"Row {row_i}");
                            row.Cell().Padding(6).Text($"{row_i * 100:N0}");
                        });
                    }
                });
            });
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── PdfPermissions flag combinations ────────────────────────────────────

    [Theory]
    [InlineData(PdfPermissions.Print)]
    [InlineData(PdfPermissions.CopyText)]
    [InlineData(PdfPermissions.ModifyContents)]
    [InlineData(PdfPermissions.ModifyAnnotations)]
    [InlineData(PdfPermissions.FillForms)]
    [InlineData(PdfPermissions.ExtractForAccessibility)]
    [InlineData(PdfPermissions.AssembleDocument)]
    [InlineData(PdfPermissions.PrintLowResolution)]
    [InlineData(PdfPermissions.Print | PdfPermissions.CopyText)]
    [InlineData(PdfPermissions.All)]
    [InlineData(PdfPermissions.None)]
    public void EachPermissionFlagProducesValidPdf(PdfPermissions perms)
    {
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "p",
            Permissions  = perms,
        }));
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── /P entry is negative (signed 32-bit int) ─────────────────────────────

    [Fact]
    public void PermissionEntryIsNegativeOrZeroInPdf()
    {
        // PDF /P is a signed 32-bit integer and is always negative when bits 1-2 = 0.
        byte[] bytes = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            Permissions = PdfPermissions.All,
        }));
        string text = System.Text.Encoding.Latin1.GetString(bytes);
        // /P value must appear and must be a negative number (per spec, bits 1-2 are 0)
        Assert.Contains("/P -", text, StringComparison.Ordinal);
    }

    // ── Encryption replaces previous options when called twice ───────────────

    [Fact]
    public void CallingEncryptTwiceUsesLastOptions()
    {
        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "first" });
            doc.Encrypt(new EncryptionOptions { UserPassword = "second" }); // overrides
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Encryption override test.");
            });
        });
        Assert.Equal("%PDF-", PdfHeader(bytes));
        Assert.True(ContainsAscii(bytes, "/Encrypt"));
    }

    // ── String objects must actually be encrypted (/StrF /StdCF) ─────────────

    [Fact]
    public void EncryptedPdfDoesNotContainPlaintextMetadataStrings()
    {
        const string title  = "TopSecretReportTitle";
        const string author = "ClassifiedAuthorName";

        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "u", OwnerPassword = "o" });
            doc.MetadataTitle(title);
            doc.MetadataAuthor(author);
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Content().Text("body");
            });
        });

        // The declared /StrF /StdCF means every string must be AES-encrypted;
        // the plaintext values must not appear anywhere in the file.
        Assert.False(ContainsAscii(bytes, title),  "Encrypted PDF leaked plaintext /Title");
        Assert.False(ContainsAscii(bytes, author), "Encrypted PDF leaked plaintext /Author");
    }

    [Fact]
    public void EncryptedPdfDoesNotContainPlaintextBookmarkTitles()
    {
        const string bookmark = "SecretChapterHeading";

        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "u" });
            doc.Bookmark(bookmark, 1);
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Content().Text("body");
            });
        });

        Assert.True(ContainsAscii(bytes, "/Outlines"), "Bookmarks must still be emitted");
        Assert.False(ContainsAscii(bytes, bookmark), "Encrypted PDF leaked plaintext bookmark title");
    }

    [Fact]
    public void EncryptedPdfDoesNotContainPlaintextHyperlinkUris()
    {
        const string url = "https://secret.example.com/private-path";

        byte[] bytes = Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions { UserPassword = "u" });
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Content().Hyperlink(url).Text("click me");
            });
        });

        Assert.True(ContainsAscii(bytes, "/URI"), "Link annotation must still be emitted");
        Assert.False(ContainsAscii(bytes, url), "Encrypted PDF leaked plaintext hyperlink URI");
    }

    [Fact]
    public void UnencryptedPdfStillContainsPlaintextStrings()
    {
        const string title = "PublicReportTitle";
        const string url   = "https://example.com/public";

        byte[] bytes = Build(doc =>
        {
            doc.MetadataTitle(title);
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Content().Hyperlink(url).Text("click me");
            });
        });

        Assert.True(ContainsAscii(bytes, title), "Unencrypted metadata must stay literal");
        Assert.True(ContainsAscii(bytes, url),   "Unencrypted URI must stay literal");
    }

    // ── Output size sanity ───────────────────────────────────────────────────

    [Fact]
    public void EncryptedPdfIsLargerThanPlaintext()
    {
        // Encrypted output must be at least as large as the plaintext
        // (IV + ciphertext padding always adds bytes).
        byte[] plain     = SimplePage();
        byte[] encrypted = SimplePage(doc => doc.Encrypt(new EncryptionOptions
        {
            UserPassword = "size-check",
        }));
        Assert.True(encrypted.Length > plain.Length,
            $"Expected encrypted ({encrypted.Length} bytes) > plain ({plain.Length} bytes)");
    }
}
