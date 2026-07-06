namespace TerraPDF.Barcodes.QrCode;

/// <summary>A generated QR symbol: its dark/light module matrix and side length in modules.</summary>
internal readonly record struct QrCode(bool[,] Modules, int Size);

/// <summary>
/// Facade for QR code generation (ISO/IEC 18004): encodes text as UTF-8 bytes
/// in byte mode, picks the smallest version that fits at the requested error
/// correction level, and builds the final module matrix.
/// </summary>
internal static class QrCodeGenerator
{
    /// <exception cref="NotSupportedException">
    /// <paramref name="text"/> is too long to fit in a QR code (max version 40)
    /// at the requested <paramref name="level"/>.
    /// </exception>
    internal static QrCode Generate(string text, QrErrorCorrectionLevel level)
    {
        var data   = QrEncoder.Encode(text, level);
        var matrix = QrMatrixBuilder.Build(data.Version, data.Level, data.Codewords);
        return new QrCode(matrix, matrix.GetLength(0));
    }
}
