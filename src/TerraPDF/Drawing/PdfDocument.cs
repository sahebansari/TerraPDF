using System;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using TerraPDF.Core;

namespace TerraPDF.Drawing;

/// <summary>
/// Builds and serializes a PDF 1.7 document to a stream.
/// No third-party dependencies - pure binary PDF construction.
/// </summary>
internal sealed class PdfDocument
{
    private readonly List<PdfPage> _pages = new();

    // Bookmark (outline) data
    private List<BookmarkInfo>? _allBookmarks; // all bookmark nodes
    private int _totalLogicalPages;

    // Document metadata (Info dictionary)
    private string? _infoTitle;
    private string? _infoAuthor;
    private string? _infoSubject;
    private string? _infoKeywords;
    private string? _infoCreator;

    // Encryption
    private PdfEncryption? _encryption;
    private EncryptionOptions? _encryptionOptions;

    /// <summary>
    /// Associates a bookmark collection and the total logical page count with this document.
    /// Called by DocumentComposer before rendering.
    /// </summary>
    internal void SetBookmarks(List<BookmarkInfo> allBookmarks, int totalLogicalPages)
    {
        _allBookmarks = allBookmarks;
        _totalLogicalPages = totalLogicalPages;
    }

    /// <summary>
    /// Sets document metadata fields for the Info dictionary.
    /// </summary>
    internal void SetMetadata(string? title, string? author, string? subject, string? keywords, string? creator)
    {
        _infoTitle    = title;
        _infoAuthor   = author;
        _infoSubject  = subject;
        _infoKeywords = keywords;
        _infoCreator  = creator;
    }

    /// <summary>
    /// Enables AES-128 encryption for the document.
    /// Must be called before <see cref="Save"/> to take effect.
    /// </summary>
    internal void SetEncryption(EncryptionOptions options)
    {
        _encryptionOptions = options;
        // PdfEncryption is created in Save() once the random fileId is available.
        _encryption = null;
    }

    internal PdfPage AddPage(double widthPt, double heightPt)
    {
        var page = new PdfPage(widthPt, heightPt);
        _pages.Add(page);
        return page;
    }

    // --------------------------------------------------------------
    //  Save
    // --------------------------------------------------------------

