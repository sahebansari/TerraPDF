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

    // Document metadata (PDF Info dictionary)
    private string? _metadataTitle;
    private string? _metadataAuthor;
    private string? _metadataSubject;
    private string? _metadataKeywords;
    private string? _metadataCreator;

    // Encryption
    private EncryptionOptions? _encryptionOptions;

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

    /// <inheritdoc/>
    public void Encrypt(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _encryptionOptions = options;
    }

    /// <summary>
    /// Builds the hierarchical bookmark tree structure after all bookmarks
    /// have been added. Establishes sibling links among all children within each parent.
    /// </summary>
    private void BuildBookmarkTree()
    {
        if (_bookmarks.Count == 0)
            return;

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


    // ---------------------------------------------------------------
    // Layout pass — the single source of pagination decisions.
    // Produces per-page fragments; page counting is fragments.Count and
    // rendering draws the placed items, so the two can never disagree.
    // ---------------------------------------------------------------

    /// <summary>
    /// A visual decorator found on the passthrough chain above the paginating
    /// element.  <c>Left/Top/Right/Bottom</c> are the insets accumulated between
    /// the decorator and the found element — how far the decorator's box extends
    /// beyond the found element's area on each side.
    /// </summary>
    private readonly record struct ChromeDecorator(
        Element Element, double Left, double Top, double Right, double Bottom);

    /// <summary>
    /// Walks the decorator chain from <paramref name="element"/> down to the first
    /// element of type <typeparamref name="T"/>, collecting the cumulative insets
    /// introduced by <see cref="Padding"/>/<see cref="Margin"/> wrappers and the
    /// visual decorators (<see cref="Element.HasDecoration"/>) encountered on the
    /// way.  Traversal is driven by <see cref="Element.PassthroughChild"/>, so
    /// every layout-transparent decorator participates automatically.
    /// Inset conventions: <c>InsetX/InsetY</c> are left/top offsets;
    /// <c>InsetW/InsetH</c> are (negative) width/height deltas.
    /// Returns <c>null</c> for the element if none is reachable.
    /// </summary>
    private static (T? Found, double InsetX, double InsetY, double InsetW, double InsetH, List<ChromeDecorator> Chrome)
        FindWithChrome<T>(Element? element) where T : Element
    {
        double iL = 0, iT = 0, iR = 0, iB = 0;
        var visuals = new List<(Element El, double L, double T, double R, double B)>();

        while (element is not null)
        {
            if (element is T found)
            {
                // Convert each visual's "insets so far" into "insets between it and T".
                var chrome = visuals
                    .Select(v => new ChromeDecorator(v.El, iL - v.L, iT - v.T, iR - v.R, iB - v.B))
                    .ToList();
                return (found, iL, iT, -(iL + iR), -(iT + iB), chrome);
            }

            if (element.HasDecoration)
                visuals.Add((element, iL, iT, iR, iB));

            var (l, t, r, b) = element.PassthroughInsets;
            iL += l;  iT += t;  iR += r;  iB += b;
            element = element.PassthroughChild;
        }
        return (null, 0, 0, 0, 0, []);
    }

    /// <summary>
    /// Computes the usable content height on a page after reserving space for
    /// margins, header, and footer.
    /// </summary>
    private static double AvailableContentHeight(PageDescriptor d, double contentW, int totalPagesHint)
    {
        double headerH = d.HeaderSlot.Child is not null
            ? d.HeaderSlot.Measure(contentW, d.PageHeight, d.DefaultStyle, totalPagesHint).Height : 0;
        double footerH = d.FooterSlot.Child is not null
            ? d.FooterSlot.Measure(contentW, d.PageHeight, d.DefaultStyle, totalPagesHint).Height : 0;

        return d.PageHeight - d.MarginTop - d.MarginBottom - headerH - footerH;
    }


    /// <summary>
    /// Lays out one <see cref="PageDescriptor"/> into page fragments, splitting a
    /// top-level Column between items and header-row tables between rows exactly
    /// as the renderer will draw them.
    /// </summary>
    private static List<PageFragment> LayoutDescriptor(PageDescriptor d, int totalPagesHint)
    {
        double contentX = d.MarginLeft;
        double contentW = d.PageWidth - d.MarginLeft - d.MarginRight;
        double contentH = AvailableContentHeight(d, contentW, totalPagesHint);

        double headerH  = d.HeaderSlot.Child is not null
            ? d.HeaderSlot.Measure(contentW, d.PageHeight, d.DefaultStyle, totalPagesHint).Height : 0;
        double contentY = d.MarginTop + headerH;

        // When the header appears on the first page only, continuation pages start
        // their content at the top margin (no header offset) and gain the header
        // height back as usable content space.
        double contContentY = d.HeaderFirstPageOnly ? d.MarginTop : contentY;
        double contContentH = d.HeaderFirstPageOnly ? contentH + headerH : contentH;

        var (col, insetX, insetY, insetW, insetH, chrome) = FindWithChrome<Column>(d.ContentSlot.Child);
        var fragments = new List<PageFragment>();

        // ── Non-Column content: single page (decorators draw via the slot) ────
        if (col is null)
        {
            var single = new PageFragment { IsFirstOfDescriptor = true };
            if (d.ContentSlot.Child is not null)
                single.Items.Add(new PlacedItem(d.ContentSlot, contentX, contentY, contentW, contentH));
            fragments.Add(single);
            return fragments;
        }

        // Apply wrapper insets (e.g. PaddingVertical) to the item area.
        double itemsX      = contentX + insetX;
        double firstItemsY = contentY     + insetY;
        double contItemsY  = contContentY + insetY;
        double itemsW      = contentW + insetW;
        double firstItemsH = contentH     + insetH;
        double contItemsH  = contContentH + insetH;

        // Mutable per-page values — updated each time a new page is started.
        double currentItemsY = firstItemsY;
        double currentItemsH = firstItemsH;
        double curY = 0;

        // Chrome covers the page's full item area (matching the non-split path,
        // where decorators receive the whole content box), expanded by the insets
        // sitting between each decorator and the column.
        PageFragment NewFragment(bool first)
        {
            var f = new PageFragment { IsFirstOfDescriptor = first };
            foreach (var c in chrome)
                f.Items.Add(new PlacedItem(c.Element,
                    itemsX        - c.Left,
                    currentItemsY - c.Top,
                    itemsW        + c.Left + c.Right,
                    currentItemsH + c.Top  + c.Bottom,
                    DecorationOnly: true));
            fragments.Add(f);
            return f;
        }

        var current = NewFragment(first: true);

        void StartNewPage()
        {
            curY          = 0;
            currentItemsY = contItemsY;
            currentItemsH = contItemsH;
            current       = NewFragment(first: false);
        }

        // ── Column content: split items across pages as needed ────────────────
        for (int i = 0; i < col.Items.Count; i++)
        {
            var item = col.Items[i];

            // Explicit page break: start a new page (unless we are already at the top).
            if (item.Child is PageBreak)
            {
                if (curY > 0)
                    StartNewPage();
                continue;   // spacing is not added before/after a page break
            }

            // Item-level decorator chrome around a *split* table is not repeated
            // per page (only column-level chrome is); non-split items draw their
            // decorators normally via item.Draw.
            var (table, tInsetX, tInsetY, tInsetW, tInsetH, _) = FindWithChrome<Table>(item.Child);

            if (table is not null && table.HeaderRowCount > 0)
            {
                // Split the table between rows; header rows repeat on every slice.
                double tableX = itemsX + tInsetX;
                double tableW = itemsW + tInsetW;

                var colWidths  = table.GetColumnWidths(tableW);
                var rowHeights = table.GetRowHeights(colWidths, d.DefaultStyle, totalPagesHint);
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
                        StartNewPage();
                        topOffset = isFirstSlice ? tInsetY : 0;
                        avail     = currentItemsH - curY - topOffset;
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

                    // A single row taller than the page: force it out anyway so
                    // layout always makes progress.
                    if (!batchHasData && dr < dataCount)
                    {
                        int    ar = table.HeaderRowCount + dr;
                        double rh = ar < rowHeights.Length ? rowHeights[ar] : 0;
                        batchH += rh;
                        batchIndices.Add(ar);
                        dr++;
                    }

                    current.Items.Add(new PlacedItem(
                        new TableSlice(table, colWidths, rowHeights, batchIndices),
                        tableX, currentItemsY + curY + topOffset, tableW, batchH));
                    curY += batchH + topOffset;

                    isFirstSlice = false;

                    if (dr < dataCount)
                        StartNewPage();

                } while (dr < dataCount);
            }
            else
            {
                double itemH = item.Measure(itemsW, currentItemsH, d.DefaultStyle, totalPagesHint).Height;

                if (curY > 0 && curY + itemH > currentItemsH)
                    StartNewPage();

                bool placed = false;

                // Item taller than a whole page: split TextBlocks between their
                // wrapped lines (mirrors the table row-splitting above).  Items
                // whose chain doesn't reach a TextBlock (Rows, images, links,
                // headings) keep the legacy overflow behaviour.
                if (itemH > currentItemsH - curY)
                {
                    var (textBlock, bInsetX, bInsetY, bInsetW, bInsetH, _) =
                        FindWithChrome<TextBlock>(item.Child);

                    if (textBlock is not null)
                    {
                        double textX = itemsX + bInsetX;
                        double textW = itemsW + bInsetW;
                        var (lines, resolved, lineH) =
                            textBlock.LayoutLines(textW, d.DefaultStyle, totalPagesHint);

                        int  li         = 0;
                        bool firstSlice = true;

                        while (li < lines.Count)
                        {
                            double topOffset = firstSlice ? bInsetY : 0;
                            double avail     = currentItemsH - curY - topOffset;
                            int    fit       = (int)Math.Floor(avail / lineH);

                            if (fit < 1)
                            {
                                if (curY > 0) { StartNewPage(); continue; }
                                fit = 1;   // page shorter than one line: force progress
                            }
                            fit = Math.Min(fit, lines.Count - li);

                            var slice = new TextBlockSlice(lines.GetRange(li, fit), resolved, lineH);
                            current.Items.Add(new PlacedItem(
                                slice, textX, currentItemsY + curY + topOffset, textW, fit * lineH));

                            curY += fit * lineH + topOffset;
                            li   += fit;
                            firstSlice = false;

                            if (li < lines.Count)
                                StartNewPage();
                        }

                        // Bottom inset of the wrapper (e.g. PaddingBottom) lands
                        // after the last slice.  PassthroughInsets convention:
                        // bInsetH = -(top + bottom).
                        curY  += -bInsetH - bInsetY;
                        placed = true;
                    }
                }

                if (!placed)
                {
                    current.Items.Add(new PlacedItem(item, itemsX, currentItemsY + curY, itemsW, itemH));
                    curY += itemH;
                }
            }

            if (i < col.Items.Count - 1)
                curY += col.Spacing;
        }

        return fragments;
    }

    // ---------------------------------------------------------------
    // Render pass — draws previously laid-out fragments
    // ---------------------------------------------------------------

    /// <summary>
    /// Renders one descriptor's laid-out fragments, emitting one PDF page per
    /// fragment with the descriptor's page background, header, and footer.
    /// </summary>
    private static void RenderFragments(
        PdfDocument doc, PageDescriptor d, List<PageFragment> fragments,
        ref int pageNumber, int totalPages,
        Action<HeadingElement, int, double>? headingRecorder,
        Action<string, string?, int, double>? bookmarkRecorder = null)
    {
        double contentX = d.MarginLeft;
        double contentW = d.PageWidth - d.MarginLeft - d.MarginRight;

        double headerH = d.HeaderSlot.Child is not null
            ? d.HeaderSlot.Measure(contentW, d.PageHeight, d.DefaultStyle, totalPages).Height : 0;
        double footerH = d.FooterSlot.Child is not null
            ? d.FooterSlot.Measure(contentW, d.PageHeight, d.DefaultStyle, totalPages).Height : 0;

        foreach (var fragment in fragments)
        {
            pageNumber++;
            var ctx = StartPage(doc, d, contentX, contentW,
                                headerH, footerH, pageNumber, totalPages,
                                fragment.IsFirstOfDescriptor, headingRecorder, bookmarkRecorder);

            foreach (var placed in fragment.Items)
            {
                var itemCtx = ctx.At(placed.X, placed.Y, placed.W, placed.H);
                if (placed.DecorationOnly)
                    placed.Element.DrawDecoration(itemCtx);
                else
                    placed.Element.Draw(itemCtx);
            }
        }
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
        bool isFirstPage,
        Action<HeadingElement, int, double>? headingRecorder,
        Action<string, string?, int, double>? bookmarkRecorder = null)
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
            HeadingRecorder  = headingRecorder,
            BookmarkRecorder = bookmarkRecorder,
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
            // Opaque wrappers that still contain scannable content.  These must
            // be traversed here or a wrapped heading is invisible to the static
            // scan while the render-time recorder still fires — desynchronising
            // the TOC entry list from its display titles.
            case Link link:
                ScanElement(link.Inner, list);
                break;
            case InternalLinkElement il:
                ScanElement(il.Inner, list);
                break;
            case BookmarkAnchorElement anchor:
                ScanElement(anchor.Inner, list);
                break;
            default:
                // Layout-transparent decorators (Container, Padding, Margin,
                // Background, borders, Alignment) — same chain the paginator walks.
                if (element?.PassthroughChild is { } passthrough)
                    ScanElement(passthrough, list);
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

    private static int Digits(int n) => n <= 0 ? 1 : (int)Math.Floor(Math.Log10(n)) + 1;

    /// <summary>
    /// Lays out every descriptor into fragments, re-measuring with the real page
    /// count as the page-number placeholder until its digit count stabilises.
    /// A wider placeholder (e.g. "120" vs "99") can change footer wrapping and
    /// thus the available content height, so counting and measuring must agree.
    /// Converges in one extra pass for realistic documents; bounded at 3.
    /// The returned fragments are exactly what will be rendered, so the page
    /// count and the drawn pages can never disagree.
    /// </summary>
    private (int TotalPages, List<List<PageFragment>> Fragments) LayoutAllStable(ref int totalPagesHint)
    {
        int hint = totalPagesHint;
        var fragments = _pages.Select(p => LayoutDescriptor(p, hint)).ToList();
        int total = fragments.Sum(f => f.Count);

        for (int i = 0; i < 3 && Digits(total) != Digits(hint); i++)
        {
            hint      = total;
            fragments = _pages.Select(p => LayoutDescriptor(p, hint)).ToList();
            total     = fragments.Sum(f => f.Count);
        }

        totalPagesHint = hint;
        return (total, fragments);
    }

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

        // 3. If TOC exists, create placeholder column
        if (tocPageIndices.Count > 0)
        {
            var placeholder = BuildPlaceholderTocColumn(allHeadings, displayTitles);
            _pages[tocPageIndices[0]].ContentSlot.Child = placeholder;
        }

        // 4. Lay out every descriptor into fragments (with placeholder TOC),
        //    iterating until the page-number placeholder used during measurement
        //    has the same digit count as the real total.
        int pagesHint = Elements.Element.DefaultTotalPagesHint;
        var (totalPages, allFragments) = LayoutAllStable(ref pagesHint);

        // The TOC's own page count offsets the page numbers it displays.
        int tocPageCount = tocPageIndices.Count > 0
            ? allFragments[tocPageIndices[0]].Count
            : 0;

        // 5. If TOC exists, render the cached fragments into a throwaway document
        //    with a local recorder to collect heading positions (no re-layout,
        //    no shared state — safe under concurrent document generation).
        if (tocPageIndices.Count > 0)
        {
            var collectedEntries = new List<TocEntry>();
            Action<HeadingElement, int, double> recorder = (h, pg, y) =>
                collectedEntries.Add(new TocEntry
                {
                    Level      = h.Level,
                    Title      = h.Title,
                    PageNumber = pg,
                    Top        = y,
                });

            var dummyDoc = new PdfDocument();
            int dummyPageNum = 0;
            for (int i = 0; i < _pages.Count; i++)
                RenderFragments(dummyDoc, _pages[i], allFragments[i], ref dummyPageNum, totalPages, recorder);

            // Replace placeholder with real TOC, applying the page-number offset,
            // then re-lay-out with the final content.
            var realToc = BuildRealTocColumn(collectedEntries, displayTitles, tocPageCount);
            _pages[tocPageIndices[0]].ContentSlot.Child = realToc;

            (totalPages, allFragments) = LayoutAllStable(ref pagesHint);
        }

        // 6. Create real document
        var doc = new PdfDocument();
        doc.SetMetadata(_metadataTitle, _metadataAuthor, _metadataSubject, _metadataKeywords, _metadataCreator);

        if (_encryptionOptions is not null)
            doc.SetEncryption(_encryptionOptions);

        // Bookmark anchors resolve their page/Y during this final render, so the
        // outline is assembled afterwards (doc.Save consumes it — safe ordering).
        // Dedupe by (title, parent): an anchor placed in a repeated table-header
        // row would otherwise record once per page.
        var anchors = new List<(string Title, string? Parent, int Page, double Top)>();
        var seenAnchors = new HashSet<(string, string?)>();
        Action<string, string?, int, double> anchorRecorder = (title, parent, page, top) =>
        {
            if (seenAnchors.Add((title, parent)))
                anchors.Add((title, parent, page, top));
        };

        int pageNumber = 0;
        for (int i = 0; i < _pages.Count; i++)
            RenderFragments(doc, _pages[i], allFragments[i], ref pageNumber, totalPages,
                headingRecorder: null, bookmarkRecorder: anchorRecorder);

        // Convert recorded anchors to outline nodes (after manual bookmarks, in
        // draw order; parents may be manual bookmarks or earlier anchors).
        foreach (var (title, parentTitle, page, top) in anchors)
        {
            var bm = new BookmarkInfo { Title = title, PageNumber = page, Top = top };
            if (parentTitle is not null)
            {
                var parent = _bookmarks.LastOrDefault(b => b.Title == parentTitle)
                    ?? throw new InvalidOperationException(
                        $"Parent bookmark '{parentTitle}' for anchor '{title}' not found.");
                bm.Parent = parent;
                parent.Children.Add(bm);
            }
            _bookmarks.Add(bm);
        }

        BuildBookmarkTree();
        doc.SetBookmarks(_bookmarks, totalPages);

        doc.Save(output);
    }
}
