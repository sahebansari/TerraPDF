import zlib, struct, json, sys
out = sys.argv[1]
W, H = 97, 61
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
def paeth(a, b, c):
    p = a + b - c; pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
    return a if pa <= pb and pa <= pc else (b if pb <= pc else c)
def filt(rows, bpp):
    out = b''; prev = bytes(len(rows[0]))
    for y, row in enumerate(rows):
        f = y % 5; r = bytearray()
        for i, x in enumerate(row):
            a = row[i-bpp] if i >= bpp else 0; b = prev[i]; c = prev[i-bpp] if i >= bpp else 0
            pred = [0, a, b, (a+b)//2, paeth(a, b, c)][f]
            r.append((x - pred) & 0xff)
        out += bytes([f]) + bytes(r); prev = row
    return out
def png(ct, rows, bpp, plte=None, split=True):
    ihdr = struct.pack('>IIBBBBB', W, H, 8, ct, 0, 0, 0)
    data = zlib.compress(filt(rows, bpp), 9)
    s = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr)
    if plte: s += chunk(b'PLTE', plte)
    # several IDAT chunks to exercise concatenation
    for i in range(0, len(data), 700): s += chunk(b'IDAT', data[i:i+700])
    return s + chunk(b'IEND', b'')
rgb = [[((x*7+y*3) % 256, (x*x+y) % 256, (x^y)*5 % 256) for x in range(W)] for y in range(H)]
open(f'{out}/rgb.png', 'wb').write(png(2, [bytes(v for p in r for v in p) for r in rgb], 3))
pal = [((i*37) % 256, (i*91) % 256, (255-i) % 256) for i in range(200)]
idx = [[(x*3 + y*7) % 200 for x in range(W)] for y in range(H)]
open(f'{out}/indexed.png', 'wb').write(png(3, [bytes(r) for r in idx], 1, plte=bytes(v for p in pal for v in p)))
rgba = [[(p[0], p[1], p[2], (x*11 + y*5) % 256) for x, p in enumerate(r)] for y, r in enumerate(rgb)]
open(f'{out}/rgba.png', 'wb').write(png(6, [bytes(v for p in r for v in p) for r in rgba], 4))
ga = [[((x*9+y) % 256, (x+y*13) % 256) for x in range(W)] for y in range(H)]
open(f'{out}/graya.png', 'wb').write(png(4, [bytes(v for p in r for v in p) for r in ga], 2))
json.dump({'W': W, 'H': H, 'rgb': rgb, 'indexed': [[pal[i] for i in r] for r in idx],
           'rgba': [[p[:3] for p in r] for r in rgba], 'rgba_a': [[p[3] for p in r] for r in rgba],
           'graya': [[(p[0],)*3 for p in r] for r in ga], 'graya_a': [[p[1] for p in r] for r in ga]}, open(f'{out}/expected.json', 'w'))
