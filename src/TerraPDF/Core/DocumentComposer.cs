using TerraPDF.Drawing;
using TerraPDF.Elements;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using System.Linq;
using System.Globalization;

namespace TerraPDF.Core;

/// <summary>
/// Collects <see cref="PageDescriptor"/> instances and renders them to a
/// <see cref="PdfDocument"/>.  Returned by <see cref="Document.Create(Action{IDocumentContainer})"/>.
/// </summary>
public sealed class DocumentComposer : IDocumentContainer
{
    private readonly List<PageDescriptor> _pages = [];

    private readonly List<BookmarkInfo> _bookmarks = new();
    private BookmarkInfo? _bookmarkRoot;  // First top-level bookmark (for tree building)

    // Document metadata (PDF Info dictionary)
    private string? _metadataTitle;
    private string? _metadataAuthor;
    private string? _metadataSubject;
    private string? _metadataKeywords;
    private string? _metadataCreator;

    /// <summary>
    /// Static recorder for heading elements during the first-pass render.
    /// </summary>
    internal static Action<HeadingElement, int, double>? CurrentHeadingRecorder { get; set; }

    // -- IDocumentContainer ----------------------------------------

    /// <inheritdoc/>
    public void Page(Action<PageDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var descriptor = new PageDescriptor();
        configure(descriptor);
        _pages.Add(descriptor);
    }

    // -- TableOfContents API ---------------------------------------

    /// <inheritdoc/>
    public void TableOfContents(Action<PageDescriptor>? configure = null)
    {
        if (_pages.Any(p => p.IsTableOfContents))
            throw new InvalidOperationException("Only one Table of Contents page is allowed.");
        var descriptor = new PageDescriptor();
        configure?.Invoke(descriptor);
        descriptor.IsTableOfContents = true;
        _pages.Add(descriptor);
    }

    // -- Bookmark API ----------------------------------------------

    /// <inheritdoc/>
    public void Bookmark(string title, int pageNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        var bm = new BookmarkInfo { Title = title, PageNumber = pageNumber };
        _bookmarks.Add(bm);
    }

