using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TerraPDF.Agents.Spec;
using TerraPDF.Barcodes;
using TerraPDF.Core;
using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Agents.Rendering;

/// <summary>Outcome of rendering a <see cref="PdfDocumentSpec"/>.</summary>
public sealed class PdfRenderResult
{
    /// <summary>True when a PDF was produced.</summary>
    public bool Success => Pdf is not null;

    /// <summary>The PDF bytes, or null when validation or rendering failed.</summary>
    public byte[]? Pdf { get; init; }

    /// <summary>Number of pages in the PDF.</summary>
    public int PageCount { get; init; }

    /// <summary>Problems that prevented rendering.</summary>
    public IReadOnlyList<SpecIssue> Errors { get; init; } = [];

    /// <summary>Problems that did not prevent rendering but probably affect the output.</summary>
    public IReadOnlyList<SpecIssue> Warnings { get; init; } = [];
}

/// <summary>
/// Validates a <see cref="PdfDocumentSpec"/> and renders it with the TerraPDF
/// fluent API. Stateless apart from its configuration; safe to reuse and to
/// call concurrently.
/// </summary>
public sealed class PdfSpecRenderer
{
    /// <summary>Default cap on a single image or font asset.</summary>
    public const long DefaultMaxAssetBytes = 20 * 1024 * 1024;

    // Spec fonts are registered under a name derived from their content, so two
    // concurrent requests that both call their font "Brand" never overwrite each
    // other in TerraPDF's process-wide font registry.
    private static readonly ConcurrentDictionary<string, bool> s_registeredFaces = new(StringComparer.Ordinal);

    private readonly string? _assetDirectory;
    private readonly long _maxAssetBytes;
    private readonly HashSet<string> _hostFamilies;

