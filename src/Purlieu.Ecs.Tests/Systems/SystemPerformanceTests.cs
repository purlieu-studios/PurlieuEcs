using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Systems;

namespace Purlieu.Ecs.Tests.Systems;

[TestFixture]
[Category("Performance")]
public class SystemPerformanceTests
{
    private const int WarmupIterations = 10;
    private const int MeasureIterations = 50;

    [Test, Explicit("Performance benchmark - run manually")]
    public void BENCH_SystemExecution_ShouldScaleLinearly()
    {
        // Measure system execution time across different entity counts
        var entityCounts = new[] { 100, 500, 1000, 2500, 5000 };
        var results = new List<(int entityCount, double avgTimeMs)>();

        foreach (var entityCount in entityCounts)
        {
            var times = new List<double>();

            for (int run = 0; run < 5; run++) // Multiple runs for stability
            {
                var runTime = MeasureSystemExecutionTime(entityCount);
                times.Add(runTime);
            }

            var avgTime = times.Average();
            results.Add((entityCount, avgTime));
            Console.WriteLine($"Entity Count: {entityCount:N0}, Avg Time: {avgTime:F3}ms");
        }

        // Verify roughly linear scaling
        var firstResult = results.First();
        var lastResult = results.Last();
        var entityScaling = (double)lastResult.entityCount / firstResult.entityCount;
        var timeScaling = lastResult.avgTimeMs / firstResult.avgTimeMs;

        Console.WriteLine($"Entity scaling: {entityScaling:F1}x, Time scaling: {timeScaling:F1}x");

        // Time scaling should be no worse than 2x the entity scaling (allowing for overhead)
        timeScaling.Should().BeLessThan(entityScaling * 2,
            "System execution time should scale roughly linearly with entity count");
    }

    [Test, Explicit("Performance benchmark - run manually")]
    public void BENCH_SystemScheduler_Registration_ShouldScaleWithSystemCount()
    {
        var systemCounts = new[] { 10, 50, 100, 200 };
        var results = new List<(int systemCount, double avgTimeMs)>();

        foreach (var systemCount in systemCounts)
        {
            var times = new List<double>();

            for (int run = 0; run < 10; run++)
            {
                var time = MeasureSystemRegistrationTime(systemCount);
                times.Add(time);
            }

            var avgTime = times.Average();
            results.Add((systemCount, avgTime));
            Console.WriteLine($"System Count: {systemCount}, Avg Registration Time: {avgTime:F3}ms");
        }

        // Should scale reasonably
        var firstResult = results.First();
        var lastResult = results.Last();
        var systemScaling = (double)lastResult.systemCount / firstResult.systemCount;
        var timeScaling = lastResult.avgTimeMs / firstResult.avgTimeMs;

        Console.WriteLine($"System scaling: {systemScaling:F1}x, Time scaling: {timeScaling:F1}x");

        // Time scaling should be reasonable (no worse than quadratic)
        timeScaling.Should().BeLessThan(systemScaling * systemScaling,
            "System registration time should not scale worse than quadratically");
    }

    [Test]
    public void PERF_SystemExecution_ShouldScaleReasonably()
    {
        // Test that system execution time scales reasonably with entity count
        var results = new List<(int entityCount, double timeMs)>();

        foreach (var entityCount in new[] { 100, 500, 1000, 2500 })
        {
            var time = MeasureSystemExecutionTime(entityCount);
            results.Add((entityCount, time));
        }

        // Verify roughly linear scaling (allowing for some overhead)
        var time100 = results.First(r => r.entityCount == 100).timeMs;
        var time2500 = results.First(r => r.entityCount == 2500).timeMs;

        // Should scale no worse than 30x for 25x entity increase (allowing overhead)
        var scalingFactor = time2500 / time100;
        scalingFactor.Should().BeLessThan(30, "System execution should scale roughly linearly with entity count");

        // Should show some scaling (not constant time)
        scalingFactor.Should().BeGreaterThan(2, "System execution should increase with entity count");
    }

    [Test]
    public void PERF_SystemRegistration_ShouldHandleManySystemsEfficiently()
    {
        // Test that system registration remains efficient with many systems
        var world = new World();
        var stopwatch = Stopwatch.StartNew();

        // Register 100 systems
        for (int i = 0; i < 100; i++)
        {
            world.RegisterSystem(new TestPerformanceSystem(i));
        }

        stopwatch.Stop();

        // Registration should complete quickly
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            "Registering 100 systems should complete in under 100ms");

        // Execution order should be maintained
        var executionOrder = world.GetSystemExecutionOrder();
        executionOrder.Should().HaveCount(100);
    }

    private double MeasureSystemExecutionTime(int entityCount)
    {
        var world = new World();

        // Create entities
        for (int i = 0; i < entityCount; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));
            world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        // Register systems
        world.RegisterSystem(new MovementSystem());
        world.RegisterSystem(new TestPerformanceSystem(0));

        // Warm up
        for (int i = 0; i < WarmupIterations; i++)
        {
            world.UpdateSystems(0.016f);
        }

        // Measure execution time over multiple iterations
        var times = new List<double>();
        for (int i = 0; i < MeasureIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            world.UpdateSystems(0.016f);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return times.Average();
    }

    private double MeasureSystemRegistrationTime(int systemCount)
    {
        var stopwatch = Stopwatch.StartNew();

        var world = new World();
        for (int i = 0; i < systemCount; i++)
        {
            world.RegisterSystem(new TestPerformanceSystem(i));
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}

// Test system for performance testing
[GamePhase(GamePhase.Update, order: 0)]
public class TestPerformanceSystem : ISystem
{
    private readonly int _id;

    public TestPerformanceSystem(int id)
    {
        _id = id;
    }

    public void Update(World world, float deltaTime)
    {
        // Minimal work to test scheduler overhead
        var query = world.Query().With<Purlieu.Ecs.Core.Position>();

        foreach (var chunk in query.Chunks())
        {
            var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();

            // Simulate minimal processing
            for (int i = 0; i < positions.Length; i++)
            {
                // Just read the position (minimal work)
                _ = positions[i].X;
            }
        }
    }
}