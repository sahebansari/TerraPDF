using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  12. ENCRYPTION & PASSWORD PROTECTION SHOWCASE
//
//  Generates FIVE separate PDFs, each demonstrating a different protection
//  scenario, then assembles a single "overview" PDF that describes all of them:
//
//    12a_open_password.pdf          — requires a password to open (AES-256)
//    12b_owner_only.pdf             — no open password, but printing/copying locked (AES-256)
//    12c_print_only.pdf             — opens freely; only printing is permitted (AES-256)
//    12d_fully_restricted.pdf       — password required; no permissions at all (AES-256)
//    12e_aes128_legacy.pdf          — legacy AES-128 opt-in for pre-2008 viewers
//    12_encryption_showcase.pdf     — overview document (not encrypted itself)
//
//  Shows: EncryptionOptions, EncryptionAlgorithm, PdfPermissions flags, owner
//         vs user passwords, page layout, tables, callout boxes, colour-coded
//         permission badges.
// =============================================================================
internal static class EncryptionShowcase
{
    // ── Palette ──────────────────────────────────────────────────────────────
    private const string Brand      = "#1A3C5E";
    private const string BrandLight = "#2E6DA4";
    private const string Accent     = "#E87722";
    private const string AccentDark = "#B85A0A";
    private const string Success    = "#27AE60";
    private const string Danger     = "#C0392B";
    private const string Muted      = "#7A8A99";
    private const string Light      = "#F4F7FA";
    private const string White      = "#FFFFFF";
    private const string GridLine   = "#D0D8E0";
    private const string Stripe     = "#F0F4F8";

    internal static void Generate(string overviewPath)
    {
        string dir = Path.GetDirectoryName(overviewPath)!;
        Directory.CreateDirectory(dir);

        string pathA = Path.Combine(dir, "12a_open_password.pdf");
        string pathB = Path.Combine(dir, "12b_owner_only.pdf");
        string pathC = Path.Combine(dir, "12c_print_only.pdf");
        string pathD = Path.Combine(dir, "12d_fully_restricted.pdf");
        string pathE = Path.Combine(dir, "12e_aes128_legacy.pdf");

        // Generate the protected variants (12a–d use the AES-256 default;
        // 12e opts into legacy AES-128 for very old viewers)
        GenerateA(pathA);
        GenerateB(pathB);
        GenerateC(pathC);
        GenerateD(pathD);
        GenerateE(pathE);

        // Generate the overview / showcase document
        GenerateOverview(overviewPath, pathA, pathB, pathC, pathD, pathE);
    }

    // =========================================================================
    //  12a — Open password + all permissions
    //  User needs "user123" to open; full access once opened.
    // =========================================================================
    private static void GenerateA(string path)
    {
        Document.Create(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = "user123",
                OwnerPassword = "admin123",
                Permissions   = PdfPermissions.All,
            });

            doc.MetadataTitle("TerraPDF — Password Protected (All Permissions)");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataCreator("TerraPDF Sample Generator");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Password Protected — Full Access", "12a");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    SectionHeader(col.Item(), "Document Protection Summary");

                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Column(info =>
                    {
                        info.Spacing(6);
                        LabelValue(info.Item(), "User Password",  "user123");
                        LabelValue(info.Item(), "Owner Password", "admin123");
                        LabelValue(info.Item(), "Permissions",    "All — print, copy, edit, fill forms, accessibility");
                        LabelValue(info.Item(), "Algorithm",      "AES-256 CBC  (PDF Standard Security Handler Rev 6)");
                        LabelValue(info.Item(), "PDF Version",    "2.0  (ISO 32000-2, required for Revision 6)");
                    });

                    SectionHeader(col.Item(), "What this means");

                    col.Item().Text(t =>
                    {
                        t.Span("This document requires a ").FontColor(Muted);
                        t.Span("user password").Bold().FontColor(Brand);
                        t.Span(" to open. Once opened with either the user password (").FontColor(Muted);
                        t.Span("user123").Bold().FontColor(Accent);
                        t.Span(") or the owner password (").FontColor(Muted);
                        t.Span("admin123").Bold().FontColor(Accent);
                        t.Span("), all document operations — printing, copying text, editing, "
                             + "filling forms, and accessibility extraction — are permitted.").FontColor(Muted);
                    });

