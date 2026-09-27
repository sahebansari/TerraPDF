#:project ../../../src/TerraPDF/TerraPDF.csproj
#:property Optimize=true
#:property EventSourceSupport=true
#:property PublishAot=false
using System.Diagnostics.Tracing;
using System.Globalization;
using TerraPDF.Core;
using TerraPDF.Helpers;

const string Brand = "#1a4a8a", LightBrand = "#EBF2FF";
int rows = int.Parse(args[0]);
string mode = args.Length > 1 ? args[1] : "table";

DocumentComposer Build() => mode switch
{
    "text" => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSize.A4); page.Margin(2, Unit.Centimetre);
        page.Content().Column(col => { for (int i = 0; i < rows; i++) col.Item().Text($"Line item number {i} consulting services"); });
    })),
    "bare" => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSize.A4); page.Margin(2, Unit.Centimetre);
        page.Content().Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            for (int i = 0; i < rows; i++) t.Row(r => { r.Cell().Text($"Line {i}"); r.Cell().Text("x"); });
        });
    })),
    _ => BuildTable(),
};

DocumentComposer BuildTable() => Document.Create(doc => doc.Page(page =>
{
    page.Size(PageSize.A4); page.Margin(2, Unit.Centimetre); page.PageColor(Color.White);
    page.DefaultTextStyle(s => s.FontSize(11));
    page.Footer().AlignCenter().Text(t => { t.Span("Page "); t.CurrentPageNumber(); });
    page.Content().Table(table =>
    {
        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2); });
        table.HeaderRow(row =>
        {
            row.Cell().Background(Brand).Padding(4).Text("Description").Bold().FontColor(Color.White);
            row.Cell().Background(Brand).Padding(4).Text("Qty").Bold().FontColor(Color.White);
            row.Cell().Background(Brand).Padding(4).AlignRight().Text("Unit").Bold().FontColor(Color.White);
            row.Cell().Background(Brand).Padding(4).AlignRight().Text("Total").Bold().FontColor(Color.White);
        });
        for (int i = 0; i < rows; i++)
        {
            string bg = (i & 1) == 0 ? Color.White : LightBrand;
            int qty = i % 7 + 1; decimal unit = 10m + i % 97;
            table.Row(row =>
            {
                row.Cell().Background(bg).Padding(4).Text($"Line item number {i} — consulting services");
                row.Cell().Background(bg).Padding(4).Text(qty.ToString(CultureInfo.InvariantCulture));
                row.Cell().Background(bg).Padding(4).AlignRight().Text(unit.ToString("N2", CultureInfo.InvariantCulture));
                row.Cell().Background(bg).Padding(4).AlignRight().Text((qty * unit).ToString("N2", CultureInfo.InvariantCulture));
            });
        }
    });
}));

for (int w = 0; w < 40; w++) Build().PublishPdf(Stream.Null); Thread.Sleep(500); // warm up

long a0 = GC.GetAllocatedBytesForCurrentThread();
var d = Build();
long a1 = GC.GetAllocatedBytesForCurrentThread();
d.PublishPdf(Stream.Null);
long a2 = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"compose: {(a1 - a0) / 1024.0 / rows:F1} KB/row   publish: {(a2 - a1) / 1024.0 / rows:F1} KB/row");

using var listener = new AllocListener();
for (int k = 0; k < 5; k++) Build().PublishPdf(Stream.Null);
Thread.Sleep(3000);
listener.Dispose();
long total = Math.Max(1, listener.ByType.Values.Sum());
Console.WriteLine($"sampled types: {listener.ByType.Count} events: {listener.Events} names: {string.Join(",", listener.Names.Take(12))}");
foreach (var (type, bytes) in listener.ByType.OrderByDescending(kv => kv.Value).Take(25))
    Console.WriteLine($"{bytes * 100.0 / total,5:F1}%  {type}");

sealed class AllocListener : EventListener
{
    public readonly Dictionary<string, long> ByType = new();
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1); // GC keyword → AllocationTick
    }
    public int Events;
    public readonly HashSet<string> Names = new();
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        Events++; if (e.EventName is not null) lock (Names) Names.Add(e.EventName);
        if (e.EventName is null || !e.EventName.StartsWith("GCAllocationTick") || e.Payload is null) return;
        int ti = e.PayloadNames!.IndexOf("TypeName"), ai = e.PayloadNames.IndexOf("AllocationAmount64");
        string type = (string)e.Payload[ti]!; long amount = Convert.ToInt64(e.Payload[ai]);
        lock (ByType) ByType[type] = ByType.GetValueOrDefault(type) + amount;
    }
}
