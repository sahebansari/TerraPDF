using System;
using System.Security.Cryptography;
using System.Text;
using TerraPDF.Core;

namespace TerraPDF.Drawing;

/// <summary>
/// PDF Standard Security Handler — Revision 4, AES-128 CBC.
/// Implements PDF 1.7 §7.6 / §7.6.5 using only <c>System.Security.Cryptography</c>.
///
/// Encryption model
/// ────────────────
///  • A 16-byte <em>file encryption key</em> (FEK) is derived from the user/owner
///    passwords via the MD5 key-derivation algorithm specified in PDF §7.6.3.3.
///  • The /O entry (owner-password verifier) is computed with PDF §7.6.3.4 algorithm 3.
///  • The /U entry (user-password verifier) is computed with PDF §7.6.3.5 algorithm 5 (Rev 4).
///  • Each object's content is encrypted with a distinct per-object AES-128 key
///    derived by extending the FEK with the 3-byte object number and 2-byte generation
///    number (PDF §7.6.2 algorithm 1, extended for AES by appending the 4 bytes sAlT).
///  • Every encrypted string and stream is prefixed with a 16-byte random IV
///    and padded to a 16-byte boundary (PKCS#7-equivalent zero-padding per PDF spec).
/// </summary>
internal sealed class PdfEncryption
{
    // ── PDF §7.6.3.3 — standard padding string (28 bytes) ────────────────────
    private static readonly byte[] _paddingString =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    // ── Public state used when building the /Encrypt dictionary ─────────────

    /// <summary>32-byte /O value.</summary>
    public byte[] OEntry { get; private set; } = [];

    /// <summary>32-byte /U value.</summary>
    public byte[] UEntry { get; private set; } = [];

    /// <summary>Permissions integer as it appears in the /P entry (32-bit signed).</summary>
    public int PEntry { get; private set; }

    // 16-byte file encryption key — kept private, used to derive per-object keys.
    private readonly byte[] _fek;

    // ── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Derives all key material from <paramref name="options"/> and prepares
    /// the encryption state ready to call <see cref="EncryptBytes"/>.
    /// </summary>
    /// <param name="options">Encryption settings.</param>
    /// <param name="fileId">
    /// The 16-byte document file identifier written to the PDF trailer /ID array
    /// (PDF §7.6.3.3 step c).  Must be exactly 16 bytes.  This value is included
    /// in the key-derivation hash so the viewer can reproduce the same key when
    /// it reads the /ID from the trailer.
    /// </param>
    public PdfEncryption(EncryptionOptions options, byte[] fileId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileId);
        if (fileId.Length != 16)
            throw new ArgumentException("fileId must be exactly 16 bytes.", nameof(fileId));

        byte[] userPwd  = PadPassword(options.UserPassword  ?? string.Empty);
        byte[] ownerPwd = PadPassword(options.OwnerPassword ?? string.Empty);

        // Permission bits as defined in PDF §7.6.3.2 Table 22.
        // Bits 1-2 are reserved (0), bits 3-12 are permission flags,
        // bits 13-32 are reserved and set to 1 (high bits always 1 in /P).
        int perms = BuildPermissionInt(options.Permissions);
        PEntry = perms;

        // /O entry  (algorithm 3)
        OEntry = ComputeOwnerEntry(userPwd, ownerPwd, 128);

        // File encryption key  (algorithm 2 for Rev 4) — uses the real fileId
        _fek = DeriveFileEncryptionKey(userPwd, OEntry, perms, 128, fileId);

        // /U entry  (algorithm 5, Rev 4) — uses the real fileId
        UEntry = ComputeUserEntry(_fek, fileId);
    }

    // ── Per-object encryption ─────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> for the PDF object identified by
    /// (<paramref name="objNum"/>, <paramref name="genNum"/>) using AES-128 CBC.
    ///
    /// The output is: 16-byte random IV || AES-128-CBC(plaintext, key, IV)
    /// where <c>key</c> is the per-object key derived from the file encryption key.
    /// The plaintext is zero-padded to the next 16-byte boundary (PDF §7.6.5).
    /// </summary>
    public byte[] EncryptBytes(byte[] plaintext, int objNum, int genNum)
    {
        // Derive per-object key (PDF §7.6.2 algorithm 1, AES extension)
        byte[] objKey = DeriveObjectKey(_fek, objNum, genNum);

        // Random 16-byte IV
        byte[] iv = RandomNumberGenerator.GetBytes(16);

        // PKCS#7 padding to 16-byte boundary (PDF §7.6.5 / RFC 2898)
        int padLen    = 16 - (plaintext.Length % 16);
        byte[] padded = new byte[plaintext.Length + padLen];
        plaintext.CopyTo(padded, 0);
        for (int i = plaintext.Length; i < padded.Length; i++)
            padded[i] = (byte)padLen;

        // AES-128 CBC encrypt
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.None; // we padded manually
        aes.Key     = objKey;
        aes.IV      = iv;

        byte[] ciphertext = aes.EncryptCbc(padded, iv, PaddingMode.None);

        // Output: IV || ciphertext
        byte[] result = new byte[16 + ciphertext.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, 16);
        return result;
    }

    /// <summary>
    /// Convenience overload: encrypts a UTF-8 string value.
    /// </summary>
    public byte[] EncryptString(string value, int objNum, int genNum)
        => EncryptBytes(Encoding.Latin1.GetBytes(value), objNum, genNum);

    // ── Key derivation ────────────────────────────────────────────────────────

    // MD5 is required by the PDF Standard Security Handler specification (§7.6.2 / §7.6.3).
    // The warning is suppressed here because there is no alternative mandated by the spec.
