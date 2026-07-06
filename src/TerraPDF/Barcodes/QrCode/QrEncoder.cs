using System.Text;

namespace TerraPDF.Barcodes.QrCode;

/// <summary>
/// Builds the final interleaved data+error-correction codeword stream for a QR
/// symbol: byte-mode bitstream construction, version selection, block
/// splitting, and Reed-Solomon interleaving (ISO/IEC 18004 §7.4-7.6).
/// </summary>
internal static class QrEncoder
{
    internal readonly record struct QrData(int Version, QrErrorCorrectionLevel Level, byte[] Codewords);

    /// <exception cref="NotSupportedException">
    /// <paramref name="text"/> is too long to fit in a QR code (max version 40)
    /// at the requested <paramref name="level"/>.
    /// </exception>
    internal static QrData Encode(string text, QrErrorCorrectionLevel level)
    {
        byte[] data    = Encoding.UTF8.GetBytes(text);
        int    version = SelectVersion(data.Length, level);
        var    ecc     = QrTables.GetEccInfo(version, level);

        var bits = new BitWriter();
        bits.WriteBits(0b0100, 4); // byte-mode indicator
        bits.WriteBits(data.Length, QrTables.CountIndicatorBits(version));
        foreach (byte b in data) bits.WriteBits(b, 8);

        int capacityBits = ecc.TotalDataCodewords * 8;
        int terminatorBits = Math.Min(4, capacityBits - bits.BitCount);
        if (terminatorBits > 0) bits.WriteBits(0, terminatorBits);
        while (bits.BitCount % 8 != 0) bits.WriteBits(0, 1);

        bool writeEcPad = true; // alternates 0xEC / 0x11 pad codewords
        while (bits.BitCount < capacityBits)
        {
            bits.WriteBits(writeEcPad ? 0xEC : 0x11, 8);
            writeEcPad = !writeEcPad;
        }

        byte[] interleaved = InterleaveWithErrorCorrection(bits.ToBytes(), ecc);
        return new QrData(version, level, interleaved);
    }

    private static int SelectVersion(int dataByteLength, QrErrorCorrectionLevel level)
    {
        for (int version = 1; version <= 40; version++)
        {
            var ecc = QrTables.GetEccInfo(version, level);
            int requiredBits = 4 + QrTables.CountIndicatorBits(version) + dataByteLength * 8;
            if (requiredBits <= ecc.TotalDataCodewords * 8) return version;
        }
        throw new NotSupportedException(
            $"Data is too long to fit in a QR code at error correction level {level} " +
            "(exceeds the version 40 capacity).");
    }

    private static byte[] InterleaveWithErrorCorrection(byte[] dataCodewords, QrEccInfo ecc)
    {
        var blocks = new List<byte[]>();
        int offset = 0;
        for (int i = 0; i < ecc.Group1Blocks; i++, offset += ecc.Group1DataCodewords)
            blocks.Add(dataCodewords[offset..(offset + ecc.Group1DataCodewords)]);
        for (int i = 0; i < ecc.Group2Blocks; i++, offset += ecc.Group2DataCodewords)
            blocks.Add(dataCodewords[offset..(offset + ecc.Group2DataCodewords)]);

        var ecBlocks = new List<byte[]>(blocks.Count);
        foreach (byte[] block in blocks)
            ecBlocks.Add(GaloisField.ComputeEcCodewords(block, ecc.EcCodewordsPerBlock));

        var result = new List<byte>();

        int maxDataLen = 0;
        foreach (byte[] block in blocks) maxDataLen = Math.Max(maxDataLen, block.Length);
        for (int i = 0; i < maxDataLen; i++)
            foreach (byte[] block in blocks)
                if (i < block.Length) result.Add(block[i]);

        for (int i = 0; i < ecc.EcCodewordsPerBlock; i++)
            foreach (byte[] ecBlock in ecBlocks)
                result.Add(ecBlock[i]);

        return [.. result];
    }

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];
        internal int BitCount => _bits.Count;

        internal void WriteBits(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
                _bits.Add(((value >> i) & 1) != 0);
        }

        internal byte[] ToBytes()
        {
            var bytes = new byte[_bits.Count / 8];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = 0;
                for (int j = 0; j < 8; j++)
                    if (_bits[i * 8 + j]) b |= (byte)(1 << (7 - j));
                bytes[i] = b;
            }
            return bytes;
        }
    }
}
