# PDF comparison and profiling tools

Scripts used to prove that performance work does not change TerraPDF's output, or changes
it only where intended, and to find where time and memory go. They were used for the work
described in [benchmarks/benchmarks-analysis-2026-09-26.md](../../benchmarks/benchmarks-analysis-2026-09-26.md).

Run everything from the repository root. The C# tools are .NET 10 file-based apps
(`dotnet run file.cs`); the Python tools need PyMuPDF:

```sh
python3 -m venv .venv && .venv/bin/pip install -r tools/pdf-compare/requirements.txt
```

## Output equivalence

| Tool | What it checks | Use when |
|---|---|---|
| `generate-samples.sh <dir>` | Builds and writes every sample PDF into `<dir>` | Before and after a change |
| `compare-bytes.sh <before> <after>` | Byte-for-byte identity of two sample folders (encrypted `12*` samples skipped: random file id). Non-zero exit on any difference | Every change meant to keep output identical |
| `visual_compare.py <before> <after>` | Renders every page at 110 dpi and counts pixels that differ by more than 24 per channel; compares the extracted text of every page; reports size changes | Changes that alter content streams |
| `glyph_positions.py <before> <after>` | Largest shift of any glyph origin, per file | Text layout or font-width changes |

Typical run:

```sh
git stash && tools/pdf-compare/generate-samples.sh /tmp/before && git stash pop
tools/pdf-compare/generate-samples.sh /tmp/after
tools/pdf-compare/compare-bytes.sh /tmp/before /tmp/after
.venv/bin/python tools/pdf-compare/visual_compare.py /tmp/before /tmp/after
```

## Focused checks

**PNG round trip** (`png-roundtrip/`): writes RGB, palette, RGBA and gray+alpha PNGs using all
five row filters and several IDAT chunks, embeds them (plain and encrypted), and checks that
the image and soft mask MuPDF decodes are pixel-identical to the source.

```sh
python3 tools/pdf-compare/png-roundtrip/make_pngs.py /tmp/png
dotnet run tools/pdf-compare/png-roundtrip/embed.cs -- /tmp/png
.venv/bin/python tools/pdf-compare/png-roundtrip/check.py /tmp/png
```

**Built-in font widths** (`font-widths/`): draws every WinAnsi character, plus two unmappable
ones, as a single run in each of the 12 built-in font variants and checks that MuPDF places
every glyph where the AFM widths say (fails above 0.01 pt).

```sh
(cd tools/pdf-compare/font-widths && python3 make_chars.py /tmp/chars.txt)
dotnet run tools/pdf-compare/font-widths/draw_all_glyphs.cs -- /tmp/chars.txt /tmp/widths.pdf
(cd tools/pdf-compare/font-widths && ../../../.venv/bin/python check_widths.py /tmp/widths.pdf /tmp/chars.txt ../../../tests/TerraPDF.Tests/TestAssets/Afm)
```

**QR symbols** (`qr-symbols/`): hashes the module matrix of 458 symbols (4 error-correction
levels, versions 1 to 40). `reference-symbols.txt` was produced by the original encoder;
any change to the QR code must reproduce it exactly.

```sh
dotnet run -c Release tools/pdf-compare/qr-symbols/dump_symbols.cs -- /tmp/qr.txt
cmp tools/pdf-compare/qr-symbols/reference-symbols.txt /tmp/qr.txt
```

`dump_symbols.cs` sets its assembly name to `TerraPDF.Benchmarks` so it can call the
library's internal QR generator (that name is in TerraPDF's `InternalsVisibleTo`).

## Profiling (`profiling/`)

| Tool | Output |
|---|---|
| `alloc_by_type.cs <rows> [table\|bare\|text]` | Allocation per row split into compose and publish, then allocated bytes by type, sampled in process from the runtime's `GCAllocationTick` events |
| `alloc_stacks.cs <trace.nettrace> <type>` | Allocation stacks for one type, from a `gc-verbose`-style trace |
| `cpu_hotspots.cs <trace.nettrace>` | Exclusive and inclusive time per method, from a thread-time trace |

Collecting traces without installing anything globally:

```sh
# CPU (sampled thread time)
dnx dotnet-trace --yes -- collect --profile dotnet-common,dotnet-sampled-thread-time -o cpu.nettrace -- dotnet <app.dll> <args>
dotnet run tools/pdf-compare/profiling/cpu_hotspots.cs -- cpu.nettrace

# Allocations with stacks
dnx dotnet-trace --yes -- collect --providers Microsoft-Windows-DotNETRuntime:0x1:5 -o alloc.nettrace -- dotnet <app.dll> <args>
dotnet run tools/pdf-compare/profiling/alloc_stacks.cs -- alloc.nettrace System.Double
```

Inlined methods are reported under their caller. To split them, temporarily mark the
methods of interest `[MethodImpl(MethodImplOptions.NoInlining)]`. Allocation samples taken
right after start-up include code that has not reached tier-1 JIT yet (for example, boxing
that the optimising JIT removes), so warm up well before measuring.
