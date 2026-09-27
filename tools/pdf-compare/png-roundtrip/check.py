# Checks that every test PNG embedded by embed.cs decodes (in MuPDF) to exactly the source
# pixels and alpha written by make_pngs.py.   python3 check.py <work-dir>
import json, os, sys, pymupdf
os.chdir(sys.argv[1] if len(sys.argv) > 1 else '.')
failures = 0
exp = json.load(open('expected.json')); W, H = exp['W'], exp['H']
for name in ['rgb', 'indexed', 'rgba', 'graya']:
    for suffix in ['', '-enc']:
        d = pymupdf.open(f'{name}{suffix}.pdf')
        img = d[0].get_images(full=True)
        # Extract the image XObject decoded by MuPDF and compare pixel-exactly.
        pix = pymupdf.Pixmap(d, img[0][0])
        if pix.alpha: pix = pymupdf.Pixmap(pix, 0)
        if pix.n != 3: pix = pymupdf.Pixmap(pymupdf.csRGB, pix)
        s = pix.samples; bad = 0
        for y in range(H):
            for x in range(W):
                i = (y * W + x) * 3
                if tuple(s[i:i+3]) != tuple(exp[name][y][x]): bad += 1
        if name + '_a' in exp:
            sm = pymupdf.Pixmap(d, img[0][1]); sa = sm.samples
            bad_a = sum(1 for y in range(H) for x in range(W) if sa[y*W+x] != exp[name + '_a'][y][x])
            print(f'  {name}{suffix} alpha mismatched px={bad_a}'); failures += bad_a
        xref = img[0][0]
        failures += bad
        print(f'{name}{suffix}: {pix.width}x{pix.height} mismatched px={bad}  obj: {d.xref_object(xref, compressed=True)[:160]}')

sys.exit(1 if failures else 0)
