# Table of Contents

TerraPDF can automatically generate a navigable Table of Contents page from the headings (`.H1()` — `.H6()`) used throughout your document.

---

## Adding a TOC page

Call `container.TableOfContents()` inside the document composition callback. The TOC page is inserted at that exact point in the document sequence.

```csharp
Document.Create(container =>
{
    // TOC page is placed first
    container.TableOfContents(p =>
    {
        p.Size(PageSize.A4);
        p.Margin(2, Unit.Centimetre);
        p.PageColor(Color.White);
        p.DefaultTextStyle(s => s.FontSize(11));
    });

    // Rest of the document (chapters, sections, etc.)
    container.Page(page => { /* … */ });
}).PublishPdf("output.pdf");
```

The TOC page can be configured like any other page (size, margins, default text style, header/footer).

### Optional configuration

The `TableOfContents` method accepts an optional `Action<PageDescriptor>` for per-page settings. If omitted, sensible defaults are used.

---

## Headings (H1 – H6)

TerraPDF provides six levels of headings via extension methods on `IContainer`:

| Method | Typical use | Default style |
|--------|------------|---------------|
| `.H1(string)` | Document / chapter title | 24 pt, bold |
| `.H2(string)` | Major section heading | 20 pt, bold |
| `.H3(string)` | Sub-section heading | 16 pt, bold |
| `.H4(string)` | Minor heading | 14 pt, italic |
| `.H5(string)` | Small heading | 12 pt, bold |
| `.H6(string)` | Smallest heading | 11 pt, regular |

All six methods accept plain text and return a `TextDescriptor`, so you can customise the appearance further:

```csharp
container.H1("Chapter 1")
          .FontColor(Color.Blue.Darken2)
          .Underline();

container.H2("2.1 Installation")
          .FontSize(18)
          .FontColor(Color.Grey.Darken2);
```

### Where to place headings

Headings can be added anywhere a normal text element is allowed — directly in a `Column`, inside a decorated container, or nested in Rows and Tables. The TOC engine walks the entire element tree, collecting every heading it finds in the order they would be rendered.

---

## How it works

TerraPDF uses a **two-pass layout engine**:

1. **First pass** – The document is measured with a placeholder TOC. Every `HeadingElement` encountered reports its level, title, and page position to the TOC builder.
2. **Second pass** – The final TOC is constructed with real page numbers and internal hyperlinks, inserted into the TOC page slot. The document is rendered again to produce the final PDF.

This approach guarantees accurate page numbers even when headings cause page breaks or overflow.

---

## Internal links

The TOC entries themselves are clickable. Under the hood TerraPDF creates `/GoTo` link annotations that jump directly to the heading's page and vertical position. You can also add your own internal links:

```csharp
container.InternalLink(targetPageNumber, topY).Text("Jump to section");
```

Use `InternalLink` to build cross-references, a custom index, or a "return to top" link at the end of a chapter.

---

## Page numbering

TOC entries display **logical page numbers** — the TOC page itself is treated as page 0 and is not counted. Therefore, the first content page after the TOC is shown as page 1 in the TOC, regardless of how many pages the TOC occupies.

Internal links use the **physical page numbers** (including the TOC pages), so clicking a TOC entry correctly jumps to the heading's location. TerraPDF computes the offset automatically from the actual height of the generated TOC.

Example: If the TOC occupies 2 pages, the first chapter heading appears on physical page 3. The TOC will display "1" for that chapter, but the link will go to page 3.

---

## Limitations & notes

- Only one `TableOfContents()` call is allowed per document. Attempting to add multiple TOC pages throws `InvalidOperationException`.
- The TOC page(s) themselves are **excluded** from the heading list. Headings placed inside the TOC container are ignored (they would appear as self-references).
- Heading levels can be mixed arbitrarily; the generated TOC defaults to 20 pt indentation per level.
- Headings inside tables or rows are collected just like block-level headings.
