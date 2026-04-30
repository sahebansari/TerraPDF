using TerraPDF.Drawing;
using TerraPDF.Elements;
using TerraPDF.Helpers;
using TerraPDF.Infra;

namespace TerraPDF.Core;

/// <summary>
/// Collects <see cref="PageDescriptor"/> instances and renders them to a
/// <see cref="PdfDocument"/>.  Returned by <see cref="Document.Create(Action{IDocumentContainer})"/>.
/// </summary>
public sealed class DocumentComposer : IDocumentContainer
{
    private readonly List<PageDescriptor> _pages = [];

    // -- IDocumentContainer ----------------------------------------

    /// <inheritdoc/>
    public void Page(Action<PageDescriptor> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var descriptor = new PageDescriptor();
        configure(descriptor);
        _pages.Add(descriptor);
    }

    // -- Output ----------------------------------------------------

    /// <summary>Saves the PDF to the given file path.</summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or whitespace.</exception>
    public void GeneratePdf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteTo(fs);
    }

    /// <summary>Returns the PDF document as a byte array.</summary>
    public byte[] GeneratePdf()
    {
        using var ms = new MemoryStream();
        WriteTo(ms);
        return ms.ToArray();
    }

    /// <summary>Writes the PDF to an existing stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
    public void GeneratePdf(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        WriteTo(stream);
    }

    // -- Rendering -------------------------------------------------

    private void WriteTo(Stream output)
    {
        var doc = new PdfDocument();

        // Measurement pass: count the total number of PDF pages that will be produced
        // across all descriptors so that page-number spans show the correct total.
        int totalPages = _pages.Sum(CountPdfPages);

        // Render pass: emit each descriptor, potentially spanning several PDF pages.
        int pageNumber = 0;
        foreach (var descriptor in _pages)
            RenderDescriptor(doc, descriptor, ref pageNumber, totalPages);

        doc.Save(output);
    }

    // ---------------------------------------------------------------
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
        bool isFirstPage = true)
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
}
