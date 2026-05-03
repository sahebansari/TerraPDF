using TerraPDF.Helpers;

namespace TerraPDF.Elements;

/// <summary>Represents a section heading that can be included in a generated Table of Contents.</summary>
internal sealed class HeadingElement : Element
{
    public int Level { get; }
    public string Title { get; }
    internal TextBlock TextBlock { get; }

    public HeadingElement(int level, string title)
    {
        Level = level;
        Title = title;
        TextBlock = new TextBlock(title);
    }

    internal override ElementSize Measure(double w, double h, TextStyle? defaultStyle = null) =>
        TextBlock.Measure(w, h, defaultStyle);

    internal override void Draw(DrawingContext ctx)
    {
        // Record heading if a recorder is provided
        ctx.HeadingRecorder?.Invoke(this, ctx.PageNumber, ctx.Y);
        TextBlock.Draw(ctx);
    }
}
