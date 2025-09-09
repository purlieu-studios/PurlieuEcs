using System;
using BenchmarkDotNet.Attributes;
using Purlieu.Ecs.Blueprints;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class BlueprintBenchmarks
{
    private struct BenchPosition
    {
        public int X, Y;
        public BenchPosition(int x, int y) { X = x; Y = y; }
    }

    private struct BenchVelocity
    {
        public float VX, VY;
        public BenchVelocity(float vx, float vy) { VX = vx; VY = vy; }
    }

    private struct BenchHealth
    {
        public int Current, Max;
        public BenchHealth(int current, int max) { Current = current; Max = max; }
    }

    private struct BenchTag { }

    private World _world;
    private EntityBlueprint _simpleBlueprint;
    private EntityBlueprint _complexBlueprint;
    private BlueprintRegistry _registry;

    [GlobalSetup]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _world = new World();

        _simpleBlueprint = EntityBlueprint.Empty
            .With(new BenchPosition(10, 20));

        _complexBlueprint = EntityBlueprint.Empty
            .With(new BenchPosition(100, 200))
            .With(new BenchVelocity(1.5f, -2.0f))
            .With(new BenchHealth(100, 100))
            .With(new BenchTag());

        _registry = new BlueprintRegistry();
        _registry.Register("Simple", _simpleBlueprint);
        _registry.Register("Complex", _complexBlueprint);
    }

    [Benchmark]
    public Entity BENCH_Blueprint_InstantiateSingle()
    {
        return _world.Instantiate(_simpleBlueprint);
    }

    [Benchmark]
    public Entity BENCH_Blueprint_InstantiateComplex()
    {
        return _world.Instantiate(_complexBlueprint);
    }

    [Benchmark]
    public Entity[] BENCH_Blueprint_InstantiateBatch10()
    {
        return _world.InstantiateBatch(_simpleBlueprint, 10);
    }

    [Benchmark]
    public Entity[] BENCH_Blueprint_InstantiateBatch100()
    {
        return _world.InstantiateBatch(_simpleBlueprint, 100);
    }

    [Benchmark]
    public Entity[] BENCH_Blueprint_InstantiateBatch1000()
    {
        return _world.InstantiateBatch(_simpleBlueprint, 1000);
    }

    [Benchmark]
    public Entity BENCH_Blueprint_VsManualCreation_Blueprint()
    {
        return _world.Instantiate(_complexBlueprint);
    }

    [Benchmark]
    public Entity BENCH_Blueprint_VsManualCreation_Manual()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new BenchPosition(100, 200));
        _world.AddComponent(entity, new BenchVelocity(1.5f, -2.0f));
        _world.AddComponent(entity, new BenchHealth(100, 100));
        _world.AddComponent(entity, new BenchTag());
        return entity;
    }

    [Benchmark]
    public string BENCH_Blueprint_SerializeToJson()
    {
        return BlueprintSerializer.SerializeToJson(_complexBlueprint);
    }

    [Benchmark]
    public byte[] BENCH_Blueprint_SerializeToBinary()
    {
        return BlueprintSerializer.SerializeToBinary(_complexBlueprint);
    }

    [Benchmark]
    public EntityBlueprint BENCH_Blueprint_DeserializeFromJson()
    {
        var json = BlueprintSerializer.SerializeToJson(_complexBlueprint);
        return BlueprintSerializer.DeserializeFromJson(json);
    }

    [Benchmark]
    public EntityBlueprint BENCH_Blueprint_DeserializeFromBinary()
    {
        var binary = BlueprintSerializer.SerializeToBinary(_complexBlueprint);
        return BlueprintSerializer.DeserializeFromBinary(binary);
    }

    [Benchmark]
    public EntityBlueprint BENCH_Registry_GetCached()
    {
        return _registry.Get("Complex");
    }

    [Benchmark]
    public EntityBlueprint BENCH_Blueprint_Clone()
    {
        return _complexBlueprint.Clone();
    }

    [Benchmark]
    public EntityBlueprint BENCH_Blueprint_FluentConstruction()
    {
        return EntityBlueprint.Empty
            .With(new BenchPosition(50, 75))
            .With(new BenchVelocity(2.0f, 3.0f))
            .With(new BenchHealth(80, 100))
            .With(new BenchTag());
    }
}
