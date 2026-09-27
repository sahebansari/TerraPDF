#:project ../../../src/TerraPDF/TerraPDF.csproj
using TerraPDF.Core;
using TerraPDF.Helpers;
string chars = File.ReadAllText(args[0]) + "中Ж";   // plus two unmappable characters
Document.Create(doc => doc.Page(p =>
{
    p.Size(2400, 400);
    p.Margin(10);
    p.Content().Column(col =>
    {
        foreach (var family in new[] { "Helvetica", "Times", "Courier" })
            foreach (var (bold, italic) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                var t = col.Item().Text(chars).FontFamily(family).FontSize(10);
                if (bold) t.Bold();
                if (italic) t.Italic();
            }
    });
})).PublishPdf(args[1]);
