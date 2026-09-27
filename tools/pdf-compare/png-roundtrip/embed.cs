#:project ../../../src/TerraPDF/TerraPDF.csproj
using TerraPDF.Core;
using TerraPDF.Helpers;

string dir = args[0];
foreach (var name in new[] { "rgb", "indexed", "rgba", "graya" })
foreach (bool encrypt in new[] { false, true })
{
    byte[] png = File.ReadAllBytes(Path.Combine(dir, name + ".png"));
    Document.Create(doc =>
    {
        if (encrypt) doc.Encrypt(new EncryptionOptions { OwnerPassword = "owner" });
        doc.Page(p =>
        {
            p.Size(97, 61);
            p.Margin(0);
            p.Content().Image(png);
        });
    }).PublishPdf(Path.Combine(dir, $"{name}{(encrypt ? "-enc" : "")}.pdf"));
}
