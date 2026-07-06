namespace TerraPDF.Barcodes;

/// <summary>
/// QR code error correction level (ISO/IEC 18004 §7.4). Higher levels can
/// recover from more physical damage or occlusion, at the cost of a denser
/// (larger-version) symbol for the same data.
/// </summary>
public enum QrErrorCorrectionLevel
{
    /// <summary>~7% of codewords can be restored.</summary>
    L,

    /// <summary>~15% of codewords can be restored. Good general-purpose default.</summary>
    M,

    /// <summary>~25% of codewords can be restored.</summary>
    Q,

    /// <summary>~30% of codewords can be restored.</summary>
    H,
}
