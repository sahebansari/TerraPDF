# Unicode & Character Encoding

TerraPDF uses **WinAnsiEncoding** for all built-in Type 1 fonts (Helvetica, Times, Courier
and their Bold/Italic variants). This page explains what that means in practice — which
characters render correctly, how they are encoded in the PDF content stream, and how to
avoid the common pitfall of characters appearing as `?` in the output.

---

## What is WinAnsiEncoding?

WinAnsiEncoding is the character encoding vector declared in the PDF font dictionary for
every built-in Type 1 font. It maps byte values in the range 0x20–0xFF to Unicode
code points (and therefore to printable glyphs) using the **Windows-1252** code page.

The mapping covers three distinct regions:

| Byte range | Region | Characters |
|------------|--------|-----------|
| 0x20–0x7E | Printable ASCII | Space through tilde — all 95 printable ASCII characters |
| 0x80–0x9F | Windows-1252 specials | 27 typographic characters (see table below) |
| 0xA0–0xFF | Latin-1 Supplement | All 96 Latin-1 characters (U+00A0–U+00FF) |

Byte values 0x7F, 0x81, 0x8D, 0x8F, 0x90, and 0x9D are **undefined** in WinAnsiEncoding
and have no glyph. Any Unicode code point outside the ranges listed above — such as
characters in the Latin Extended-A/B blocks (U+0100+) — likewise has no mapping and
**will render as `?`** in the PDF viewer.

---

## Safe character ranges

To guarantee correct rendering with WinAnsiEncoding, limit your text to:

- All standard ASCII printable characters (U+0020–U+007E).
- The 27 Windows-1252 typographic specials listed below.
- The full Latin-1 Supplement block (U+00A0–U+00FF), which includes all common
  Western-European accented letters (À–ÿ), currency symbols (¢ £ ¥), mathematical
  symbols (± × ÷ °), and more.

Characters in **U+0100 and above** (e.g. Polish Ł/ł, Czech Č/č/Ř/ř, Turkish Ş/ş/Ğ/ğ/ı,
Romanian Ș/ț, Hungarian Ő/ő/Ű/ű) are **not** covered by WinAnsiEncoding and will not
render correctly with the built-in Type 1 fonts.

---

## Windows-1252 Typographic Specials (0x80–0x9F)

These 27 characters occupy the byte range that the C1 control codes occupy in pure
ISO-8859-1. Windows-1252 repurposes them for useful typographic glyphs, and TerraPDF's
AFM width tables cover all 27:

| Unicode | WinAnsi byte | Character | Name |
|---------|-------------|-----------|------|
| U+2018 | 0x91 | ' | Left single quotation mark |
| U+2019 | 0x92 | ' | Right single quotation mark |
| U+201C | 0x93 | " | Left double quotation mark |
| U+201D | 0x94 | " | Right double quotation mark |
| U+2013 | 0x96 | – | En dash |
| U+2014 | 0x97 | — | Em dash |
| U+2026 | 0x85 | … | Horizontal ellipsis |
| U+2022 | 0x95 | • | Bullet |
| U+20AC | 0x80 | € | Euro sign |
| U+2122 | 0x99 | ™ | Trade mark sign |
| U+0152 | 0x8C | Œ | OE ligature (capital) |
| U+0153 | 0x9C | œ | OE ligature (small) |
| U+2020 | 0x86 | † | Dagger |
| U+2021 | 0x87 | ‡ | Double dagger |
| U+2030 | 0x89 | ‰ | Per mille sign |
| U+0160 | 0x8A | Š | S with caron (capital) |
| U+0161 | 0x9A | š | S with caron (small) |
| U+2018 | 0x91 | ' | Left single quotation mark |
| U+201A | 0x82 | ‚ | Single low-9 quotation mark |
| U+0192 | 0x83 | ƒ | Latin small letter f with hook |
| U+201E | 0x84 | „ | Double low-9 quotation mark |
| U+2020 | 0x86 | † | Dagger |
| U+0152 | 0x8C | Œ | OE ligature (capital) |
| U+017D | 0x8E | Ž | Z with caron (capital) |
| U+017E | 0x9E | ž | Z with caron (small) |
| U+0178 | 0x9F | Ÿ | Y with diaeresis (capital) |
| U+2039 | 0x8B | ‹ | Single left-pointing angle quotation mark |
| U+203A | 0x9B | › | Single right-pointing angle quotation mark |

Use these characters directly in your C# strings with their Unicode escape sequences
or by typing them as literal characters:

```csharp
// Typographic quotes, dashes, and ellipsis
container.Text("\u201CHello,\u201D she said\u2026");
container.Text("Pages 42\u201347");         // en dash
container.Text("Time\u2014and tide.");      // em dash
container.Text("Price: \u20AC 1,299.00");  // Euro sign
container.Text("TerraPDF\u2122");           // trade mark
```

---

## Latin-1 Supplement (U+00A0–U+00FF)