                    col.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span("All content streams and image data in this PDF are encrypted with ")
                         .FontColor(Muted);
                        t.Span("AES-256 CBC").Bold().FontColor(Brand);
                        t.Span(". The 256-bit file encryption key is protected by the SHA-2 based "
                             + "Revision 6 key-derivation algorithm (ISO 32000-2). For very old "
                             + "viewers, AES-128 remains available via Algorithm = EncryptionAlgorithm.Aes128.")
                         .FontColor(Muted);
                    });

                    PermissionGrid(col.Item(), PdfPermissions.All);

                    CodeBlock(col.Item(),
                        "container.Encrypt(new EncryptionOptions\n"
                      + "{\n"
                      + "    UserPassword  = \"user123\",\n"
                      + "    OwnerPassword = \"admin123\",\n"
                      + "    Permissions   = PdfPermissions.All,\n"
                      + "});");
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [12a] Encryption showcase -> {path}");
    }

    // =========================================================================
    //  12e — Legacy AES-128 (Revision 4) opt-in for very old viewers
    // =========================================================================
    private static void GenerateE(string path)
    {
        Document.Create(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = "legacy123",
                OwnerPassword = "legacyAdmin",
                Permissions   = PdfPermissions.All,
                Algorithm     = EncryptionAlgorithm.Aes128,
            });

            doc.MetadataTitle("TerraPDF — Legacy AES-128 Encryption");
            doc.MetadataAuthor("TerraPDF Engineering Team");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Legacy AES-128 — Compatibility Mode", "12e");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    SectionHeader(col.Item(), "Document Protection Summary");

                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Column(info =>
                    {
                        info.Spacing(6);
                        LabelValue(info.Item(), "User Password",  "legacy123");
                        LabelValue(info.Item(), "Owner Password", "legacyAdmin");
                        LabelValue(info.Item(), "Algorithm",      "AES-128 CBC  (PDF Standard Security Handler Rev 4)");
                        LabelValue(info.Item(), "PDF Version",    "1.6  (minimum required for AES-128)");
                    });

                    SectionHeader(col.Item(), "When to use this mode");

                    col.Item().Text(t =>
                    {
                        t.Span("TerraPDF encrypts with ").FontColor(Muted);
                        t.Span("AES-256 (Revision 6)").Bold().FontColor(Brand);
                        t.Span(" by default. Set ").FontColor(Muted);
                        t.Span("Algorithm = EncryptionAlgorithm.Aes128").Bold().FontColor(Accent);
                        t.Span(" only when documents must open in viewers released before "
                             + "roughly 2008, which predate AES-256 support.").FontColor(Muted);
                    });

                    CodeBlock(col.Item(),
                        "container.Encrypt(new EncryptionOptions\n"
                      + "{\n"
                      + "    UserPassword  = \"legacy123\",\n"
                      + "    OwnerPassword = \"legacyAdmin\",\n"
                      + "    Permissions   = PdfPermissions.All,\n"
                      + "    Algorithm     = EncryptionAlgorithm.Aes128,\n"
                      + "});");
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [12e] Encryption showcase -> {path}");
    }

    // =========================================================================
    //  12b — No open password; owner locks print + copy
    //  Opens without a prompt, but viewer enforces restrictions.
    // =========================================================================
    private static void GenerateB(string path)
    {
        Document.Create(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = "",          // no open password
                OwnerPassword = "ownerOnly",
                Permissions   = PdfPermissions.ExtractForAccessibility,
            });

            doc.MetadataTitle("TerraPDF — No Open Password, Restricted Permissions");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataCreator("TerraPDF Sample Generator");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "No Open Password — Restricted Access", "12b");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    SectionHeader(col.Item(), "Document Protection Summary");

                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Column(info =>
                    {
                        info.Spacing(6);
                        LabelValue(info.Item(), "User Password",  "(none — opens without a password prompt)");
                        LabelValue(info.Item(), "Owner Password", "ownerOnly");
                        LabelValue(info.Item(), "Permissions",    "Accessibility extraction only");
                        LabelValue(info.Item(), "Algorithm",      "AES-256 CBC  (PDF Standard Security Handler Rev 6)");
                    });

                    SectionHeader(col.Item(), "What this means");

                    col.Item().Text(t =>
                    {
                        t.Span("This document opens ").FontColor(Muted);
                        t.Span("without any password prompt").Bold().FontColor(Success);
                        t.Span(", yet its content is still encrypted with AES-256. "
                             + "The PDF viewer enforces the permission flags — "
                             + "printing, copying, and editing are all ").FontColor(Muted);
                        t.Span("disabled").Bold().FontColor(Danger);
                        t.Span(". Only screen-reader / accessibility text extraction is permitted. "
                             + "Entering the owner password (").FontColor(Muted);
                        t.Span("ownerOnly").Bold().FontColor(Accent);
                        t.Span(") in Acrobat's security settings unlocks all operations.").FontColor(Muted);
                    });

                    col.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span("This pattern is useful for ").FontColor(Muted);
                        t.Span("published reports or invoices").Bold().FontColor(Brand);
                        t.Span(" where you want recipients to view the document freely but "
                             + "prevent them from printing or extracting the content.").FontColor(Muted);
                    });

                    PermissionGrid(col.Item(), PdfPermissions.ExtractForAccessibility);

                    CodeBlock(col.Item(),
                        "container.Encrypt(new EncryptionOptions\n"
                      + "{\n"
                      + "    UserPassword  = \"\",           // no open password\n"
                      + "    OwnerPassword = \"ownerOnly\",\n"
                      + "    Permissions   = PdfPermissions.ExtractForAccessibility,\n"
                      + "});");
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [12b] Encryption showcase -> {path}");
    }

    // =========================================================================
    //  12c — Open password + print only
    //  Must enter password to open; can print but cannot copy or edit.
    // =========================================================================
    private static void GenerateC(string path)
    {
        Document.Create(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = "printme",
                OwnerPassword = "printAdmin",
                Permissions   = PdfPermissions.Print
                              | PdfPermissions.PrintLowResolution
                              | PdfPermissions.ExtractForAccessibility,
            });

            doc.MetadataTitle("TerraPDF — Print-Only Protection");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataCreator("TerraPDF Sample Generator");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Open Password — Print Only", "12c");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    SectionHeader(col.Item(), "Document Protection Summary");

                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Column(info =>
                    {
                        info.Spacing(6);
                        LabelValue(info.Item(), "User Password",  "printme");
                        LabelValue(info.Item(), "Owner Password", "printAdmin");
                        LabelValue(info.Item(), "Permissions",    "Print (high + low quality) + Accessibility");
                        LabelValue(info.Item(), "Algorithm",      "AES-256 CBC  (PDF Standard Security Handler Rev 6)");
                    });

                    SectionHeader(col.Item(), "What this means");

                    col.Item().Text(t =>
                    {
                        t.Span("Open with ").FontColor(Muted);
                        t.Span("printme").Bold().FontColor(Accent);
                        t.Span(". Once open, the viewer allows ").FontColor(Muted);
                        t.Span("printing").Bold().FontColor(Success);
                        t.Span(" at both high and low resolution but ").FontColor(Muted);
                        t.Span("disables").Bold().FontColor(Danger);
                        t.Span(" text selection, copy-paste, editing, and form filling. "
                             + "Ideal for distributing reports that recipients may print "
                             + "but not digitally re-use.").FontColor(Muted);
                    });

                    PermissionGrid(col.Item(),
                        PdfPermissions.Print
                      | PdfPermissions.PrintLowResolution
                      | PdfPermissions.ExtractForAccessibility);

                    CodeBlock(col.Item(),
                        "container.Encrypt(new EncryptionOptions\n"
                      + "{\n"
                      + "    UserPassword  = \"printme\",\n"
                      + "    OwnerPassword = \"printAdmin\",\n"
                      + "    Permissions   = PdfPermissions.Print\n"
                      + "                  | PdfPermissions.PrintLowResolution\n"
                      + "                  | PdfPermissions.ExtractForAccessibility,\n"
                      + "});");
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [12c] Encryption showcase -> {path}");
    }

    // =========================================================================
    //  12d — Fully restricted (view only, no permissions at all)
    //  Password required; viewer allows no operations beyond reading.
    // =========================================================================
    private static void GenerateD(string path)
    {
        Document.Create(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = "viewonly",
                OwnerPassword = "superadmin",
                Permissions   = PdfPermissions.None,
            });

            doc.MetadataTitle("TerraPDF — Fully Restricted");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataCreator("TerraPDF Sample Generator");

            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Fully Restricted — View Only", "12d");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    SectionHeader(col.Item(), "Document Protection Summary");

                    col.Item().Background(Light).Border(0.5, GridLine).Padding(14).Column(info =>
                    {
                        info.Spacing(6);
                        LabelValue(info.Item(), "User Password",  "viewonly");
                        LabelValue(info.Item(), "Owner Password", "superadmin");
                        LabelValue(info.Item(), "Permissions",    "None — view only");
                        LabelValue(info.Item(), "Algorithm",      "AES-256 CBC  (PDF Standard Security Handler Rev 6)");
                    });

                    SectionHeader(col.Item(), "What this means");

                    col.Item().Text(t =>
                    {
                        t.Span("Open with ").FontColor(Muted);
                        t.Span("viewonly").Bold().FontColor(Accent);
                        t.Span(". After opening, the viewer disables ").FontColor(Muted);
                        t.Span("every").Bold().FontColor(Danger);
                        t.Span(" document operation: no printing, no copying, no editing, "
                             + "no form filling, no assembly. The document can only be "
                             + "scrolled and read on screen. The owner password (").FontColor(Muted);
                        t.Span("superadmin").Bold().FontColor(Accent);
                        t.Span(") unlocks all restrictions.").FontColor(Muted);
                    });

                    col.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span("This is the maximum-restriction configuration, suitable for "
                             + "highly confidential documents distributed to known recipients "
                             + "who must not be able to reproduce or extract the content.")
                         .FontColor(Muted);
                    });

                    PermissionGrid(col.Item(), PdfPermissions.None);

                    CodeBlock(col.Item(),
                        "container.Encrypt(new EncryptionOptions\n"
                      + "{\n"
                      + "    UserPassword  = \"viewonly\",\n"
                      + "    OwnerPassword = \"superadmin\",\n"
                      + "    Permissions   = PdfPermissions.None,\n"
                      + "});");
                });
            });
        }).PublishPdf(path);
        Console.WriteLine($"  [12d] Encryption showcase -> {path}");
    }

    // =========================================================================
    //  Overview / showcase document  (not encrypted)
    // =========================================================================
    private static void GenerateOverview(
        string path, string pathA, string pathB, string pathC, string pathD, string pathE)
    {
        // Permission rows for the master reference table
        (string Flag, string Bit, string Description)[] allFlags =
        [
            ("Print",                    "Bit 3",  "High-quality printing"),
            ("ModifyContents",           "Bit 4",  "Modify document contents"),
            ("CopyText",                 "Bit 5",  "Copy or extract text and graphics"),
            ("ModifyAnnotations",        "Bit 6",  "Add or modify annotations and form fields"),
            ("FillForms",                "Bit 9",  "Fill in interactive form fields"),
            ("ExtractForAccessibility",  "Bit 10", "Text extraction for screen readers"),
            ("AssembleDocument",         "Bit 11", "Insert, rotate, or delete pages"),
            ("PrintLowResolution",       "Bit 12", "Low-resolution (degraded) printing only"),
        ];

        // Scenario summary table
        (string File, string UserPwd, string OwnerPwd, string Permissions, string UseCase)[] scenarios =
        [
            ("12a", "user123",   "admin123",   "All",              "Protect authorship; allow all viewer operations (AES-256)"),
            ("12b", "(none)",    "ownerOnly",  "Accessibility",    "Distribute freely; prevent print / copy (AES-256)"),
            ("12c", "printme",   "printAdmin", "Print only",       "Allow printing; prevent digital re-use (AES-256)"),
            ("12d", "viewonly",  "superadmin", "None",             "Maximum restriction — view on screen only (AES-256)"),
            ("12e", "legacy123", "legacyAdmin","All",              "Legacy AES-128 opt-in for pre-2008 viewers"),
        ];

        Document.Create(doc =>
        {
            doc.MetadataTitle("TerraPDF — Encryption & Password Protection Showcase");
            doc.MetadataAuthor("TerraPDF Engineering Team");
            doc.MetadataSubject("Demonstrates AES-256 PDF encryption (with legacy AES-128 support), password protection, and PdfPermissions flags");
            doc.MetadataKeywords("pdf; encryption; password; permissions; AES; security; TerraPDF");
            doc.MetadataCreator("TerraPDF Sample Generator");

            // ── PAGE 1  Introduction ──────────────────────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "Encryption & Password Protection", "Overview");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    // ── Hero banner ──────────────────────────────────────────
                    col.Item().RoundedBox(6, Brand, Brand).Padding(18).Column(hero =>
                    {
                        hero.Item().Text("AES-256 PDF Encryption")
                            .Bold().FontSize(20).FontColor(Color.White);
                        hero.Item().PaddingTop(4).Text(
                            "Zero-dependency password protection using the PDF Standard Security "
                          + "Handler, Revision 6 by default (legacy Revision 4 / AES-128 available). "
                          + "Implemented entirely with System.Security.Cryptography.")
                            .FontSize(9).FontColor("#B8D0E8");
                    });

                    // ── How it works ─────────────────────────────────────────
                    SectionHeader(col.Item(), "How It Works");

                    col.Item().Text(t =>
                    {
                        t.Span("By default, TerraPDF derives a 32-byte ").FontColor(Muted);
                        t.Span("File Encryption Key (FEK)").Bold().FontColor(Brand);
                        t.Span(" from your passwords using the PDF 2.0 SHA-2 based key-derivation "
                             + "algorithm (Algorithm 2.B, Revision 6). Each PDF object — pages, images, "
                             + "content streams, metadata, bookmark titles, hyperlink URIs — is then "
                             + "encrypted with a unique ").FontColor(Muted);
                        t.Span("per-object AES-256 CBC key").Bold().FontColor(Brand);
                        t.Span(" derived from the FEK plus the object number. "
                             + "Set Algorithm = EncryptionAlgorithm.Aes128 to opt into the legacy "
                             + "16-byte-key, MD5-derived Revision 4 handler instead (see 12e).")
                         .FontColor(Muted);
                    });

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2.6f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            TableHeader(row.Cell(), "Component");
                            TableHeader(row.Cell(), "Detail");
                        });

                        string[][] rows =
                        [
                            ["Cipher",             "AES-256 CBC (per ISO 32000-2 §7.6.5)"],
                            ["IV",                 "16 random bytes — unique per object"],
                            ["Key derivation",     "SHA-2 based, Algorithm 2.B (ISO 32000-2, Revision 6)"],
                            ["O/U entries",        "48-byte verifiers, with /OE + /UE key-wrapping"],
                            ["Handler",            "PDF Standard Security Handler, Revision 6"],
                            ["PDF version",        "2.0 (required for Revision 6)"],
                            ["Legacy mode",        "Algorithm = EncryptionAlgorithm.Aes128 → AES-128 CBC, Revision 4, PDF 1.6"],
                            ["Dependencies",       "System.Security.Cryptography only — no packages"],
                        ];

                        for (int i = 0; i < rows.Length; i++)
                        {
                            bool odd = i % 2 != 0;
                            var row  = tbl.Row;
                            row(r =>
                            {
                                r.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                 .Text(rows[i][0]).Bold().FontSize(9).FontColor(Brand);
                                r.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                 .Text(rows[i][1]).FontSize(9).FontColor(Muted);
                            });
                        }
                    });

                    // ── Five scenarios ───────────────────────────────────────
                    SectionHeader(col.Item(), "Five Protection Scenarios (see companion files)");

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(30);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2.4f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            TableHeader(row.Cell(), "File");
                            TableHeader(row.Cell(), "User pwd");
                            TableHeader(row.Cell(), "Owner pwd");
                            TableHeader(row.Cell(), "Permissions");
                            TableHeader(row.Cell(), "Use case");
                        });

                        for (int i = 0; i < scenarios.Length; i++)
                        {
                            var (file, user, owner, perms, useCase) = scenarios[i];
                            bool odd = i % 2 != 0;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine)
                                   .Padding(5).AlignCenter()
                                   .Text(file).Bold().FontSize(8).FontColor(BrandLight);
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine)
                                   .Padding(5).Text(user).FontSize(8).FontColor(Accent).Bold();
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine)
                                   .Padding(5).Text(owner).FontSize(8).FontColor(AccentDark).Bold();
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine)
                                   .Padding(5).Text(perms).FontSize(8).FontColor(Brand);
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine)
                                   .Padding(5).Text(useCase).FontSize(8).FontColor(Muted);
                            });
                        }
                    });
                });
            });

            // ── PAGE 2  PdfPermissions reference + API ────────────────────────
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(10));

                PageHeader(page, "PdfPermissions Reference & API", "Overview");
                PageFooter(page);

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    // ── Permissions reference table ───────────────────────────
                    SectionHeader(col.Item(), "PdfPermissions Flags");

                    col.Item().Text(
                        "Combine flags with bitwise-OR. Use PdfPermissions.All to grant "
                      + "everything or PdfPermissions.None to deny all.")
                        .FontSize(9).FontColor(Muted);

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(0.8f);
                            c.RelativeColumn(3.0f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            TableHeader(row.Cell(), "Flag");
                            TableHeader(row.Cell(), "PDF bit");
                            TableHeader(row.Cell(), "Description");
                        });

                        for (int i = 0; i < allFlags.Length; i++)
                        {
                            var (flag, bit, desc) = allFlags[i];
                            bool odd = i % 2 != 0;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text($"PdfPermissions.{flag}").FontSize(8).FontColor(Brand).Bold();
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(bit).FontSize(8).FontColor(Muted);
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(desc).FontSize(8).FontColor(Muted);
                            });
                        }

                        // All / None rows
                        tbl.Row(row =>
                        {
                            row.Cell().Background(Stripe).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("PdfPermissions.All").FontSize(8).FontColor(Success).Bold();
                            row.Cell().Background(Stripe).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("Bits 3-12").FontSize(8).FontColor(Muted);
                            row.Cell().Background(Stripe).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("All flags combined — full viewer access").FontSize(8).FontColor(Muted);
                        });
                        tbl.Row(row =>
                        {
                            row.Cell().Background(White).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("PdfPermissions.None").FontSize(8).FontColor(Danger).Bold();
                            row.Cell().Background(White).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("Bits 3-12").FontSize(8).FontColor(Muted);
                            row.Cell().Background(White).BorderBottom(0.3, GridLine).Padding(5)
                               .Text("No flags set — view on screen only").FontSize(8).FontColor(Muted);
                        });
                    });

                    // ── EncryptionOptions API ─────────────────────────────────
                    SectionHeader(col.Item(), "EncryptionOptions API");

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(0.8f);
                            c.RelativeColumn(2.8f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            TableHeader(row.Cell(), "Property");
                            TableHeader(row.Cell(), "Type");
                            TableHeader(row.Cell(), "Default");
                            TableHeader(row.Cell(), "Description");
                        });

                        (string Prop, string Type, string Def, string Desc)[] props =
                        [
                            ("UserPassword",  "string?",         "null", "Password to open the document. Omit for no open prompt."),
                            ("OwnerPassword", "string?",         "null", "Full-access password. Auto-generated when null."),
                            ("Permissions",   "PdfPermissions",  "All",  "Bitwise flags controlling viewer operations."),
                        ];

                        for (int i = 0; i < props.Length; i++)
                        {
                            var (prop, type, def, desc) = props[i];
                            bool odd = i % 2 != 0;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(prop).FontSize(8).FontColor(Brand).Bold();
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(type).FontSize(8).FontColor(AccentDark);
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(def).FontSize(8).FontColor(Muted);
                                row.Cell().Background(odd ? Stripe : White).BorderBottom(0.3, GridLine).Padding(5)
                                   .Text(desc).FontSize(8).FontColor(Muted);
                            });
                        }
                    });

                    // ── Complete code example ─────────────────────────────────
                    SectionHeader(col.Item(), "Complete API Example");

                    CodeBlock(col.Item(),
                        "Document.Create(container =>\n"
                      + "{\n"
                      + "    container.Encrypt(new EncryptionOptions\n"
                      + "    {\n"
                      + "        UserPassword  = \"open123\",\n"
                      + "        OwnerPassword = \"admin456\",\n"
                      + "        Permissions   = PdfPermissions.Print\n"
                      + "                      | PdfPermissions.CopyText,\n"
                      + "    });\n"
                      + "\n"
                      + "    container.Page(page =>\n"
                      + "    {\n"
                      + "        page.Size(PageSize.A4);\n"
                      + "        page.Margin(2, Unit.Centimetre);\n"
                      + "        page.Content().Text(\"Confidential\").Bold();\n"
                      + "    });\n"
                      + "})\n"
                      + ".PublishPdf(\"protected.pdf\");");
                });
            });
        }).PublishPdf(path);
    }

    // =========================================================================
    //  Shared UI helpers
    // =========================================================================

    private static void PageHeader(PageDescriptor page, string subtitle, string tag)
    {
        page.Header().Column(col =>
        {
            col.Item().Background(Brand).PaddingVertical(10).PaddingHorizontal(14)
               .Row(hdr =>
               {
                   hdr.RelativeItem().AlignMiddle().Column(t =>
                   {
                       t.Item().Text("TerraPDF — Encryption Showcase")
                        .Bold().FontSize(13).FontColor(Color.White);
                       t.Item().Text(subtitle)
                        .FontSize(9).FontColor("#B8D0E8");
                   });
                   hdr.AutoItem().AlignRight().AlignMiddle()
                      .Background(Accent).Padding(6)
                      .Text(tag).Bold().FontSize(9).FontColor(Color.White);
               });
            col.Item().Canvas(3, _ => { });   // accent stripe
        });
    }

    private static void PageFooter(PageDescriptor page)
    {
        page.Footer().BorderTop(0.5, GridLine).PaddingTop(6).Row(row =>
        {
            row.RelativeItem()
               .Text("TerraPDF — AES-256 Encryption Showcase")
               .FontSize(8).FontColor(Muted);
            row.AutoItem().Text(t =>
            {
                t.Span("Page ").FontSize(8).FontColor(Muted);
                t.CurrentPageNumber().FontSize(8).FontColor(Brand).Bold();
                t.Span(" / ").FontSize(8).FontColor(Muted);
                t.TotalPages().FontSize(8).FontColor(Brand).Bold();
            });
        });
    }

    private static void SectionHeader(IContainer container, string title)
    {
        container.PaddingTop(4).BorderBottom(1.5, BrandLight).PaddingBottom(4)
                 .Text(title).Bold().FontSize(11).FontColor(Brand);
    }

    private static void LabelValue(IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(130).Text(label + ":").Bold().FontSize(9).FontColor(Brand);
            row.RelativeItem().Text(value).FontSize(9).FontColor(Muted);
        });
    }

    private static void TableHeader(IContainer cell, string text)
    {
        cell.Background(Brand).Padding(5)
            .Text(text).Bold().FontSize(8).FontColor(Color.White);
    }

    private static void CodeBlock(IContainer container, string code)
    {
        container.RoundedBox(4, "#1E2D3D", "#1E2D3D").Padding(12)
                 .Text(code).FontSize(8).FontColor("#A8D8EA");
    }

    /// <summary>
    /// Renders a grid of all PdfPermissions flags with green (allowed) or
    /// red (denied) badges showing which flags are set in <paramref name="granted"/>.
    /// </summary>
    private static void PermissionGrid(IContainer container, PdfPermissions granted)
    {
        (string Label, PdfPermissions Flag)[] badges =
        [
            ("Print",             PdfPermissions.Print),
            ("Print (low res)",   PdfPermissions.PrintLowResolution),
            ("Modify",            PdfPermissions.ModifyContents),
            ("Copy Text",         PdfPermissions.CopyText),
            ("Annotations",       PdfPermissions.ModifyAnnotations),
            ("Fill Forms",        PdfPermissions.FillForms),
            ("Accessibility",     PdfPermissions.ExtractForAccessibility),
            ("Assemble",          PdfPermissions.AssembleDocument),
        ];

        container.Column(col =>
        {
            col.Item().PaddingBottom(6)
               .Text("Permission Badges").Bold().FontSize(9).FontColor(Brand);

            col.Item().Row(row =>
            {
                row.Spacing(6);
                foreach (var (label, flag) in badges)
                {
                    bool allowed = granted.HasFlag(flag);
                    row.AutoItem()
                       .Background(allowed ? "#D5F5E3" : "#FADBD8")
                       .RoundedBorder(3, 0.5, allowed ? Success : Danger)
                       .Padding(4)
                       .Text((allowed ? "+ " : "x ") + label)
                       .FontSize(7).Bold()
                       .FontColor(allowed ? Success : Danger);
                }
            });
        });
    }
}
