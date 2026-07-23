# Custom Fonts & Full Unicode

TerraPDF ships with the three PDF standard-14 families (Helvetica, Times,
Courier), rendered via `WinAnsiEncoding` — this covers Western European
languages but not brand typefaces, and not scripts outside Windows-1252
(Cyrillic, Greek, and beyond). `FontFamily.Register` embeds a real TrueType
font so you can use any typeface, with full Unicode text support wherever
that font has glyphs.

No external packages are required. Font parsing, embedding, and CID-keyed
text encoding are implemented entirely in `System`-namespace code.

---

## Quick start

```csharp
using TerraPDF.Helpers;

// Register once (e.g. at application startup) — parsing is cached, so this
// is safe to call from a long-lived server process and reused by every
// document rendered afterwards, including concurrently.
FontFamily.Register("Brand", "fonts/Brand-Regular.ttf");
FontFamily.Register("Brand", "fonts/Brand-Bold.ttf", bold: true);

Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSize.A4);
        page.DefaultTextStyle(s => s.FontFamily("Brand"));

        page.Content().Column(col =>
        {
            col.Item().Text("Héllo, Привет, Γειά σου").FontSize(18);
            col.Item().Text("Now in bold.").Bold();
        });
    });
})
.PublishPdf("branded.pdf");
```

`TextStyle.FontFamily("Brand")` and `.Bold()`/`.Italic()` are the same API
you already use for the built-in families — nothing else changes.

---

## `FontFamily.Register` overloads

| Overload | Use when |
|----------|----------|
| `Register(string familyName, string fontFilePath, bool bold = false, bool italic = false)` | Loading directly from a `.ttf`/`.otf` file on disk. |
| `Register(string familyName, byte[] fontFileBytes, bool bold = false, bool italic = false)` | The font data is already in memory (embedded resource, downloaded, etc.). |
| `Register(string familyName, Stream fontFileStream, bool bold = false, bool italic = false)` | Reading from a stream (read to completion; the stream is not disposed). |

A family can have up to four registered variants — regular, bold, italic,
and bold-italic — registered independently under the same `familyName`.
Requesting a style that wasn't registered falls back to the closest
available variant instead of throwing (e.g. calling `.Bold()` on a family
that only registered a regular file renders with the regular outlines).

---

## What gets embedded

Each registered variant is embedded as a `Type0`/`CIDFontType2` composite
font with `Identity-H` encoding: text is addressed by glyph ID, not by a
fixed 256-slot encoding, so any glyph the font contains can be shown —
not just Windows-1252. A `ToUnicode` CMap is included so copy/paste and
text extraction recover the correct Unicode text.

The font is embedded **once per document**, even when used across many
pages or many times per page — a registered font used throughout a
200-page report still costs one embedded copy.

Characters the font has no glyph for render as the font's `.notdef` glyph
(usually a blank box) rather than throwing — the same graceful-fallback
behaviour as the standard fonts substituting `?` for unmappable characters.

---

## Known limitations

- **TrueType outlines only.** `.ttf` files, and `.otf` files that still
  carry a `glyf`/`loca` table, are supported. CFF-flavoured OpenType
  (`OTTO`) and TrueType Collections (`.ttc`) throw
  `NotSupportedException` — a different embedding path
  (`/FontFile3`, `CIDFontType0`) would be needed for those. Future versions
  may add it.
- **No glyph subsetting.** The whole font file is embedded, so a large
  font increases the output PDF's size accordingly. Future versions may
  add subsetting to embed only the glyphs actually used.
- **No synthetic bold/italic.** If a style wasn't registered, TerraPDF
  falls back to the closest registered variant rather than skewing or
  thickening glyphs to approximate it.
- **Partial OpenType shaping for Devanagari — no full GSUB/GPOS engine.**
  Codepoints map through the font's `cmap` to a glyph ID as usual, but
  TerraPDF applies two pure-C# corrections automatically for any text drawn
  through a registered custom font (no dependency, no public API — this is
  transparent):
  - **Matra reordering**: the vowel sign ि (`U+093F`) is stored after its
    consonant in Unicode but must render before it; TerraPDF moves it to the
    correct position before the cluster it attaches to.
  - **Conjunct ligatures**: TerraPDF reads the font's own `GSUB` table for
    the `half`/`akhn`/`cjct` features and substitutes the ligature glyphs
    the font itself defines — e.g. स्व, स्थ, क्ष, ज्ञ render as proper
    joined forms, not separate glyphs with a visible ् mark, whenever the
    font provides those substitution rules (most well-designed Devanagari
    fonts define them for nearly every consonant).
  - **र-conjuncts**: reph (र् at the start of a cluster, e.g. धर्म, वर्तमान)
    is corrected by moving the र् pair to the end of the cluster it
    attaches to, then substituting it for the font's reph glyph via GSUB's
    `rphf` feature. Below-base/post-base 'ra' forms (प्र, क्र, त्र, ष्ट्र, …)
    are substituted via GSUB's `rkrf` feature — no reordering needed there,
    since the codepoints are already in the right order. Both use the same
    substitution mechanism as the other conjunct ligatures above, applied
    automatically whenever the font provides those GSUB rules.
  - **Not covered**: `blwf` — below-base forms for consonants *other* than
    र (used by some fonts for other subjoined forms) — uses contextual GSUB
    lookups TerraPDF's reader doesn't parse, so those specific cases may
    still draw as separate glyphs.
  - This is intentional: `src/TerraPDF` stays pure managed C# with no
    native dependency and will never bundle a full shaping engine (no
    GPOS mark positioning, no general Indic reordering beyond the ि
    matra and reph cases above) — this is a scoped, font-data-driven
    substitution, not a shaping engine.
