using BenchmarkDotNet.Running;
using Purlieu.Ecs.Benchmark;

namespace Purlieu.Ecs.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            // Run BenchmarkDotNet with provided arguments
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
        else
        {
            // Default: run Entity benchmarks
            BenchmarkRunner.Run<EntityBenchmarks>();
        }
    }
}
