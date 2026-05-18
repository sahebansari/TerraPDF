namespace TerraPDF.Core;

/// <summary>
/// Configuration for PDF encryption.  Pass an instance to
/// <c>container.Encrypt(options)</c> inside <c>Document.Create</c>.
/// </summary>
/// <remarks>
/// TerraPDF uses the <b>PDF Standard Security Handler revision 4 with AES-128</b>
/// (PDF 1.7 §7.6.5).  This is supported by every major PDF viewer
/// (Adobe Acrobat, Chrome, Edge, Firefox, Preview, Foxit, Okular, …).
///
/// <para>
/// <b>Passwords:</b> Either or both passwords may be set.
/// <list type="bullet">
///   <item>
///     <b>User password</b> — required to open the document in a viewer.
///     Leave <c>null</c> or empty to allow opening without a password (but still
///     encrypt the content so that copy/print restrictions are enforced).
///   </item>
///   <item>
///     <b>Owner password</b> — grants full access, bypassing all permission
///     restrictions.  Defaults to a random value when not set so that the
///     encryption dictionary is still valid.
///   </item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Document.Create(container =>
/// {
///     container.Encrypt(new EncryptionOptions
///     {
///         UserPassword  = "open123",
///         OwnerPassword = "admin456",
///         Permissions   = PdfPermissions.Print | PdfPermissions.CopyText,
///     });
///     container.Page(page => { /* … */ });
/// })
/// .PublishPdf("protected.pdf");
/// </code>
/// </example>
public sealed class EncryptionOptions
{
    /// <summary>
    /// Password required to <em>open</em> the document.
    /// <c>null</c> or empty string means the document opens without a password
    /// (encryption is still applied and permissions are still enforced).
    /// </summary>
    public string? UserPassword { get; set; }

    /// <summary>
    /// Password that grants <em>full</em> (unrestricted) access to the document,
    /// bypassing the <see cref="Permissions"/> restrictions.
    /// When <c>null</c> or empty a random 16-byte owner password is generated
    /// automatically so that the encryption dictionary is always valid.
    /// </summary>
    public string? OwnerPassword { get; set; }

    /// <summary>
    /// Bitwise combination of <see cref="PdfPermissions"/> flags that controls
    /// what a user who opens the document with the <see cref="UserPassword"/>
    /// may do.  Defaults to <see cref="PdfPermissions.All"/>.
    /// </summary>
    public PdfPermissions Permissions { get; set; } = PdfPermissions.All;
}
