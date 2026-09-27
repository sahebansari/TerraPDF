# WinAnsiEncoding glyph names for bytes 32..255 (PDF 32000-1, Annex D). None = undefined.
ASCII = ("space exclam quotedbl numbersign dollar percent ampersand quotesingle parenleft parenright asterisk plus comma "
 "hyphen period slash zero one two three four five six seven eight nine colon semicolon less equal greater question at "
 "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z bracketleft backslash bracketright asciicircum underscore grave "
 "a b c d e f g h i j k l m n o p q r s t u v w x y z braceleft bar braceright asciitilde").split()
HIGH = ("Euro - quotesinglbase florin quotedblbase ellipsis dagger daggerdbl circumflex perthousand Scaron guilsinglleft OE - Zcaron - "
 "- quoteleft quoteright quotedblleft quotedblright bullet endash emdash tilde trademark scaron guilsinglright oe - zcaron Ydieresis "
 "space exclamdown cent sterling currency yen brokenbar section dieresis copyright ordfeminine guillemotleft logicalnot hyphen registered macron "
 "degree plusminus twosuperior threesuperior acute mu paragraph periodcentered cedilla onesuperior ordmasculine guillemotright onequarter onehalf threequarters questiondown "
 "Agrave Aacute Acircumflex Atilde Adieresis Aring AE Ccedilla Egrave Eacute Ecircumflex Edieresis Igrave Iacute Icircumflex Idieresis "
 "Eth Ntilde Ograve Oacute Ocircumflex Otilde Odieresis multiply Oslash Ugrave Uacute Ucircumflex Udieresis Yacute Thorn germandbls "
 "agrave aacute acircumflex atilde adieresis aring ae ccedilla egrave eacute ecircumflex edieresis igrave iacute icircumflex idieresis "
 "eth ntilde ograve oacute ocircumflex otilde odieresis divide oslash ugrave uacute ucircumflex udieresis yacute thorn ydieresis").split()
assert len(ASCII) == 95 and len(HIGH) == 128, (len(ASCII), len(HIGH))
NAMES = ASCII + [None] + [None if n == '-' else n for n in HIGH]   # index = byte - 32 (DEL at 95)
# Euro is missing from the 1997 AFMs; viewers use these widths (as PDFBox/pdf.js do).
EURO = {'Helvetica': 556, 'Helvetica-Bold': 556, 'Times-Roman': 500, 'Times-Bold': 500, 'Times-Italic': 500, 'Times-BoldItalic': 500}

def afm_widths(path):
    w = {}
    for line in open(path, encoding='latin-1'):
        if line.startswith('C '):
            parts = dict(p.strip().split(' ', 1) for p in line.split(';') if p.strip() and ' ' in p.strip())
            w[parts['N']] = int(float(parts['WX']))
    return w

def table(font_dir, font):
    w = afm_widths(f'{font_dir}/{font}.afm')
    return [0 if n is None else w.get(n, EURO.get(font) if n == 'Euro' else None) for n in NAMES]
