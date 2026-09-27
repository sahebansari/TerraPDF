# Contributing to TerraPDF

Thank you for taking the time to contribute! This document explains how to get
started, what we expect from contributions, and how the release process works.

---

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Project Structure](#project-structure)
4. [Making Changes](#making-changes)
5. [Coding Standards](#coding-standards)
6. [Tests](#tests)
7. [Benchmarks](#benchmarks)
8. [Submitting a Pull Request](#submitting-a-pull-request)
9. [Reporting Bugs](#reporting-bugs)
10. [Suggesting Features](#suggesting-features)
11. [Release Process](#release-process)

---

## Code of Conduct

Be respectful, constructive, and welcoming. We will not tolerate harassment or
discrimination in any form.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (needed to build all
  targets; the library multi-targets .NET 8, .NET 9, and .NET 10)
- Git

### Clone and Build

```sh
git clone https://github.com/sahebansari/TerraPDF.git
cd TerraPDF
dotnet restore
dotnet build
dotnet test
```

---

## Project Structure

```
TerraPDF/
├── src/
│   └── TerraPDF/
│       ├── Core/          # Fluent API — descriptors, extension methods, Document entry point
│       ├── Drawing/       # PDF rendering — PdfDocument, PdfPage, FontMetrics, image decoders
│       ├── Elements/      # Internal layout tree — Column, Row, Table, TextBlock, decorators
│       ├── Helpers/       # Public helpers — Color, PageSize, TextStyle, Unit
│       └── Infra/         # Public interfaces — IContainer, IDocument, IComponent
├── tests/
│   └── TerraPDF.Tests/    # xUnit test projects
├── benchmarks/
│   └── TerraPDF.Benchmarks/  # BenchmarkDotNet performance suite
├── samples/
│   └── TerraPDF.Sample/   # Six sample PDFs covering all major features
├── docs/                  # Markdown documentation
├── .github/workflows/     # CI and publish workflows
├── Directory.Build.props  # Shared MSBuild properties (nullable, warnings, etc.)
└── .editorconfig          # Code style rules
```

---

## Making Changes

1. **Fork** the repository and create a branch from `master`:
   ```sh
   git checkout -b feature/my-feature
   ```

2. Make your changes. Keep commits focused and atomic.

3. Ensure all existing tests still pass and add new tests for any behaviour
   you introduce or change.

4. Run the full test suite before pushing:
   ```sh
   dotnet test -c Release
   ```

5. Open a Pull Request against `master`.

---

## Coding Standards

All standards are enforced automatically by the build:

- **Nullable reference types** are enabled (`<Nullable>enable</Nullable>`) —
  no nullable warnings are accepted.
- **Warnings as errors** — the build fails on any warning.
- **`.editorconfig`** — code style (indentation, naming, var usage, etc.) is
  enforced at build time via `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`.
- **XML doc comments** are required on all public members.
- **Input validation** — every public API method that takes user-supplied
  arguments must guard them with the appropriate
  `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`,
  or `ArgumentOutOfRangeException.ThrowIfNegative/ThrowIfNegativeOrZero` call
  (all available since .NET 8).
- **No third-party dependencies** in the library project — TerraPDF has zero
  runtime dependencies and this must remain true.

---

## Tests

Tests live in `tests/TerraPDF.Tests/` and use **xUnit**.

| File | What it tests |
|------|---------------|
| `DocumentGenerationTests.cs` | Integration — produces valid PDF bytes |
| `FontMetricsTests.cs` | Unit — glyph-width accuracy against Adobe AFM values |
| `TextStyleTests.cs` | Unit — `TextStyle` immutability and merge logic |
| `ValidationTests.cs` | Unit — every public method throws the right exception on bad input |
| `BehaviourTests.cs` | Integration — layout, formatting, decorator, and component behaviour |
| `HighPriorityFeatureTests.cs` | Integration — underline, line-height, hyperlink, per-edge borders |
| `RoundedBorderTests.cs` | Unit/Integration — `RoundedBorder` and `RoundedBox` geometry and validation |
| `PageBreakTests.cs` | Integration — explicit page-break positioning |
| `FontEmbeddingTests.cs` | Unit/Integration — TrueType parsing, `FontFamily` registration/fallback, and the embedded-font PDF object graph |
| `DevanagariReorderingTests.cs` | Unit/Integration — ि matra and reph (र्) codepoint reordering for custom Devanagari fonts |
| `DevanagariConjunctsTests.cs` | Unit/Integration — GSUB-driven conjunct ligature substitution (`half`/`akhn`/`cjct`/`rphf`/`rkrf`) |
| `WordBreakTests.cs` | Integration — character-boundary breaking of a single word wider than the available line width |
| `HeaderFirstPageOnlyTests.cs` | Integration — conditional first-page-only header rendering |
| `CanvasExtrasTests.cs` | Unit/Integration — canvas dashes on curves, rounded-rect paths, gradient shadings, links, bookmarks, and QR codes |

### Running with coverage

```sh
dotnet test -c Release --collect:"XPlat Code Coverage"
```

Coverage reports (Cobertura XML) are written to `TestResults/`.

---

## Benchmarks

Performance is tracked with the BenchmarkDotNet suite in
`benchmarks/TerraPDF.Benchmarks/`:

```sh
dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*'
```

If your change touches layout, text, fonts, images, encryption or serialization,
run the relevant benchmarks on `master` and on your branch, and paste both result
tables (time **and** allocations) into the PR description. See
[docs/benchmarks.md](docs/benchmarks.md) for the scenarios and how to compare runs.

---

## Submitting a Pull Request

- Fill in the PR template with a clear description of what changed and why.
- Reference any related issues (e.g. `Closes #42`).
- All CI checks must pass before a review is requested.
- At least one maintainer approval is required before merging.

---

## Reporting Bugs

Open a [GitHub Issue](https://github.com/sahebansari/TerraPDF/issues) and include:

- TerraPDF version
- .NET runtime version
- A minimal reproducible code snippet
- The expected vs. actual behaviour

---

## Suggesting Features

Open a [GitHub Issue](https://github.com/sahebansari/TerraPDF/issues) with the
label `enhancement`. Describe the use-case, not just the solution.

---

## Release Process

1. Bump `<Version>` in `src/TerraPDF/TerraPDF.csproj`.
2. Update `CHANGELOG.md` — move items from `[Unreleased]` to a new versioned section.
3. Commit and push to `master`.
4. Create a **GitHub Release** with a tag matching the version (e.g. `v1.1.0`).
   - The `publish.yml` workflow automatically packs and pushes the `.nupkg`
     and `.snupkg` (symbols) to nuget.org.
