using TerraPDF.Sample.Samples;


//  TerraPDF Samples  -  PDFs from simple to complex, saved to Desktop.


string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
string imgDir    = AppContext.BaseDirectory;          // images live next to the exe
string headerImg = Path.Combine(imgDir, "header_logo.png");
string smallImg  = Path.Combine(imgDir, "small_logo.jpg");

Console.WriteLine("TerraPDF Samples - generating ten PDFs to Desktop...");
Console.WriteLine();

SimpleReport.Generate(Path.Combine(desktop, "SamplePDF", "01_simple_report.pdf"));
StyledNewsletter.Generate(Path.Combine(desktop, "SamplePDF", "02_newsletter.pdf"));
Invoice.Generate(Path.Combine(desktop, "SamplePDF", "03_invoice.pdf"));
CompanyProfile.Generate(Path.Combine(desktop, "SamplePDF", "04_company_profile.pdf"), headerImg, smallImg);
EventBrochure.Generate(Path.Combine(desktop, "SamplePDF", "05_event_brochure.pdf"), headerImg, smallImg);
ProductCatalogue.Generate(Path.Combine(desktop, "SamplePDF", "06_product_catalogue.pdf"), headerImg, smallImg);
ProductCatalogCover.Generate(Path.Combine(desktop, "SamplePDF", "07_product_catalog_cover.pdf"), smallImg);
ReportWithBookmarks.Generate(Path.Combine(desktop, "SamplePDF", "08_report_with_bookmarks.pdf"));
ReportWithTocSample.Generate(Path.Combine(desktop, "SamplePDF", "09_report_with_toc.pdf"));
VectorGraphicsShowcase.Generate(Path.Combine(desktop, "SamplePDF", "10_vector_graphics_showcase.pdf"));
UnicodeShowcase.Generate(Path.Combine(desktop, "SamplePDF", "11_unicode_showcase.pdf"));

Console.WriteLine();
Console.WriteLine("Done.");