    public void Save(Stream output)
    {
        // PDF requires ISO-8859-1 (Latin-1) encoding for raw bytes.
        var enc = Encoding.Latin1;

        // Stream directly to the output, tracking the byte offset ourselves so
        // the xref table can be written without buffering the whole file.
        // Works on non-seekable streams (no Position reads, no seeking).
        // The BufferedStream is deliberately not disposed — that would close
        // the caller's stream; it is flushed at the end instead.
        var buffered = new BufferedStream(output, 64 * 1024);
        long position = 0;
        void WriteBytes(ReadOnlySpan<byte> b)
        {
            buffered.Write(b);
            position += b.Length;
        }
        void WriteStr(string s) => WriteBytes(enc.GetBytes(s));

        // -- Object allocation -------------------------------------
        // Text objects: (id, body string).
        // Binary stream objects: (id, dict string, raw stream bytes) - used for image XObjects.
        var objects       = new List<(int id, string body)>();
        var binaryObjects = new List<(int id, string dict, byte[] stream)>();
        int nextId = 1;

        // Standard Type1 fonts - no embedding required.
        // Aliases F1-F12 cover the three standard families (Helvetica, Times,
        // Courier) in all four weight/slant variants; see PdfFonts.All.
        var fontIds = new int[PdfFonts.All.Length];
        for (int fi = 0; fi < PdfFonts.All.Length; fi++)
        {
            fontIds[fi] = nextId++;
            objects.Add((fontIds[fi],
                $"<< /Type /Font /Subtype /Type1 /BaseFont /{PdfFonts.All[fi].BaseFont} /Encoding /WinAnsiEncoding >>"));
        }
        string fontResources = string.Join(" ",
            PdfFonts.All.Select((f, fi) => $"/{f.Alias} {fontIds[fi]} 0 R"));

        // Generate file ID and initialise encryption now that options are available.
        // The file ID must be the same value used in key derivation AND written to /ID.
        // PDF spec §7.6.3.3 step c requires the first element of the /ID array in the
        // key-derivation hash — so it must be fixed before any encryption happens.
        byte[]? fileId     = null;
        string  fileIdHex  = string.Empty;
        if (_encryptionOptions is not null)
        {
            fileId    = RandomNumberGenerator.GetBytes(16);
            fileIdHex = BytesToHexString(fileId);
            _encryption = new PdfEncryption(_encryptionOptions, fileId);
        }

        // Image XObjects — deduplicated document-wide: identical image data
        // (same pixels/dimensions/format) is embedded once and shared by every
        // page's /Resources, so a logo repeated on N pages costs one object.
        var imageObjectByHash = new Dictionary<string, int>();
        var pageImageMaps = new List<Dictionary<string, int>>();
        foreach (var page in _pages)
        {
            var imgMap = new Dictionary<string, int>();
            foreach (var (alias, img) in page.ImageObjects)
            {
                string key = img.ContentKey;
                if (imageObjectByHash.TryGetValue(key, out int existingId))
                {
                    imgMap[alias] = existingId;
                    continue;
                }

                // RGB and palette PNGs are embedded still compressed: the viewer
                // undoes the PNG row filters itself (/Predictor 15), so there is
                // neither a decode nor a re-compression.
                var passthrough = img.IsJpeg ? null : PngDecoder.TryReadPassthrough(img.Data);
                if (passthrough is not null)
                {
                    string colorSpace = "/DeviceRGB";
                    int colors = 3;
                    if (passthrough.Palette is { } palette)
                    {
                        // Lookup table as its own stream, so encryption covers it like any stream.
                        int paletteId = nextId++;
                        byte[] paletteData = _encryption is not null
                            ? _encryption.EncryptBytes(palette, paletteId, 0)
                            : palette;
                        binaryObjects.Add((paletteId, $"<< /Length {paletteData.Length} >>", paletteData));
                        colorSpace = $"[/Indexed /DeviceRGB {palette.Length / 3 - 1} {paletteId} 0 R]";
                        colors = 1;
                    }

                    int rawId = nextId++;
                    imgMap[alias] = rawId;
                    imageObjectByHash[key] = rawId;
                    byte[] rawData = _encryption is not null
                        ? _encryption.EncryptBytes(passthrough.ZlibData, rawId, 0)
                        : passthrough.ZlibData;
                    string rawDict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace {colorSpace} /BitsPerComponent 8 " +
                        $"/Filter /FlateDecode " +
                        $"/DecodeParms << /Predictor 15 /Colors {colors} /BitsPerComponent 8 /Columns {img.Width} >> " +
                        $"/Length {rawData.Length} >>";
                    binaryObjects.Add((rawId, rawDict, rawData));
                    continue;
                }

                // Other PNGs (alpha channel) are decoded and compressed here — or taken
                // from the process-wide cache when an earlier document already converted
                // the same file; JPEG bytes are embedded verbatim.
                var encoded = img.IsJpeg ? null : EncodedImageCache.GetOrAdd(img, ConvertPng);

                // RGBA transparency: emit the alpha channel as an 8-bit
                // DeviceGray soft-mask image referenced via /SMask.
                string smaskRef = string.Empty;
                if (encoded?.CompressedAlpha is { } alphaCompressed)
                {
                    int smaskId = nextId++;
                    byte[] alphaData = _encryption is not null
                        ? _encryption.EncryptBytes(alphaCompressed, smaskId, 0)
                        : alphaCompressed;
                    string smaskDict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace /DeviceGray /BitsPerComponent 8 " +
                        $"/Filter /FlateDecode /Length {alphaData.Length} >>";
                    binaryObjects.Add((smaskId, smaskDict, alphaData));
                    smaskRef = $"/SMask {smaskId} 0 R ";
                }

                int imgId = nextId++;
                imgMap[alias] = imgId;
                imageObjectByHash[key] = imgId;

                if (img.IsJpeg)
                {
                    string cs = img.Components switch
                    {
                        1 => "/DeviceGray",
                        4 => "/DeviceCMYK",
                        _ => "/DeviceRGB",
                    };
                    byte[] imgData = _encryption is not null
                        ? _encryption.EncryptBytes(img.Data, imgId, 0)
                        : img.Data;
                    string dict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace {cs} /BitsPerComponent 8 " +
                        $"{smaskRef}" +
                        $"/Filter /DCTDecode /Length {imgData.Length} >>";
                    binaryObjects.Add((imgId, dict, imgData));
                }
                else
                {
                    byte[] compressed = encoded!.CompressedRgb;
                    byte[] imgData = _encryption is not null
                        ? _encryption.EncryptBytes(compressed, imgId, 0)
                        : compressed;
                    string dict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace /DeviceRGB /BitsPerComponent 8 " +
                        $"{smaskRef}" +
                        $"/Filter /FlateDecode /Length {imgData.Length} >>";
                    binaryObjects.Add((imgId, dict, imgData));
                }
            }
            pageImageMaps.Add(imgMap);
        }

        // Constant-alpha ExtGState resources — deduplicated document-wide by
        // opacity value, same pattern as images/fonts above: two shapes at the
        // same opacity (even on different pages) share one /ExtGState object.
        var extGStateObjectByOpacity = new Dictionary<double, int>();
        var pageExtGStateMaps = new List<Dictionary<string, int>>();
        foreach (var page in _pages)
        {
            var gsMap = new Dictionary<string, int>();
            foreach (var (alias, opacity) in page.ExtGStateObjects)
            {
                if (!extGStateObjectByOpacity.TryGetValue(opacity, out int existingId))
                {
                    existingId = nextId++;
                    extGStateObjectByOpacity[opacity] = existingId;
                    string alphaStr = opacity.ToString("F4", CultureInfo.InvariantCulture);
                    objects.Add((existingId, $"<< /Type /ExtGState /ca {alphaStr} /CA {alphaStr} >>"));
                }
                gsMap[alias] = existingId;
            }
            pageExtGStateMaps.Add(gsMap);
        }

        // Custom (embedded) fonts — deduplicated document-wide by variant identity,
        // mirroring the image dedup above: a font is embedded once regardless of how
        // many pages (or how many times per page) it is used. Glyph usage is merged
        // across every page first so the /W width array and /ToUnicode CMap cover
        // exactly the glyphs this document actually shows — no more, no less.
        var customGlyphUsageAll = new Dictionary<TrueType.CustomFontVariant, Dictionary<ushort, int>>();
        foreach (var page in _pages)
        {
            foreach (var (variant, glyphs) in page.CustomGlyphUsage)
            {
                if (!customGlyphUsageAll.TryGetValue(variant, out var merged))
                    customGlyphUsageAll[variant] = merged = new Dictionary<ushort, int>();
                foreach (var (gid, codepoint) in glyphs)
                    merged.TryAdd(gid, codepoint);
            }
        }

        var customFontObjectIds = new Dictionary<TrueType.CustomFontVariant, int>();
        foreach (var (variant, glyphs) in customGlyphUsageAll)
        {
            // FontFile2: the TrueType file with every glyph this document never
            // shows blanked out of 'glyf' (see TrueTypeFont.Subsetting.cs — glyph
            // IDs are never renumbered, so cmap/hmtx/GSUB/CIDToGIDMap all stay
            // valid unchanged), Flate-compressed. /Length1 is the required
            // uncompressed byte length (PDF §9.9) of what's actually embedded.
            int fontFileId = nextId++;
            byte[] subsetFont = variant.Font.BuildSubsetRawData(glyphs.Keys.ToHashSet());
            byte[] compressedFont = Compress(subsetFont);
            byte[] fontFileData = _encryption is not null
                ? _encryption.EncryptBytes(compressedFont, fontFileId, 0)
                : compressedFont;
            binaryObjects.Add((fontFileId,
                $"<< /Length1 {subsetFont.Length} /Filter /FlateDecode /Length {fontFileData.Length} >>",
                fontFileData));

            // Subset tag (PDF §9.6.4): flags to any consumer that this FontFile2
            // is not the full original font installed elsewhere under this name.
            // Deterministic per variant, not random, so re-generating the same
            // document produces the same bytes.
            string subsetBaseFontName = $"{SubsetTag(variant.BaseFontName)}+{variant.BaseFontName}";

            // FontDescriptor
            int descriptorId = nextId++;
            var (xMin, yMin, xMax, yMax) = variant.Font.FontBBox;
            objects.Add((descriptorId,
                $"<< /Type /FontDescriptor /FontName /{subsetBaseFontName} " +
                $"/Flags {variant.DescriptorFlags} " +
                $"/FontBBox [{Inv(xMin)} {Inv(yMin)} {Inv(xMax)} {Inv(yMax)}] " +
                $"/ItalicAngle {Inv(variant.Font.ItalicAngle)} " +
                $"/Ascent {Inv(variant.Font.Ascent)} /Descent {Inv(variant.Font.Descent)} " +
                $"/CapHeight {Inv(variant.Font.CapHeight)} /StemV {variant.StemV} " +
                $"/FontFile2 {fontFileId} 0 R >>"));

            // CIDFontType2 descendant — /W built only from glyph IDs actually used
            // anywhere in the document (CIDToGIDMap /Identity: the CID a content
            // stream shows *is* the glyph index into the embedded font, since glyph
            // IDs are preserved as-is by the blanking-only subsetting above).
            int cidFontId = nextId++;
            string wArray = string.Join(" ", glyphs.Keys.OrderBy(g => g)
                .Select(gid => $"{gid} [{Inv(variant.Font.GetAdvanceWidthInEm(gid))}]"));
            objects.Add((cidFontId,
                $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{subsetBaseFontName} " +
                $"/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
                $"/FontDescriptor {descriptorId} 0 R /DW 1000 /W [{wArray}] /CIDToGIDMap /Identity >>"));

            // ToUnicode CMap (GID -> Unicode) so copy/paste and text extraction work.
            int toUnicodeId = nextId++;
            byte[] cmapCompressed = Compress(Encoding.ASCII.GetBytes(BuildToUnicodeCMap(glyphs)));
            byte[] cmapData = _encryption is not null
                ? _encryption.EncryptBytes(cmapCompressed, toUnicodeId, 0)
                : cmapCompressed;
            binaryObjects.Add((toUnicodeId, $"<< /Filter /FlateDecode /Length {cmapData.Length} >>", cmapData));

            // Type0 composite font — the object actually referenced from /Resources /Font.
            int type0Id = nextId++;
            objects.Add((type0Id,
                $"<< /Type /Font /Subtype /Type0 /BaseFont /{subsetBaseFontName} " +
                $"/Encoding /Identity-H /DescendantFonts [{cidFontId} 0 R] /ToUnicode {toUnicodeId} 0 R >>"));

            customFontObjectIds[variant] = type0Id;
        }

        // Content streams (one per page) — always Flate-compressed.
        // When encrypted, compression happens first (the /Filter describes the
        // decoded stream; encryption is transparent to filters per PDF §7.6.1),
        // mirroring the PNG XObject path above.
        var compressedPages = CompressContentStreams(_pages);
        var contentIds = new List<int>();
        for (int pi = 0; pi < _pages.Count; pi++)
        {
            int cid  = nextId++;
            contentIds.Add(cid);

            byte[] compressed = compressedPages[pi];
            byte[] data = _encryption is not null
                ? _encryption.EncryptBytes(compressed, cid, 0)
                : compressed;
            binaryObjects.Add((cid, $"<< /Length {data.Length} /Filter /FlateDecode >>", data));
        }

        // Link annotation objects
        var pageAnnotIds = new List<List<int>>();
        foreach (var page in _pages)
        {
            var annotIds = new List<int>();
            foreach (var annot in page.LinkAnnotations)
            {
                int annotId = nextId++;
                double pdfX1 = annot.X;
                double pdfY1 = page.Height - annot.Y - annot.Height;
                double pdfX2 = annot.X + annot.Width;
                double pdfY2 = page.Height - annot.Y;
                string uri = PdfByteStringForObject(annot.Url, annotId);
                objects.Add((annotId,
                    $"<< /Type /Annot /Subtype /Link " +
                    $"/Rect [{Inv(pdfX1)} {Inv(pdfY1)} {Inv(pdfX2)} {Inv(pdfY2)}] " +
                    $"/Border [0 0 0] " +
                    $"/A << /Type /Action /S /URI /URI {uri} >> >>"));
                annotIds.Add(annotId);
            }
            pageAnnotIds.Add(annotIds);
        }

        // Allocate page object IDs first (needed for internal link destinations)
        int pagesId = nextId++;
        var pageIds = new List<int>();
        for (int i = 0; i < _pages.Count; i++)
            pageIds.Add(nextId++);

        string kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        objects.Add((pagesId, $"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>"));

        // Internal link annotations (GoTo destinations)
        var pageInternalAnnotIds = new List<List<int>>();
        foreach (var page in _pages)
        {
            var internalIds = new List<int>();
            foreach (var annot in page.InternalLinkAnnotations)
            {
                int annotId = nextId++;
                double pdfX1 = annot.X;
                double pdfY1 = page.Height - annot.Y - annot.Height;
                double pdfX2 = annot.X + annot.Width;
                double pdfY2 = page.Height - annot.Y;

                int targetIdx = annot.PageNumber - 1;
                if (targetIdx < 0 || targetIdx >= pageIds.Count)
                    throw new InvalidOperationException($"Internal link targets page {annot.PageNumber}, but document has only {pageIds.Count} pages.");

                int targetPageObjId = pageIds[targetIdx];
                string destPart = annot.Top.HasValue
                    ? $"/Dest [{targetPageObjId} 0 R /XYZ 0 {(_pages[targetIdx].Height - annot.Top.Value).ToString("F2", CultureInfo.InvariantCulture)} 0]"
                    : $"/Dest [{targetPageObjId} 0 R /Fit]";

                objects.Add((annotId,
                    $"<< /Type /Annot /Subtype /Link " +
                    $"/Rect [{Inv(pdfX1)} {Inv(pdfY1)} {Inv(pdfX2)} {Inv(pdfY2)}] " +
                    $"/Border [0 0 0] " +
                    $"{destPart} >>"));
                internalIds.Add(annotId);
            }
            pageInternalAnnotIds.Add(internalIds);
        }

        // Page objects
        for (int i = 0; i < _pages.Count; i++)
        {
            var p   = _pages[i];
            int pid = pageIds[i];

            string xObjectDict = pageImageMaps[i].Count > 0
                ? "/XObject << " +
                  string.Join(" ", pageImageMaps[i].Select(kv => $"/{kv.Key} {kv.Value} 0 R")) +
                  " >> "
                : string.Empty;

            string extGStateDict = pageExtGStateMaps[i].Count > 0
                ? "/ExtGState << " +
                  string.Join(" ", pageExtGStateMaps[i].Select(kv => $"/{kv.Key} {kv.Value} 0 R")) +
                  " >> "
                : string.Empty;

            // Gradient fills drawn on this page, written inline as shading dictionaries.
            string shadingDict = p.ShadingObjects.Count > 0
                ? "/Shading << " +
                  string.Join(" ", p.ShadingObjects.Select(kv => $"/{kv.Key} {ShadingDictionary(kv.Value)}")) +
                  " >> "
                : string.Empty;

            // Custom-font entries this page actually uses, resolved to their
            // document-wide-deduplicated Type0 object — appended alongside the
            // (unchanged) standard-font resources every page already carries.
            string customFontResources = p.CustomFontObjects.Count > 0
                ? " " + string.Join(" ", p.CustomFontObjects.Select(kv =>
                    $"/{kv.Key} {customFontObjectIds[kv.Value]} 0 R"))
                : string.Empty;

            var allAnnotIds = pageAnnotIds[i].Concat(pageInternalAnnotIds[i]).ToList();
            string annotStr = allAnnotIds.Count > 0
                ? "/Annots [" + string.Join(" ", allAnnotIds.Select(id => $"{id} 0 R")) + "] "
                : string.Empty;

            objects.Add((pid,
                $"<< /Type /Page /Parent {pagesId} 0 R " +
                $"/MediaBox [0 0 {Inv(p.Width)} {Inv(p.Height)}] " +
                $"/Contents {contentIds[i]} 0 R " +
                $"{annotStr}" +
                $"/Resources << /Font << {fontResources}{customFontResources} >> {xObjectDict}{extGStateDict}{shadingDict}>> >>"));
        }

        // Outlines (bookmarks)
        int outlinesId = WriteBookmarks(ref nextId, objects, pageIds);

        // Info dictionary
        int infoId = WriteInfoDictionary(ref nextId, objects);

        // Encrypt dictionary (if encryption is enabled)
        int encryptId = WriteEncryptDictionary(ref nextId, objects);

        // Catalog
        int catalogId = nextId++;
        var catalogParts = new List<string>
        {
            $"/Type /Catalog",
            $"/Pages {pagesId} 0 R"
        };
        if (outlinesId != 0)
            catalogParts.Add($"/Outlines {outlinesId} 0 R");

        string catalogDict = $"<< {string.Join(" ", catalogParts)} >>";
        objects.Add((catalogId, catalogDict));

        // Sort for xref
        objects.Sort((a, b) => a.id.CompareTo(b.id));
        binaryObjects.Sort((a, b) => a.id.CompareTo(b.id));

        // -- Write header ------------------------------------------
        // AES-256 Rev 6 is defined by ISO 32000-2 (PDF 2.0); AES-128 Rev 4
        // requires PDF 1.6+; unencrypted documents are emitted as PDF 1.7.
        string pdfVersion = _encryptionOptions is null
            ? "%PDF-1.7\n"
            : _encryptionOptions.Algorithm == EncryptionAlgorithm.Aes256
                ? "%PDF-2.0\n"
                : "%PDF-1.6\n";
        WriteStr(pdfVersion);
        WriteBytes(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        // -- Write body objects ------------------------------------
        var offsets  = new Dictionary<int, long>();
        int txtIndex = 0;
        int binIndex = 0;

        while (txtIndex < objects.Count || binIndex < binaryObjects.Count)
        {
            bool writeBinary =
                binIndex < binaryObjects.Count &&
                (txtIndex >= objects.Count ||
                 binaryObjects[binIndex].id < objects[txtIndex].id);

            if (writeBinary)
            {
                var (id, dict, stream) = binaryObjects[binIndex++];
                offsets[id] = position;
                WriteStr($"{id} 0 obj\n{dict}\nstream\n");
                WriteBytes(stream);
                WriteStr("\nendstream\nendobj\n");
            }
            else
            {
                var (id, body) = objects[txtIndex++];
                offsets[id] = position;
                WriteStr($"{id} 0 obj\n{body}\nendobj\n");
            }
        }

        int totalCount = objects.Count + binaryObjects.Count + 1;

        // -- Cross-reference table ---------------------------------
        long xrefOffset = position;
        WriteStr("xref\n");
        WriteStr($"0 {totalCount}\n");
        WriteStr("0000000000 65535 f \n");
        for (int i = 1; i < totalCount; i++)
            WriteStr($"{offsets[i]:D10} 00000 n \n");

        // -- Trailer -----------------------------------------------
        // The Info dictionary is referenced from the trailer's /Info entry
        // (PDF 1.7 §7.5.5), not from the catalog.
        string infoRef = infoId != 0 ? $" /Info {infoId} 0 R" : string.Empty;
        WriteStr("trailer\n");
        if (encryptId != 0)
            WriteStr($"<< /Size {totalCount} /Root {catalogId} 0 R{infoRef} /Encrypt {encryptId} 0 R /ID [{fileIdHex} {fileIdHex}] >>\n");
        else
            WriteStr($"<< /Size {totalCount} /Root {catalogId} 0 R{infoRef} >>\n");
        WriteStr("startxref\n");
        WriteStr($"{xrefOffset}\n");
        WriteStr("%%EOF\n");

        buffered.Flush();
    }

        // --------------------------------------------------------------
        //  Helpers
        // --------------------------------------------------------------

        // Formats a page dimension for the /MediaBox array using invariant culture
        private static string Inv(double d) =>
            d.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>Documents at least this long compress their pages in parallel…</summary>
        internal const int ParallelCompressionMinPages = 8;

        /// <summary>…provided their content streams total at least this many characters.</summary>
        internal const long ParallelCompressionMinChars = 512 * 1024;

        /// <summary>Allows tests to compare the parallel and sequential paths; output is identical.</summary>
        internal static bool ParallelCompressionEnabled { get; set; } = true;

        // Compresses every page's content stream. Deflate is the most expensive step of
        // saving a large document, and pages are independent, so large documents compress
        // them on several cores. The results are placed by page index, so the output is
        // identical either way. Small documents stay sequential: there the scheduling cost
        // outweighs the gain, and a busy service already keeps every core occupied.
        private static byte[][] CompressContentStreams(List<PdfPage> pages)
        {
            var result = new byte[pages.Count][];
            long chars = 0;
            foreach (var page in pages) chars += page.ContentLength;

            if (ParallelCompressionEnabled
                && Environment.ProcessorCount > 1
                && pages.Count >= ParallelCompressionMinPages
                && chars >= ParallelCompressionMinChars)
            {
                Parallel.For(0, pages.Count, i => result[i] = CompressContentStream(pages[i]));
            }
            else
            {
                for (int i = 0; i < pages.Count; i++)
                    result[i] = CompressContentStream(pages[i]);
            }
            return result;
        }

        // Compresses a page's content stream straight from its operator buffer; the
        // output is identical to Compress() over the stream's Latin-1 bytes.
        private static byte[] CompressContentStream(PdfPage page)
        {
            using var ms   = new MemoryStream();
            using var zlib = new ZLibStream(ms, CompressionLevel.Optimal);
            page.WriteContentStream(zlib);
            zlib.Flush();
            zlib.Dispose();
            return ms.ToArray();
        }

        // Decodes a PNG with an alpha channel and compresses its colour and alpha planes
        // into the streams embedded as the image and its /SMask.
        private static EncodedImageCache.Entry ConvertPng(ImageSource image)
        {
            byte[] rgb = image.DecodePng(out byte[]? alpha);
            return new EncodedImageCache.Entry(Compress(rgb), alpha is null ? null : Compress(alpha));
        }

        // Compresses raw bytes using zlib/deflate (FlateDecode in PDF terms)
        private static byte[] Compress(byte[] data)
        {
            using var ms   = new MemoryStream();
            using var zlib = new ZLibStream(ms, CompressionLevel.Optimal);
            zlib.Write(data, 0, data.Length);
            zlib.Flush();
            zlib.Dispose();
            return ms.ToArray();
        }

        /// <summary>
        /// Deterministic 6-uppercase-letter PDF subset tag (§9.6.4) derived from
        /// <paramref name="baseFontName"/> via FNV-1a — stable across runs (unlike
        /// <see cref="string.GetHashCode()"/>, which is randomized per process), so
        /// regenerating the same document produces byte-identical output.
        /// </summary>
        private static string SubsetTag(string baseFontName)
        {
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(baseFontName))
            {
                hash ^= b;
                hash *= 16777619u;
            }
            var chars = new char[6];
            for (int i = 0; i < 6; i++)
            {
                chars[i] = (char)('A' + (int)(hash % 26));
                hash = hash * 2654435761u + 1;
            }
            return new string(chars);
        }

