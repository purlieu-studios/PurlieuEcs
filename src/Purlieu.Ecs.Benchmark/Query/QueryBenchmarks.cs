using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Query;
using System.Linq;

namespace Purlieu.Ecs.Benchmark.Query;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[JsonExporter]
public class QueryBenchmarks
{
    private World _world = null!;
    private IQuery _simpleQuery = null!;
    private IQuery _complexQuery = null!;
    private IQuery _exclusionQuery = null!;

    [Params(100, 1000, 10000)]
    public int EntityCount { get; set; }

    [Params(1, 10, 100)]
    public int ArchetypeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();

        // Create entities distributed across archetypes
        int entitiesPerArchetype = EntityCount / ArchetypeCount;

        for (int archetype = 0; archetype < ArchetypeCount; archetype++)
        {
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                var entity = _world.CreateEntity();

                // All entities get Position
                _world.AddComponent(entity, new TestPosition { X = i, Y = i * 2, Z = i * 3 });

                // 50% get Velocity
                if ((archetype + i) % 2 == 0)
                {
                    _world.AddComponent(entity, new TestVelocity { X = i * 0.1f, Y = i * 0.2f, Z = i * 0.3f });
                }

                // 33% get Health
                if ((archetype + i) % 3 == 0)
                {
                    _world.AddComponent(entity, new TestHealth { Current = i * 10, Max = 100 });
                }

                // 25% get Tag
                if ((archetype + i) % 4 == 0)
                {
                    _world.AddComponent(entity, new TestTag());
                }
            }
        }

        // Pre-create queries for benchmarking
        _simpleQuery = _world.Query().With<TestPosition>();
        _complexQuery = _world.Query().With<TestPosition>().With<TestVelocity>();
        _exclusionQuery = _world.Query().With<TestPosition>().Without<TestHealth>();
    }

    [Benchmark(Baseline = true)]
    public int SimpleQuery_Iteration()
    {
        int count = 0;
        foreach (var chunk in _simpleQuery.Chunks())
        {
            var positions = chunk.GetSpan<TestPosition>();
            for (int i = 0; i < positions.Length; i++)
            {
                // Simulate minimal work to prevent optimization
                count += positions[i].X;
            }
        }
        return count;
    }

    [Benchmark]
    public int ComplexQuery_Iteration()
    {
        int count = 0;
        foreach (var chunk in _complexQuery.Chunks())
        {
            var positions = chunk.GetSpan<TestPosition>();
            var velocities = chunk.GetSpan<TestVelocity>();

            for (int i = 0; i < chunk.Count; i++)
            {
                // Simulate typical physics update
                count += positions[i].X + (int)velocities[i].X;
            }
        }
        return count;
    }

    [Benchmark]
    public int ExclusionQuery_Iteration()
    {
        int count = 0;
        foreach (var chunk in _exclusionQuery.Chunks())
        {
            var positions = chunk.GetSpan<TestPosition>();
            for (int i = 0; i < positions.Length; i++)
            {
                count += positions[i].X;
            }
        }
        return count;
    }

    [Benchmark]
    public void QueryConstruction_Simple()
    {
        var query = _world.Query().With<TestPosition>();
        // Force evaluation to prevent optimization
        _ = query.Chunks().FirstOrDefault();
    }

    [Benchmark]
    public void QueryConstruction_Complex()
    {
        var query = _world.Query()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Without<TestHealth>()
            .Without<TestTag>();
        _ = query.Chunks().FirstOrDefault();
    }

    [Benchmark]
    public int ChunkEnumeration_Count()
    {
        return _simpleQuery.Chunks().Count();
    }

    [Benchmark]
    public int MultipleQueries_Sequential()
    {
        int count = 0;

        // Simulate running multiple systems
        foreach (var chunk in _simpleQuery.Chunks())
        {
            count += chunk.Count;
        }

        foreach (var chunk in _complexQuery.Chunks())
        {
            count += chunk.Count;
        }

        foreach (var chunk in _exclusionQuery.Chunks())
        {
            count += chunk.Count;
        }

        return count;
    }
}

// Test components for benchmarking
public struct TestPosition
{
    public int X, Y, Z;
}

public struct TestVelocity
{
    public float X, Y, Z;
}

public struct TestHealth
{
    public int Current, Max;
}

public struct TestTag { }
