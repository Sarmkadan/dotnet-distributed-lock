using BenchmarkDotNet.Running;

var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);

Console.WriteLine("Distributed Lock Benchmarks");
Console.WriteLine("=========================");
Console.WriteLine();
Console.WriteLine("Available benchmarks:");
Console.WriteLine("1. BasicBenchmark - Core lock operations");
Console.WriteLine("2. ThroughputBenchmark - High-throughput scenarios");
Console.WriteLine("3. ContentionBenchmark - Contention scenarios");
Console.WriteLine("4. FencingTokenBenchmark - Fencing token operations");
Console.WriteLine();
Console.WriteLine("Usage: dotnet run -c Release -- --filter *");
Console.WriteLine(" dotnet run -c Release -- --filter BasicBenchmark");
Console.WriteLine(" dotnet run -c Release -- --filter * --memory");
Console.WriteLine();

// Run all benchmarks with MemoryDiagnoser
switcher.RunAll(args: args, warmupCount: 5);
```