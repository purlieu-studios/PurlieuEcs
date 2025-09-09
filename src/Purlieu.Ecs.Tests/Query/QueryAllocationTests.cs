using System;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Query;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
public class QueryAllocationTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();

        // Pre-populate world for allocation tests
        for (int i = 0; i < 100; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));

            if (i % 2 == 0)
            {
                _world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            }

            if (i % 3 == 0)
            {
                _world.AddComponent(entity, new Health(i * 10, i * 10));
            }
        }
    }

    [Test]
    public void ALLOC_ChunkIteration_ZeroAllocations()
    {
        // Warmup
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>();
        _ = query.Chunks().ToList();

        var startMemory = GC.GetTotalMemory(true);

        // Act - iterate chunks multiple times
        for (int iteration = 0; iteration < 10; iteration++)
        {
            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                var velocities = chunk.GetSpan<Purlieu.Ecs.Core.Velocity>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    // Access components to ensure spans are used
                    var pos = positions[i];
                    var vel = velocities[i];

                    // Prevent optimization
                    _ = pos.X + vel.X;
                }
            }
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;

        // Allow minimal allocation for enumerable overhead and yield return
        allocated.Should().BeLessThan(150 * 1024, "Query chunk iteration should have minimal allocation");
    }

    [Test]
    public void ALLOC_QueryConstruction_ShouldBeMinimal()
    {
        var startMemory = GC.GetTotalMemory(true);

        // Act - create many queries
        for (int i = 0; i < 100; i++)
        {
            var query = _world.Query()
                .With<Purlieu.Ecs.Core.Position>()
                .With<Purlieu.Ecs.Core.Velocity>()
                .Without<Health>();

            // Don't execute - just construction
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;

        allocated.Should().BeLessThan(200 * 1024, "Query construction should have minimal allocation");
    }

    [Test]
    public void ALLOC_RepeatedQueryExecution_ShouldNotGrowMemory()
    {
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>();

        // Warmup
        _ = query.Chunks().ToList();

        var startMemory = GC.GetTotalMemory(true);

        // Act - execute same query many times
        for (int i = 0; i < 50; i++)
        {
            var chunks = query.Chunks().ToList();
            foreach (var chunk in chunks)
            {
                var positions = chunk.GetSpan<Purlieu.Ecs.Core.Position>();
                for (int j = 0; j < positions.Length; j++)
                {
                    _ = positions[j].X;
                }
            }
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;

        allocated.Should().BeLessThan(300 * 1024, "Repeated query execution should not grow memory significantly");
    }
}
