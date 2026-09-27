using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

// Entry point for the TerraPDF benchmark suite.
// Examples:
//   dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*'
//   dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --filter '*Table*' --job short
//   dotnet run -c Release --project benchmarks/TerraPDF.Benchmarks -- --list flat
var config = DefaultConfig.Instance
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddExporter(MarkdownExporter.GitHub)
    .AddExporter(JsonExporter.Full);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
