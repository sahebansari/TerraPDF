using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  5. EVENT BROCHURE
//  Shows: header banner + overlay text, agenda table with alternating rows,
//  speaker cards in two-column layout, sponsor logos row, call-to-action banner.
// =============================================================================
internal static class EventBrochure
{
    internal static void Generate(string path, string headerImg, string smallImg)
    {
        const string primary = "#6A0572";
        const string gold    = "#D4A017";
        const string light   = "#FAF5FB";
        const string muted   = "#7A7A8C";

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(0);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                // ── Header: banner image + event title overlay ────────────────
                page.Header().Column(col =>
                {
                    if (File.Exists(headerImg))
                        col.Item().Image(headerImg);
                    else
                        col.Item().Background(primary).Padding(40);

                    col.Item().Background(primary).Padding(12).Column(overlay =>
                    {
                        overlay.Item().AlignCenter()
                               .Text("TECHVISION SUMMIT 2025").Bold().FontSize(22).FontColor(Color.White);
                        overlay.Item().AlignCenter()
                               .Text("September 18-19, 2025  |  Convention Center, San Francisco")
                               .FontColor(gold).FontSize(11);
                    });
                });

                // ── Content ──────────────────────────────────────────────────
                page.Content().Padding(35).Column(col =>
                {
                    col.Spacing(12);

                    // Welcome
                    col.Item().Background(light).Padding(12).Column(w =>
                    {
                        w.Item().Text("WELCOME").Bold().FontSize(10).FontColor(gold);
                        w.Item().PaddingTop(4).Text(
                            "TechVision Summit brings together 2,000+ technology leaders, " +
                            "engineers, and innovators for two days of cutting-edge talks, " +
                            "workshops, and networking. Explore the future of AI, cloud, " +
                            "security, and developer tools — all in one place.").Justify();
                    });

                    // Agenda table
                    col.Item().Text("CONFERENCE AGENDA – DAY 1").Bold().FontColor(primary);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(primary).Padding(6).Text("Time").Bold().FontColor(Color.White);
                            row.Cell().Background(primary).Padding(6).Text("Session").Bold().FontColor(Color.White);
                            row.Cell().Background(primary).Padding(6).Text("Track").Bold().FontColor(Color.White);
                        });

                        var agenda = new[]
                        {
                            ("09:00", "Registration & Breakfast",                       "All"),
                            ("10:00", "Opening Keynote: The Next Decade of AI",          "Keynote"),
                            ("11:00", "Building Resilient Cloud-Native Architectures",   "Cloud"),
                            ("11:00", "Zero-Trust Security in the Modern Enterprise",    "Security"),
                            ("12:00", "Lunch Break & Expo Hall",                         "All"),
                            ("13:00", "LLM Fine-Tuning at Scale",                        "AI"),
                            ("13:00", "Developer Experience: Making Teams 10x Faster",   "DevEx"),
                            ("14:30", "Panel: Open Source and the Enterprise",            "Open Source"),
                            ("15:30", "Coffee Break & Networking",                        "All"),
                            ("16:00", "Workshop: Hands-on Kubernetes GitOps",             "Cloud"),
                            ("16:00", "Workshop: RAG Pipelines with Local LLMs",          "AI"),
                            ("17:30", "Day 1 Closing & Drinks Reception",                 "All"),
                        };

                        bool shade = false;
                        foreach (var (time, session, track) in agenda)
                        {
                            string bg = shade ? light : Color.White;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(5).Text(time).Bold().FontColor(primary);
                                row.Cell().Background(bg).Padding(5).Text(session);
                                row.Cell().Background(bg).Padding(5).AlignCenter()
                                   .Text(track).Italic().FontColor(muted).FontSize(10);
                            });
                            shade = !shade;
                        }
                    });

                    // Speakers
                    col.Item().Text("FEATURED SPEAKERS").Bold().FontColor(primary);

                    col.Item().Row(row =>
                    {
                        void Speaker(string name, string role, string org, string topic)
                        {
                            row.RelativeItem().Margin(5).Border(1, Color.Grey.Lighten2).Padding(10).Column(s =>
                            {
                                s.Item().Background(primary).Padding(4)
                                 .Text(name).Bold().FontColor(Color.White).FontSize(11);
                                s.Item().PaddingTop(4).Text(role).FontColor(gold).FontSize(10);
                                s.Item().Text(org).FontColor(muted).FontSize(10);
                                s.Item().PaddingTop(6).Text("Topic: " + topic)
                                 .Italic().FontColor(primary).FontSize(10);
                            });
                        }

                        Speaker("Dr. Aiko Tanaka",  "Research Director",  "OpenMind Labs",    "The Next Decade of AI");
                        Speaker("Carlos Rivera",    "VP Engineering",     "CloudScale Inc.",  "Resilient Cloud Architectures");
                        Speaker("Emma Johansson",   "CISO",               "Fortress Security","Zero-Trust in Enterprise");
                    });

                    col.Item().LineHorizontal(1, Color.Grey.Lighten2);

                    // Sponsors
                    col.Item().Text("OUR SPONSORS").Bold().FontColor(primary);

                    col.Item().Row(row =>
                    {
                        void Sponsor(string tier, string name, string bg)
                        {
                            row.RelativeItem().Margin(3).Background(bg).Border(1, gold).Padding(10).Column(s =>
                            {
                                s.Item().AlignCenter().Text(tier).FontSize(8).FontColor(muted);
                                s.Item().AlignCenter().Text(name).Bold().FontColor(primary);
                                if (File.Exists(smallImg))
                                    s.Item().AlignCenter().Image(smallImg, 40);
                            });
                        }

                        Sponsor("PLATINUM", "TerraPDF Co.",  light);
                        Sponsor("GOLD",     "CloudScale Inc.", Color.White);
                        Sponsor("GOLD",     "Fortress Sec.",   light);
                        Sponsor("SILVER",   "DevEx Labs",      Color.White);
                    });

                    // CTA banner
                    col.Item().MarginTop(10).Background(primary).Padding(14).Column(cta =>
                    {
                        cta.Item().AlignCenter()
                           .Text("REGISTER NOW — EARLY BIRD ENDS AUGUST 1")
                           .Bold().FontSize(14).FontColor(Color.White);
                        cta.Item().PaddingTop(4).AlignCenter()
                           .Text("summit.techvision.example  |  tickets@techvision.example  |  +1 (800) 123-4567")
                           .FontColor(gold).FontSize(10);
                    });
                });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer().Padding(16).Row(row =>
                {
                    row.ConstantItem(55).AlignMiddle().Column(c =>
                    {
                        if (File.Exists(smallImg))
                            c.Item().Image(smallImg, 45);
                    });

                    row.RelativeItem().PaddingLeft(8).AlignMiddle()
                       .Text("TechVision Summit 2025  |  summit.techvision.example")
                       .FontColor(muted).FontSize(9);

                    row.AutoItem().AlignMiddle().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(9).FontColor(muted);
                        t.CurrentPageNumber().FontSize(9).FontColor(muted);
                        t.Span(" / ").FontSize(9).FontColor(muted);
                        t.TotalPages().FontSize(9).FontColor(muted);
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [5] Event brochure        -> {path}");
    }
}
