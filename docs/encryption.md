# Encryption & Password Protection

TerraPDF encrypts documents with **AES-256** using the PDF Standard Security
Handler **Revision 6** (ISO 32000-2 / PDF 2.0) by default — SHA-2 based key
derivation with no MD5 or RC4 in the chain. Encrypted documents are opened by
every major PDF viewer — Adobe Acrobat 9+ (2008), Chrome, Edge, Firefox,
Preview, Foxit, Okular, and others.

For documents that must open in very old viewers, legacy **AES-128
(Revision 4)** remains available via
`Algorithm = EncryptionAlgorithm.Aes128`.

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
| `Algorithm` | `EncryptionAlgorithm` | `Aes256` | `Aes256` = Revision 6 (modern, PDF 2.0, default). `Aes128` = Revision 4 for viewers released before ~2008. |

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

### AES-256 — Revision 6 (default)

| Property | Value |
|----------|-------|
| Security Handler | PDF Standard Security Handler |
| Revision | 6 (ISO 32000-2 §7.6.4 / PDF 2.0) |
| Content cipher | AES-256 CBC, file encryption key used directly (V5 has no per-object keys) |
| IV | 16-byte random (per string/stream) |
| Padding | PKCS#7 to 16-byte boundary |
| Key derivation | SHA-256/384/512 iterated hash (algorithm 2.B) with random salts |
| O / U entries | 48-byte verifiers (algorithms 8 & 9) |
| OE / UE entries | File key wrapped with AES-256 under password-derived intermediate keys |
| Perms entry | Permission bits encrypted with the file key (algorithm 10) |
| Passwords | UTF-8, up to 127 bytes |
| PDF version | 2.0 (set automatically) |

### AES-128 — Revision 4 (legacy opt-in)

| Property | Value |
|----------|-------|
| Revision | 4 (PDF 1.6 / §7.6.5) |
| Content cipher | AES-128 CBC with per-object keys (Algorithm 1 — FEK + obj/gen bytes + `sAlT`) |
| Key derivation | MD5 + 50 rounds (PDF §7.6.3.3 Algorithm 2) |
| O entry | Algorithm 3 — MD5 key + RC4 × 20 |
| U entry | Algorithm 5 (Rev 4) — MD5 + RC4 × 20 |
| PDF version | 1.6 (set automatically) |

> **Note on MD5/RC4:** used exclusively in the Revision 4 key-derivation steps
> mandated by the PDF specification — never for content encryption, and not at
> all in the Revision 6 default.

### What gets encrypted

- **Content streams** — the PDF drawing operators for every page
- **Image XObjects** — PNG and JPEG pixel data (and alpha soft masks)
- **Strings** — document metadata, bookmark titles, hyperlink URIs

### What is NOT encrypted (per PDF specification)

- The `/Encrypt` dictionary itself (§7.6.1)
- Cross-reference tables and trailer
- Stream lengths
- The `%PDF-` header

### Zero-dependency

The implementation uses only `System.Security.Cryptography`
(`Aes`, `SHA256`/`SHA384`/`SHA512`, `MD5` for Rev 4 only, and
`RandomNumberGenerator`).

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
