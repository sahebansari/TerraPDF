# Visual + text equivalence of two sample-PDF folders (encrypted samples skipped).
import sys, os, pymupdf
a_dir, b_dir = sys.argv[1], sys.argv[2]
dpi = int(sys.argv[3]) if len(sys.argv) > 3 else 110
worst = 0; problems = 0
for name in sorted(os.listdir(a_dir)):
    if not name.endswith('.pdf') or name.startswith('12'): continue
    a = pymupdf.open(os.path.join(a_dir, name)); b = pymupdf.open(os.path.join(b_dir, name))
    msgs = []
    if len(a) != len(b): msgs.append(f'pages {len(a)} vs {len(b)}')
    size_a = os.path.getsize(os.path.join(a_dir, name)); size_b = os.path.getsize(os.path.join(b_dir, name))
    maxfrac = 0.0
    for i in range(min(len(a), len(b))):
        ta = ' '.join(a[i].get_text().split()); tb = ' '.join(b[i].get_text().split())
        if ta != tb: msgs.append(f'p{i+1} text differs')
        pa = a[i].get_pixmap(dpi=dpi); pb = b[i].get_pixmap(dpi=dpi)
        if (pa.width, pa.height) != (pb.width, pb.height): msgs.append(f'p{i+1} size'); continue
        sa, sb = pa.samples, pb.samples
        n = pa.n; diff = 0
        for j in range(0, len(sa), n):
            if max(abs(sa[j+k]-sb[j+k]) for k in range(n)) > 24: diff += 1
        frac = diff / (pa.width*pa.height); maxfrac = max(maxfrac, frac)
        if frac > 0.001: msgs.append(f'p{i+1} {frac*100:.2f}% px differ')
    worst = max(worst, maxfrac)
    if msgs: problems += 1
    print(f'{name}: size {size_a}->{size_b} ({(size_b/size_a-1)*100:+.1f}%)  max px diff {maxfrac*100:.3f}%  {"; ".join(msgs[:5])}')
print(f'files with problems: {problems}; worst page px diff {worst*100:.3f}%')
