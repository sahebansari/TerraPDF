namespace TerraPDF.Core;

/// <summary>
/// Permission flags for an encrypted PDF document (PDF 1.7 §7.6.3.2, Table 22).
/// These flags control what a user who opens the document with the <em>user password</em>
/// (or with no password at all) is allowed to do. The owner password always grants
/// unrestricted access.
/// </summary>
/// <remarks>
/// Combine flags with the bitwise-OR operator:
/// <code>
/// PdfPermissions.Print | PdfPermissions.CopyText
/// </code>
/// Use <see cref="All"/> to grant every permission, or <see cref="None"/> to deny all.
/// </remarks>
[Flags]
public enum PdfPermissions
{
    /// <summary>No permissions — the user may only view the document.</summary>
    None = 0,

    /// <summary>
    /// Allow printing at the highest quality the device supports.
    /// When this flag is <em>not</em> set but <see cref="PrintLowResolution"/> is set,
    /// only degraded (low-resolution) printing is allowed.
    /// </summary>
    Print = 1 << 0,

    /// <summary>
    /// Allow low-resolution (degraded) printing.
    /// Ignored when <see cref="Print"/> is also set.
    /// </summary>
    PrintLowResolution = 1 << 1,

    /// <summary>Allow modifying the document (other than annotations and form fields).</summary>
    ModifyContents = 1 << 2,

    /// <summary>Allow copying or extracting text and graphics.</summary>
    CopyText = 1 << 3,

    /// <summary>Allow adding or modifying annotations and form fields.</summary>
    ModifyAnnotations = 1 << 4,

    /// <summary>Allow filling in form fields.</summary>
    FillForms = 1 << 5,

    /// <summary>Allow text extraction for accessibility (screen readers).</summary>
    ExtractForAccessibility = 1 << 6,

    /// <summary>Allow assembling the document (inserting, rotating, or deleting pages).</summary>
    AssembleDocument = 1 << 7,

    /// <summary>All permissions granted.</summary>
    All = Print | PrintLowResolution | ModifyContents | CopyText |
          ModifyAnnotations | FillForms | ExtractForAccessibility | AssembleDocument,
}
