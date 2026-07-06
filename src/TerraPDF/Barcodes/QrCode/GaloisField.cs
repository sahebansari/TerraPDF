namespace TerraPDF.Barcodes.QrCode;

/// <summary>
/// GF(256) arithmetic (primitive polynomial x^8+x^4+x^3+x^2+1 = 0x11D) and the
/// Reed-Solomon error-correction codeword computation used by QR codes
/// (ISO/IEC 18004 §7.5).
/// </summary>
internal static class GaloisField
{
    private const int Prime = 0x11D;

    private static readonly int[] ExpTable = new int[256];
    private static readonly int[] LogTable = new int[256];

    static GaloisField()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            ExpTable[i] = x;
            LogTable[x] = i;
            x <<= 1;
            if (x >= 256) x ^= Prime;
        }
    }

    internal static int Multiply(int a, int b)
    {
        if (a == 0 || b == 0) return 0;
        return ExpTable[(LogTable[a] + LogTable[b]) % 255];
    }

    /// <summary>Returns alpha^<paramref name="i"/> in GF(256). Exposed for Reed-Solomon syndrome verification in tests.</summary>
    internal static int Exp(int i) => ExpTable[i % 255];

    /// <summary>
    /// Computes the <paramref name="ecCount"/> Reed-Solomon error-correction
    /// codewords for a block of data codewords.
    /// </summary>
    internal static byte[] ComputeEcCodewords(byte[] data, int ecCount)
    {
        int[] generator = BuildGeneratorPolynomial(ecCount);

        // Polynomial long division of (data padded with ecCount zero coefficients)
        // by the generator; the final ecCount coefficients are the remainder.
        var remainder = new int[data.Length + ecCount];
        for (int i = 0; i < data.Length; i++) remainder[i] = data[i];

        for (int i = 0; i < data.Length; i++)
        {
            int coef = remainder[i];
            if (coef == 0) continue;
            for (int j = 0; j < generator.Length; j++)
                remainder[i + j] ^= Multiply(generator[j], coef);
        }

        var ec = new byte[ecCount];
        for (int i = 0; i < ecCount; i++)
            ec[i] = (byte)remainder[data.Length + i];
        return ec;
    }

    // Generator polynomial = product of (x + alpha^i) for i in [0, degree),
    // represented as coefficients from highest degree (index 0) to constant term.
    private static int[] BuildGeneratorPolynomial(int degree)
    {
        int[] generator = [1];
        for (int i = 0; i < degree; i++)
            generator = MultiplyPolynomials(generator, [1, ExpTable[i]]);
        return generator;
    }

    private static int[] MultiplyPolynomials(int[] a, int[] b)
    {
        var result = new int[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                result[i + j] ^= Multiply(a[i], b[j]);
        return result;
    }
}
