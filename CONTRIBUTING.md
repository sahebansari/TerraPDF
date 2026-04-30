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
7. [Submitting a Pull Request](#submitting-a-pull-request)
8. [Reporting Bugs](#reporting-bugs)
9. [Suggesting Features](#suggesting-features)
10. [Release Process](#release-process)

---

## Code of Conduct

Be respectful, constructive, and welcoming. We will not tolerate harassment or
discrimination in any form.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later (the library
  targets both .NET 8 and .NET 9)
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
├── samples/
│   └── TerraPDF.Sample/   # Six sample PDFs covering all major features
├── docs/                  # Markdown documentation
├── .github/workflows/     # CI and publish workflows
├── Directory.Build.props  # Shared MSBuild properties (nullable, warnings, etc.)
└── .editorconfig          # Code style rules
```

---

## Making Changes

1. **Fork** the repository and create a branch from `main`:
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

5. Open a Pull Request against `main`.

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
| `HeaderFirstPageOnlyTests.cs` | Integration — conditional first-page-only header rendering |

### Running with coverage

```sh
dotnet test -c Release --collect:"XPlat Code Coverage"
```

Coverage reports (Cobertura XML) are written to `TestResults/`.

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
3. Commit and push to `main`.
4. Create a **GitHub Release** with a tag matching the version (e.g. `v1.1.0`).
   - The `publish.yml` workflow automatically packs and pushes the `.nupkg`
     and `.snupkg` (symbols) to nuget.org.
