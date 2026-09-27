using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TerraPDF.Core;
using TerraPDF.Throughput;

// Throughput harness: how many PDF pages per second a process (typically a container with
// a CPU and memory limit) can produce, and what it costs in CPU time and memory.
//
//   dotnet run -c Release --project benchmarks/TerraPDF.Throughput -- --scenario invoice --seconds 20
//
// Options: --scenario invoice|report|all  --seconds N (measure)  --warmup N  --workers N
//          --label text  --json path (append one JSON line per scenario)
var options = Options.Parse(args);
var scenarios = options.Scenario switch
{
    "invoice" => new[] { "invoice" },
    "report"  => new[] { "report" },
    _         => new[] { "invoice", "report" },
};

Console.WriteLine($"TerraPDF throughput — {options.Label}");
Console.WriteLine($"  .NET {Environment.Version}, {RuntimeInfo()}, workers {options.Workers}, " +
                  $"CPUs visible {Environment.ProcessorCount}, GC {(System.Runtime.GCSettings.IsServerGC ? "server" : "workstation")}, " +
                  $"memory limit {FormatBytes(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)}");

foreach (string scenario in scenarios)
{
    Func<int, DocumentComposer> build = scenario == "invoice" ? Scenarios.Invoice : _ => Scenarios.AnnualReport();
    int pagesPerDoc = CountPages(build(0));

    Run(build, options.Warmup, options.Workers);                  // JIT, tiering, caches
    GC.Collect();
    var result = Run(build, options.Seconds, options.Workers);
    result.Scenario = scenario;
    result.Label = options.Label;
    result.PagesPerDocument = pagesPerDoc;
    result.Workers = options.Workers;
    result.ProcessorCount = Environment.ProcessorCount;

    Report(result);
    if (options.JsonPath is not null)
        File.AppendAllText(options.JsonPath, JsonSerializer.Serialize(result) + Environment.NewLine);
}

static Result Run(Func<int, DocumentComposer> build, double seconds, int workers)
{
    var process = Process.GetCurrentProcess();
    process.Refresh();
    TimeSpan cpu0 = process.TotalProcessorTime;
    long alloc0 = GC.GetTotalAllocatedBytes(precise: true);
    int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
    TimeSpan pause0 = GC.GetTotalPauseDuration();

    var latencies = new List<double>[workers];
    int documents = 0;
    long deadline = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
    var wall = Stopwatch.StartNew();

    // Peak memory, sampled: Process.PeakWorkingSet64 is not available on every OS.
    long peakWorkingSet = Environment.WorkingSet;
    var sampler = new Thread(() =>
    {
        while (Stopwatch.GetTimestamp() < deadline)
        {
            peakWorkingSet = Math.Max(peakWorkingSet, Environment.WorkingSet);
            Thread.Sleep(50);
        }
    }) { IsBackground = true };
    sampler.Start();

    var threads = Enumerable.Range(0, workers).Select(w => new Thread(() =>
    {
        var output = new MemoryStream(1 << 20);
        var mine = latencies[w] = new List<double>(4096);
        for (int n = w; Stopwatch.GetTimestamp() < deadline; n += workers)
        {
            long start = Stopwatch.GetTimestamp();
            output.SetLength(0);
            build(n).PublishPdf(output);
            mine.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Interlocked.Increment(ref documents);
        }
    }) { IsBackground = true }).ToList();
    threads.ForEach(t => t.Start());
    threads.ForEach(t => t.Join());
    wall.Stop();
    sampler.Join();

    process.Refresh();
    var all = latencies.SelectMany(l => l).Order().ToArray();
    return new Result
    {
        Documents = documents,
        WallSeconds = wall.Elapsed.TotalSeconds,
        CpuSeconds = (process.TotalProcessorTime - cpu0).TotalSeconds,
        AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - alloc0,
        Gen0 = GC.CollectionCount(0) - g0,
        Gen1 = GC.CollectionCount(1) - g1,
        Gen2 = GC.CollectionCount(2) - g2,
        GcPauseSeconds = (GC.GetTotalPauseDuration() - pause0).TotalSeconds,
        GcHeapBytes = GC.GetGCMemoryInfo().HeapSizeBytes,
        LatencyP50Ms = Percentile(all, 0.50),
        LatencyP95Ms = Percentile(all, 0.95),
        PeakWorkingSetBytes = peakWorkingSet,
    };
}

