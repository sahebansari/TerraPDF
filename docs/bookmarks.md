# Bookmarks (PDF Outlines)

TerraPDF supports PDF bookmarks (also called outlines) — the hierarchical tree structure displayed in PDF viewers' sidebar that allows readers to jump directly to sections of a document.

---

## Overview

Bookmarks are defined at the document level via `IDocumentContainer.Bookmark()` methods. Each bookmark entry consists of:

- A **title** displayed in the viewer's outline pane
- A **destination** — the page number (and optional position) the bookmark links to
- Optional **hierarchy** — child bookmarks nested under a parent

When the PDF is saved, TerraPDF generates a complete `/Outlines` dictionary tree referenced from the document catalog.

---

## Anchor-Based Bookmarks (recommended)

Instead of supplying a page number, anchor a bookmark directly to content with
the `IContainer.Bookmark()` extension — the target page and vertical position
are resolved automatically when the document is rendered, so bookmarks stay
correct as content grows or reflows:

```csharp
Document.Create(c =>
{
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Column(col =>
        {
            col.Item().Bookmark("Chapter 1").H1("Chapter 1");
            col.Item().Text("...");
            col.PageBreak();
            // Nested under Chapter 1, wherever it lands
            col.Item().Bookmark("Section 1.1", parentTitle: "Chapter 1").H2("Section 1.1");
        });
    });
})
.PublishPdf("book.pdf");
```

Anchored bookmarks generate zoom-retaining `/XYZ` destinations at the anchored
element's position — clicking one scrolls to the element without changing the
reader's zoom level — and can be nested under other anchors or manual bookmarks
via `parentTitle`. Anchors wrap individual items — wrapping a whole multi-page
column records only the column's starting position. These destinations also
keep the reader's zoom level and land at the correct Y position after layout.

The page-number-based API below remains available for cases where you want an
outline entry that does not correspond to a specific element.

---

## Basic Usage (manual page numbers)

### Simple bookmark

```csharp
Document.Create(c =>
{
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Text("Chapter 1 content...");
    });

    // Bookmark pointing to page 1
    c.Bookmark("Chapter 1", 1);
})
.PublishPdf("book.pdf");
```

The bookmark above appears in the PDF viewer's outline pane as **Chapter 1**. Clicking it jumps to page 1.

### Bookmark with view position

Supply a Y-coordinate (in points from the top of the page) to control where the view is positioned when the bookmark is activated:

```csharp
c.Bookmark("Introduction", 1, 72.0);  // starts 1 inch from page top
```

This generates an `/XYZ` destination with a null zoom, so the view scrolls to the given position while keeping the reader's current zoom level. If the `top` parameter is omitted, the view scrolls to the top of the page (still zoom-retaining).

---

## Hierarchical Bookmarks

Create nested bookmark structures by specifying a parent title:

```csharp
Document.Create(c =>
{
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Column(col =>
        {
            col.Item().Text("Chapter 1 content...");
            col.Item().Text("Section 1.1 content...");
            col.Item().Text("Section 1.2 content...");
        });
    });

    // Parent bookmark — must be added first
    c.Bookmark("Chapter 1", 1);

    // Children
    c.Bookmark("1.1 Introduction", 1, "Chapter 1");
    c.Bookmark("1.2 Overview",   1, "Chapter 1");

    // Grandchild (three levels deep)
    c.Bookmark("1.2.1 Background", 1, "1.2 Overview");
})
.PublishPdf("book.pdf");
```

**Rules:**

- Parent bookmarks must exist before creating children. Call `c.Bookmark()` for the parent before any child calls that reference it.
- Parent lookup is by **exact title match**. The title string is case-sensitive.
- Children can reference any previously defined bookmark as their parent, not only top-level entries.
- All bookmarks (parents and children) are linked into a single outline tree with proper `/First`, `/Last`, `/Count`, `/Parent`, `/Prev`, and `/Next` references.

---

## Multi-Page Documents

Bookmarks work seamlessly with multi-page documents. The `pageNumber` argument is the 1-based logical page number of the target page.

```csharp
Document.Create(c =>
{
    // Page 1
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Text("Cover page content");
    });

    // Page 2
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Text("Table of Contents");
    });

    // Page 3+
    c.Page(p =>
    {
        p.Size(PageSize.A4);
        p.Content().Column(col =>
        {
            col.Item().Text("Chapter 1 — Basics");
            col.Item().Text("Chapter 2 — Advanced");
        });
    });

    // Bookmarks
    c.Bookmark("Cover",     1);
    c.Bookmark("Contents",  2);
    c.Bookmark("Chapter 1", 3);
    c.Bookmark("Chapter 2", 3);
})
.PublishPdf("book.pdf");
```

> **Note:** If a bookmark targets a page number greater than the total pages in the final document, an `InvalidOperationException` is thrown at save time.

