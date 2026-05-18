using TerraPDF.Core;
using TerraPDF.Helpers;

namespace TerraPDF.Sample.Samples;

// =============================================================================
//  2. STYLED NEWSLETTER
//  Shows: two-column row layout, coloured banners, pull-quote block, statistics
//  row with bordered boxes, In-Brief table, multi-page repeating footer.
// =============================================================================
internal static class StyledNewsletter
{
    internal static void Generate(string path)
    {
        const string primary   = "#1B4332";
        const string secondary = "#52B788";
        const string highlight = "#D8F3DC";
        const string muted     = "#6C757D";

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Color.White);
                page.DefaultTextStyle(s => s.FontSize(11));

                // ── Header ───────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Background(primary).Padding(12).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle()
                           .Text("GreenTech Monthly").Bold().FontSize(20).FontColor(Color.White);

                        row.AutoItem().AlignRight().MarginTop(4)
                           .Text("Issue #47  -  April 2026").FontColor(secondary).FontSize(10);
                    });
                    // Thin coloured rule under masthead
                    col.Item().Background(secondary).Padding(3);
                });

                // ── Content ──────────────────────────────────────────────────────
                page.Content().PaddingVertical(0.8, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(10);

                    // Feature story banner
                    col.Item().Background(highlight).Border(1, secondary).Padding(10).Column(inner =>
                    {
                        inner.Item().Text("FEATURE STORY").Bold().FontSize(9).FontColor(primary);
                        inner.Item().PaddingTop(2)
                             .Text("Renewable Energy Hits Record 40% of Global Power in 2025")
                             .Bold().FontSize(15).FontColor(primary);
                        inner.Item().PaddingTop(4).Text(
                            "In a landmark achievement for the global energy transition, renewables " +
                            "accounted for more than 40% of total electricity generation in 2025, " +
                            "driven by unprecedented growth in solar and offshore wind capacity. " +
                            "This milestone surpassed projections by two years, signalling a decisive " +
                            "acceleration in the clean-energy shift across all major economies.")
                            .FontColor(Color.Grey.Darken2).Justify();
                    });

                    // Two-column articles
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(art =>
                        {
                            art.Item().Background(primary).Padding(5)
                               .Text("TECHNOLOGY").Bold().FontSize(8).FontColor(secondary);
                            art.Item().PaddingTop(4)
                               .Text("Solid-State Batteries Finally Go Commercial")
                               .Bold().FontSize(12).FontColor(primary);
                            art.Item().PaddingTop(4).PaddingRight(4).Text(
                                "Three major manufacturers announced production-ready solid-state " +
                                "cells this quarter, promising twice the energy density of lithium-ion " +
                                "at competitive cost. Volume shipments are expected by Q4 2025.").Justify();
                        });

                        // Vertical divider
                        row.ConstantItem(2).PaddingLeft(4).Background(secondary);

                        row.RelativeItem().PaddingLeft(8).Column(art =>
                        {
                            art.Item().Background(secondary).Padding(5)
                               .Text("POLICY").Bold().FontSize(8).FontColor(primary);
                            art.Item().PaddingTop(4)
                               .Text("G20 Nations Agree on Carbon Border Tax Framework")
                               .Bold().FontSize(12).FontColor(primary);
                            art.Item().PaddingTop(4).Text(
                                "After two years of negotiation, G20 finance ministers endorsed a " +
                                "harmonised carbon border adjustment mechanism set to take effect " +
                                "in January 2026. Analysts predict significant shifts in trade " +
                                "patterns for carbon-intensive industries.").Justify();
                        });
                    });

                    col.Item().LineHorizontal(1, secondary);

                    // Pull quote
                    col.Item().Margin(10)
                       .RoundedBox(radius: 12, fillHexColor: highlight,
                                   borderHexColor: secondary, lineWidth: 1.5)
                       .Padding(16).Column(q =>
                    {
                        q.Item().AlignCenter()
                         .Text("The energy transition is no longer a question of if - it is a question of how fast.")
                         .Italic().FontSize(13).FontColor(primary);
                        q.Item().PaddingTop(6).AlignRight()
                         .Text("- Dr. Amara Osei, IRENA Director General")
                         .FontSize(10).FontColor(muted);
                    });

                    // Statistics row
                    col.Item().Row(row =>
                    {
                        void Stat(string value, string label, string fillColor) =>
                            row.RelativeItem().Margin(4)
                               .RoundedBox(radius: 10, fillHexColor: fillColor,
                                           borderHexColor: secondary, lineWidth: 1.5)
                               .Padding(12).Column(s =>
                               {
                                   s.Item().AlignCenter().Text(value).Bold().FontSize(22).FontColor(primary);
                                   s.Item().AlignCenter().Text(label).FontSize(9).FontColor(muted);
                               });

                        Stat("40%",   "Renewables share",       highlight);
                        Stat("$2.8T", "Global clean investment", Color.White);
                        Stat("180M",  "EVs on the road",         highlight);
                        Stat("-18%",  "Carbon intensity drop",   Color.White);
                    });

                    col.Item().LineHorizontal(1, secondary);

                    // In Brief table
                    col.Item().Text("IN BRIEF").Bold().FontColor(primary);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(2); });

                        table.HeaderRow(row =>
                        {
                            row.Cell().Background(primary).Padding(5).Text("Story").Bold().FontColor(Color.White);
                            row.Cell().Background(primary).Padding(5).AlignCenter().Text("Category").Bold().FontColor(Color.White);
                        });

                        (string Story, string Cat)[] briefs =
                        [
                            ("EU mandates heat-pump installations in all new builds from 2026", "Policy"),
                            ("Ocean thermal energy conversion pilot exceeds efficiency targets", "Technology"),
                            ("Green hydrogen production costs fall below $2/kg for first time", "Energy"),
                            ("Antarctica ice-shelf monitoring network expanded to 400 sensors",  "Climate"),
                            ("Vertical farming sector attracts record $9B in 2025 investments",  "Agriculture"),
                            ("New global biodiversity treaty signed by 134 nations",             "Conservation"),
                        ];

                        bool shade = false;
                        foreach (var b in briefs)
                        {
                            string bg = shade ? highlight : Color.White;
                            table.Row(row =>
                            {
                                row.Cell().Background(bg).Padding(5).Text(b.Story);
                                row.Cell().Background(bg).Padding(5).AlignCenter()
                                   .Text(b.Cat).Italic().FontColor(muted);
                            });
                            shade = !shade;
                        }
                    });

                    col.Item().PaddingTop(4).Text(
                        "Thank you for reading GreenTech Monthly. To subscribe, update your " +
                        "preferences, or access the full article archive, visit our website. " +
                        "Forward this newsletter to a colleague who cares about sustainability.")
                       .Justify().FontColor(muted).FontSize(10);
                });

                // ── Footer ───────────────────────────────────────────────────────
                page.Footer().Column(col =>
                {
                    col.Item().Background(secondary).Padding(2);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem()
                           .Text("GreenTech Monthly  |  editor@greentech.example")
                           .FontSize(9).FontColor(muted);

                        row.AutoItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(9).FontColor(muted);
                            t.CurrentPageNumber().FontSize(9).FontColor(muted);
                            t.Span(" / ").FontSize(9).FontColor(muted);
                            t.TotalPages().FontSize(9).FontColor(muted);
                        });
                    });
                });
            });
        }).PublishPdf(path);

        Console.WriteLine($"  [2] Newsletter            -> {path}");
    }
}
