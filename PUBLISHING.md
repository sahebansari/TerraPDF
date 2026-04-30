# Publishing TerraPDF to NuGet.org

This guide covers everything needed to publish a new version of TerraPDF as a
free, publicly available NuGet package.

---

## One-time Setup

### 1. Create a nuget.org account

1. Go to [https://www.nuget.org](https://www.nuget.org) and sign in with a
   Microsoft account (or create one for free).
2. Confirm your email address if prompted.

### 2. Claim the package ID

The first time you push `TerraPDF`, nuget.org automatically registers
the package ID to your account. No prior reservation is needed.

### 3. Generate an API key

1. Sign in to nuget.org.
2. Click your username → **API Keys** → **Create**.
3. Give it a name (e.g. `TerraPDF-GitHub-Actions`).
4. Set **Glob pattern** to `TerraPDF` (or `*` for all packages).
5. Set an expiry (365 days is common; renew before it expires).
6. Copy the key — it is shown **only once**.

### 4. Add the API key as a GitHub secret

1. On GitHub, open the repository → **Settings** → **Secrets and variables** →
   **Actions** → **New repository secret**.
2. Name: `NUGET_API_KEY`
3. Value: paste the key from step 3.
4. Click **Add secret**.

The `publish.yml` workflow reads `${{ secrets.NUGET_API_KEY }}` automatically.

---

## Release Checklist (every release)

Follow these steps in order. The automated workflow does the actual push —
your job is to prepare the repository correctly.

```
Step  What to do
────  ──────────────────────────────────────────────────────────────────
 1    Bump <Version> in src/TerraPDF/TerraPDF.csproj
      Example: 1.2.0  →  1.3.0

 2    Update CHANGELOG.md
      - Rename [Unreleased] to [1.3.0] - YYYY-MM-DD
      - Add a fresh empty [Unreleased] section at the top
      - Update the comparison links at the bottom

 3    Commit and push to main
      git add .
      git commit -m "chore: release v1.3.0"
      git push origin main

 4    Wait for CI to go green (build-and-test + pack dry-run must pass)

 5    Create a GitHub Release
      - GitHub → Releases → "Draft a new release"
      - Tag: v1.3.0   (must match <Version> exactly, with "v" prefix)
      - Title: TerraPDF v1.3.0
      - Release notes: paste the [1.3.0] section from CHANGELOG.md
      - Click "Publish release"

 6    The "Publish NuGet" workflow triggers automatically.
      Monitor it in GitHub → Actions → "Publish NuGet".
      It will:
        a. Build and test the solution
        b. Verify the Git tag matches the csproj version
        c. Pack  →  TerraPDF.1.3.0.nupkg + TerraPDF.1.3.0.snupkg
        d. Upload packages as a workflow artifact
        e. Push .nupkg to nuget.org
        f. Push .snupkg (symbols) to nuget.org

 7    Verify on nuget.org
      https://www.nuget.org/packages/TerraPDF
      The package usually appears within 1–5 minutes.
      Full indexing (search) can take up to 15 minutes.
```

---

## Verify the Package Locally Before Releasing

Run these commands from the repository root to test pack and inspect output:

```sh
# Build Release
dotnet build src/TerraPDF/TerraPDF.csproj -c Release

# Pack to ./artifacts
dotnet pack src/TerraPDF/TerraPDF.csproj --no-build -c Release -o ./artifacts

# Inspect contents (rename to .zip on Windows to open in Explorer)
#   Linux/macOS:
unzip -l artifacts/TerraPDF.*.nupkg

# Test-install from a local feed
dotnet nuget add source ./artifacts --name local-terra
dotnet add <your-test-project>.csproj package TerraPDF
dotnet nuget remove source local-terra
```

---

## Package Contents Reference

| Path in .nupkg | Description |
|----------------|-------------|
| `README.md` | Displayed on the nuget.org package page |
| `TerraPDF.nuspec` | Generated package manifest |
| `lib/net8.0/TerraPDF.dll` | Library assembly for .NET 8 |
| `lib/net8.0/TerraPDF.xml` | IntelliSense XML doc comments |
| `lib/net9.0/TerraPDF.dll` | Library assembly for .NET 9 |
| `lib/net9.0/TerraPDF.xml` | IntelliSense XML doc comments |

The `.snupkg` symbol package contains PDBs with Source Link data pointing to
the exact commit on GitHub, enabling debugger step-in for consumers.

---

## Key Properties in `TerraPDF.csproj`

| Property | Purpose |
|----------|---------|
| `<PackageId>` | nuget.org package identifier |
| `<Version>` | Semantic version — must match the Git tag |
| `<PackageReadmeFile>` | Bundles `README.md` into the package |
| `<PackageLicenseExpression>MIT` | SPDX licence, shown on nuget.org |
| `<PackageProjectUrl>` | GitHub link shown on nuget.org |
| `<PackageReleaseNotes>` | URL to CHANGELOG.md |
| `<IncludeSymbols>` / `<SymbolPackageFormat>snupkg` | Produces the `.snupkg` |
| `<Deterministic>` | Reproducible byte-identical builds |
| `<ContinuousIntegrationBuild>` | Activates deterministic mode on GitHub Actions |
| `Microsoft.SourceLink.GitHub` | Maps PDB paths to GitHub source lines |

---

## Renewing the API Key

nuget.org API keys expire. Before expiry:

1. Sign in to nuget.org → **API Keys** → **Edit** → **Regenerate**.
2. Copy the new key.
3. Update the `NUGET_API_KEY` GitHub secret (Settings → Secrets → Actions).

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Workflow fails: "tag does not match csproj version" | Ensure the GitHub Release tag (e.g. `v1.3.0`) matches `<Version>1.3.0</Version>` in the csproj exactly. |
| `403 Forbidden` from nuget.org | API key expired or has insufficient scope. Regenerate and update the secret. |
| Package visible but README is blank on nuget.org | Verify `README.md` is included via `<PackageReadmeFile>README.md</PackageReadmeFile>` and the `<None Include=... Pack="true">` item. |
| Source Link warning locally | Expected when building outside a git repo. The warning disappears on GitHub Actions where `fetch-depth: 0` is used. |
| Old `.nupkg` in `artifacts/` | The folder is gitignored. Delete it with `Remove-Item artifacts/ -Recurse` before a fresh pack. |
