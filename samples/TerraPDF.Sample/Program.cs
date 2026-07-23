using TerraPDF.Sample.Samples;


//  TerraPDF Samples  -  PDFs from simple to complex.
//  Output directory: first command-line argument, or Desktop\SamplePDF by default.


string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
string sampleDir = args.Length > 0 ? args[0] : Path.Combine(desktop, "SamplePDF");
Directory.CreateDirectory(sampleDir);

string imgDir    = AppContext.BaseDirectory;          // images live next to the exe
string headerImg = Path.Combine(imgDir, "header_logo.png");
string smallImg  = Path.Combine(imgDir, "small_logo.jpg");

Console.WriteLine("TerraPDF Samples - generating ten PDFs to Desktop...");
Console.WriteLine();

SimpleReport.Generate(Path.Combine(sampleDir, "01_simple_report.pdf"));
StyledNewsletter.Generate(Path.Combine(sampleDir, "02_newsletter.pdf"));
Invoice.Generate(Path.Combine(sampleDir, "03_invoice.pdf"));
CompanyProfile.Generate(Path.Combine(sampleDir, "04_company_profile.pdf"), headerImg, smallImg);
EventBrochure.Generate(Path.Combine(sampleDir, "05_event_brochure.pdf"), headerImg, smallImg);
ProductCatalogue.Generate(Path.Combine(sampleDir, "06_product_catalogue.pdf"), headerImg, smallImg);
ProductCatalogCover.Generate(Path.Combine(sampleDir, "07_product_catalog_cover.pdf"), smallImg);
ReportWithBookmarks.Generate(Path.Combine(sampleDir, "08_report_with_bookmarks.pdf"));
ReportWithTocSample.Generate(Path.Combine(sampleDir, "09_report_with_toc.pdf"));
VectorGraphicsShowcase.Generate(Path.Combine(sampleDir, "10_vector_graphics_showcase.pdf"));
UnicodeShowcase.Generate(Path.Combine(sampleDir, "11_unicode_showcase.pdf"));
EncryptionShowcase.Generate(Path.Combine(sampleDir, "12_encryption_showcase.pdf"));
BarcodesAndQrShowcase.Generate(Path.Combine(sampleDir, "13_barcodes_and_qr_showcase.pdf"));
CustomFontShowcase.Generate(Path.Combine(sampleDir, "14_custom_font_showcase.pdf"),
    Path.Combine(imgDir, "Fonts", "Lato-Regular.ttf"), Path.Combine(imgDir, "Fonts", "Lato-Bold.ttf"));
ChildNutritionIndiaReport.Generate(Path.Combine(sampleDir, "15_child_nutrition_india_report.pdf"),
    Path.Combine(imgDir, "Fonts", "NotoSansDevanagari-Regular.ttf"), Path.Combine(imgDir, "Fonts", "NotoSansDevanagari-Bold.ttf"));

Console.WriteLine();
Console.WriteLine("Done.");
