using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TerraPDF.Core;
using TerraPDF.Drawing;
using TerraPDF.Helpers;
using TerraPDF.Infra;
using Xunit;

namespace TerraPDF.Tests;

/// <summary>
/// Tests for AES-256 encryption (Standard Security Handler Revision 6).
/// Includes a full reader-side round trip: password validation via the 2.B
/// hash, file-encryption-key recovery from /UE and /OE, /Perms decryption,
/// and content-stream decryption — exercising salts, IV framing, padding,
/// and the iterated hash end-to-end.
/// </summary>
public sealed class Aes256EncryptionTests
{
    private static byte[] Build(Action<IDocumentContainer> compose) =>
        Document.Create(compose).PublishPdf();

    private static string Raw(byte[] b) => Encoding.Latin1.GetString(b);

    private static byte[] SecretDoc(string userPwd, string ownerPwd = "owner-secret") =>
        Build(doc =>
        {
            doc.Encrypt(new EncryptionOptions
            {
                UserPassword  = userPwd,
                OwnerPassword = ownerPwd,
                Permissions   = PdfPermissions.Print | PdfPermissions.CopyText,
            });
            doc.MetadataTitle("Rev6SecretTitle");
            doc.Page(p =>
            {
                p.Size(PageSize.A4);
                p.Content().Text("Rev6SecretBody");
            });
        });

    private static byte[] HexEntry(string pdf, string name, int expectedBytes)
    {
        var m = Regex.Match(pdf, $@"/{name} <([0-9A-Fa-f]+)>");
        Assert.True(m.Success, $"/{name} entry not found");
        byte[] bytes = Convert.FromHexString(m.Groups[1].Value);
        Assert.Equal(expectedBytes, bytes.Length);
        return bytes;
    }

    // ── Dictionary structure ──────────────────────────────────────────────────

    [Fact]
    public void Rev6DictionaryHasAllRequiredEntries()
    {
        string pdf = Raw(SecretDoc("user-pwd"));

        Assert.Contains("/V 5", pdf);
        Assert.Contains("/R 6", pdf);
        Assert.Contains("/Length 256", pdf);
        Assert.Contains("/CFM /AESV3", pdf);
        Assert.Contains("/Length 32", pdf);

        HexEntry(pdf, "O",     48);
        HexEntry(pdf, "U",     48);
        HexEntry(pdf, "OE",    32);
        HexEntry(pdf, "UE",    32);
        HexEntry(pdf, "Perms", 16);
    }

    [Fact]
    public void Rev6DocumentsUsePdf20Header()
    {
        byte[] bytes = SecretDoc("u");
        Assert.Equal("%PDF-2.0", Encoding.ASCII.GetString(bytes, 0, 8));
    }

    [Fact]
    public void Rev6EntriesDifferBetweenRuns()
    {
        // Random FEK and salts: identical inputs must still produce different
        // key material each time.
        string a = Raw(SecretDoc("same"));
        string b = Raw(SecretDoc("same"));
        Assert.NotEqual(
            Convert.ToHexString(HexEntry(a, "U", 48)),
            Convert.ToHexString(HexEntry(b, "U", 48)));
    }

    [Fact]
    public void Rev6DoesNotLeakPlaintext()
    {
        string pdf = Raw(SecretDoc("user-pwd"));
        Assert.DoesNotContain("Rev6SecretTitle", pdf);
        Assert.DoesNotContain("Rev6SecretBody", pdf);
    }

    // ── Reader-side round trip ────────────────────────────────────────────────