static void Report(Result r)
{
    long pages = (long)r.Documents * r.PagesPerDocument;
    double pagesPerSecond = pages / r.WallSeconds;
    Console.WriteLine();
    Console.WriteLine($"  [{r.Scenario}] {r.PagesPerDocument} page(s)/document, {r.Documents:N0} documents in {r.WallSeconds:F1} s");
    Console.WriteLine($"    throughput      {pagesPerSecond,10:N1} pages/s   {pagesPerSecond * 60,12:N0} pages/min");
    Console.WriteLine($"    CPU             {r.CpuSeconds * 1000 / pages,10:F3} ms CPU/page   ({r.CpuSeconds / r.WallSeconds / r.ProcessorCount:P0} of {r.ProcessorCount} CPU(s))");
    Console.WriteLine($"    allocated       {FormatBytes(r.AllocatedBytes / pages),10}/page");
    Console.WriteLine($"    GC              gen0 {r.Gen0}, gen1 {r.Gen1}, gen2 {r.Gen2}, pause {r.GcPauseSeconds / r.WallSeconds:P1} of wall time");
    Console.WriteLine($"    memory          peak working set {FormatBytes(r.PeakWorkingSetBytes)}, GC heap {FormatBytes(r.GcHeapBytes)}");
    Console.WriteLine($"    latency         p50 {r.LatencyP50Ms:F2} ms, p95 {r.LatencyP95Ms:F2} ms per document");
}

static int CountPages(DocumentComposer document)
{
    byte[] pdf = document.PublishPdf();
    string text = System.Text.Encoding.Latin1.GetString(pdf);
    int count = 0;
    for (int i = text.IndexOf("/Type /Page ", StringComparison.Ordinal); i >= 0;
         i = text.IndexOf("/Type /Page ", i + 1, StringComparison.Ordinal))
        count++;
    return count;
}

static double Percentile(double[] sorted, double p) =>
    sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];

static string FormatBytes(long bytes) => bytes switch
{
    >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
    >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
    >= 1L << 10 => $"{bytes / 1024.0:F1} KB",
    _ => $"{bytes} B",
};

static string RuntimeInfo() =>
    $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription.Split(' ')[0]} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";

internal sealed class Result
{
    public string Label { get; set; } = "";
    public string Scenario { get; set; } = "";
    public int PagesPerDocument { get; set; }
    public int Workers { get; set; }
    public int ProcessorCount { get; set; }
    public int Documents { get; set; }
    public double WallSeconds { get; set; }
    public double CpuSeconds { get; set; }
    public long AllocatedBytes { get; set; }
    public int Gen0 { get; set; }
    public int Gen1 { get; set; }
    public int Gen2 { get; set; }
    public double GcPauseSeconds { get; set; }
    public long GcHeapBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public double LatencyP50Ms { get; set; }
    public double LatencyP95Ms { get; set; }
}

internal sealed record Options(string Scenario, double Seconds, double Warmup, int Workers, string Label, string? JsonPath)
{
    internal static Options Parse(string[] args)
    {
        string Get(string name, string fallback)
        {
            int i = Array.IndexOf(args, "--" + name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        return new Options(
            Get("scenario", "all"),
            double.Parse(Get("seconds", "20"), CultureInfo.InvariantCulture),
            double.Parse(Get("warmup", "5"), CultureInfo.InvariantCulture),
            int.Parse(Get("workers", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            Get("label", "current"),
            args.Contains("--json") ? Get("json", "throughput.jsonl") : null);
    }
}
