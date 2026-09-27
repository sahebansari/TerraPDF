# Writes every defined WinAnsi character (except space) to chars.txt for draw_all_glyphs.cs.
import sys
from winansi import NAMES
chars = ''.join(bytes([i + 32]).decode('cp1252') for i, n in enumerate(NAMES) if n is not None and n != 'space')
open(sys.argv[1] if len(sys.argv) > 1 else 'chars.txt', 'w', encoding='utf-8').write(chars)