        /// <summary>
        /// Builds a <c>/ToUnicode</c> CMap stream (PDF §9.10.3) mapping each used glyph ID
        /// to its Unicode codepoint, chunked into ≤100-entry <c>beginbfchar</c> blocks per
        /// the conventional CMap limit. Codepoints above the BMP are emitted as their
        /// UTF-16BE surrogate pair, matching how <c>bfchar</c> destination strings are read.
        /// </summary>
        private static string BuildToUnicodeCMap(Dictionary<ushort, int> glyphs)
        {
            var sb = new StringBuilder();
            sb.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
            sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
            sb.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
            sb.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");

            var ordered = glyphs.OrderBy(kv => kv.Key).ToList();
            for (int i = 0; i < ordered.Count; i += 100)
            {
                var chunk = ordered.Skip(i).Take(100).ToList();
                sb.Append(CultureInfo.InvariantCulture, $"{chunk.Count} beginbfchar\n");
                foreach (var (gid, codepoint) in chunk)
                    sb.Append(CultureInfo.InvariantCulture, $"<{gid:X4}> <{ToUnicodeHex(codepoint)}>\n");
                sb.Append("endbfchar\n");
            }

            sb.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
            return sb.ToString();
        }

        /// <summary>Encodes a Unicode codepoint as a <c>bfchar</c> destination hex string (UTF-16BE).</summary>
        private static string ToUnicodeHex(int codepoint)
        {
            if (codepoint <= 0xFFFF)
                return codepoint.ToString("X4", CultureInfo.InvariantCulture);

            string utf16 = char.ConvertFromUtf32(codepoint);
            return ((int)utf16[0]).ToString("X4", CultureInfo.InvariantCulture) +
                   ((int)utf16[1]).ToString("X4", CultureInfo.InvariantCulture);
        }

