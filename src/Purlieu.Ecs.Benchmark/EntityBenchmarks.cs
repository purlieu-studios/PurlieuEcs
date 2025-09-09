using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Purlieu.Ecs.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Purlieu.Ecs.Benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[MarkdownExporter]
public class EntityBenchmarks
{
    private Entity[] _entities = null!;
    private ulong[] _packedEntities = null!;
    private HashSet<Entity> _entityHashSet = null!;
    private const int EntityCount = 10000;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42); // Fixed seed for consistent results

        _entities = new Entity[EntityCount];
        _packedEntities = new ulong[EntityCount];

        for (int i = 0; i < EntityCount; i++)
        {
            var id = (uint)random.Next();
            var version = (uint)random.Next();
            _entities[i] = new Entity(id, version);
            _packedEntities[i] = _entities[i].ToPacked();
        }

        _entityHashSet = new HashSet<Entity>(_entities);
    }

    [Benchmark]
    public Entity BENCH_EntityConstruction()
    {
        return new Entity(12345u, 67890u);
    }

    [Benchmark]
    public bool BENCH_EntityEquality()
    {
        var entity1 = new Entity(12345u, 67890u);
        var entity2 = new Entity(12345u, 67890u);
        return entity1.Equals(entity2);
    }

    [Benchmark]
    public int BENCH_EntityComparison()
    {
        var entity1 = new Entity(12345u, 67890u);
        var entity2 = new Entity(12346u, 67890u);
        return entity1.CompareTo(entity2);
    }

    [Benchmark]
    public int BENCH_EntityHashCode()
    {
        var entity = new Entity(12345u, 67890u);
        return entity.GetHashCode();
    }

    [Benchmark]
    public ulong BENCH_EntityPacking()
    {
        var entity = new Entity(12345u, 67890u);
        return entity.ToPacked();
    }

    [Benchmark]
    public Entity BENCH_EntityUnpacking()
    {
        return Entity.FromPacked(0x123456789ABCDEF0UL);
    }

    [Benchmark]
    public Entity[] BENCH_EntityArrayCreation()
    {
        var entities = new Entity[1000];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new Entity((uint)i, 1);
        }
        return entities;
    }

    [Benchmark]
    public Entity[] BENCH_EntitySorting()
    {
        var entities = new Entity[_entities.Length];
        Array.Copy(_entities, entities, _entities.Length);
        Array.Sort(entities);
        return entities;
    }

    [Benchmark]
    public bool BENCH_EntityHashSetLookup()
    {
        var target = _entities[EntityCount / 2];
        return _entityHashSet.Contains(target);
    }

    [Benchmark]
    public HashSet<Entity> BENCH_EntityHashSetCreation()
    {
        return new HashSet<Entity>(_entities);
    }

    [Benchmark]
    public ulong[] BENCH_EntityBatchPacking()
    {
        var packed = new ulong[_entities.Length];
        for (int i = 0; i < _entities.Length; i++)
        {
            packed[i] = _entities[i].ToPacked();
        }
        return packed;
    }

    [Benchmark]
    public Entity[] BENCH_EntityBatchUnpacking()
    {
        var entities = new Entity[_packedEntities.Length];
        for (int i = 0; i < _packedEntities.Length; i++)
        {
            entities[i] = Entity.FromPacked(_packedEntities[i]);
        }
        return entities;
    }

    [Benchmark]
    public uint BENCH_EntityPropertyAccess()
    {
        var entity = new Entity(12345u, 67890u);
        return entity.Id + entity.Version;
    }

    [Benchmark]
    public bool BENCH_EntityNullCheck()
    {
        var entity = new Entity(12345u, 67890u);
        return entity.IsNull;
    }

    [Benchmark]
    public Entity[] BENCH_EntityLinqOperations()
    {
        return _entities
            .Where(e => e.Id % 2 == 0)
            .OrderBy(e => e.Version)
            .Take(100)
            .ToArray();
    }

    [Benchmark]
    public string BENCH_EntityToString()
    {
        var entity = new Entity(12345u, 67890u);
        return entity.ToString();
    }

    [Benchmark]
    public bool BENCH_EntityOperatorComparison()
    {
        var entity1 = new Entity(12345u, 67890u);
        var entity2 = new Entity(12346u, 67890u);
        return entity1 < entity2;
    }

    [Benchmark]
    public ulong BENCH_EntityImplicitConversion()
    {
        var entity = new Entity(12345u, 67890u);
        ulong packed = entity; // Implicit conversion
        return packed;
    }

    [Benchmark]
    public Entity BENCH_EntityExplicitConversion()
    {
        ulong packed = 0x123456789ABCDEF0UL;
        return (Entity)packed; // Explicit conversion
    }
}
