namespace TerraPDF.Sample.Samples;

using TerraPDF.Core;

internal static class ReportWithTocSample
{
    internal static void Generate(string path)
    {
        Document.Create(new ReportWithToc()).PublishPdf(path);
        Console.WriteLine($"  [9] TOC demo               -> {path}");
    }
}