#pragma warning disable CA5351

    /// <summary>
    /// PDF §7.6.2 Algorithm 1 (extended for AES-128).
    /// Derives a per-object 16-byte key from the file encryption key.
    /// </summary>
    private static byte[] DeriveObjectKey(byte[] fek, int objNum, int genNum)
    {
        // Append 3 low bytes of object number, 2 low bytes of generation number,
        // then the 4 bytes "sAlT" as required for AES (PDF §7.6.5).
        byte[] input = new byte[fek.Length + 9];
        fek.CopyTo(input, 0);
        int offset = fek.Length;
        input[offset++] = (byte)( objNum        & 0xFF);
        input[offset++] = (byte)((objNum >>  8) & 0xFF);
        input[offset++] = (byte)((objNum >> 16) & 0xFF);
        input[offset++] = (byte)( genNum        & 0xFF);
        input[offset++] = (byte)((genNum >>  8) & 0xFF);
        // AES extension bytes
        input[offset++] = (byte)'s';
        input[offset++] = (byte)'A';
        input[offset++] = (byte)'l';
        input[offset++] = (byte)'T';

        byte[] digest = MD5.HashData(input);

        // Key length is min(fek.Length + 5, 16) → always 16 for 128-bit FEK
        int keyLen = Math.Min(fek.Length + 5, 16);
        byte[] key = new byte[keyLen];
        Array.Copy(digest, key, keyLen);
        return key;
    }

    /// <summary>
    /// PDF §7.6.3.3 Algorithm 2 (Revision 4 variant) — derives the file encryption key.
    /// Key length = 128 bits (16 bytes).
    /// </summary>
    private static byte[] DeriveFileEncryptionKey(
        byte[] paddedUserPwd, byte[] oEntry, int perms, int keyBits, byte[] fileId)
    {
        int keyBytes = keyBits / 8;

        // Step a: MD5 of (padded user password | O entry | P as 4 LE bytes | file-ID)
        int inputLen = 32 + 32 + 4 + 16;
        byte[] input = new byte[inputLen];
        int pos = 0;
        paddedUserPwd.CopyTo(input, pos); pos += 32;
        oEntry.CopyTo(input, pos);        pos += 32;
        input[pos++] = (byte)( perms        & 0xFF);
        input[pos++] = (byte)((perms >>  8) & 0xFF);
        input[pos++] = (byte)((perms >> 16) & 0xFF);
        input[pos++] = (byte)((perms >> 24) & 0xFF);
        fileId.CopyTo(input, pos);

        byte[] digest = MD5.HashData(input);

        // Steps b–d: 50 additional MD5 rounds for keyBytes > 5
        for (int i = 0; i < 50; i++)
            digest = MD5.HashData(digest[..keyBytes]);

        byte[] key = new byte[keyBytes];
        Array.Copy(digest, key, keyBytes);
        return key;
    }

    /// <summary>
    /// PDF §7.6.3.4 Algorithm 3 — computes the /O (owner password) entry.
    /// </summary>
    private static byte[] ComputeOwnerEntry(
        byte[] paddedUserPwd, byte[] paddedOwnerPwd, int keyBits)
    {
        int keyBytes = keyBits / 8;

        // Step a: MD5 of padded owner password
        byte[] digest = MD5.HashData(paddedOwnerPwd);

        // Step b: 50 additional MD5 rounds for keyBytes > 5
        for (int i = 0; i < 50; i++)
            digest = MD5.HashData(digest[..keyBytes]);

        byte[] key = digest[..keyBytes];

        // Step c: RC4-encrypt the padded user password using the derived key,
        // then encrypt the result 19 more times with a modified key for Rev 3+.
        byte[] result = Rc4(paddedUserPwd, key);
        for (int i = 1; i <= 19; i++)
        {
            byte[] modKey = new byte[keyBytes];
            for (int j = 0; j < keyBytes; j++)
                modKey[j] = (byte)(key[j] ^ i);
            result = Rc4(result, modKey);
        }

        return result; // 32 bytes
    }

    /// <summary>
    /// PDF §7.6.3.5 Algorithm 5 (Rev 4) — computes the /U (user password) entry.
    /// Returns 32 bytes; only the first 16 are significant for verification.
    /// </summary>
    private static byte[] ComputeUserEntry(byte[] fileEncryptionKey, byte[] fileId)
    {
        // Step a: MD5 of (standard padding string | first entry of file-ID array)
        byte[] input = new byte[32 + 16];
        _paddingString.CopyTo(input, 0);
        fileId.CopyTo(input, 32);
        byte[] digest = MD5.HashData(input);

        // Step b: RC4-encrypt the 16-byte result using the FEK
        byte[] result = Rc4(digest, fileEncryptionKey);

        // Steps c–d: encrypt result 19 more times with modified key
        int keyBytes = fileEncryptionKey.Length;
        for (int i = 1; i <= 19; i++)
        {
            byte[] modKey = new byte[keyBytes];
            for (int j = 0; j < keyBytes; j++)
                modKey[j] = (byte)(fileEncryptionKey[j] ^ i);
            result = Rc4(result, modKey);
        }

        // Append 16 arbitrary bytes to pad to 32 bytes (required by spec)
        byte[] uEntry = new byte[32];
        result.CopyTo(uEntry, 0);
        return uEntry;
    }

