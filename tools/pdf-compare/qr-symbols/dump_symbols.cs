#:project ../../../src/TerraPDF/TerraPDF.csproj
#:property AssemblyName=TerraPDF.Benchmarks
#:property Optimize=true
using System.Security.Cryptography;
using System.Text;
using TerraPDF.Barcodes;
using TerraPDF.Barcodes.QrCode;

// Dumps a hash of the module matrix for many (text, level) pairs, covering versions 1-40.
var sb = new StringBuilder();
var versions = new HashSet<int>();
foreach (QrErrorCorrectionLevel level in Enum.GetValues<QrErrorCorrectionLevel>())
{
    for (int len = 1; ; len = len < 40 ? len + 1 : len * 21 / 20 + 1)
    {
        string text = string.Concat(Enumerable.Range(0, len).Select(i => (char)('!' + (i * 7 + len) % 90)));
        QrCode qr;
        try { qr = QrCodeGenerator.Generate(text, level); } catch (NotSupportedException) { break; }
        var bits = new byte[qr.Size * qr.Size];
        for (int r = 0; r < qr.Size; r++) for (int c = 0; c < qr.Size; c++) bits[r * qr.Size + c] = qr.Modules[r, c] ? (byte)1 : (byte)0;
        versions.Add((qr.Size - 17) / 4);
        sb.Append(level).Append(' ').Append(len).Append(' ').Append(qr.Size).Append(' ').AppendLine(Convert.ToHexString(SHA256.HashData(bits))[..16]);
    }
}
File.WriteAllText(args[0], sb.ToString());
Console.WriteLine($"cases: {sb.ToString().Split('\n').Length - 1}, versions covered: {versions.Count} (min {versions.Min()}, max {versions.Max()})");
