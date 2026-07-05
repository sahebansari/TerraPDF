using System.IO.Compression;
using System.Text;

namespace TerraPDF.Tests;

/// <summary>
/// Helpers for asserting against generated PDF bytes now that content streams
/// are Flate-compressed.
/// </summary>
internal static class PdfTestUtils
{
    /// <summary>
    /// Returns the PDF as Latin-1 text with every <c>stream … endstream</c>
    /// section replaced by its zlib-inflated content when inflation succeeds.
    /// Streams that are not Flate data (encrypted streams, raw JPEG) are kept
    /// as-is, so assertions on dictionaries and uncompressed objects still work
    /// on the same string.
    /// </summary>
    internal static string InflatedText(byte[] pdf)
    {
        string raw = Encoding.Latin1.GetString(pdf);
        var sb = new StringBuilder(raw.Length * 2);

        int pos = 0;
        while (true)
        {
            int streamStart = raw.IndexOf("stream\n", pos, StringComparison.Ordinal);
            if (streamStart < 0)
            {
                sb.Append(raw, pos, raw.Length - pos);
                break;
            }

            int dataStart = streamStart + "stream\n".Length;
            int dataEnd   = raw.IndexOf("\nendstream", dataStart, StringComparison.Ordinal);
            if (dataEnd < 0)
            {
                sb.Append(raw, pos, raw.Length - pos);
                break;
            }

            sb.Append(raw, pos, dataStart - pos);

            byte[] streamBytes = Encoding.Latin1.GetBytes(raw[dataStart..dataEnd]);
            sb.Append(TryInflate(streamBytes, out string inflated)
                ? inflated
                : raw[dataStart..dataEnd]);

            sb.Append(raw, dataEnd, "\nendstream".Length);
            pos = dataEnd + "\nendstream".Length;
        }

        return sb.ToString();
    }

    private static bool TryInflate(byte[] data, out string text)
    {
        try
        {
            using var input  = new MemoryStream(data);
            using var zlib   = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            text = Encoding.Latin1.GetString(output.ToArray());
            return true;
        }
        catch (InvalidDataException)
        {
            text = string.Empty;
            return false;
        }
    }
}
