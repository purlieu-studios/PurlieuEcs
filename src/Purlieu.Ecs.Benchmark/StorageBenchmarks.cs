using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Tests.Core;
using System;
using System.Collections.Generic;

namespace Purlieu.Ecs.Benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[MarkdownExporter]
public class StorageBenchmarks
{
    private World _world = null!;
    private Entity[] _entities = null!;
    private ComponentSignature _signature;
    private Archetype _archetype = null!;
    private const int EntityCount = 10000;

    [GlobalSetup]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _world = new World();
        _entities = new Entity[EntityCount];

        _signature = ComponentSignature.Empty
            .With<Position>()
            .With<Velocity>();

        _archetype = new Archetype(_signature);

        // Pre-create entities for benchmarks
        for (int i = 0; i < EntityCount; i++)
        {
            _entities[i] = _world.CreateEntity();
        }
    }

    [Benchmark]
    public Entity BENCH_EntityCreation()
    {
        var world = new World();
        return world.CreateEntity();
    }

    [Benchmark]
    public void BENCH_AddComponent()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(1, 2, 3));
    }

    [Benchmark]
    public void BENCH_AddMultipleComponents()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(1, 2, 3));
        world.AddComponent(entity, new Velocity(0.1f, 0.2f, 0.3f));
        world.AddComponent(entity, new Health(100, 100));
    }

    [Benchmark]
    public Position BENCH_GetComponent()
    {
        var entity = _entities[EntityCount / 2];
        return _world.GetComponent<Position>(entity);
    }

    [Benchmark]
    public void BENCH_SetComponent()
    {
        var entity = _entities[EntityCount / 2];
        _world.SetComponent(entity, new Position(10, 20, 30));
    }

    [Benchmark]
    public bool BENCH_HasComponent()
    {
        var entity = _entities[EntityCount / 2];
        return _world.HasComponent<Position>(entity);
    }

    [Benchmark]
    public ComponentSignature BENCH_SignatureCreation()
    {
        return ComponentSignature.Empty
            .With<Position>()
            .With<Velocity>()
            .With<Health>()
            .Without<Player>();
    }

    [Benchmark]
    public bool BENCH_SignatureHasCheck()
    {
        return _signature.Has<Position>();
    }

    [Benchmark]
    public bool BENCH_SignatureHasAll()
    {
        var required = ComponentSignature.Empty
            .With<Position>()
            .With<Velocity>();

        return _signature.HasAll(required);
    }

    [Benchmark]
    public int BENCH_ArchetypeAddEntity()
    {
        var archetype = new Archetype(_signature);
        var entity = new Entity(1, 1);
        return archetype.AddEntity(entity);
    }

    [Benchmark]
    public Position BENCH_ArchetypeGetComponent()
    {
        var entity = new Entity(1, 1);
        _archetype.AddEntity(entity);
        _archetype.SetComponent(entity, new Position(10, 20, 30));

        return _archetype.GetComponent<Position>(entity);
    }

    [Benchmark]
    public Entity BENCH_ChunkAddEntity()
    {
        var chunk = new Chunk(_signature);
        var entity = new Entity(1, 1);
        chunk.AddEntity(entity);
        return entity;
    }

    [Benchmark]
    public void BENCH_ChunkComponentAccess()
    {
        var chunk = new Chunk(_signature);
        chunk.AddEntity(new Entity(1, 1));

        var positions = chunk.GetSpan<Position>();
        positions[0] = new Position(10, 20, 30);

        var retrieved = positions[0];
    }

    [Benchmark]
    public Entity[] BENCH_CreateManyEntities()
    {
        var world = new World();
        var entities = new Entity[1000];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
        }

        return entities;
    }

    [Benchmark]
    public void BENCH_AddComponentsToManyEntities()
    {
        var world = new World();
        var entities = new Entity[1000];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent(entities[i], new Position(i, i * 2, i * 3));
            world.AddComponent(entities[i], new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }
    }

    [Benchmark]
    public float BENCH_IterateAndSum()
    {
        var world = new World();
        var entities = new Entity[1000];

        // Setup
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent(entities[i], new Position(i, i * 2, i * 3));
        }

        // Iterate and sum
        float sum = 0;
        foreach (var entity in entities)
        {
            var position = world.GetComponent<Position>(entity);
            sum += position.X + position.Y + position.Z;
        }

        return sum;
    }

    [Benchmark]
    public void BENCH_ComponentMigration()
    {
        var world = new World();
        var entity = world.CreateEntity();

        // Add components one by one (triggers migrations)
        world.AddComponent(entity, new Position(1, 2, 3));
        world.AddComponent(entity, new Velocity(0.1f, 0.2f, 0.3f));
        world.AddComponent(entity, new Health(100, 100));
        world.AddComponent(entity, new Name("Test"));

        // Remove components (triggers migrations)
        world.RemoveComponent<Name>(entity);
        world.RemoveComponent<Health>(entity);
    }

    [Benchmark]
    public Archetype[] BENCH_ArchetypeCreation()
    {
        var world = new World();
        var archetypes = new Archetype[100];

        // Create various archetype combinations
        for (int i = 0; i < archetypes.Length; i++)
        {
            var signature = ComponentSignature.Empty;

            if (i % 2 == 0) signature = signature.With<Position>();
            if (i % 3 == 0) signature = signature.With<Velocity>();
            if (i % 5 == 0) signature = signature.With<Health>();
            if (i % 7 == 0) signature = signature.With<Name>();

            archetypes[i] = world.GetOrCreateArchetype(signature);
        }

        return archetypes;
    }

    [Benchmark]
    public void BENCH_ChunkIteration()
    {
        var archetype = new Archetype(_signature);

        // Fill with entities
        for (int i = 0; i < 1000; i++)
        {
            var entity = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entity);
            archetype.SetComponent(entity, new Position(i, i * 2, i * 3));
            archetype.SetComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        // Iterate through chunks
        float sum = 0;
        foreach (var chunk in archetype.Chunks)
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();

            for (int i = 0; i < chunk.Count; i++)
            {
                sum += positions[i].X + velocities[i].X;
            }
        }
    }
}
