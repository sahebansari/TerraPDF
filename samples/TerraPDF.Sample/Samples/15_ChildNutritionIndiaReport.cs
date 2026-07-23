using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  15. CHILD NUTRITION IN INDIA — DEVANAGARI REPORT
//  Shows: FontFamily.Register(...) embedding a Devanagari-script TrueType font
//  ("Noto Sans Devanagari" by Google, SIL Open Font License) and composing a
//  full multi-page Hindi-language report — headings, running text, bullet
//  lists, and data tables — entirely in Devanagari script. Both the regular
//  and bold static instances are registered, so `.Bold()` calls below use
//  the font's real bold outlines.
//
//  IMPORTANT CAVEAT: TerraPDF maps codepoints to glyphs via the font's cmap,
//  plus pure-C# corrections applied automatically for any custom-font text:
//  (1) the vowel sign "ि" is reordered to draw before its consonant, matching
//  its visual position rather than its stored order (see "प्रतिशत"/"ठिगनापन"
//  below); (2) conjunct-forming ligatures the font itself defines via GSUB's
//  half/akhn/cjct features are substituted, so most conjuncts (e.g.
//  "स्वास्थ्य", "स्थिति") render as proper joined forms, not separate glyphs
//  with a visible "्" mark; (3) reph — र् at a cluster's start, e.g. "दुर्बलता",
//  "वर्तमान" — is reordered to the end of its cluster and substituted via
//  GSUB's rphf feature; (4) below-base/post-base 'ra' forms — प्र, क्र, त्र,
//  e.g. "प्रधानमंत्री" — are substituted via GSUB's rkrf feature, no
//  reordering needed. What's NOT covered: `blwf`, other fonts' below-base
//  forms for consonants other than र, which needs contextual GSUB lookups
//  this reader doesn't parse. See docs/custom-fonts.md's "Known limitations"
//  for the full picture.
// =============================================================================
internal static class ChildNutritionIndiaReport
{
    internal static void Generate(string path, string fontPath, string boldFontPath)
    {
        const string brand    = "#B71C1C"; // Indian red
        const string brand2   = "#F57F17"; // saffron accent
        const string green    = "#1B5E20"; // deep green
        const string light    = "#FFF3E0"; // warm tint
        const string muted    = "#5D4037";
        const string gridLine = "#D7CCC8";
        const string white    = "#FFFFFF";

        FontFamily.Register("Noto Sans Devanagari", fontPath);
        FontFamily.Register("Noto Sans Devanagari", boldFontPath, bold: true);

        (string Indicator, string Value, string Note)[] stats =
        [
            ("ठिगनापन",  "35.5%", "आयु के अनुसार कम लंबाई"),
            ("दुर्बलता",  "19.3%", "लंबाई के अनुसार कम वजन"),
            ("कम वजन",   "32.1%", "आयु के अनुसार कम वजन"),
            ("एनीमिया",  "67.1%", "6-59 माह के बच्चों में"),
        ];

        (string Scheme, string Detail)[] schemes =
        [
            ("पोषण अभियान",
                "2018 में शुरू किया गया राष्ट्रीय पोषण मिशन, जिसका लक्ष्य ठिगनापन, कुपोषण और एनीमिया को चरणबद्ध तरीके से कम करना है।"),
            ("एकीकृत बाल विकास सेवा योजना",
                "आंगनवाड़ी केंद्रों के माध्यम से पूरक पोषण, टीकाकरण और प्रारंभिक शिक्षा प्रदान की जाती है।"),
            ("मध्याह्न भोजन योजना",
                "सरकारी विद्यालयों में बच्चों को पका हुआ पौष्टिक भोजन उपलब्ध कराया जाता है।"),
            ("प्रधानमंत्री मातृ वंदना योजना",
                "गर्भवती और स्तनपान कराने वाली माताओं को नकद प्रोत्साहन दिया जाता है।"),
        ];

        void PageHeader(PageDescriptor page, string subtitle)
        {
            page.Header().Column(col =>
            {
                col.Item().Background(brand).PaddingVertical(10).PaddingHorizontal(14).Row(hdr =>
                {
                    hdr.RelativeItem().AlignMiddle()
                       .Text("भारत में बाल पोषण").FontFamily("Noto Sans Devanagari").Bold().FontSize(18).FontColor(white);
                    hdr.AutoItem().AlignRight().AlignMiddle()
                       .Text(subtitle).FontFamily("Noto Sans Devanagari").FontSize(10).FontColor(light);
                });
                col.Item().Background(brand2).Canvas(3, _ => { });
            });
        }

        void PageFooter(PageDescriptor page)
        {
            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(0.5, gridLine);
                f.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem()
                       .Text("भारत में बाल पोषण — रिपोर्ट").FontFamily("Noto Sans Devanagari").FontSize(8).FontColor(muted);
                    row.AutoItem().AlignRight().Text(t =>
                    {
                        t.Span("पृष्ठ ").FontFamily("Noto Sans Devanagari").FontSize(8).FontColor(muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(brand);
                        t.Span(" / ").FontSize(8).FontColor(muted);
                        t.TotalPages().FontSize(8).FontColor(brand);
                    });
                });
            });
        }

        void SectionHeading(TerraPDF.Infra.IContainer c, string text) =>
            c.Background(light).PaddingVertical(6).PaddingHorizontal(10)
             .Text(text).FontFamily("Noto Sans Devanagari").Bold().FontSize(13).FontColor(brand);

        Document.Create(doc =>
        {
            doc.MetadataTitle("भारत में बाल पोषण — रिपोर्ट");
            doc.MetadataAuthor("TerraPDF Sample Generator");
            doc.MetadataSubject(
                "Demonstrates FontFamily.Register embedding a Devanagari TrueType font " +
                "(Noto Sans Devanagari) for a full Hindi-language report.");
            doc.MetadataKeywords("pdf; devanagari; hindi; unicode; custom-font; nutrition; india");

            // ==============================================================
            //  PAGE 1 — परिचय एवं वर्तमान स्थिति
            // ==============================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(white);
                page.DefaultTextStyle(s => s.FontFamily("Noto Sans Devanagari").FontSize(11));

                PageHeader(page, "परिचय एवं वर्तमान स्थिति");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    SectionHeading(col.Item(), "परिचय");
                    col.Item().Text(
                        "भारत में बच्चों का पोषण एक महत्वपूर्ण सार्वजनिक स्वास्थ्य विषय है। पांच वर्ष " +
                        "से कम आयु के लाखों बच्चे कुपोषण से प्रभावित हैं, जिसका सीधा असर उनके शारीरिक " +
                        "और मानसिक विकास पर पड़ता है। पिछले दशक में सरकारी योजनाओं के कारण स्थिति में " +
                        "सुधार हुआ है, परंतु चुनौतियाँ अभी भी बनी हुई हैं।").FontColor(muted).Justify();

                    col.Item().Text(
                        "यह रिपोर्ट वर्तमान स्थिति, प्रमुख कारणों, सरकारी योजनाओं और आगे की सिफारिशों " +
                        "का संक्षिप्त विवरण प्रस्तुत करती है।").FontColor(muted).Justify();

                    SectionHeading(col.Item(), "वर्तमान स्थिति (राष्ट्रीय औसत)");

                    col.Item().Text(
                        "नीचे दिए गए आंकड़े राष्ट्रीय परिवार स्वास्थ्य सर्वेक्षण के अनुमानित आंकड़ों पर " +
                        "आधारित एक उदाहरण हैं, जो केवल प्रदर्शन हेतु सरल किए गए हैं।")
                        .FontSize(8).FontColor(muted).Italic();

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2f);
                            c.ConstantColumn(70);
                            c.RelativeColumn(2.4f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(brand).Padding(6)
                               .Text("संकेतक").FontFamily("Noto Sans Devanagari").Bold().FontSize(9).FontColor(white);
                            row.Cell().Background(brand).Padding(6).AlignCenter()
                               .Text("प्रतिशत").FontFamily("Noto Sans Devanagari").Bold().FontSize(9).FontColor(white);
                            row.Cell().Background(brand).Padding(6)
                               .Text("विवरण").FontFamily("Noto Sans Devanagari").Bold().FontSize(9).FontColor(white);
                        });

                        for (int i = 0; i < stats.Length; i++)
                        {
                            var (indicator, value, note) = stats[i];
                            string bg = i % 2 == 0 ? white : light;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(indicator).FontSize(9).FontColor(Color.Black);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6).AlignCenter()
                                   .Text(value).Bold().FontSize(10).FontColor(brand);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(note).FontSize(8).FontColor(muted);
                            });
                        }
                    });
                });
            });

            // ==============================================================
            //  PAGE 2 — कुपोषण के कारण एवं सरकारी योजनाएं
            // ==============================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(white);
                page.DefaultTextStyle(s => s.FontFamily("Noto Sans Devanagari").FontSize(11));

                PageHeader(page, "कारण एवं सरकारी योजनाएं");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    SectionHeading(col.Item(), "कुपोषण के प्रमुख कारण");

                    string[] causes =
                    [
                        "गरीबी और खाद्य असुरक्षा",
                        "माताओं में एनीमिया और कुपोषण",
                        "स्वच्छता और स्वास्थ्य सेवाओं की कमी",
                        "स्तनपान एवं पूरक आहार संबंधी जागरूकता की कमी",
                        "पेयजल की गुणवत्ता संबंधी समस्याएं",
                    ];

                    col.Item().Column(list =>
                    {
                        list.Spacing(4);
                        foreach (var cause in causes)
                        {
                            list.Item().Row(row =>
                            {
                                row.ConstantItem(14).Text("•").FontColor(brand2).Bold();
                                row.RelativeItem().Text(cause).FontColor(muted);
                            });
                        }
                    });

                    SectionHeading(col.Item(), "प्रमुख सरकारी योजनाएं");

                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(3.2f);
                        });

                        tbl.HeaderRow(row =>
                        {
                            row.Cell().Background(green).Padding(6)
                               .Text("योजना").FontFamily("Noto Sans Devanagari").Bold().FontSize(9).FontColor(white);
                            row.Cell().Background(green).Padding(6)
                               .Text("विवरण").FontFamily("Noto Sans Devanagari").Bold().FontSize(9).FontColor(white);
                        });

                        for (int i = 0; i < schemes.Length; i++)
                        {
                            var (scheme, detail) = schemes[i];
                            string bg = i % 2 == 0 ? white : light;
                            tbl.Row(row =>
                            {
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(scheme).Bold().FontSize(9).FontColor(brand);
                                row.Cell().Background(bg).BorderBottom(0.3, gridLine).Padding(6)
                                   .Text(detail).FontSize(9).FontColor(muted).Justify();
                            });
                        }
                    });
                });
            });

            // ==============================================================
            //  PAGE 3 — सिफारिशें एवं निष्कर्ष
            // ==============================================================
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(white);
                page.DefaultTextStyle(s => s.FontFamily("Noto Sans Devanagari").FontSize(11));

                PageHeader(page, "सिफारिशें एवं निष्कर्ष");
                PageFooter(page);

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    SectionHeading(col.Item(), "सिफारिशें");

                    string[] recommendations =
                    [
                        "आंगनवाड़ी केंद्रों के बुनियादी ढांचे और स्टाफ प्रशिक्षण को मजबूत बनाना",
                        "सामुदायिक स्तर पर पोषण जागरूकता कार्यक्रम बढ़ाना",
                        "स्वास्थ्य, स्वच्छता और पोषण सेवाओं को एक साथ जोड़ना",
                        "आंकड़ों की नियमित निगरानी एवं मूल्यांकन सुनिश्चित करना",
                    ];

                    col.Item().Column(list =>
                    {
                        list.Spacing(4);
                        foreach (var rec in recommendations)
                        {
                            list.Item().Row(row =>
                            {
                                row.ConstantItem(14).Text("•").FontColor(brand2).Bold();
                                row.RelativeItem().Text(rec).FontColor(muted);
                            });
                        }
                    });

                    SectionHeading(col.Item(), "निष्कर्ष");
                    col.Item().Text(
                        "बाल पोषण में सुधार के लिए सरकारी योजनाओं, सामुदायिक भागीदारी और निरंतर " +
                        "निगरानी का समन्वित प्रयास आवश्यक है। सही दिशा में उठाए गए कदम आने वाली पीढ़ी " +
                        "के स्वस्थ भविष्य को सुनिश्चित कर सकते हैं।").FontColor(muted).Justify();

                    col.Item().PaddingTop(10).Background(light).Border(0.5, gridLine).Padding(10).Text(t =>
                    {
                        t.Span("तकनीकी टिप्पणी: ").Bold().FontColor(brand).FontSize(9);
                        t.Span(
                            "यह दस्तावेज़ टेरापीडीएफ़ की फ़ॉन्ट-फैमिली रजिस्टर सुविधा का प्रदर्शन है, जो " +
                            "देवनागरी लिपि वाले ट्रूटाइप फ़ॉन्ट को एम्बेड करती है। जटिल संयुक्ताक्षरों " +
                            "और मात्राओं की सही स्थिति के लिए ओपनटाइप शेपिंग आवश्यक है, " +
                            "जो अभी इस लाइब्रेरी में उपलब्ध नहीं है — इसलिए प्रस्तुतीकरण सर्वोत्तम-प्रयास " +
                            "स्तर का है।").FontSize(9).FontColor(muted);
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [15] Child nutrition in India (Devanagari) -> {path}");
    }
}
