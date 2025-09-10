using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Query;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
[Category("Performance")]
public class QueryPerformanceTests
{
    private World _world;
    private static readonly int WarmupIterations = PlatformTestHelper.AdjustIterations(10);
    private static readonly int MeasureIterations = PlatformTestHelper.AdjustIterations(100);

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void BENCH_SimpleQuery_ShouldIterateEfficiently()
    {
        // Arrange - Create 10,000 entities
        for (int i = 0; i < 10000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));
        }

        var query = _world.Query().With<Purlieu.Ecs.Core.Position>();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                for (int j = 0; j < positions.Length; j++)
                {
                    _ = positions[j].X;
                }
            }
        }

        // Measure
        var sw = Stopwatch.StartNew();
        for (int iteration = 0; iteration < MeasureIterations; iteration++)
        {
            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                for (int i = 0; i < positions.Length; i++)
                {
                    _ = positions[i].X;
                }
            }
        }
        sw.Stop();

        var timePerIteration = sw.Elapsed.TotalMilliseconds / MeasureIterations;

        // Assert - Should iterate 10k entities efficiently (with platform adjustments)
        var timeThreshold = PlatformTestHelper.IsLinux || PlatformTestHelper.IsWindows ? 5.0 : 1.0;
        timePerIteration.Should().BeLessThan(timeThreshold,
            $"Query iteration over 10k entities should take less than {timeThreshold}ms on {PlatformTestHelper.PlatformDescription}, took {timePerIteration:F3}ms");
    }

    [Test]
    public void BENCH_ComplexQuery_ShouldFilterEfficiently()
    {
        // Arrange - Create mixed archetypes
        for (int i = 0; i < 10000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));

            if (i % 2 == 0)
                _world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));

            if (i % 3 == 0)
                _world.AddComponent(entity, new Health(i * 10, i * 10));
        }

        var query = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>().Without<Health>();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            _ = query.Chunks().Count();
        }

        // Measure
        var sw = Stopwatch.StartNew();
        int totalEntities = 0;
        for (int iteration = 0; iteration < MeasureIterations; iteration++)
        {
            foreach (var chunk in query.Chunks())
            {
                totalEntities += chunk.Count;
            }
        }
        sw.Stop();

        var timePerIteration = sw.Elapsed.TotalMilliseconds / MeasureIterations;

        // Assert - Complex query filtering (with platform adjustments)
        var timeThreshold = PlatformTestHelper.IsLinux || PlatformTestHelper.IsWindows ? 2.5 : 0.5;
        timePerIteration.Should().BeLessThan(timeThreshold,
            $"Complex query filtering should take less than {timeThreshold}ms on {PlatformTestHelper.PlatformDescription}, took {timePerIteration:F3}ms");

        // Verify correct filtering (about 3333 entities match the criteria)
        (totalEntities / MeasureIterations).Should().BeCloseTo(3333, 100);
    }

    [Test]
    public void BENCH_QueryConstruction_ShouldBeFast()
    {
        // Arrange - Prepare world with entities
        for (int i = 0; i < 1000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i, i));
        }

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            var q = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>().Without<Health>();
        }

        // Measure query construction time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var query = _world.Query()
                .With<Purlieu.Ecs.Core.Position>()
                .With<Purlieu.Ecs.Core.Velocity>()
                .Without<Health>()
                .Without<Enemy>();
        }
        sw.Stop();

        var timePerQuery = sw.Elapsed.TotalMicroseconds / 10000;

        // Assert - Query construction should be fast (with platform adjustments)
        var microsecondsThreshold = PlatformTestHelper.IsMacOS ? 2.0 :
                                   (PlatformTestHelper.IsLinux || PlatformTestHelper.IsWindows ? 10.0 : 1.0);
        timePerQuery.Should().BeLessThan(microsecondsThreshold,
            $"Query construction should take less than {microsecondsThreshold}μs on {PlatformTestHelper.PlatformDescription}, took {timePerQuery:F3}μs");
    }

    [Test]
    [Ignore("Linear scaling test is too sensitive to CPU cache effects")]
    public void BENCH_ChunkIteration_ShouldScaleLinearly()
    {
        // Test that performance scales linearly with entity count
        var times = new double[3];
        var entityCounts = new[] { 1000, 2000, 4000 };

        for (int test = 0; test < entityCounts.Length; test++)
        {
            // Setup fresh world for each test
            var world = new World();
            for (int i = 0; i < entityCounts[test]; i++)
            {
                var entity = world.CreateEntity();
                world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i, i));
            }

            var query = world.Query().With<Purlieu.Ecs.Core.Position>();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                foreach (var chunk in query.Chunks())
                {
                    _ = chunk.Count;
                }
            }

            // Measure
            var sw = Stopwatch.StartNew();
            for (int iteration = 0; iteration < MeasureIterations; iteration++)
            {
                int count = 0;
                foreach (var chunk in query.Chunks())
                {
                    var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                    for (int i = 0; i < positions.Length; i++)
                    {
                        count += (int)positions[i].X;
                    }
                }
            }
            sw.Stop();

            times[test] = sw.Elapsed.TotalMilliseconds / MeasureIterations;
        }

        // Assert - Time should roughly double as entity count doubles
        var ratio1to2 = times[1] / times[0];
        var ratio2to4 = times[2] / times[1];

        ratio1to2.Should().BeApproximately(2.0, 0.5,
            "Performance should scale linearly (2x entities = ~2x time)");
        ratio2to4.Should().BeApproximately(2.0, 0.5,
            "Performance should scale linearly (2x entities = ~2x time)");
    }

    [Test]
    public void BENCH_MultipleQueries_ShouldNotInterfere()
    {
        // Skip on macOS due to extreme performance variability in CI
        if (PlatformTestHelper.IsMacOS)
        {
            Assert.Ignore("Test skipped on macOS due to extreme timing variance in CI environment");
        }

        // Arrange - Create entities
        for (int i = 0; i < 5000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i, i));
            if (i % 2 == 0)
                _world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(i, i, i));
        }

        var query1 = _world.Query().With<Purlieu.Ecs.Core.Position>();
        var query2 = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>();
        var query3 = _world.Query().With<Purlieu.Ecs.Core.Position>().Without<Purlieu.Ecs.Core.Velocity>();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            _ = query1.Chunks().Count();
            _ = query2.Chunks().Count();
            _ = query3.Chunks().Count();
        }

        // Measure individual query times
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < MeasureIterations; i++)
        {
            foreach (var chunk in query1.Chunks())
            {
                _ = chunk.Count;
            }
        }
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < MeasureIterations; i++)
        {
            foreach (var chunk in query2.Chunks())
            {
                _ = chunk.Count;
            }
        }
        sw2.Stop();

        var sw3 = Stopwatch.StartNew();
        for (int i = 0; i < MeasureIterations; i++)
        {
            foreach (var chunk in query3.Chunks())
            {
                _ = chunk.Count;
            }
        }
        sw3.Stop();

        // Measure all queries together
        var swAll = Stopwatch.StartNew();
        for (int i = 0; i < MeasureIterations; i++)
        {
            foreach (var chunk in query1.Chunks())
            {
                _ = chunk.Count;
            }
            foreach (var chunk in query2.Chunks())
            {
                _ = chunk.Count;
            }
            foreach (var chunk in query3.Chunks())
            {
                _ = chunk.Count;
            }
        }
        swAll.Stop();

        var sumOfIndividual = sw1.Elapsed + sw2.Elapsed + sw3.Elapsed;
        var allTogether = swAll.Elapsed;

        // Assert - Running queries together should be roughly the sum of individual times
        // (Allow higher variance for macOS due to different CPU scheduling and cache characteristics)
        var tolerance = PlatformTestHelper.IsMacOS ? 0.6 : 0.2;
        allTogether.TotalMilliseconds.Should().BeApproximately(
            sumOfIndividual.TotalMilliseconds, sumOfIndividual.TotalMilliseconds * tolerance,
            "Multiple queries should not have significant interference");
    }

    [Test]
    [TestCase(100)]
    [TestCase(1000)]
    [TestCase(10000)]
    public void BENCH_PerformanceRegression_QueryThroughput(int entityCount)
    {
        // This test establishes baseline performance metrics
        // If these fail, it indicates a performance regression

        // Adjust entity count for macOS performance characteristics
        var adjustedEntityCount = PlatformTestHelper.AdjustEntityCount(entityCount);

        for (int i = 0; i < adjustedEntityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i, i));
        }

        var query = _world.Query().With<Purlieu.Ecs.Core.Position>();

        // Warmup
        foreach (var chunk in query.Chunks())
        {
            _ = chunk.Count;
        }

        var sw = Stopwatch.StartNew();
        int iterations = 0;
        while (sw.Elapsed.TotalMilliseconds < 100) // Run for 100ms
        {
            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                for (int i = 0; i < positions.Length; i++)
                {
                    _ = positions[i].X;
                }
            }
            iterations++;
        }
        sw.Stop();

        var throughput = (adjustedEntityCount * iterations) / sw.Elapsed.TotalSeconds;

        // Assert minimum throughput (entities processed per second)
        // Adjust expectations for macOS which has slower CI runners
        var baseThroughput = entityCount switch
        {
            100 => 50_000_000,    // 50M entities/sec for small sets
            1000 => 100_000_000,  // 100M entities/sec for medium sets  
            10000 => 200_000_000, // 200M entities/sec for large sets
            _ => 0
        };

        // Adjust expectations for different environments
        var minimumThroughput = baseThroughput;
        if (PlatformTestHelper.IsMacOS)
        {
            // macOS performance is highly variable, adjust by entity count
            var macOsFactor = entityCount switch
            {
                100 => 0.1,    // Very small entity counts have high overhead on macOS
                1000 => 0.18,  // Medium entity counts perform better
                10000 => 0.25, // Large entity counts maintain reasonable throughput
                _ => 0.2
            };
            minimumThroughput = (int)(minimumThroughput * macOsFactor);
        }
        else if (PlatformTestHelper.IsCI && PlatformTestHelper.IsLinux)
        {
            // Linux CI environments have variable performance, adjust by entity count
            var factor = entityCount switch
            {
                100 => 0.13,   // Small entity counts have proportionally more overhead
                1000 => 0.4,   // Medium entity counts perform better
                10000 => 0.4,  // Large entity counts maintain good throughput
                _ => 0.4
            };
            minimumThroughput = (int)(minimumThroughput * factor);
        }
        else if (PlatformTestHelper.IsCI && PlatformTestHelper.IsWindows)
        {
            // Windows CI environments also show reduced performance
            var factor = entityCount switch
            {
                100 => 0.13,   // Small entity counts have high overhead in CI
                1000 => 0.78,  // Medium entity counts perform reasonably well
                10000 => 0.8,  // Large entity counts maintain good throughput
                _ => 0.8
            };
            minimumThroughput = (int)(minimumThroughput * factor);
        }

        throughput.Should().BeGreaterThan(minimumThroughput,
            $"Query throughput for {adjustedEntityCount} entities (adjusted from {entityCount} for {PlatformTestHelper.PlatformDescription}) " +
            $"should exceed {minimumThroughput:N0} entities/sec, but was {throughput:N0} entities/sec");
    }
}
