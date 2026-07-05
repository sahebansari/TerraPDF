using System;
using System.Security.Cryptography;
using System.Text;
using TerraPDF.Core;

namespace TerraPDF.Drawing;

/// <summary>
/// PDF Standard Security Handler — Revision 6 (AES-256, the default) and
/// Revision 4 (AES-128, legacy opt-in), implemented using only
/// <c>System.Security.Cryptography</c>.
///
/// Revision 6 (ISO 32000-2 §7.6.4, /V 5 /R 6, /AESV3)
/// ───────────────────────────────────────────────────
///  • A random 32-byte <em>file encryption key</em> (FEK) encrypts all strings
///    and streams directly — V5 has no per-object key derivation.
///  • /U and /O are 48-byte verifiers built from the SHA-2 based iterated hash
///    (algorithm 2.B) over the password plus random validation/key salts.
///  • /UE and /OE hold the FEK wrapped with AES-256-CBC under intermediate keys
///    derived from the respective password and key salt.
///  • /Perms holds the permission bits encrypted with the FEK (algorithm 10).
///  • Passwords are UTF-8, truncated to 127 bytes (SASLprep is not applied —
///    the common practical simplification; pure-ASCII passwords are unaffected).
///
/// Revision 4 (PDF 1.7 §7.6, /V 4 /R 4, /AESV2)
/// ────────────────────────────────────────────
///  • A 16-byte FEK is derived from the user/owner passwords via the MD5
///    key-derivation algorithm (§7.6.3.3); /O and /U use algorithms 3 and 5.
///  • Each object's content is encrypted with a per-object AES-128 key derived
///    by extending the FEK with the object/generation numbers plus "sAlT".
///
/// In both revisions every encrypted string and stream is emitted as a random
/// 16-byte IV followed by the AES-CBC ciphertext with PKCS#7 padding.
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

    /// <summary>/O value: 32 bytes for Revision 4, 48 bytes for Revision 6.</summary>
    public byte[] OEntry { get; private set; } = [];

    /// <summary>/U value: 32 bytes for Revision 4, 48 bytes for Revision 6.</summary>
    public byte[] UEntry { get; private set; } = [];

    /// <summary>32-byte /OE value (Revision 6 only).</summary>
    public byte[] OEEntry { get; private set; } = [];

    /// <summary>32-byte /UE value (Revision 6 only).</summary>
    public byte[] UEEntry { get; private set; } = [];

    /// <summary>16-byte /Perms value (Revision 6 only).</summary>
    public byte[] PermsEntry { get; private set; } = [];

    /// <summary>Permissions integer as it appears in the /P entry (32-bit signed).</summary>
    public int PEntry { get; private set; }

    /// <summary>True for AES-256 / Revision 6; false for AES-128 / Revision 4.</summary>
    public bool IsAes256 { get; }

    // File encryption key: 32 bytes (used directly) for Revision 6,
    // 16 bytes (per-object keys derived from it) for Revision 4.
    private readonly byte[] _fek;

    // ── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Derives all key material from <paramref name="options"/> and prepares
    /// the encryption state ready to call <see cref="EncryptBytes"/>.
    /// </summary>
    /// <param name="options">Encryption settings.</param>
    /// <param name="fileId">
    /// The 16-byte document file identifier written to the PDF trailer /ID array.
    /// Must be exactly 16 bytes.  Revision 4 includes it in the key-derivation
    /// hash (PDF §7.6.3.3 step c); Revision 6 does not use it for key derivation
    /// but the trailer /ID is still required for encrypted files.
    /// </param>
    public PdfEncryption(EncryptionOptions options, byte[] fileId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileId);
        if (fileId.Length != 16)
            throw new ArgumentException("fileId must be exactly 16 bytes.", nameof(fileId));

        // Permission bits as defined in PDF §7.6.3.2 Table 22.
        // Bits 1-2 are reserved (0), bits 3-12 are permission flags,
        // bits 13-32 are reserved and set to 1 (high bits always 1 in /P).
        int perms = BuildPermissionInt(options.Permissions);
        PEntry = perms;

        if (options.Algorithm == EncryptionAlgorithm.Aes256)
        {
            IsAes256 = true;

            // An unset owner password defaults to a random value so the
            // dictionary is valid without granting owner access to everyone.
            string ownerPassword = string.IsNullOrEmpty(options.OwnerPassword)
                ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
                : options.OwnerPassword;

            (_fek, UEntry, UEEntry, OEntry, OEEntry, PermsEntry) =
                ComputeRev6(options.UserPassword ?? string.Empty, ownerPassword, perms);
        }
        else
        {
            byte[] userPwd  = PadPassword(options.UserPassword  ?? string.Empty);
            byte[] ownerPwd = PadPassword(options.OwnerPassword ?? string.Empty);

            // /O entry  (algorithm 3)
            OEntry = ComputeOwnerEntry(userPwd, ownerPwd, 128);

            // File encryption key  (algorithm 2 for Rev 4) — uses the real fileId
            _fek = DeriveFileEncryptionKey(userPwd, OEntry, perms, 128, fileId);

            // /U entry  (algorithm 5, Rev 4) — uses the real fileId
            UEntry = ComputeUserEntry(_fek, fileId);
        }
    }

    // ── Per-object encryption ─────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> for the PDF object identified by
    /// (<paramref name="objNum"/>, <paramref name="genNum"/>) using AES CBC.
    ///
    /// The output is: 16-byte random IV || AES-CBC(plaintext, key, IV).
    /// Revision 4 derives a per-object AES-128 key from the file encryption key;
    /// Revision 6 uses the 32-byte file encryption key directly (V5 has no
    /// per-object key derivation).  The plaintext is PKCS#7-padded to the next
    /// 16-byte boundary (PDF §7.6.5).
    /// </summary>
    public byte[] EncryptBytes(byte[] plaintext, int objNum, int genNum)
    {
        byte[] key = IsAes256
            ? _fek                                    // Rev 6: FEK used directly
            : DeriveObjectKey(_fek, objNum, genNum);  // Rev 4: per-object key (§7.6.2 alg. 1)

        // Random 16-byte IV
        byte[] iv = RandomNumberGenerator.GetBytes(16);

        // PKCS#7 padding to 16-byte boundary (PDF §7.6.5 / RFC 2898)
        int padLen    = 16 - (plaintext.Length % 16);
        byte[] padded = new byte[plaintext.Length + padLen];
        plaintext.CopyTo(padded, 0);
        for (int i = plaintext.Length; i < padded.Length; i++)
            padded[i] = (byte)padLen;

        // AES CBC encrypt (key size follows the key: 16 → AES-128, 32 → AES-256)
        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.None; // we padded manually
        aes.Key     = key;
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

    // ── Revision 6 key derivation (ISO 32000-2 §7.6.4) ───────────────────────

    /// <summary>
    /// Computes all Revision 6 key material: the random file encryption key and
    /// the /U, /UE, /O, /OE, /Perms dictionary entries (algorithms 8, 9, 10).
    /// </summary>
    private static (byte[] Fek, byte[] U, byte[] Ue, byte[] O, byte[] Oe, byte[] Perms)
        ComputeRev6(string userPassword, string ownerPassword, int permissions)
    {
        byte[] userPwd  = Utf8Password(userPassword);
        byte[] ownerPwd = Utf8Password(ownerPassword);
        byte[] zeroIv   = new byte[16];

        // The file encryption key is simply 32 random bytes in Revision 6.
        byte[] fek = RandomNumberGenerator.GetBytes(32);

        // ── /U and /UE (algorithm 8) ─────────────────────────────────────────
        byte[] uValidationSalt = RandomNumberGenerator.GetBytes(8);
        byte[] uKeySalt        = RandomNumberGenerator.GetBytes(8);

        byte[] u = new byte[48];
        HashRev6(userPwd, uValidationSalt, []).CopyTo(u, 0);
        uValidationSalt.CopyTo(u, 32);
        uKeySalt.CopyTo(u, 40);

        byte[] userIntermediateKey = HashRev6(userPwd, uKeySalt, []);
        byte[] ue = AesCbcNoPad(userIntermediateKey, zeroIv, fek, encrypt: true);

        // ── /O and /OE (algorithm 9) — hashes include the full 48-byte /U ────
        byte[] oValidationSalt = RandomNumberGenerator.GetBytes(8);
        byte[] oKeySalt        = RandomNumberGenerator.GetBytes(8);

        byte[] o = new byte[48];
        HashRev6(ownerPwd, oValidationSalt, u).CopyTo(o, 0);
        oValidationSalt.CopyTo(o, 32);
        oKeySalt.CopyTo(o, 40);

        byte[] ownerIntermediateKey = HashRev6(ownerPwd, oKeySalt, u);
        byte[] oe = AesCbcNoPad(ownerIntermediateKey, zeroIv, fek, encrypt: true);

        // ── /Perms (algorithm 10): P (4 LE bytes) ‖ FF FF FF FF ‖ 'T' ‖ "adb"
        //    ‖ 4 random bytes, AES-256-ECB encrypted with the FEK ─────────────
        byte[] permsBlock = new byte[16];
        permsBlock[0] = (byte)( permissions        & 0xFF);
        permsBlock[1] = (byte)((permissions >>  8) & 0xFF);
        permsBlock[2] = (byte)((permissions >> 16) & 0xFF);
        permsBlock[3] = (byte)((permissions >> 24) & 0xFF);
        permsBlock[4] = permsBlock[5] = permsBlock[6] = permsBlock[7] = 0xFF;
        permsBlock[8]  = (byte)'T';   // EncryptMetadata = true
        permsBlock[9]  = (byte)'a';
        permsBlock[10] = (byte)'d';
        permsBlock[11] = (byte)'b';
        RandomNumberGenerator.GetBytes(4).CopyTo(permsBlock, 12);
        byte[] perms = AesEcbNoPad(fek, permsBlock);

        return (fek, u, ue, o, oe, perms);
    }

    /// <summary>
    /// ISO 32000-2 algorithm 2.B — the Revision 6 iterated password hash.
    /// <paramref name="udata"/> is the full 48-byte /U entry when hashing the
    /// owner password, empty when hashing the user password.
    /// </summary>
    internal static byte[] HashRev6(byte[] password, byte[] salt, byte[] udata)
    {
        byte[] k = SHA256.HashData(Concat(password, salt, udata));
        byte[] e = [];

        // At least 64 rounds; afterwards continue while the last byte of E
        // exceeds (round − 32).  The SHA variant for the next round is chosen
        // by the first 16 bytes of E interpreted as a big-endian integer mod 3
        // — equivalent to the byte sum mod 3, since 256 ≡ 1 (mod 3).
        for (int round = 0; round < 64 || e[^1] > round - 32; round++)
        {
            byte[] block = Concat(password, k, udata);
            byte[] k1 = new byte[block.Length * 64];
            for (int i = 0; i < 64; i++)
                block.CopyTo(k1, i * block.Length);

            e = AesCbcNoPad(k[..16], k[16..32], k1, encrypt: true);

            int sum = 0;
            for (int i = 0; i < 16; i++)
                sum += e[i];

            k = (sum % 3) switch
            {
                0 => SHA256.HashData(e),
                1 => SHA384.HashData(e),
                _ => SHA512.HashData(e),
            };
        }

        return k[..32];
    }

    /// <summary>UTF-8 password bytes, truncated to 127 bytes (ISO 32000-2 §7.6.4.3.2).</summary>
    private static byte[] Utf8Password(string password)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length <= 127 ? bytes : bytes[..127];
    }

    internal static byte[] AesCbcNoPad(byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return encrypt
            ? aes.EncryptCbc(data, iv, PaddingMode.None)
            : aes.DecryptCbc(data, iv, PaddingMode.None);
    }

    private static byte[] AesEcbNoPad(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptEcb(data, PaddingMode.None);
    }

    private static byte[] Concat(byte[] a, byte[] b, byte[] c)
    {
        byte[] result = new byte[a.Length + b.Length + c.Length];
        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);
        c.CopyTo(result, a.Length + b.Length);
        return result;
    }

    // ── Revision 4 key derivation ────────────────────────────────────────────

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
