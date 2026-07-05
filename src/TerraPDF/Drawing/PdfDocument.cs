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
                string key = ImageContentKey(img.Data, img.Width, img.Height, img.IsJpeg, img.Components, img.Alpha);
                if (imageObjectByHash.TryGetValue(key, out int existingId))
                {
                    imgMap[alias] = existingId;
                    continue;
                }

                // RGBA transparency: emit the alpha channel as an 8-bit
                // DeviceGray soft-mask image referenced via /SMask.
                string smaskRef = string.Empty;
                if (img.Alpha is not null)
                {
                    int smaskId = nextId++;
                    byte[] alphaCompressed = Compress(img.Alpha);
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
                    byte[] compressed = Compress(img.Data);
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

        // Content streams (one per page) — always Flate-compressed.
        // When encrypted, compression happens first (the /Filter describes the
        // decoded stream; encryption is transparent to filters per PDF §7.6.1),
        // mirroring the PNG XObject path above.
        var contentIds = new List<int>();
        for (int pi = 0; pi < _pages.Count; pi++)
        {
            var page = _pages[pi];
            string ops = page.BuildContentStream();
            int cid    = nextId++;
            contentIds.Add(cid);

            byte[] compressed = Compress(enc.GetBytes(ops));
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

            var allAnnotIds = pageAnnotIds[i].Concat(pageInternalAnnotIds[i]).ToList();
            string annotStr = allAnnotIds.Count > 0
                ? "/Annots [" + string.Join(" ", allAnnotIds.Select(id => $"{id} 0 R")) + "] "
                : string.Empty;

            objects.Add((pid,
                $"<< /Type /Page /Parent {pagesId} 0 R " +
                $"/MediaBox [0 0 {Inv(p.Width)} {Inv(p.Height)}] " +
                $"/Contents {contentIds[i]} 0 R " +
                $"{annotStr}" +
                $"/Resources << /Font << {fontResources} >> {xObjectDict}>> >>"));
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

    /// <summary>
    /// Content-identity key for image deduplication: SHA-256 over the pixel/file
    /// bytes plus the parameters that affect the emitted XObject.
    /// </summary>
    private static string ImageContentKey(byte[] data, int width, int height,
        bool isJpeg, int components, byte[]? alpha)
    {
        using var sha = SHA256.Create();
        sha.TransformBlock(data, 0, data.Length, null, 0);
        if (alpha is not null)
            sha.TransformBlock(alpha, 0, alpha.Length, null, 0);
        byte[] meta =
        [
            (byte)(width  & 0xFF), (byte)((width  >> 8) & 0xFF), (byte)((width  >> 16) & 0xFF),
            (byte)(height & 0xFF), (byte)((height >> 8) & 0xFF), (byte)((height >> 16) & 0xFF),
            (byte)(isJpeg ? 1 : 0), (byte)components, (byte)(alpha is not null ? 1 : 0),
        ];
        sha.TransformFinalBlock(meta, 0, meta.Length);
        return Convert.ToHexString(sha.Hash!);
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
