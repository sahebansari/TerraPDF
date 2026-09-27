# Max glyph-origin displacement (pt) between two folders of PDFs, per file.
import sys, os, pymupdf
def chars(page):
    out = []
    for b in page.get_text('rawdict')['blocks']:
        for l in b.get('lines', []):
            for s in l['spans']:
                for c in s['chars']:
                    out.append((c['c'], c['origin']))
    return out
a_dir, b_dir = sys.argv[1], sys.argv[2]
for name in sorted(os.listdir(a_dir)):
    if not name.endswith('.pdf') or name.startswith('12'): continue
    a = pymupdf.open(os.path.join(a_dir, name)); b = pymupdf.open(os.path.join(b_dir, name))
    worst = (0, None)
    for i in range(len(a)):
        ca = sorted(chars(a[i]), key=lambda t: (round(t[1][1], 1), t[1][0]))
        cb = sorted(chars(b[i]), key=lambda t: (round(t[1][1], 1), t[1][0]))
        if len(ca) != len(cb): print(name, 'page', i+1, 'char count', len(ca), len(cb)); continue
        for (x, pa), (y, pb) in zip(ca, cb):
            d = max(abs(pa[0]-pb[0]), abs(pa[1]-pb[1]))
            if d > worst[0]: worst = (d, (i+1, x, pa))
    print(f'{name}: max glyph shift {worst[0]:.3f} pt at {worst[1]}')