    // ==============================================================
    // Bookmarks / Outlines generation
    // ==============================================================

    /// <summary>
    /// Generates the PDF outline tree (bookmarks) if any were defined.
    /// Returns the object ID of the Outlines dictionary, or 0 if no bookmarks.
    /// </summary>
    private int WriteBookmarks(ref int nextId, List<(int id, string body)> objects, List<int> pageIds)
    {
        if (_allBookmarks == null || _allBookmarks.Count == 0)
            return 0;

        // Allocate an object ID for the Outlines dictionary itself
        int outlinesId = nextId++;

        // Build ID map and nodes list
        var nodeToId = new Dictionary<BookmarkInfo, int>();
        var nodesInOrder = new List<BookmarkInfo>();
        foreach (var bm in _allBookmarks)
        {
            nodeToId[bm] = nextId++;  // assign next sequential ID
            nodesInOrder.Add(bm);
        }

        // Second pass: create each bookmark item object in ascending ID order.
        foreach (var node in nodesInOrder.OrderBy(n => nodeToId[n]))
        {
            int id = nodeToId[node];
            string body = BuildBookmarkItemBody(node, id, nodeToId, pageIds, outlinesId);
            objects.Add((id, body));
        }

        // Build the Outlines dictionary (root)
        var topLevel = _allBookmarks.Where(b => b.Parent == null).ToList();
        if (topLevel.Count == 0)
            return 0; // Should not happen

        int firstId = nodeToId[topLevel[0]];
        int lastId = nodeToId[topLevel[^1]];
        int totalCount = nodesInOrder.Count;

        string outlinesBody = $"<< /Type /Outlines /First {firstId} 0 R /Last {lastId} 0 R /Count {totalCount} >>";
        objects.Add((outlinesId, outlinesBody));

        return outlinesId;
    }