All 96 characters in the Latin-1 Supplement block are covered by WinAnsiEncoding.
This includes:

- **Accented capitals**: À Á Â Ã Ä Å Æ Ç È É Ê Ë Ì Í Î Ï Ð Ñ Ò Ó Ô Õ Ö Ø Ù Ú Û Ü Ý Þ
- **Accented lowercase**: à á â ã ä å æ ç è é ê ë ì í î ï ð ñ ò ó ô õ ö ø ù ú û ü ý þ ÿ ß
- **Punctuation & spacing**: non-breaking space, «, », ¿, ¡, ·, ¶, §
- **Mathematical & technical**: ± × ÷ ° µ ² ³ ¼ ½ ¾
- **Currency & commerce**: ¢ £ ¤ ¥ ¦ © ® ¯

These render perfectly in all built-in TerraPDF fonts:

```csharp
// All of these are safe — they are in Latin-1 Supplement
container.Text("Ágnes sétált a városban.");       // á, é — U+00E1, U+00E9 ✓
container.Text("Français, señor, São Paulo.");    // ç, ñ, ã — all Latin-1 ✓
container.Text("Björk åkte till Göteborg.");      // ö, å — U+00F6, U+00E5 ✓
container.Text("Mjölk kostar 3 kr/l ± 5 öre."); // ö, ± — all Latin-1 ✓
```

---

## Content-stream encoding

Internally, TerraPDF converts every non-ASCII character to its WinAnsi byte value and
writes it as an **octal escape** (`\nnn`) in the PDF string literal. This keeps the
content stream pure 7-bit ASCII while the PDF reader resolves each byte value through
the font's `/WinAnsiEncoding` vector.

For example, the character `é` (U+00E9) maps to WinAnsi byte 0xE9 (233 decimal) and
is written as `\351` in the content stream. The PDF reader sees byte 0xE9, looks it up
in the `/WinAnsiEncoding` array, finds `eacute`, and renders the correct glyph.

Metadata fields (document title, author, subject, keywords) and bookmark titles are
encoded as **UTF-16BE hex strings** (`<FEFF…>`) so that viewer title bars and outline
panels display the correct text regardless of encoding limitations.

---

## AFM glyph-width tables

TerraPDF ships with extended AFM (Adobe Font Metrics) advance-width tables covering the
**full WinAnsi byte range** 0x20–0xFF — not just printable ASCII. This ensures that
word-wrapping, justification, and column-width calculations are pixel-accurate for
every accented character and typographic special.

The width tables are sourced from the Adobe Core-14 AFM files and are compiled directly
into the library (no external files required at runtime).

---

## Avoiding `?` characters

A character renders as `?` when TerraPDF cannot map it to a WinAnsi byte value. The
most common cause is using characters from the Latin Extended-A/B blocks (U+0100+) that
Windows-1252 does not cover.

**Checklist:**

1. Check the Unicode code point of the problem character (e.g. `Ł` is U+0141).
2. If it is above U+00FF and not one of the 27 Windows-1252 specials, it will not render.
3. Replace it with a visually similar character that is within the safe range, or
   rewrite the text to avoid it.

**Common substitutions:**

| Problematic character | Unicode | Substitute | Notes |
|-----------------------|---------|-----------|-------|
| Ł / ł (Polish L) | U+0141 / U+0142 | L / l | Drop the stroke |
| Ż / ż (Polish Z dot) | U+017B / U+017C | Z / z | Drop the dot |
| Č / č (Czech C caron) | U+010C / U+010D | C / c | Drop the caron |
| Ř / ř (Czech R caron) | U+0158 / U+0159 | R / r | Drop the caron |
| Ş / ş (Turkish S cedilla) | U+015E / U+015F | Ş → use ş from Latin-1? No — use S/s | Not in WinAnsi |
| Ğ / ğ (Turkish G breve) | U+011E / U+011F | G / g | Drop the breve |
| İ / ı (Turkish dotted/dotless I) | U+0130 / U+0131 | I / i | Use plain I |
| Ő / ő (Hungarian O double acute) | U+0150 / U+0151 | Ö / ö (U+00D6 / U+00F6) | Close visual match |
| Ű / ű (Hungarian U double acute) | U+0170 / U+0171 | Ü / ü (U+00DC / U+00FC) | Close visual match |

---

## Sample: language showcase

The `11_UnicodeShowcase.cs` sample in `samples/TerraPDF.Sample/Samples/` generates a
5-page PDF that demonstrates all aspects of WinAnsiEncoding support:

```
Page 1  Introduction and 18-language sample table
Page 2  Windows-1252 specials table + Latin-1 supplement groups
Page 3  Complete WinAnsiEncoding reference grid (0x20–0xFF)
Page 4  Multi-font comparison (Helvetica / Times / Courier)
Page 5  Font-metrics deep-dive: advance-width heat-map + justified paragraph
```

Run it with:

```sh
cd samples/TerraPDF.Sample
dotnet run
# Output: 11_unicode_showcase.pdf
```