#pragma warning restore CA5351

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Pads or truncates a password to exactly 32 bytes using the PDF padding string
    /// (PDF §7.6.3.3 step a).
    /// </summary>
    private static byte[] PadPassword(string password)
    {
        byte[] raw = Encoding.Latin1.GetBytes(password);
        byte[] padded = new byte[32];
        int copyLen = Math.Min(raw.Length, 32);
        Array.Copy(raw, padded, copyLen);
        if (copyLen < 32)
            Array.Copy(_paddingString, 0, padded, copyLen, 32 - copyLen);
        return padded;
    }

    /// <summary>
    /// Converts <see cref="PdfPermissions"/> to the 32-bit integer required by
    /// the PDF /P entry (PDF §7.6.3.2 Table 22).
    ///
    /// Bit numbering is 1-based in the spec; bits 1-2 reserved = 0,
    /// bits 13-32 reserved = 1.  The value is interpreted as a signed 32-bit
    /// integer in the PDF file.
    /// </summary>
    private static int BuildPermissionInt(PdfPermissions perms)
    {
        // Start with all bits set (0xFFFFFFFF), then clear the restricted bits.
        uint p = 0xFFFFFFFC; // bits 1-2 always 0

        // Map PdfPermissions flags to PDF permission bit positions (1-based):
        //   Bit 3  = Print (high quality)       → 0x00000004
        //   Bit 4  = ModifyContents             → 0x00000008
        //   Bit 5  = CopyText                   → 0x00000010
        //   Bit 6  = ModifyAnnotations          → 0x00000020
        //   Bit 9  = FillForms                  → 0x00000100
        //   Bit 10 = ExtractForAccessibility    → 0x00000200
        //   Bit 11 = AssembleDocument           → 0x00000400
        //   Bit 12 = PrintLowResolution         → 0x00000800

        // Clear all permission bits first, then set those granted.
        const uint allPermBits = 0x00000004 | 0x00000008 | 0x00000010 | 0x00000020
                                | 0x00000100 | 0x00000200 | 0x00000400 | 0x00000800;
        p &= ~allPermBits;

        if (perms.HasFlag(PdfPermissions.Print))                 p |= 0x00000004;
        if (perms.HasFlag(PdfPermissions.ModifyContents))        p |= 0x00000008;
        if (perms.HasFlag(PdfPermissions.CopyText))              p |= 0x00000010;
        if (perms.HasFlag(PdfPermissions.ModifyAnnotations))     p |= 0x00000020;
        if (perms.HasFlag(PdfPermissions.FillForms))             p |= 0x00000100;
        if (perms.HasFlag(PdfPermissions.ExtractForAccessibility)) p |= 0x00000200;
        if (perms.HasFlag(PdfPermissions.AssembleDocument))      p |= 0x00000400;
        if (perms.HasFlag(PdfPermissions.PrintLowResolution))    p |= 0x00000800;

        return (int)p; // signed 32-bit integer for PDF /P entry
    }

    /// <summary>
    /// RC4 stream cipher — used only in the PDF key-derivation algorithms
    /// (O and U entry computation), <em>not</em> for content encryption.
    /// AES-128 CBC is used for all content.
    /// </summary>
    private static byte[] Rc4(byte[] data, byte[] key)
    {
        // KSA
        byte[] s = new byte[256];
        for (int i = 0; i < 256; i++) s[i] = (byte)i;
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        // PRGA
        byte[] output = new byte[data.Length];
        int a = 0, b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + 1) & 0xFF;
            b = (b + s[a]) & 0xFF;
            (s[a], s[b]) = (s[b], s[a]);
            output[i] = (byte)(data[i] ^ s[(s[a] + s[b]) & 0xFF]);
        }
        return output;
    }
}
