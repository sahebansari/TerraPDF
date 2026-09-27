#:package Microsoft.Diagnostics.Tracing.TraceEvent@3.1.*
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

using var log = new TraceLog(TraceLog.CreateFromEventPipeDataFile(args[0]));
var excl = new Dictionary<string, int>(); var incl = new Dictionary<string, int>(); int total = 0;
foreach (var ev in log.Events)
{
    if (ev.EventName != "Thread/Sample") continue;
    var cs = ev.CallStack(); if (cs is null) continue;
    // only samples inside a PublishPdf/Build call (skip idle threads)
    var frames = new List<string>();
    for (var f = cs; f is not null; f = f.Caller) { var m = f.CodeAddress.FullMethodName; if (!string.IsNullOrEmpty(m)) frames.Add(m); }
    if (!frames.Any(m => m.Contains("TerraPDF"))) continue;
    total++;
    string Short(string m) { int p = m.IndexOf('('); m = p > 0 ? m[..p] : m; return m.Length > 95 ? m[^95..] : m; }
    excl[Short(frames[0])] = excl.GetValueOrDefault(Short(frames[0])) + 1;
    foreach (var m in frames.Select(Short).Distinct()) incl[m] = incl.GetValueOrDefault(m) + 1;
}
Console.WriteLine($"samples: {total}\n-- exclusive (self) --");
foreach (var (k, v) in excl.OrderByDescending(x => x.Value).Take(14)) Console.WriteLine($"{v * 100.0 / total,5:F1}%  {k}");
Console.WriteLine("-- inclusive, TerraPDF --");
foreach (var (k, v) in incl.Where(x => x.Key.Contains("TerraPDF")).OrderByDescending(x => x.Value).Take(22)) Console.WriteLine($"{v * 100.0 / total,5:F1}%  {k}");