    /// <param name="assetDirectory">
    /// Directory that image and font file paths in a spec resolve against. When
    /// null, specs may only embed assets as base64 data.
    /// </param>
    /// <param name="maxAssetBytes">Maximum size of a single image or font.</param>
    /// <param name="hostFontFamilies">
    /// Families the host application registered itself via
    /// <see cref="FontFamily.Register(string, string, bool, bool)"/>, which specs may reference by name.
    /// </param>
    public PdfSpecRenderer(string? assetDirectory = null, long maxAssetBytes = DefaultMaxAssetBytes,
        IEnumerable<string>? hostFontFamilies = null)
    {
        _assetDirectory = assetDirectory;
        _maxAssetBytes = maxAssetBytes;
        _hostFamilies = new HashSet<string>(hostFontFamilies ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Parses, validates, and renders spec JSON.</summary>
    public PdfRenderResult Render(string json)
    {
        PdfDocumentSpec? spec = PdfSpecReader.TryRead(json, out SpecIssue? error);
        return spec is null ? new PdfRenderResult { Errors = [error!] } : Render(spec);
    }

    /// <summary>Validates without rendering.</summary>
    public PdfRenderResult Validate(PdfDocumentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var validator = CreateValidator(out _);
        validator.Validate(spec);
        return new PdfRenderResult { Errors = validator.Errors, Warnings = validator.Warnings };
    }

    /// <summary>Validates and renders a spec.</summary>
    public PdfRenderResult Render(PdfDocumentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var validator = CreateValidator(out AssetResolver assets);
        validator.Validate(spec);
        if (validator.Errors.Count > 0)
            return new PdfRenderResult { Errors = validator.Errors, Warnings = validator.Warnings };

        try
        {
            var context = new RenderContext(spec, assets, RegisterFonts(spec.Fonts, assets));
            byte[] pdf = Document.Create(doc => ComposeDocument(doc, spec, context)).PublishPdf();
            return new PdfRenderResult { Pdf = pdf, PageCount = CountPages(pdf), Warnings = validator.Warnings };
        }
        // InvalidDataException (a malformed PNG, found when the document is saved) is not an IOException.
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException
                                       or FormatException or IOException or InvalidDataException)
        {
            // Validation catches the common cases; anything TerraPDF still rejects
            // goes back to the model as an error it can act on, never as a crash.
            return new PdfRenderResult
            {
                Errors = [new SpecIssue("$", $"TerraPDF could not render the document: {ex.Message}")],
                Warnings = validator.Warnings,
            };
        }
    }

    private PdfSpecValidator CreateValidator(out AssetResolver assets)
    {
        assets = new AssetResolver(_assetDirectory, _maxAssetBytes);
        var validator = new PdfSpecValidator(assets);
        validator.AddKnownFamilies(_hostFamilies);
        return validator;
    }

    // ── Document ────────────────────────────────────────────────────────

    private static void ComposeDocument(IDocumentContainer doc, PdfDocumentSpec spec, RenderContext ctx)
    {
        doc.MetadataTitle(spec.Title);
        doc.MetadataAuthor(spec.Author);
        doc.MetadataSubject(spec.Subject);
        doc.MetadataKeywords(spec.Keywords);
        doc.MetadataCreator("TerraPDF.Agents");

        if (spec.Encryption is { } enc)
            doc.Encrypt(BuildEncryption(enc));

        if (spec.TableOfContents == true)
            doc.TableOfContents(page => ConfigurePage(page, ctx));

        doc.Page(page =>
        {
            ConfigurePage(page, ctx);
            if (spec.HeaderOnFirstPageOnly == true) page.HeaderOnFirstPageOnly();

            if (spec.Header is { Count: > 0 })
            {
                page.Header().PaddingBottom(10).Column(col =>
                {
                    col.Spacing(4);
                    RenderBlocks(col, spec.Header, ctx, ctx.ContentWidth);
                });
            }

            page.Content().Column(col =>
            {
                col.Spacing(8);
                RenderBlocks(col, spec.Content!, ctx, ctx.ContentWidth);
            });

            bool pageNumbers = spec.PageNumbers ?? true;
            if (spec.Footer is { Count: > 0 } || pageNumbers)
            {
                page.Footer().PaddingTop(10).Column(col =>
                {
                    col.Spacing(4);
                    if (spec.Footer is not null) RenderBlocks(col, spec.Footer, ctx, ctx.ContentWidth);
                    if (pageNumbers)
                    {
                        col.Item().AlignCenter().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(ctx.Muted);
                            t.CurrentPageNumber().FontSize(8).FontColor(ctx.Muted);
                            t.Span(" of ").FontSize(8).FontColor(ctx.Muted);
                            t.TotalPages().FontSize(8).FontColor(ctx.Muted);
                        });
                    }
                });
            }
        });
    }

    private static void ConfigurePage(PageDescriptor page, RenderContext ctx)
    {
        page.Size(ctx.PageSize);
        page.Margin(ctx.Margin);
        if (ctx.PageColor is not null) page.PageColor(ctx.PageColor);
        page.DefaultTextStyle(s => s.FontSize(ctx.FontSize).FontColor(ctx.TextColor).FontFamily(ctx.FontFamily));
    }

    private static EncryptionOptions BuildEncryption(EncryptionSpec enc)
    {
        PdfPermissions permissions = PdfPermissions.ExtractForAccessibility;
        if (enc.AllowPrinting ?? true) permissions |= PdfPermissions.Print;
        if (enc.AllowCopying ?? true) permissions |= PdfPermissions.CopyText;
        if (enc.AllowEditing ?? false)
            permissions |= PdfPermissions.ModifyContents | PdfPermissions.ModifyAnnotations
                         | PdfPermissions.FillForms | PdfPermissions.AssembleDocument;

        return new EncryptionOptions
        {
            UserPassword = enc.UserPassword,
            OwnerPassword = enc.OwnerPassword,
            Permissions = permissions,
        };
    }

    private static Dictionary<string, string> RegisterFonts(List<FontSpec>? fonts, AssetResolver assets)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (fonts is null) return map;

        foreach (FontSpec font in fonts)
        {
            // Hash every face together so the internal name identifies this exact family.
            var faces = new List<(byte[] Data, bool Bold, bool Italic)>();
            void Add(string? source, bool bold, bool italic)
            {
                if (!string.IsNullOrWhiteSpace(source) && assets.TryLoad(source, AssetKind.Font, out byte[] data, out _))
                    faces.Add((data, bold, italic));
            }
            Add(font.Regular, false, false);
            Add(font.Bold, true, false);
            Add(font.Italic, false, true);
            Add(font.BoldItalic, true, true);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var face in faces) hash.AppendData(face.Data);
            string internalName = "agent-" + Convert.ToHexString(hash.GetHashAndReset())[..16];

            if (s_registeredFaces.TryAdd(internalName, true))
            {
                foreach (var face in faces) FontFamily.Register(internalName, face.Data, face.Bold, face.Italic);
            }
            map[font.Family!] = internalName;
        }
        return map;
    }

    private static int CountPages(byte[] pdf)
    {
        // The page-tree root's /Count is plain text even in encrypted output
        // (encryption covers strings and streams, not dictionary integers).
        string text = Encoding.Latin1.GetString(pdf);
        int pages = text.IndexOf("/Type /Pages", StringComparison.Ordinal);
        int count = pages < 0 ? -1 : text.IndexOf("/Count ", pages, StringComparison.Ordinal);
        if (count < 0) return 0;
        int start = count + "/Count ".Length, end = start;
        while (end < text.Length && char.IsDigit(text[end])) end++;
        return int.TryParse(text.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
    }

    // ── Blocks ──────────────────────────────────────────────────────────

    private static void RenderBlocks(ColumnDescriptor col, List<BlockSpec> blocks, RenderContext ctx, double width)
    {
        foreach (BlockSpec block in blocks)
        {
            if (block.Type == "pageBreak") col.PageBreak();
            else RenderBlock(col.Item(), block, ctx, width);
        }
    }

    private static void RenderBlock(IContainer item, BlockSpec b, RenderContext ctx, double width)
    {
        if (b.Link is not null && b.Type is "paragraph" or "image" or "heading")
            item = item.Hyperlink(b.Link);

        switch (b.Type)
        {
            case "heading": RenderHeading(item, b, ctx); break;
            case "paragraph": RenderParagraph(item, b, ctx); break;
            case "list": RenderList(item, b, ctx); break;
            case "table": RenderTable(item, b, ctx); break;
            case "keyValue": RenderKeyValue(item, b, ctx, width); break;
            case "callout": RenderCallout(item, b, ctx); break;
            case "columns": RenderColumns(item, b, ctx, width); break;
            case "image": RenderImage(item, b, ctx, width); break;
            case "chart": item.Canvas(b.Height ?? 220, c => ChartRenderer.Draw(c, b, ctx, width)); break;
            case "barcode":
                Align(item, b.Align ?? "left").Barcode(b.Data!, Math.Min(b.Width ?? 220, width), b.Height ?? 40,
                    SpecText.NormalizeColor(b.Color) ?? "#000000", showCaption: b.ShowCaption ?? true);
                break;
            case "qrCode":
                Align(item, b.Align ?? "left").QrCode(b.Data!, Math.Min(b.Size ?? 100, width), ParseLevel(b.ErrorCorrection),
                    SpecText.NormalizeColor(b.Color) ?? "#000000");
                break;
            case "divider":
                item.PaddingVertical(4).LineHorizontal(b.Thickness ?? 1, SpecText.NormalizeColor(b.Color) ?? "#D0D5DD");
                break;
            case "spacer":
                item.Canvas(b.Height!.Value, _ => { });
                break;
        }
    }

    private static void RenderHeading(IContainer item, BlockSpec b, RenderContext ctx)
    {
        int level = Math.Clamp(b.Level ?? 2, 1, 6);
        IContainer target = level <= 2 ? item.PaddingTop(6) : item.PaddingTop(2);
        TextDescriptor text = level switch
        {
            1 => target.H1(b.Text!),
            2 => target.H2(b.Text!),
            3 => target.H3(b.Text!),
            4 => target.H4(b.Text!),
            5 => target.H5(b.Text!),
            _ => target.H6(b.Text!),
        };
        text.FontColor(SpecText.NormalizeColor(b.Color) ?? ctx.Accent);
        if (b.FontSize is { } size) text.FontSize(size);
        ApplyAlign(text, b.Align);
    }

    private static void RenderParagraph(IContainer item, BlockSpec b, RenderContext ctx)
    {
        if (b.Spans is { Count: > 0 })
        {
            TextDescriptor text = item.Text(t =>
            {
                foreach (SpanSpec s in b.Spans)
                {
                    SpanDescriptor span = t.Span(s.Text ?? string.Empty);
                    if (s.Bold == true) span.Bold();
                    if (s.Italic == true) span.Italic();
                    if (s.Underline == true) span.Underline();
                    if (s.Strikethrough == true) span.Strikethrough();
                    if (SpecText.NormalizeColor(s.Color) is { } color) span.FontColor(color);
                    if (s.FontSize is { } size) span.FontSize(size);
                }
            });
            StyleBlockText(text, b, ctx);
            return;
        }

        string color = SpecText.NormalizeColor(b.Color) ?? (b.Link is not null ? ctx.Accent : ctx.TextColor);
        RenderRichText(item, b.Text!, ctx, b, color, underline: b.Link is not null);
    }

    /// <summary>Renders text with inline markup, one text block per line so explicit newlines are honoured.</summary>
    private static void RenderRichText(IContainer item, string text, RenderContext ctx, BlockSpec? style,
        string? color = null, bool underline = false, bool bold = false)
    {
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 1)
        {
            RenderLine(item, lines[0]);
            return;
        }

        item.Column(col =>
        {
            col.Spacing(3);
            foreach (string line in lines) RenderLine(col.Item(), line);
        });

        void RenderLine(IContainer target, string line)
        {
            TextDescriptor descriptor = target.Text(t =>
            {
                foreach (InlineRun run in SpecText.ParseInline(line))
                {
                    SpanDescriptor span = t.Span(run.Text);
                    if (run.Bold) span.Bold();
                    if (run.Italic) span.Italic();
                    if (color is not null) span.FontColor(color);
                    if (underline) span.Underline();
                }
            });
            if (bold) descriptor.Bold();
            if (style is not null) StyleBlockText(descriptor, style, ctx);
        }
    }

    private static void StyleBlockText(TextDescriptor text, BlockSpec b, RenderContext ctx)
    {
        if (b.FontSize is { } size) text.FontSize(size);
        if (b.Bold == true) text.Bold();
        if (b.Italic == true) text.Italic();
        if (b.Spans is { Count: > 0 } && SpecText.NormalizeColor(b.Color) is { } color) text.FontColor(color);
        ApplyAlign(text, b.Align);
    }

    private static void RenderList(IContainer item, BlockSpec b, RenderContext ctx)
    {
        bool ordered = b.Ordered == true;
        double markerWidth = ordered ? 8 + (b.Items!.Count.ToString(CultureInfo.InvariantCulture).Length * 6) : 12;

        item.Column(col =>
        {
            col.Spacing(3);
            for (int i = 0; i < b.Items!.Count; i++)
            {
                string marker = ordered ? $"{i + 1}." : "•";
                col.Item().Row(row =>
                {
                    row.Spacing(4);
                    TextDescriptor m = row.ConstantItem(markerWidth).Text(marker).FontColor(ordered ? ctx.TextColor : ctx.Accent);
                    if (b.FontSize is { } size) m.FontSize(size);
                    RenderRichText(row.RelativeItem(), b.Items[i] ?? string.Empty, ctx, b, SpecText.NormalizeColor(b.Color));
                });
            }
        });
    }

    private static void RenderTable(IContainer item, BlockSpec b, RenderContext ctx)
    {
        List<ColumnSpec> columns = b.Columns!;
        bool striped = b.Striped ?? true;
        string headerBackground = SpecText.NormalizeColor(b.Color) ?? ctx.Accent;
        double fontSize = b.FontSize ?? Math.Max(ctx.FontSize - 1, 6);

        item.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                foreach (ColumnSpec c in columns) def.RelativeColumn(c.Width ?? 1);
            });

            if (columns.Any(c => !string.IsNullOrEmpty(c.Header)))
            {
                table.HeaderRow(row =>
                {
                    foreach (ColumnSpec c in columns)
                    {
                        TextDescriptor t = row.Cell().Background(headerBackground).Padding(5)
                            .Text(c.Header ?? string.Empty).Bold().FontSize(fontSize).FontColor("#FFFFFF");
                        ApplyAlign(t, c.Align);
                    }
                });
            }

            int index = 0;
            foreach (List<string?> cells in b.Rows ?? [])
            {
                string? background = striped && index++ % 2 == 1 ? "#F4F6F9" : null;
                AddRow(table, cells, background, bold: false);
            }

            foreach (List<string?> cells in b.FooterRows ?? [])
                AddRow(table, cells, ctx.AccentTint, bold: true);
        });

        void AddRow(TableDescriptor table, List<string?> cells, string? background, bool bold) =>
            table.Row(row =>
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    IContainer cell = row.Cell();
                    if (background is not null) cell = cell.Background(background);
                    cell = cell.BorderBottom(0.5, "#DDE1E6").Padding(5);

                    string value = c < cells.Count ? cells[c] ?? string.Empty : string.Empty;
                    TextDescriptor t = cell.Text(tx =>
                    {
                        foreach (InlineRun run in SpecText.ParseInline(value))
                        {
                            SpanDescriptor span = tx.Span(run.Text);
                            if (run.Bold) span.Bold();
                            if (run.Italic) span.Italic();
                        }
                    }).FontSize(fontSize);
                    if (bold) t.Bold();
                    ApplyAlign(t, columns[c].Align);
                }
            });
    }

    private static void RenderKeyValue(IContainer item, BlockSpec b, RenderContext ctx, double width)
    {
        double fontSize = b.FontSize ?? ctx.FontSize;
        double labelWidth = b.Entries!
            .Select(e => VectorCanvas.MeasureTextWidth(e.Label ?? string.Empty, fontSize, ctx.FontFamily, bold: true))
            .DefaultIfEmpty(0).Max() + 12;
        labelWidth = Math.Min(labelWidth, width * 0.45);

        item.Column(col =>
        {
            col.Spacing(3);
            foreach (KeyValueSpec entry in b.Entries!)
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(labelWidth).Text(entry.Label ?? string.Empty).Bold().FontSize(fontSize).FontColor(ctx.Muted);
                    RenderRichText(row.RelativeItem(), entry.Value ?? string.Empty, ctx, b, SpecText.NormalizeColor(b.Color));
                });
            }
        });
    }

    private static void RenderCallout(IContainer item, BlockSpec b, RenderContext ctx)
    {
        string color = SpecText.NormalizeColor(b.Color) ?? ctx.Accent;
        item.Background(SpecText.Tint(color, 0.9)).BorderLeft(3, color).Padding(10).Column(col =>
        {
            col.Spacing(4);
            if (!string.IsNullOrWhiteSpace(b.Title))
                col.Item().Text(b.Title).Bold().FontColor(color).FontSize((b.FontSize ?? ctx.FontSize) + 1);
            RenderRichText(col.Item(), b.Text!, ctx, b);
        });
    }

    private static void RenderColumns(IContainer item, BlockSpec b, RenderContext ctx, double width)
    {
        List<ColumnSpec> columns = b.Columns!;
        double spacing = b.Spacing ?? 16;
        double totalWeight = columns.Sum(c => c.Width ?? 1);
        double usable = Math.Max(width - (spacing * (columns.Count - 1)), 1);

        item.Row(row =>
        {
            row.Spacing(spacing);
            foreach (ColumnSpec c in columns)
            {
                double share = usable * (c.Width ?? 1) / totalWeight;
                row.RelativeItem(c.Width ?? 1).Column(col =>
                {
                    col.Spacing(6);
                    RenderBlocks(col, c.Content!, ctx, share);
                });
            }
        });
    }

    private static void RenderImage(IContainer item, BlockSpec b, RenderContext ctx, double width)
    {
        ctx.Assets.TryLoad(b.Source!, AssetKind.Image, out byte[] data, out _);

        // Default to the image's natural size rather than full width, so a logo
        // stays logo-sized; never exceed the space available.
        double target = b.Width ?? VectorCanvas.GetImageSizeInPoints(data).Width;
        Align(item, b.Align ?? "left").Image(data, Math.Min(target, width));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static IContainer Align(IContainer item, string align) => align.ToLowerInvariant() switch
    {
        "center" => item.AlignCenter(),
        "right" => item.AlignRight(),
        _ => item.AlignLeft(),
    };

    private static void ApplyAlign(TextDescriptor text, string? align)
    {
        switch (align?.ToLowerInvariant())
        {
            case "center": text.AlignCenter(); break;
            case "right": text.AlignRight(); break;
            case "justify": text.Justify(); break;
        }
    }

    private static QrErrorCorrectionLevel ParseLevel(string? level) => level?.ToUpperInvariant() switch
    {
        "L" => QrErrorCorrectionLevel.L,
        "Q" => QrErrorCorrectionLevel.Q,
        "H" => QrErrorCorrectionLevel.H,
        _ => QrErrorCorrectionLevel.M,
    };
}
