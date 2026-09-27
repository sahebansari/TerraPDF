#:package Microsoft.Diagnostics.Tracing.TraceEvent@3.1.*
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

string path = args[0], wanted = args[1];
using var log = new TraceLog(TraceLog.CreateFromEventPipeDataFile(path));
var byStack = new Dictionary<string, long>();
foreach (var ev in log.Events)
{
    if (ev is not GCAllocationTickTraceData tick || tick.TypeName != wanted) continue;
    var frames = new List<string>();
    for (var cs = ev.CallStack(); cs is not null && frames.Count < 7; cs = cs.Caller)
    {
        string m = cs.CodeAddress.FullMethodName;
        if (string.IsNullOrEmpty(m)) continue;
        frames.Add(m.Length > 110 ? m[..110] : m);
    }
    string key = string.Join("\n    <- ", frames);
    byStack[key] = byStack.GetValueOrDefault(key) + tick.AllocationAmount64;
}
foreach (var (k, v) in byStack.OrderByDescending(kv => kv.Value).Take(4))
    Console.WriteLine($"{v / 1024} KB\n    {k}\n");
