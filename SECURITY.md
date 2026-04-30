# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x (latest) | ✅ Active |
| < 1.0 | ❌ No longer supported |

We always recommend using the latest published version on
[NuGet](https://www.nuget.org/packages/TerraPDF).

---

## Scope

TerraPDF is a **PDF generation library**. The following are in scope for
security reports:

- Memory-safety issues in the PDF rendering or image-decoding code
  (e.g. buffer overflows, out-of-bounds reads from malformed PNG/JPEG input)
- Denial-of-service vectors triggered by crafted document inputs
  (e.g. infinite loops, excessive memory allocation)
- Path-traversal or arbitrary file access via the `Image()` API
- Any issue in the public API that could allow an attacker to influence
  PDF output in an unintended way when user-supplied data is processed

The following are **out of scope**:

- Vulnerabilities in the consuming application's use of the library
  (e.g. writing generated PDFs to a publicly accessible path)
- Issues in .NET itself (report those to the
  [dotnet/runtime security team](https://github.com/dotnet/runtime/security/policy))

---

## Reporting a Vulnerability

**Please do not file public GitHub Issues for security vulnerabilities.**

Report security issues by emailing:

> **security@terrapdf.example**

Include in your report:

1. A clear description of the vulnerability
2. Steps to reproduce (minimal code snippet or test case)
3. The potential impact (e.g. crash, data exposure, arbitrary code execution)
4. Your name / handle for acknowledgement (optional)

### Response timeline

| Stage | Target time |
|-------|------------|
| Acknowledgement | ≤ 2 business days |
| Initial assessment | ≤ 5 business days |
| Fix and patched release | ≤ 30 days for critical / high issues |
| Public disclosure | Coordinated with reporter after fix is released |

---

## Preferred Languages

We can communicate in **English**.

---

## Acknowledgements

We publicly credit reporters of confirmed vulnerabilities in the
[CHANGELOG](CHANGELOG.md) unless they prefer to remain anonymous.
