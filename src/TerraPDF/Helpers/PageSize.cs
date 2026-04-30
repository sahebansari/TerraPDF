namespace TerraPDF.Helpers;

/// <summary>Standard paper sizes as (width, height) in PDF points (1 pt = 1/72 inch).</summary>
public static class PageSize
{
    public static readonly (double Width, double Height) A0     = (2383.94, 3370.39);
    public static readonly (double Width, double Height) A1     = (1683.78, 2383.94);
    public static readonly (double Width, double Height) A2     = (1190.55, 1683.78);
    public static readonly (double Width, double Height) A3     = (841.89,  1190.55);
    public static readonly (double Width, double Height) A4     = (595.28,   841.89);
    public static readonly (double Width, double Height) A5     = (419.53,   595.28);
    public static readonly (double Width, double Height) A6     = (297.64,   419.53);
    public static readonly (double Width, double Height) Letter = (612.00,   792.00);
    public static readonly (double Width, double Height) Legal  = (612.00,  1008.00);
    public static readonly (double Width, double Height) Tabloid= (792.00,  1224.00);
    public static readonly (double Width, double Height) Executive = (521.86, 756.00);

    /// <summary>Returns the landscape orientation of a given size.</summary>
    public static (double Width, double Height) Landscape(
        (double Width, double Height) size) => (size.Height, size.Width);
}