    [Fact]
    public void UserPasswordValidatesAndRecoversFileKeyAndContent()
    {
        const string userPwd = "correct horse";
        byte[] bytes = SecretDoc(userPwd);
        string pdf   = Raw(bytes);

        byte[] u  = HexEntry(pdf, "U", 48);
        byte[] ue = HexEntry(pdf, "UE", 32);
        byte[] pwd = Encoding.UTF8.GetBytes(userPwd);

        // Algorithm 11: hash(password, validation salt) must equal U[0..32].
        byte[] validationSalt = u[32..40];
        byte[] keySalt        = u[40..48];
        Assert.Equal(u[..32], PdfEncryption.HashRev6(pwd, validationSalt, []));

        // Wrong password must NOT validate.
        Assert.NotEqual(u[..32], PdfEncryption.HashRev6(Encoding.UTF8.GetBytes("wrong"), validationSalt, []));

        // Recover the file encryption key from /UE.
        byte[] intermediate = PdfEncryption.HashRev6(pwd, keySalt, []);
        byte[] fek = PdfEncryption.AesCbcNoPad(intermediate, new byte[16], ue, encrypt: false);
        Assert.Equal(32, fek.Length);

        // /Perms decrypts with the FEK to the P value + 'T' + "adb" block.
        byte[] perms = HexEntry(pdf, "Perms", 16);
        using var aes = Aes.Create();
        aes.Key = fek;
        byte[] permsPlain = aes.DecryptEcb(perms, PaddingMode.None);
        Assert.Equal((byte)'a', permsPlain[9]);
        Assert.Equal((byte)'d', permsPlain[10]);
        Assert.Equal((byte)'b', permsPlain[11]);
        Assert.Equal((byte)'T', permsPlain[8]);

        var pMatch = Regex.Match(pdf, @"/P (-?\d+)");
        Assert.True(pMatch.Success);
        int p = int.Parse(pMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        int pFromPerms = permsPlain[0] | (permsPlain[1] << 8) | (permsPlain[2] << 16) | (permsPlain[3] << 24);
        Assert.Equal(p, pFromPerms);

        // Decrypt + inflate the first content stream and find the body text.
        var streamMatch = Regex.Match(pdf, @"/Filter /FlateDecode >>\nstream\n", RegexOptions.Singleline);
        Assert.True(streamMatch.Success, "No FlateDecode stream found");
        int dataStart = streamMatch.Index + streamMatch.Length;
        int dataEnd   = pdf.IndexOf("\nendstream", dataStart, StringComparison.Ordinal);
        byte[] encrypted = Encoding.Latin1.GetBytes(pdf[dataStart..dataEnd]);

        byte[] iv     = encrypted[..16];
        byte[] cipher = encrypted[16..];
        byte[] padded = PdfEncryption.AesCbcNoPad(fek, iv, cipher, encrypt: false);
        byte[] deflated = padded[..^padded[^1]];   // strip PKCS#7 padding

        using var input  = new MemoryStream(deflated);
        using var zlib   = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        string content = Encoding.Latin1.GetString(output.ToArray());

        Assert.Contains("Rev6SecretBody", content);
    }

    [Fact]
    public void OwnerPasswordValidatesAndRecoversTheSameFileKey()
    {
        const string userPwd  = "user-pw";
        const string ownerPwd = "owner-pw";
        byte[] bytes = SecretDoc(userPwd, ownerPwd);
        string pdf   = Raw(bytes);

        byte[] u  = HexEntry(pdf, "U", 48);
        byte[] o  = HexEntry(pdf, "O", 48);
        byte[] oe = HexEntry(pdf, "OE", 32);
        byte[] ue = HexEntry(pdf, "UE", 32);
        byte[] opwd = Encoding.UTF8.GetBytes(ownerPwd);

        // Algorithm 12: owner hash includes the full 48-byte /U.
        Assert.Equal(o[..32], PdfEncryption.HashRev6(opwd, o[32..40], u));

        // FEK recovered via the owner route must equal the user-route FEK.
        byte[] ownerKey = PdfEncryption.HashRev6(opwd, o[40..48], u);
        byte[] fekViaOwner = PdfEncryption.AesCbcNoPad(ownerKey, new byte[16], oe, encrypt: false);

        byte[] upwd = Encoding.UTF8.GetBytes(userPwd);
        byte[] userKey = PdfEncryption.HashRev6(upwd, u[40..48], []);
        byte[] fekViaUser = PdfEncryption.AesCbcNoPad(userKey, new byte[16], ue, encrypt: false);

        Assert.Equal(Convert.ToHexString(fekViaUser), Convert.ToHexString(fekViaOwner));
    }
}
