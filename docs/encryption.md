# Encryption & Password Protection

TerraPDF supports **AES-128 PDF encryption** using the PDF Standard Security Handler
(Revision 4).  Encrypted documents are opened by every major PDF viewer — Adobe Acrobat,
Chrome, Edge, Firefox, Preview, Foxit, Okular, and others.

No external packages are required.  The entire implementation uses
`System.Security.Cryptography` only.

---

## Quick start

```csharp
Document.Create(container =>
{
    container.Encrypt(new EncryptionOptions
    {
        UserPassword  = "open123",    // required to open the document
        OwnerPassword = "admin456",   // grants full access regardless of permissions
        Permissions   = PdfPermissions.Print | PdfPermissions.CopyText,
    });

    container.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(2, Unit.Centimetre);
        page.Content().Text("This PDF is password-protected.").Bold().FontSize(18);
    });
})
.PublishPdf("protected.pdf");
```

---

## `EncryptionOptions` properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UserPassword` | `string?` | `null` | Password required to *open* the document. `null` or empty = no open password (but content is still encrypted and permissions still enforced). |
| `OwnerPassword` | `string?` | `null` | Password granting *full* access, bypassing all `Permissions` restrictions. When `null` a random value is used so the encryption dictionary is always valid. |
| `Permissions` | `PdfPermissions` | `All` | Bitwise combination of permission flags that apply when the user opens with the `UserPassword`. |

---

## `PdfPermissions` flags

Combine flags with the bitwise-OR operator:

```csharp
PdfPermissions.Print | PdfPermissions.CopyText
```

| Flag | Description |
|------|-------------|
| `PdfPermissions.Print` | High-quality printing |
| `PdfPermissions.PrintLowResolution` | Degraded (low-resolution) printing only |
| `PdfPermissions.ModifyContents` | Modify document contents |
| `PdfPermissions.CopyText` | Copy or extract text and graphics |
| `PdfPermissions.ModifyAnnotations` | Add or modify annotations and form fields |
| `PdfPermissions.FillForms` | Fill in interactive form fields |
| `PdfPermissions.ExtractForAccessibility` | Text extraction for screen readers |
| `PdfPermissions.AssembleDocument` | Insert, rotate, or delete pages |
| `PdfPermissions.All` | All permissions granted (default) |
| `PdfPermissions.None` | No permissions — view only |

---

## Common patterns

### View-only — no printing or copying

```csharp
container.Encrypt(new EncryptionOptions
{
    UserPassword = "readonly",
    Permissions  = PdfPermissions.None,
});
```

### Open without a password, but restrict printing

Leaving `UserPassword` empty means the viewer opens the document without prompting,
while still enforcing the permission flags:

```csharp
container.Encrypt(new EncryptionOptions
{
    OwnerPassword = "admin",
    Permissions   = PdfPermissions.None,   // viewer cannot print or copy
});
```

### Owner password only — full restriction for all users

```csharp
container.Encrypt(new EncryptionOptions
{
    UserPassword  = "userpass",
    OwnerPassword = "ownerpass",
    Permissions   = PdfPermissions.Print | PdfPermissions.ExtractForAccessibility,
});
```

### Encrypt everything, allow all permissions

This encrypts content (making copy-paste from outside viewers harder) while
allowing full interactive use inside the viewer:

```csharp
container.Encrypt(new EncryptionOptions
{
    UserPassword = "open",
    Permissions  = PdfPermissions.All,
});
```

---

## Technical details

### Algorithm

| Property | Value |
|----------|-------|
| Security Handler | PDF Standard Security Handler |
| Revision | 4 (PDF 1.6 / §7.6.5) |
| Content cipher | AES-128 CBC |
| IV | 16-byte random (per object) |
| Padding | Zero-padding to 16-byte boundary (per PDF AES spec) |
| Key derivation | MD5 + 50 rounds (PDF §7.6.3.3 Algorithm 2) |
| O entry | Algorithm 3 — MD5 key + RC4 × 20 |
| U entry | Algorithm 5 (Rev 4) — MD5 + RC4 × 20 |
| Per-object key | Algorithm 1 — FEK + obj/gen bytes + `sAlT` suffix |
| Minimum PDF version | 1.6 (set automatically when encryption is active) |

### What gets encrypted

- **Content streams** — the PDF drawing operators for every page
- **Image XObjects** — PNG and JPEG pixel data
- All encrypted objects use a unique per-object AES-128 key derived from the
  file encryption key, the object number, and the generation number

### What is NOT encrypted (per PDF specification)

- The `/Encrypt` dictionary itself (§7.6.1)
- Cross-reference tables and trailer
- Stream lengths
- The `%PDF-` header

### Zero-dependency

The implementation uses only:
- `System.Security.Cryptography.Aes` — AES-128 CBC encryption
- `System.Security.Cryptography.MD5` — PDF-mandated key derivation (§7.6.3.3)
- `System.Security.Cryptography.RandomNumberGenerator` — IV generation

> **Note on MD5:** MD5 is used exclusively for the PDF key-derivation steps that are
> mandated by the PDF specification (§7.6.2 / §7.6.3).  It is *not* used for content
> encryption — all page content is encrypted with AES-128 CBC.

---

## Calling `Encrypt` in your document

Call `container.Encrypt(options)` once inside the `Document.Create` callback.
It may be called before or after adding pages; the encryption is applied when
`PublishPdf()` is called.  Calling `Encrypt` a second time replaces the previous
settings.

```csharp
Document.Create(container =>
{
    // Encryption must be configured before PublishPdf() is called.
    // Position relative to Page() calls does not matter.
    container.Encrypt(new EncryptionOptions { UserPassword = "secret" });

    container.MetadataTitle("Confidential Report");

    container.Page(page => { /* … */ });
    container.Page(page => { /* … */ });
})
.PublishPdf("report.pdf");
```