    /// <inheritdoc/>
    public void Bookmark(string title, int pageNumber, double top)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        var bm = new BookmarkInfo { Title = title, PageNumber = pageNumber, Top = top };
        _bookmarks.Add(bm);
    }

    /// <inheritdoc/>
    public void Bookmark(string title, int pageNumber, string parentTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentTitle);

        var parent = _bookmarks.LastOrDefault(b => b.Title == parentTitle) ?? throw new InvalidOperationException($"Parent bookmark '{parentTitle}' not found.");
        var bm = new BookmarkInfo { Title = title, PageNumber = pageNumber, Parent = parent };
        parent.Children.Add(bm);
        _bookmarks.Add(bm);
    }

    /// <inheritdoc/>
    public void Bookmark(string title, int pageNumber, string parentTitle, double top)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentTitle);
        ArgumentOutOfRangeException.ThrowIfNegative(top);

        var parent = _bookmarks.LastOrDefault(b => b.Title == parentTitle) ?? throw new InvalidOperationException($"Parent bookmark '{parentTitle}' not found.");
        var bm = new BookmarkInfo { Title = title, PageNumber = pageNumber, Top = top, Parent = parent };
        parent.Children.Add(bm);
        _bookmarks.Add(bm);
    }

    // -- Metadata API ------------------------------------------------

    /// <inheritdoc/>
    public void MetadataTitle(string? title)
    {
        _metadataTitle = string.IsNullOrWhiteSpace(title) ? null : title;
    }

    /// <inheritdoc/>
    public void MetadataAuthor(string? author)
    {
        _metadataAuthor = string.IsNullOrWhiteSpace(author) ? null : author;
    }

    /// <inheritdoc/>
    public void MetadataSubject(string? subject)
    {
        _metadataSubject = string.IsNullOrWhiteSpace(subject) ? null : subject;
    }

    /// <inheritdoc/>
    public void MetadataKeywords(string? keywords)
    {
        _metadataKeywords = string.IsNullOrWhiteSpace(keywords) ? null : keywords;
    }

    /// <inheritdoc/>
    public void MetadataCreator(string? creator)
    {
        _metadataCreator = string.IsNullOrWhiteSpace(creator) ? null : creator;
    }

    /// <summary>
    /// Builds the hierarchical bookmark tree structure after all bookmarks
    /// have been added. Establishes sibling links among all children within each parent.
    /// </summary>
    private BookmarkInfo? BuildBookmarkTree()
    {
        if (_bookmarks.Count == 0)
            return null;

        // Link siblings for every parent (including top-level where Parent is null)
        foreach (var group in _bookmarks.GroupBy(b => b.Parent))
        {
            var siblings = group.ToList();
            for (int i = 0; i < siblings.Count; i++)
            {
                if (i > 0) siblings[i].Prev = siblings[i - 1];
                if (i < siblings.Count - 1) siblings[i].Next = siblings[i + 1];
            }
        }

        // Top-level (no parent) entries form the root's children
        _bookmarkRoot = _bookmarks.FirstOrDefault(b => b.Parent is null);
        return _bookmarkRoot;
    }

    // -- Output ----------------------------------------------------

    /// <summary>Saves the PDF to the given file path.</summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or whitespace.</exception>
    public void PublishPdf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteTo(fs);
    }

    /// <summary>Returns the PDF document as a byte array.</summary>
    public byte[] PublishPdf()
    {
        using var ms = new MemoryStream();
        WriteTo(ms);
        return ms.ToArray();
    }

    /// <summary>Writes the PDF to an existing stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
    public void PublishPdf(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        WriteTo(stream);
    }


    // Measurement helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Returns the number of PDF pages that <paramref name="d"/> will produce when rendered.
    /// Non-Column content always produces exactly one page.
    /// </summary>
    private static int CountPdfPages(PageDescriptor d)
    {
        double contentW   = d.PageWidth - d.MarginLeft - d.MarginRight;
        double firstPageH = AvailableContentHeight(d, contentW);

        // When the header is shown only on the first page, continuation pages
        // have the header height returned to them as extra content space.
        double contPageH = firstPageH;
        if (d.HeaderFirstPageOnly && d.HeaderSlot.Child is not null)
        {
            double hdrH = d.HeaderSlot.Measure(contentW, d.PageHeight, d.DefaultStyle).Height;
            contPageH = firstPageH + hdrH;
        }

        var (col, _, _, insetW, insetH) = FindColumnWithInsets(d.ContentSlot.Child);
        return col is null ? 1 : CountColumnPages(
            col, contentW + insetW,
            firstPageH + insetH, contPageH + insetH,
            d.DefaultStyle);
    }

    /// <summary>Simulates item-by-item rendering to count how many pages a Column needs.</summary>
    private static int CountColumnPages(
        Column col, double w,
        double firstPageH, double continuationPageH,
        TextStyle? style)
    {
        int    pages        = 1;
        double curY         = 0;
        double currentPageH = firstPageH;

        for (int i = 0; i < col.Items.Count; i++)
        {
            var item = col.Items[i];

            // Explicit page break: start a new page (unless we are already at the top).
            if (item.Child is PageBreak)
            {
                if (curY > 0) { pages++; curY = 0; currentPageH = continuationPageH; }
                continue;   // spacing is not added before/after a page break
            }

            var (table, _, _, tInsetW, tInsetH) = FindTableWithInsets(item.Child);

            if (table is not null && table.HeaderRowCount > 0)
            {
                double tableW     = w + tInsetW;
                double tablePageH = currentPageH + tInsetH;
                var colWidths     = table.GetColumnWidths(tableW);
                var rowHeights    = table.GetRowHeights(colWidths, style);
                int dataCount     = Math.Max(0, rowHeights.Length - table.HeaderRowCount);

                double tHdrH = Enumerable
                    .Range(0, Math.Min(table.HeaderRowCount, rowHeights.Length))
                    .Sum(r => rowHeights[r]);

                if (tHdrH > currentPageH - curY && curY > 0)
                {
                    pages++;
                    curY = 0;
                    currentPageH = continuationPageH;
                    tablePageH   = currentPageH + tInsetH;
                }

                double batchH       = tHdrH;
                bool   batchHasData = false;

                for (int dr = 0; dr < dataCount; dr++)
                {
                    int    ar = table.HeaderRowCount + dr;
                    double rh = ar < rowHeights.Length ? rowHeights[ar] : 0;

                    if (batchH + rh > tablePageH - curY && batchHasData)
                    {
                        pages++;
                        curY         = 0;
                        currentPageH = continuationPageH;
                        tablePageH   = currentPageH + tInsetH;
                        batchH       = tHdrH + rh;
                        batchHasData = true;
                    }
                    else
                    {
                        batchH      += rh;
                        batchHasData = true;
                    }
                }

                curY += batchH;
            }
            else
            {
                double itemH = item.Measure(w, currentPageH, style).Height;
                if (curY > 0 && curY + itemH > currentPageH)
                {
                    pages++;
                    curY = 0;
                    currentPageH = continuationPageH;
                }
                curY += itemH;
            }

            if (i < col.Items.Count - 1)
                curY += col.Spacing;
        }

        return pages;
    }

    /// <summary>
    /// Walks the decorator chain from <paramref name="element"/> down to the first
    /// <see cref="Column"/>, collecting the cumulative insets introduced by any
    /// <see cref="Padding"/> wrappers encountered along the way.
    /// Transparent wrappers (<see cref="Container"/>, <see cref="Background"/>,
    /// <see cref="Border"/>, <see cref="Alignment"/>) are traversed without adding insets.
    /// Returns <c>null</c> for the column if none is reachable.
    /// </summary>
    private static (Column? Column, double InsetX, double InsetY, double InsetW, double InsetH)
        FindColumnWithInsets(Element? element)
    {
        double iX = 0, iY = 0, iW = 0, iH = 0;

        while (element is not null)
        {
            switch (element)
            {
                case Column col:
                    return (col, iX, iY, iW, iH);
                case Container c:
                    element = c.Child;
                    break;
                case Padding p:
                    iX += p.Left;  iY += p.Top;
                    iW -= p.Left + p.Right;
                    iH -= p.Top  + p.Bottom;
                    element = p.Inner.Child;
                    break;
                case Background bg:
                    element = bg.Inner.Child;
                    break;
                case Border b:
                    element = b.Inner.Child;
                    break;
                case Alignment a:
                    element = a.Inner.Child;
                    break;
                default:
                    return (null, 0, 0, 0, 0);
            }
        }
        return (null, 0, 0, 0, 0);
    }

    /// <summary>
    /// Computes the usable content height on a page after reserving space for
    /// margins, header, and footer.
    /// </summary>
    private static double AvailableContentHeight(PageDescriptor d, double contentW)
    {
        double headerH = d.HeaderSlot.Child is not null
            ? d.HeaderSlot.Measure(contentW, d.PageHeight, d.DefaultStyle).Height : 0;
        double footerH = d.FooterSlot.Child is not null
            ? d.FooterSlot.Measure(contentW, d.PageHeight, d.DefaultStyle).Height : 0;

        return d.PageHeight - d.MarginTop - d.MarginBottom - headerH - footerH;
    }

    /// <summary>
    /// Walks the decorator chain from <paramref name="element"/> to find the first
    /// <see cref="Table"/>, collecting <see cref="Padding"/> insets along the way.
    /// </summary>
    private static (Table? Table, double InsetX, double InsetY, double InsetW, double InsetH)
        FindTableWithInsets(Element? element)
    {
        double iX = 0, iY = 0, iW = 0, iH = 0;

        while (element is not null)
        {
            switch (element)
            {
                case Table t:       return (t, iX, iY, iW, iH);
                case Container c:   element = c.Child; break;
                case Padding p:
                    iX += p.Left;  iY += p.Top;
                    iW -= p.Left + p.Right;
                    iH -= p.Top  + p.Bottom;
                    element = p.Inner.Child; break;
                case Background bg: element = bg.Inner.Child; break;
                case Border b:      element = b.Inner.Child; break;
                case Alignment a:   element = a.Inner.Child; break;
                default:            return (null, 0, 0, 0, 0);
            }
        }
        return (null, 0, 0, 0, 0);
    }

    // ---------------------------------------------------------------
    // Render pass
    // ---------------------------------------------------------------

    /// <summary>
    /// Renders one <see cref="PageDescriptor"/>, emitting as many PDF pages as required.
    /// </summary>
    private static void RenderDescriptor(
        PdfDocument doc, PageDescriptor descriptor,
        ref int pageNumber, int totalPages)
    {
        double contentX = descriptor.MarginLeft;
        double contentW = descriptor.PageWidth - descriptor.MarginLeft - descriptor.MarginRight;
        double contentH = AvailableContentHeight(descriptor, contentW);

        double headerH  = descriptor.HeaderSlot.Child is not null
            ? descriptor.HeaderSlot.Measure(contentW, descriptor.PageHeight, descriptor.DefaultStyle).Height : 0;
        double footerH  = descriptor.FooterSlot.Child is not null
            ? descriptor.FooterSlot.Measure(contentW, descriptor.PageHeight, descriptor.DefaultStyle).Height : 0;
        double contentY = descriptor.MarginTop + headerH;

        // When the header appears on the first page only, continuation pages start
        // their content at the top margin (no header offset) and gain the header
        // height back as usable content space.
        double contContentY = descriptor.HeaderFirstPageOnly
            ? descriptor.MarginTop
            : contentY;
        double contContentH = descriptor.HeaderFirstPageOnly
            ? contentH + headerH
            : contentH;

        var (col, insetX, insetY, insetW, insetH) = FindColumnWithInsets(descriptor.ContentSlot.Child);

        // ── Non-Column content: single page (original behaviour) ──────────────
        if (col is null)
        {
            pageNumber++;
            var ctx = StartPage(doc, descriptor, contentX, contentW,
                                headerH, footerH, pageNumber, totalPages, isFirstPage: true);
            if (descriptor.ContentSlot.Child is not null)
                descriptor.ContentSlot.Draw(ctx.At(contentX, contentY, contentW, contentH));
            return;
        }

        // Apply wrapper insets (e.g. PaddingVertical) to the item draw area.
        double itemsX      = contentX + insetX;
        double firstItemsY = contentY     + insetY;
        double contItemsY  = contContentY + insetY;
        double itemsW      = contentW + insetW;
        double firstItemsH = contentH     + insetH;
        double contItemsH  = contContentH + insetH;

        // Mutable per-page values — updated each time a new page is started.
        double currentItemsY = firstItemsY;
        double currentItemsH = firstItemsH;

        // ── Column content: split items across pages as needed ────────────────
        pageNumber++;
        var baseCtx = StartPage(doc, descriptor, contentX, contentW,
                                headerH, footerH, pageNumber, totalPages, isFirstPage: true);
        double curY = 0;

        for (int i = 0; i < col.Items.Count; i++)
        {
            var item = col.Items[i];

            // Explicit page break: start a new page (unless we are already at the top).
            if (item.Child is PageBreak)
            {
                if (curY > 0)
                {
                    pageNumber++;
                    baseCtx = StartPage(doc, descriptor, contentX, contentW,
                                        headerH, footerH, pageNumber, totalPages, isFirstPage: false);
                    curY          = 0;
                    currentItemsY = contItemsY;
                    currentItemsH = contItemsH;
                }
                continue;   // spacing is not added before/after a page break
            }

            var (table, tInsetX, tInsetY, tInsetW, tInsetH) = FindTableWithInsets(item.Child);

            if (table is not null && table.HeaderRowCount > 0)
            {
                RenderTableSplit(
                    table, tInsetX, tInsetY, tInsetW, tInsetH,
                    ref curY, ref pageNumber, ref baseCtx,
                    ref currentItemsY, ref currentItemsH,
                    doc, descriptor, contentX, contentW, headerH, footerH,
                    itemsX, itemsW, contItemsY, contItemsH, totalPages);
            }
            else
            {
                double itemH = item.Measure(itemsW, currentItemsH, descriptor.DefaultStyle).Height;

                if (curY > 0 && curY + itemH > currentItemsH)
                {
                    pageNumber++;
                    baseCtx = StartPage(doc, descriptor, contentX, contentW,
                                        headerH, footerH, pageNumber, totalPages, isFirstPage: false);
                    curY          = 0;
                    currentItemsY = contItemsY;
                    currentItemsH = contItemsH;
                }

                item.Draw(baseCtx.At(itemsX, currentItemsY + curY, itemsW, itemH));
                curY += itemH;
            }

            if (i < col.Items.Count - 1)
                curY += col.Spacing;
        }
    }

    /// <summary>
    /// Renders a <see cref="Table"/> that has header rows across as many PDF pages as needed.
    /// Header rows are repeated at the top of every continuation page.
    /// </summary>
    private static void RenderTableSplit(
        Table  table,
        double tInsetX, double tInsetY, double tInsetW, double tInsetH,
        ref double curY, ref int pageNumber, ref DrawingContext baseCtx,
        ref double currentItemsY, ref double currentItemsH,
        PdfDocument doc, PageDescriptor descriptor,
        double contentX, double contentW, double pageHeaderH, double footerH,
        double itemsX,   double itemsW,
        double contItemsY, double contItemsH,
        int totalPages)
    {
        double tableX = itemsX + tInsetX;
        double tableW = itemsW + tInsetW;

        var colWidths  = table.GetColumnWidths(tableW);
        var rowHeights = table.GetRowHeights(colWidths, descriptor.DefaultStyle);
        int totalRows  = rowHeights.Length;
        int dataCount  = Math.Max(0, totalRows - table.HeaderRowCount);

        var    headerIndices = Enumerable.Range(0, Math.Min(table.HeaderRowCount, totalRows)).ToList();
        double tHdrH         = headerIndices.Sum(r => rowHeights[r]);

        bool isFirstSlice = true;
        int  dr           = 0;

        do
        {
            double topOffset = isFirstSlice ? tInsetY : 0;
            double avail     = currentItemsH - curY - topOffset;

            if (tHdrH > avail && curY > 0)
            {
                pageNumber++;
                baseCtx = StartPage(doc, descriptor, contentX, contentW,
                                    pageHeaderH, footerH, pageNumber, totalPages, isFirstPage: false);
                curY           = 0;
                currentItemsY  = contItemsY;
                currentItemsH  = contItemsH;
                topOffset      = isFirstSlice ? tInsetY : 0;
                avail          = currentItemsH - curY - topOffset;
            }

            double batchH       = tHdrH;
            var    batchIndices = new List<int>(headerIndices);
            bool   batchHasData = false;

            while (dr < dataCount)
            {
                int    ar = table.HeaderRowCount + dr;
                double rh = ar < rowHeights.Length ? rowHeights[ar] : 0;

                if (batchH + rh > avail && batchHasData)
                    break;

                batchH += rh;
                batchIndices.Add(ar);
                batchHasData = true;
                dr++;
            }

            if (!batchHasData && dr < dataCount)
            {
                int    ar = table.HeaderRowCount + dr;
                double rh = ar < rowHeights.Length ? rowHeights[ar] : 0;
                batchH += rh;
                batchIndices.Add(ar);
                dr++;
            }

            double drawY = currentItemsY + curY + topOffset;
            table.DrawRows(baseCtx.At(tableX, drawY, tableW, batchH),
                           colWidths, rowHeights, batchIndices);
            curY += batchH + topOffset;

            isFirstSlice = false;

            if (dr < dataCount)
            {
                pageNumber++;
                baseCtx = StartPage(doc, descriptor, contentX, contentW,
                                    pageHeaderH, footerH, pageNumber, totalPages, isFirstPage: false);
                curY          = 0;
                currentItemsY = contItemsY;
                currentItemsH = contItemsH;
            }

        } while (dr < dataCount);
    }

    /// <summary>
    /// Creates a new PDF page, draws the page background, header, and footer,
    /// and returns the base <see cref="DrawingContext"/> for content rendering.
    /// </summary>
    private static DrawingContext StartPage(
        PdfDocument doc, PageDescriptor d,
        double contentX, double contentW,
        double headerH, double footerH,
        int pageNumber, int totalPages,
        bool isFirstPage)
    {
        var pdfPage = doc.AddPage(d.PageWidth, d.PageHeight);

        if (d.BackgroundColor.HasValue)
            pdfPage.AddFilledRect(0, 0, d.PageWidth, d.PageHeight, d.BackgroundColor.Value);

        var ctx = new DrawingContext
        {
            Page             = pdfPage,
            DefaultTextStyle = d.DefaultStyle,
            PageNumber       = pageNumber,
            TotalPages       = totalPages,
            X                = 0,
            Y                = 0,
            Width            = d.PageWidth,
            Height           = d.PageHeight,
            HeadingRecorder  = CurrentHeadingRecorder,
        };

        // Draw header only on the first page when HeaderFirstPageOnly is set.
        if (d.HeaderSlot.Child is not null && (!d.HeaderFirstPageOnly || isFirstPage))
            d.HeaderSlot.Draw(ctx.At(contentX, d.MarginTop, contentW, headerH));

        if (d.FooterSlot.Child is not null)
        {
            double footerY = d.PageHeight - d.MarginBottom - footerH;
            d.FooterSlot.Draw(ctx.At(contentX, footerY, contentW, footerH));
        }

        return ctx;
    }

    // ==============================================================
    // Table of Contents support
    // ==============================================================

    private record HeadingInfo(int Level, string Title);

    private sealed class TocEntry
    {
        public int Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public double Top { get; set; }
    }

    private static List<string> ComputeDisplayTitles(List<HeadingInfo> headings)
    {
        int[] counters = new int[6];
        var display = new List<string>();
        foreach (var h in headings)
        {
            counters[h.Level - 1]++;           // increment this level
            for (int i = h.Level; i < 6; i++) // reset deeper levels
                counters[i] = 0;

            var parts = new List<string>();
            for (int i = 0; i < h.Level; i++)
                if (counters[i] > 0)
                    parts.Add(counters[i].ToString(CultureInfo.InvariantCulture));

            string number = string.Join(".", parts);
            string title = $"{number} {h.Title}";
            display.Add(title);
        }
        return display;
    }

    private static void ScanElement(Element? element, List<HeadingInfo> list)
    {
        if (element is HeadingElement h)
        {
            list.Add(new HeadingInfo(h.Level, h.Title));
            return;
        }

        switch (element)
        {
            case Container c:
                ScanElement(c.Child, list);
                break;
            case Column col:
                foreach (var item in col.Items)
                    ScanElement(item.Child, list);
                break;
            case Row row:
                foreach (var item in row.Items)
                    ScanElement(item.Slot.Child, list);
                break;
            case Table table:
                foreach (var cell in table.Cells)
                    ScanElement(cell.Slot.Child, list);
                break;
        }
    }

    private List<HeadingInfo> ScanAllHeadings()
    {
        var list = new List<HeadingInfo>();
        foreach (var page in _pages)
        {
            ScanElement(page.ContentSlot.Child, list);
        }
        return list;
    }

    private static Column BuildPlaceholderTocColumn(List<HeadingInfo> headings, List<string> displayTitles)
    {
        var col = new Column();
        col.Spacing = 4;
        for (int i = 0; i < headings.Count; i++)
        {
            var h = headings[i];
            string display = displayTitles[i];
            var row = new Row();
            var rd = new RowDescriptor(row);
            var titleSlot = rd.AutoItem();
            titleSlot.MarginLeft(20 * (h.Level - 1)).Text(display);
            rd.ConstantItem(60).AlignRight().Text("0"); // placeholder page number
            col.AddItem().Child = row;
        }
        return col;
    }

    private static Column BuildRealTocColumn(List<TocEntry> entries, List<string> displayTitles, int tocPageOffset)
    {
        var col = new Column();
        col.Spacing = 4;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            string display = displayTitles[i];
            var row = new Row();
            var rd = new RowDescriptor(row);
            var titleSlot = rd.AutoItem();
            titleSlot.MarginLeft(20 * (entry.Level - 1)).Text(display);
            int displayedPage = entry.PageNumber - tocPageOffset;
            rd.ConstantItem(60).AlignRight().Text(displayedPage.ToString(CultureInfo.InvariantCulture));

            var link = new InternalLinkElement
            {
                TargetPage = entry.PageNumber,
                TargetTop = entry.Top
            };
            link.Inner.Child = row;
            col.AddItem().Child = link;
        }
        return col;
    }

    // ==============================================================
    // Output
    // ==============================================================

    private void WriteTo(Stream output)
    {
        // 1. Locate any TOC page(s)
        var tocPageIndices = new List<int>();
        for (int i = 0; i < _pages.Count; i++)
        {
            if (_pages[i].IsTableOfContents)
                tocPageIndices.Add(i);
        }

        if (tocPageIndices.Count > 1)
            throw new InvalidOperationException("Only one Table of Contents page is allowed.");

        // 2. Scan all headings from content slots
        var allHeadings = ScanAllHeadings();

        // Compute hierarchical numbering for TOC display titles (e.g., "1.2.3 Title")
        var displayTitles = ComputeDisplayTitles(allHeadings);

        // 3. If TOC exists, create placeholder column and measure TOC page count
        int tocPageCount = 0;
        if (tocPageIndices.Count > 0)
        {
            var placeholder = BuildPlaceholderTocColumn(allHeadings, displayTitles);
            _pages[tocPageIndices[0]].ContentSlot.Child = placeholder;

            // Measure how many pages the TOC (with placeholder) will occupy.
            // This count is used to offset displayed page numbers.
            tocPageCount = CountPdfPages(_pages[tocPageIndices[0]]);
        }

        // 4. Compute total pages (with placeholder TOC)
        int totalPages = _pages.Sum(CountPdfPages);

        // 5. If TOC exists, perform dummy render to collect heading positions
        List<TocEntry> collectedEntries = new();
        if (tocPageIndices.Count > 0)
        {
            var dummyDoc = new PdfDocument();
            DocumentComposer.CurrentHeadingRecorder = (h, pg, y) =>
            {
                collectedEntries.Add(new TocEntry
                {
                    Level = h.Level,
                    Title = h.Title,
                    PageNumber = pg,
                    Top = y
                });
            };
            int dummyPageNum = 0;
            foreach (var desc in _pages)
            {
                RenderDescriptor(dummyDoc, desc, ref dummyPageNum, totalPages);
            }
            DocumentComposer.CurrentHeadingRecorder = null;

            // Replace placeholder with real TOC, applying the page-number offset
            var realToc = BuildRealTocColumn(collectedEntries, displayTitles, tocPageCount);
            _pages[tocPageIndices[0]].ContentSlot.Child = realToc;

            // Recompute total pages after real TOC
            totalPages = _pages.Sum(CountPdfPages);
        }

        // 6. Create real document
        var doc = new PdfDocument();
        doc.SetMetadata(_metadataTitle, _metadataAuthor, _metadataSubject, _metadataKeywords, _metadataCreator);

        BookmarkInfo? bookmarkRoot = BuildBookmarkTree();
        doc.SetBookmarks(_bookmarks, totalPages);

        int pageNumber = 0;
        foreach (var descriptor in _pages)
        {
            RenderDescriptor(doc, descriptor, ref pageNumber, totalPages);
        }

        doc.Save(output);
    }
}
