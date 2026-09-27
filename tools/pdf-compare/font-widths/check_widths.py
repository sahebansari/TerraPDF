# Verifies built-in font widths against a real viewer: draw_all_glyphs.cs draws every WinAnsi
# character (plus two unmappable ones) as one text run in each of the 12 built-in variants;
# this script checks that MuPDF places every glyph where the AFM widths say it should.
#   python3 check_widths.py <all.pdf> <chars.txt> <afm-dir>
import sys, pymupdf
from winansi import table

pdf, chars_file, afm_dir = sys.argv[1], sys.argv[2], sys.argv[3]
afm_name = {('Helvetica', False, False): 'Helvetica', ('Helvetica', True, False): 'Helvetica-Bold',
            ('Helvetica', False, True): 'Helvetica-Oblique', ('Helvetica', True, True): 'Helvetica-BoldOblique',
            ('Times', False, False): 'Times-Roman', ('Times', True, False): 'Times-Bold',
            ('Times', False, True): 'Times-Italic', ('Times', True, True): 'Times-BoldItalic'}
chars = open(chars_file, encoding='utf-8').read() + '中Ж'
lines = []
for b in pymupdf.open(pdf)[0].get_text('rawdict')['blocks']:
    for l in b.get('lines', []):
        lines.append((l['spans'][0]['font'], [c for s in l['spans'] for c in s['chars']]))

worst_all, i = 0.0, 0
for family in ['Helvetica', 'Times', 'Courier']:
    for bold, italic in [(False, False), (True, False), (False, True), (True, True)]:
        font, cs = lines[i]; i += 1
        t = None if family == 'Courier' else table(afm_dir, afm_name[(family, bold, italic)])
        def width(ch):
            if t is None: return 600
            try: code = ch.encode('cp1252')[0]
            except UnicodeEncodeError: code = ord('?')
            return t[code - 32]
        expected, worst = cs[0]['origin'][0], 0.0
        for ch, c in zip(chars, cs):
            worst = max(worst, abs(c['origin'][0] - expected))
            expected += width(ch) * 10 / 1000
        worst_all = max(worst_all, worst)
        print(f'{font:24} glyphs={len(cs)} max drift {worst:.3f} pt')
print(f'worst drift {worst_all:.3f} pt')
sys.exit(0 if worst_all < 0.01 else 1)