---

## Complete API Reference

All bookmark methods are defined on `IDocumentContainer` (the parameter passed to `Document.Create`).

### Method Signatures

| Method | Parameters | Description |
|--------|------------|-------------|
| `Bookmark(string title, int pageNumber)` | `title`: display text<br>`pageNumber`: 1-based page | Top-level bookmark with `/Fit` destination |
| `Bookmark(string title, int pageNumber, double top)` | `title`, `pageNumber`, `top`: Y position in points | Top-level bookmark with `/FitH` destination |
| `Bookmark(string title, int pageNumber, string parentTitle)` | `title`, `pageNumber`, `parentTitle`: existing bookmark title | Child bookmark under `parentTitle` with `/Fit` |
| `Bookmark(string title, int pageNumber, string parentTitle, double top)` | `title`, `pageNumber`, `parentTitle`, `top` | Child bookmark with `/FitH` destination |

### Exceptions

- `ArgumentNullException` / `ArgumentException` — `title` or `parentTitle` is null/empty/whitespace
- `ArgumentOutOfRangeException` — `pageNumber ≤ 0` or `top < 0`
- `InvalidOperationException` — `parentTitle` does not match any previously defined bookmark

---

## PDF Structure Details

TerraPDF emits standard PDF 1.7 outline objects:

- **Outlines dictionary** (`/Type /Outlines`) — the root node referenced from `/Catalog`
  - `/First` → first top-level bookmark object
  - `/Last` → last top-level bookmark object
  - `/Count` → total number of outline entries (positive integer; negative when collapsed, but TerraPDF emits positive)

Each **bookmark item** dictionary contains:

- `/Type /Outlines`
- `/Title (string)` — the display title (PDF string literal; special chars escaped)
- `/Parent N 0 R` — reference to parent, or to the Outlines root for top-level
- `/Prev N 0 R` — previous sibling (omitted for first sibling)
- `/Next N 0 R` — next sibling (omitted for last sibling)
- `/First N 0 R` — first child (if any)
- `/Last N 0 R` — last child (if any)
- `/Count N` — number of children (positive; negative would indicate collapsed state, unused)
- `/Dest [ pageObj N 0 R /Fit ]` — fit-whole-page destination, **or**
- `/Dest [ pageObj N 0 R /FitH top ]` — fit-width with top edge at `top` coordinate

Page object references are resolved from the 1-based `pageNumber` after all pages are created.

---

## Best Practices

### Order of Definition

Always define parent bookmarks **before** their children:

```csharp
// Good — parent first
c.Bookmark("Chapter 1", 1);
c.Bookmark("Section 1.1", 1, "Chapter 1");

// Bad — child before parent throws InvalidOperationException
c.Bookmark("Section 1.1", 1, "Chapter 1");  // ❌ parent not yet defined
c.Bookmark("Chapter 1", 1);
```

### Unique Titles

Bookmark titles must be unique among siblings but can be reused in different branches:

```csharp
c.Bookmark("Chapter 1", 1);
c.Bookmark("Section A", 1, "Chapter 1");   // OK

c.Bookmark("Chapter 2", 1);
c.Bookmark("Section A", 1, "Chapter 2");   // OK — different parent
```

### Page Number Validation

Page numbers are validated at `PublishPdf()` time after the full document is composed. This means bookmarks can reference any page regardless of the order in which `Page()` calls appear:

```csharp
c.Bookmark("Appendix", 5);        // OK even if Page 5 is defined later
c.Page(p => { /* page 5 definition */ });
```

### Use Positioning for Precision

For long documents, consider setting `top` on section-opening bookmarks so the view lands at the section heading rather than the page top:

```csharp
c.Bookmark("Chapter 1", 3, 72.0);   // starts 1 inch down, heading area
c.Bookmark("Chapter 2", 8, 72.0);
```

---

## Limitations

- PDF outlines do not support styling (font, colour, icons). Appearance is controlled by the PDF viewer.
- No support for **named destinations** with zoom levels beyond `/Fit` and `/FitH`. Future versions may add `/XYZ` for custom zoom.
- No direct API for collapsible initial state — all outline trees open by default in viewers.
- Bookmarks are document-global; they cannot be scoped to a single `PageDescriptor`.

---

## Sample Code

A full working example is available in the TerraPDF sample application:

```
samples/TerraPDF.Sample/Program.cs → GenerateReportWithBookmarks()
```

This generates `08_report_with_bookmarks.pdf` with 5 top-level bookmarks, nested children, and a mix of `/Fit` and `/FitH` destinations across 6 pages.

---

## Related

- [Getting Started](getting-started.md) — basic document structure
- [Page Sizes & Units](page-sizes-and-units.md) — setting page dimensions
- [Layout](layout.md) — Column, Row, Table for structuring content