    /// <summary>
    /// Generates the PDF Info dictionary if any metadata fields were set.
    /// Returns the object ID, or 0 if no metadata.
    /// </summary>
    private int WriteInfoDictionary(ref int nextId, List<(int id, string body)> objects)
    {
        // Check if any metadata field is non-null
        if (string.IsNullOrEmpty(_infoTitle) && string.IsNullOrEmpty(_infoAuthor) &&
            string.IsNullOrEmpty(_infoSubject) && string.IsNullOrEmpty(_infoKeywords) &&
            string.IsNullOrEmpty(_infoCreator))
        {
            return 0;
        }

        int infoId = nextId++;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_infoTitle))
            parts.Add($"/Title {PdfTextStringForObject(_infoTitle, infoId)}");
        if (!string.IsNullOrEmpty(_infoAuthor))
            parts.Add($"/Author {PdfTextStringForObject(_infoAuthor, infoId)}");
        if (!string.IsNullOrEmpty(_infoSubject))
            parts.Add($"/Subject {PdfTextStringForObject(_infoSubject, infoId)}");
        if (!string.IsNullOrEmpty(_infoKeywords))
            parts.Add($"/Keywords {PdfTextStringForObject(_infoKeywords, infoId)}");
        if (!string.IsNullOrEmpty(_infoCreator))
            parts.Add($"/Creator {PdfTextStringForObject(_infoCreator, infoId)}");

        string infoBody = "<< " + string.Join(" ", parts) + " >>";
        objects.Add((infoId, infoBody));
        return infoId;
    }

    /// <summary>
    /// Generates the PDF Standard Security Handler /Encrypt dictionary —
    /// Revision 6 (AES-256, /AESV3) or Revision 4 (AES-128, /AESV2).
    /// Returns the object ID, or 0 if encryption is not configured.
    /// The /Encrypt object is referenced from the trailer, NOT from the catalog,
    /// and is itself NOT encrypted (per PDF spec §7.6.1).
    /// </summary>
    private int WriteEncryptDictionary(ref int nextId, List<(int id, string body)> objects)
    {
        if (_encryption is null)
            return 0;

        int encId = nextId++;

        string oHex = BytesToHexString(_encryption.OEntry);
        string uHex = BytesToHexString(_encryption.UEntry);

        string body;
        if (_encryption.IsAes256)
        {
            // Standard Security Handler Rev 6, AES-256 (ISO 32000-2 §7.6.4)
            body =
                "<< " +
                "/Filter /Standard " +
                "/V 5 " +               // Algorithm 5 (AES-256)
                "/R 6 " +               // Revision 6
                "/Length 256 " +        // Key length in bits
                $"/P {_encryption.PEntry} " +
                $"/O {oHex} " +
                $"/U {uHex} " +
                $"/OE {BytesToHexString(_encryption.OEEntry)} " +
                $"/UE {BytesToHexString(_encryption.UEEntry)} " +
                $"/Perms {BytesToHexString(_encryption.PermsEntry)} " +
                "/CF << /StdCF << /AuthEvent /DocOpen /CFM /AESV3 /Length 32 >> >> " +
                "/StmF /StdCF " +       // all streams use StdCF
                "/StrF /StdCF " +       // all strings use StdCF
                "/EncryptMetadata true " +
                ">>";
        }
        else
        {
            // Standard Security Handler Rev 4, AES-128
            body =
                "<< " +
                "/Filter /Standard " +
                "/V 4 " +               // Algorithm 4 (CF-based)
                "/R 4 " +               // Revision 4
                "/Length 128 " +        // Key length in bits
                $"/P {_encryption.PEntry} " +
                $"/O {oHex} " +
                $"/U {uHex} " +
                "/CF << /StdCF << /AuthEvent /DocOpen /CFM /AESV2 /Length 16 >> >> " +
                "/StmF /StdCF " +       // all streams use StdCF
                "/StrF /StdCF " +       // all strings use StdCF
                "/EncryptMetadata true " +
                ">>";
        }

        objects.Add((encId, body));
        return encId;
    }

    /// <summary>
    /// Inline dictionary for a two-stop shading: axial (type 2) or radial (type 3), with an
    /// exponential interpolation function (type 2, N = 1) between the two RGB colors.
    /// </summary>
    private static string ShadingDictionary(PdfPage.ShadingSpec s)
    {
        string Rgb(TerraPDF.Helpers.PdfColor c) => string.Create(CultureInfo.InvariantCulture, $"{c.R:F4} {c.G:F4} {c.B:F4}");
        string function = $"<< /FunctionType 2 /Domain [0 1] /C0 [{Rgb(s.From)}] /C1 [{Rgb(s.To)}] /N 1 >>";
        string coords = s.Radial
            ? string.Create(CultureInfo.InvariantCulture, $"[{s.X0:F2} {s.Y0:F2} {s.R0:F2} {s.X1:F2} {s.Y1:F2} {s.R1:F2}]")
            : string.Create(CultureInfo.InvariantCulture, $"[{s.X0:F2} {s.Y0:F2} {s.X1:F2} {s.Y1:F2}]");
        return $"<< /ShadingType {(s.Radial ? 3 : 2)} /ColorSpace /DeviceRGB /Coords {coords} " +
               $"/Function {function} /Extend [true true] >>";
    }

    /// <summary>Total number of descendants (children, grandchildren, …) of an outline node.</summary>
    private static int CountDescendants(BookmarkInfo node)
    {
        int count = node.Children.Count;
        foreach (var child in node.Children)
            count += CountDescendants(child);
        return count;
    }

    /// <summary>
    /// Returns a PDF string token for a <em>text string</em> that belongs to object
    /// <paramref name="objNum"/>.  When encryption is enabled the string bytes are
    /// AES-encrypted with that object's key and emitted as a hex string, matching the
    /// /StrF /StdCF declaration in the Encrypt dictionary (PDF §7.6.2: all strings in
    /// an encrypted document are encrypted with the key of the object that carries them).
    /// </summary>
    private string PdfTextStringForObject(string s, int objNum)
    {
        if (_encryption is null)
            return PdfTextString(s);
        return BytesToHexString(_encryption.EncryptBytes(EncodeTextStringBytes(s), objNum, 0));
    }

    /// <summary>
    /// Returns a PDF string token for a <em>byte string</em> (e.g. a URI, which is
    /// 7-bit ASCII per PDF §12.6.4.7) that belongs to object <paramref name="objNum"/>.
    /// Encrypted when document encryption is enabled, literal otherwise.
    /// </summary>
    private string PdfByteStringForObject(string s, int objNum)
    {
        if (_encryption is null)
            return $"({Escape(s)})";
        return BytesToHexString(_encryption.EncryptBytes(Encoding.Latin1.GetBytes(s), objNum, 0));
    }

    /// <summary>
    /// Encodes a text string to bytes: ASCII when possible, otherwise
    /// UTF-16BE with BOM (PDF 1.7 §7.9.2.2).
    /// </summary>
    private static byte[] EncodeTextStringBytes(string s)
    {
        bool ascii = true;
        foreach (char c in s)
        {
            if (c > '~' || c < ' ') { ascii = false; break; }
        }
        if (ascii)
            return Encoding.ASCII.GetBytes(s);

        byte[] utf16 = Encoding.BigEndianUnicode.GetBytes(s);
        byte[] withBom = new byte[utf16.Length + 2];
        withBom[0] = 0xFE;
        withBom[1] = 0xFF;
        utf16.CopyTo(withBom, 2);
        return withBom;
    }


    /// <summary>Converts a byte array to a PDF hex string token, e.g. &lt;AABBCC…&gt;.</summary>
    private static string BytesToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2 + 2);
        sb.Append('<');
        foreach (byte b in bytes)
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append('>');
        return sb.ToString();
    }

    /// <summary>
    /// Builds the PDF dictionary string for a single bookmark item.
    /// <paramref name="selfId"/> is the item's own object id, needed to encrypt
    /// the /Title string when document encryption is enabled.
    /// </summary>
    private string BuildBookmarkItemBody(
        BookmarkInfo node,
        int selfId,
        Dictionary<BookmarkInfo, int> idMap,
        List<int> pageIds,
        int outlinesRootId)
    {
        // Outline items carry no /Type entry (PDF 1.7 §12.3.3); only the
        // root Outlines dictionary does.
        var parts = new List<string>
        {
            $"/Title {PdfTextStringForObject(node.Title, selfId)}"
        };

        // Parent entry
        if (node.Parent != null)
            parts.Add($"/Parent {idMap[node.Parent]} 0 R");
        else
            parts.Add($"/Parent {outlinesRootId} 0 R");

        // Sibling links
        if (node.Prev != null) parts.Add($"/Prev {idMap[node.Prev]} 0 R");
        if (node.Next != null) parts.Add($"/Next {idMap[node.Next]} 0 R");

        // Children subtree (if any)
        if (node.Children.Count > 0)
        {
            int firstChildId = idMap[node.Children[0]];
            int lastChildId = idMap[node.Children[^1]];
            parts.Add($"/First {firstChildId} 0 R");
            parts.Add($"/Last {lastChildId} 0 R");
            // /Count of an open item = number of visible (open) descendants,
            // which for an all-open tree is the full descendant count (§12.3.3).
            parts.Add($"/Count {CountDescendants(node)}");
        }

        // Destination.  /XYZ with null left/zoom scrolls the target position to
        // the top of the window while RETAINING the reader's current zoom and
        // horizontal scroll (unlike /Fit and /FitH, which force a zoom change).
        // node.Top is measured from the top of the page (caller coordinates);
        // PDF destinations use a bottom-left origin, so flip against the page
        // height — same as internal-link destinations.
        int pageIdx = node.PageNumber - 1;
        if (pageIdx < 0 || pageIdx >= pageIds.Count)
            throw new InvalidOperationException($"Bookmark '{node.Title}' targets page {node.PageNumber} but document has only {pageIds.Count} pages.");

        int pageObjId = pageIds[pageIdx];
        double pageHeight = _pages[pageIdx].Height;
        double destTop = node.Top.HasValue
            ? pageHeight - node.Top.Value   // element position, flipped to PDF coords
            : pageHeight;                   // no position given: top of the page
        string topStr = destTop.ToString("F2", CultureInfo.InvariantCulture);
        parts.Add($"/Dest [{pageObjId} 0 R /XYZ null {topStr} null]");

        return "<< " + string.Join(" ", parts) + " >>";
    }

    /// <summary>
    /// Escapes special characters in a PDF string literal (backslash, parentheses).
    /// For ASCII-only strings (metadata, bookmark titles that are pure ASCII).
    /// </summary>
    private static string Escape(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("(", "\\(")
         .Replace(")", "\\)");

    /// <summary>
    /// Returns a PDF string token for a text value that may contain non-ASCII characters.
    /// <list type="bullet">
    ///   <item>If all characters are representable as printable ASCII the string is emitted
    ///         as a PDF literal  <c>(text)</c>  with backslash / paren escaping.</item>
    ///   <item>Otherwise the string is encoded as UTF-16 Big-Endian with a BOM (0xFEFF)
    ///         and returned as a PDF hex string  <c>&lt;FEFF…&gt;</c>, which all
    ///         conforming viewers must support (PDF 1.7 §7.9.2.2).</item>
    /// </list>
    /// </summary>
    private static string PdfTextString(string s)
    {
        // Fast path: pure ASCII literal
        bool needsUtf16 = false;
        foreach (char c in s)
        {
            if (c > '\u007E' || c < '\u0020')
            {
                needsUtf16 = true;
                break;
            }
        }

        if (!needsUtf16)
            return $"({Escape(s)})";

        // UTF-16BE with BOM
        byte[] bytes = System.Text.Encoding.BigEndianUnicode.GetBytes(s);
        var sb = new StringBuilder(4 + bytes.Length * 2);
        sb.Append("<FEFF"); // BOM
        foreach (byte b in bytes)
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append('>');
        return sb.ToString();
    }
}
