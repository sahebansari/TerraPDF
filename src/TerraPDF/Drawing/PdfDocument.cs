using System;
using System.Globalization;
using System.IO.Compression;
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

        // We buffer everything into a MemoryStream so that we can record
        // exact byte offsets for the cross-reference (xref) table.
        using var ms = new MemoryStream();
        void WriteStr(string s) => ms.Write(enc.GetBytes(s));

        // -- Object allocation -------------------------------------
        // Text objects: (id, body string).
        // Binary stream objects: (id, dict string, raw stream bytes) - used for image XObjects.
        var objects       = new List<(int id, string body)>();
        var binaryObjects = new List<(int id, string dict, byte[] stream)>();
        int nextId = 1;

        // Standard Type1 fonts - no embedding required.
        // F1-F6 match the StandardFont enum aliases used in PdfPage.AddText.
        int f1Id = nextId++; // F1 Helvetica             (normal)
        int f2Id = nextId++; // F2 Times-Bold            (bold)
        int f3Id = nextId++; // F3 Courier               (monospace)
        int f4Id = nextId++; // F4 Helvetica-Oblique     (italic)
        int f5Id = nextId++; // F5 Times-BoldItalic      (bold + italic)
        int f6Id = nextId++; // F6 Times-Italic          (italic, non-bold)
        objects.Add((f1Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add((f2Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold /Encoding /WinAnsiEncoding >>"));
        objects.Add((f3Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"));
        objects.Add((f4Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>"));
        objects.Add((f5Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Times-BoldItalic /Encoding /WinAnsiEncoding >>"));
        objects.Add((f6Id, "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Italic /Encoding /WinAnsiEncoding >>"));

        // Image XObjects - one PDF object per unique alias per page.
        // We build per-page maps of alias -> object id for use in /Resources.
        var pageImageMaps = new List<Dictionary<string, int>>();
        foreach (var page in _pages)
        {
            var imgMap = new Dictionary<string, int>();
            foreach (var (alias, img) in page.ImageObjects)
            {
                int imgId = nextId++;
                imgMap[alias] = imgId;

                if (img.IsJpeg)
                {
                    // JPEG: embed raw file bytes verbatim - PDF's DCTDecode filter handles decompression.
                    // ColorSpace depends on component count: 1=grayscale, 3=RGB/YCbCr, 4=CMYK.
                    string cs = img.Components switch
                    {
                        1 => "/DeviceGray",
                        4 => "/DeviceCMYK",
                        _ => "/DeviceRGB",
                    };
                    string dict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace {cs} /BitsPerComponent 8 " +
                        $"/Filter /DCTDecode /Length {img.Data.Length} >>";
                    binaryObjects.Add((imgId, dict, img.Data));
                }
                else
                {
                    // PNG: compress decoded RGB pixels with zlib (FlateDecode)
                    byte[] compressed = Compress(img.Data);
                    string dict =
                        $"<< /Type /XObject /Subtype /Image " +
                        $"/Width {img.Width} /Height {img.Height} " +
                        $"/ColorSpace /DeviceRGB /BitsPerComponent 8 " +
                        $"/Filter /FlateDecode /Length {compressed.Length} >>";
                    binaryObjects.Add((imgId, dict, compressed));
                }
            }
            pageImageMaps.Add(imgMap);
        }

        // Content streams (one per page)
        var contentIds = new List<int>();
        foreach (var page in _pages)
        {
            string ops = page.BuildContentStream();
            int len    = enc.GetByteCount(ops);
            int cid    = nextId++;
            contentIds.Add(cid);
            objects.Add((cid, $"<< /Length {len} >>\nstream\n{ops}\nendstream"));
        }

        // Link annotation objects (one PDF object per annotation, grouped per page).
        // Annotations reference the page they belong to via the page dict /Annots array.
        var pageAnnotIds = new List<List<int>>();
        foreach (var page in _pages)
        {
            var annotIds = new List<int>();
            foreach (var annot in page.LinkAnnotations)
            {
                int annotId = nextId++;
                // Convert from top-left origin to PDF bottom-left origin
                double pdfX1 = annot.X;
                double pdfY1 = page.Height - annot.Y - annot.Height;
                double pdfX2 = annot.X + annot.Width;
                double pdfY2 = page.Height - annot.Y;
                // Escape parentheses and backslashes inside the URI PDF string literal
                string uri = annot.Url.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                objects.Add((annotId,
                    $"<< /Type /Annot /Subtype /Link " +
                    $"/Rect [{Inv(pdfX1)} {Inv(pdfY1)} {Inv(pdfX2)} {Inv(pdfY2)}] " +
                    $"/Border [0 0 0] " +
                    $"/A << /Type /Action /S /URI /URI ({uri}) >> >>"));
                annotIds.Add(annotId);
            }
            pageAnnotIds.Add(annotIds);
        }

        // Allocate page object IDs first (needed for internal link destinations)
        int pagesId = nextId++;
        var pageIds = new List<int>();
        for (int i = 0; i < _pages.Count; i++)
        {
            pageIds.Add(nextId++);
        }

        // Create Pages dictionary object (references page IDs)
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
            var p = _pages[i];
            int pid = pageIds[i];

            // Build the /XObject sub-dictionary if this page has any images
            string xObjectDict = pageImageMaps[i].Count > 0
                ? "/XObject << " +
                  string.Join(" ", pageImageMaps[i].Select(kv => $"/{kv.Key} {kv.Value} 0 R")) +
                  " >> "
                : string.Empty;

            // Combine external and internal link annotations
            var allAnnotIds = pageAnnotIds[i].Concat(pageInternalAnnotIds[i]).ToList();
            string annotStr = allAnnotIds.Count > 0
                ? "/Annots [" + string.Join(" ", allAnnotIds.Select(id => $"{id} 0 R")) + "] "
                : string.Empty;

            objects.Add((pid,
                $"<< /Type /Page /Parent {pagesId} 0 R " +
                $"/MediaBox [0 0 {Inv(p.Width)} {Inv(p.Height)}] " +
                $"/Contents {contentIds[i]} 0 R " +
                $"{annotStr}" +
                $"/Resources << /Font << " +
                    $"/F1 {f1Id} 0 R " +
                    $"/F2 {f2Id} 0 R " +
                    $"/F3 {f3Id} 0 R " +
                    $"/F4 {f4Id} 0 R " +
                    $"/F5 {f5Id} 0 R " +
                    $"/F6 {f6Id} 0 R " +
                $">> {xObjectDict}>> >>"));
        }

        // Outlines (bookmarks) - if any were defined
        int outlinesId = WriteBookmarks(ref nextId, objects, pageIds);

        // Info dictionary - if any metadata was provided
        int infoId = WriteInfoDictionary(ref nextId, objects);

        // Catalog - must be the last object so its id is known
        int catalogId = nextId++;
        var catalogParts = new List<string>
        {
            $"/Type /Catalog",
            $"/Pages {pagesId} 0 R"
        };
        if (outlinesId != 0)
            catalogParts.Add($"/Outlines {outlinesId} 0 R");
        if (infoId != 0)
            catalogParts.Add($"/Info {infoId} 0 R");

        string catalogDict = $"<< {string.Join(" ", catalogParts)} >>";
        objects.Add((catalogId, catalogDict));

        // Merge text and binary objects into a single id-ordered list for xref
        // Binary objects carry their raw stream bytes separately.
        objects.Sort((a, b) => a.id.CompareTo(b.id));
        binaryObjects.Sort((a, b) => a.id.CompareTo(b.id));

        // -- Write header ------------------------------------------
        WriteStr("%PDF-1.7\n");
        // Comment with high-byte characters signals that the file is binary
        ms.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        // -- Write body objects, recording byte offsets -------------
        // We interleave text and binary objects in ascending id order.
        var offsets   = new Dictionary<int, long>();
        int txtIndex  = 0;
        int binIndex  = 0;

        while (txtIndex < objects.Count || binIndex < binaryObjects.Count)
        {
            bool writeBinary =
                binIndex < binaryObjects.Count &&
                (txtIndex >= objects.Count ||
                 binaryObjects[binIndex].id < objects[txtIndex].id);

            if (writeBinary)
            {
                var (id, dict, stream) = binaryObjects[binIndex++];
                offsets[id] = ms.Position;
                WriteStr($"{id} 0 obj\n{dict}\nstream\n");
                ms.Write(stream);                      // raw compressed bytes
                WriteStr("\nendstream\nendobj\n");
            }
            else
            {
                var (id, body) = objects[txtIndex++];
                offsets[id] = ms.Position;
                WriteStr($"{id} 0 obj\n{body}\nendobj\n");
            }
        }

        // Total object count for xref = text objects + binary objects + free entry (obj 0)
        int totalCount = objects.Count + binaryObjects.Count + 1;

        // -- Cross-reference table ----------------------------------
        // Each entry is exactly 20 bytes:
        //   nnnnnnnnnn(10) SP(1) ggggg(5) SP(1) [n|f](1) SP(1) LF(1) = 20
        long xrefOffset = ms.Position;

        WriteStr("xref\n");
        WriteStr($"0 {totalCount}\n");
        WriteStr("0000000000 65535 f \n"); // free-list head (object 0)

        for (int i = 1; i < totalCount; i++)
            WriteStr($"{offsets[i]:D10} 00000 n \n");

        // -- Trailer -----------------------------------------------
        WriteStr("trailer\n");
        WriteStr($"<< /Size {totalCount} /Root {catalogId} 0 R >>\n");
        WriteStr("startxref\n");
        WriteStr($"{xrefOffset}\n");
        WriteStr("%%EOF\n");

        // -- Flush to caller's stream ------------------------------
        ms.Position = 0;
        ms.CopyTo(output);
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

        // First pass: assign sequential IDs to every bookmark item.
        foreach (var bm in _allBookmarks)
        {
            // (IDs assigned in WriteBookmarks loop)
        }

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
            string body = BuildBookmarkItemBody(node, nodeToId, pageIds, outlinesId);
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
            parts.Add($"/Title ({Escape(_infoTitle)})");
        if (!string.IsNullOrEmpty(_infoAuthor))
            parts.Add($"/Author ({Escape(_infoAuthor)})");
        if (!string.IsNullOrEmpty(_infoSubject))
            parts.Add($"/Subject ({Escape(_infoSubject)})");
        if (!string.IsNullOrEmpty(_infoKeywords))
            parts.Add($"/Keywords ({Escape(_infoKeywords)})");
        if (!string.IsNullOrEmpty(_infoCreator))
            parts.Add($"/Creator ({Escape(_infoCreator)})");

        string infoBody = "<< " + string.Join(" ", parts) + " >>";
        objects.Add((infoId, infoBody));
        return infoId;
    }

    /// <summary>
    /// Builds the PDF dictionary string for a single bookmark item.
    /// </summary>
    private static string BuildBookmarkItemBody(
        BookmarkInfo node,
        Dictionary<BookmarkInfo, int> idMap,
        List<int> pageIds,
        int outlinesRootId)
    {
        var parts = new List<string>
        {
            "/Type /Outlines",
            $"/Title ({Escape(node.Title)})"
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
            parts.Add($"/Count {node.Children.Count}");
        }

        // Destination
        int pageIdx = node.PageNumber - 1;
        if (pageIdx < 0 || pageIdx >= pageIds.Count)
            throw new InvalidOperationException($"Bookmark '{node.Title}' targets page {node.PageNumber} but document has only {pageIds.Count} pages.");

        int pageObjId = pageIds[pageIdx];

        if (node.Top.HasValue)
        {
            // /FitH: fit width, set top edge at specified Y from top of page
            double top = node.Top.Value;
            string topStr = top.ToString("F2", CultureInfo.InvariantCulture);
            parts.Add($"/Dest [{pageObjId} 0 R /FitH {topStr}]");
        }
        else
        {
            parts.Add($"/Dest [{pageObjId} 0 R /Fit]");
        }

        return "<< " + string.Join(" ", parts) + " >>";
    }

    /// <summary>
    /// Escapes special characters in a PDF string literal (backslash, parentheses).
    /// </summary>
    private static string Escape(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("(", "\\(")
         .Replace(")", "\\)");
}
