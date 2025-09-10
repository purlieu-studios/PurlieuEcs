using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
public class AdvancedQueryTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void QUERY_ChangedComponents_ShouldTrackDirtyEntities()
    {
        // Arrange - Create entities with Position components
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();

        _world.AddComponent(entity1, new Position(0, 0, 0));
        _world.AddComponent(entity2, new Position(10, 10, 10));

        // Act - Modify only entity1's position
        _world.SetComponent(entity1, new Position(5, 5, 5));

        // Query for entities with changed Position components
        var changedEntities = new System.Collections.Generic.List<Entity>();
        var query = _world.Query().Changed<Position>();

        foreach (var chunk in query.Chunks())
        {
            var entities = chunk.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                changedEntities.Add(entities[i]);
            }
        }

        // Assert - Only entity1 should be in the changed query
        changedEntities.Should().ContainSingle();
        changedEntities.Should().Contain(entity1);
        changedEntities.Should().NotContain(entity2);
    }

    [Test]
    public void QUERY_OptionalComponents_ShouldMatchPartialArchetypes()
    {
        // Arrange - Create entities with different component combinations
        var entityWithBoth = _world.CreateEntity();
        var entityWithPositionOnly = _world.CreateEntity();
        var entityWithVelocityOnly = _world.CreateEntity();

        _world.AddComponent(entityWithBoth, new Position(0, 0, 0));
        _world.AddComponent(entityWithBoth, new Velocity(1, 1, 1));

        _world.AddComponent(entityWithPositionOnly, new Position(10, 10, 10));

        _world.AddComponent(entityWithVelocityOnly, new Velocity(2, 2, 2));

        // Act - Query for entities with Position and optionally Velocity
        var matchedEntities = new System.Collections.Generic.List<Entity>();
        var query = _world.Query().With<Position>().Optional<Velocity>();

        foreach (var chunk in query.Chunks())
        {
            var entities = chunk.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                matchedEntities.Add(entities[i]);
            }
        }

        // Assert - Should match entities with Position (regardless of Velocity)
        matchedEntities.Should().HaveCount(2);
        matchedEntities.Should().Contain(entityWithBoth);
        matchedEntities.Should().Contain(entityWithPositionOnly);
        matchedEntities.Should().NotContain(entityWithVelocityOnly);
    }

    [Test]
    public void QUERY_ComplexQueries_WithAndWithoutOptional_ShouldBeZeroAlloc()
    {
        // Arrange - Create many entities for performance testing
        var entityCount = PlatformTestHelper.AdjustEntityCount(100);
        for (int i = 0; i < entityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));

            // Add Velocity to every other entity
            if (i % 2 == 0)
            {
                _world.AddComponent(entity, new Velocity(i, i, i));
            }
        }

        // Act - Execute complex query multiple times
        var startMemory = System.GC.GetTotalMemory(true);

        for (int iteration = 0; iteration < 10; iteration++)
        {
            var query = _world.Query()
                .With<Position>()
                .Optional<Velocity>()
                .Without<Health>();

            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Position>();
                for (int i = 0; i < positions.Length; i++)
                {
                    _ = positions[i].X; // Access data to ensure it's not optimized away
                }
            }
        }

        var endMemory = System.GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;

        // Assert - Should have minimal allocations
        allocated.Should().BeLessThan(50 * 1024, "Complex queries should have minimal allocation overhead");
    }

    [Test]
    public void QUERY_NextFrame_ShouldClearChangedFlags()
    {
        // Arrange - Create entity and modify component
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Position(0, 0, 0));
        _world.SetComponent(entity, new Position(5, 5, 5));

        // Verify entity appears in changed query
        var query = _world.Query().Changed<Position>();
        var initialMatches = query.Chunks().Count();
        initialMatches.Should().BeGreaterThan(0);

        // Act - Advance to next frame
        _world.NextFrame();

        // Assert - Entity should no longer appear in changed query
        var afterFrameMatches = query.Chunks().Count();
        afterFrameMatches.Should().Be(0);
    }

    [Test]
    public void IT_DynamicComponentSystems_ShouldScaleEfficiently()
    {
        // Arrange - Create entities with dynamic component patterns
        var entityCount = PlatformTestHelper.AdjustEntityCount(500);
        var entities = new Entity[entityCount];

        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));

            // Dynamic component assignment based on ID
            if (i % 3 == 0) _world.AddComponent(entities[i], new Velocity(1, 1, 1));
            if (i % 5 == 0) _world.AddComponent(entities[i], new Health(100, 100));
        }

        // Act - Simulate frame updates with component modifications
        var startTime = System.DateTime.UtcNow;

        for (int frame = 0; frame < 5; frame++)
        {
            // Modify some components
            for (int i = 0; i < entityCount / 10; i++)
            {
                var entity = entities[i];
                if (_world.HasComponent<Position>(entity))
                {
                    var pos = _world.GetComponent<Position>(entity);
                    _world.SetComponent(entity, new Position(pos.X + 1, pos.Y + 1, pos.Z + 1));
                }
            }

            // Query changed entities
            var changedQuery = _world.Query().Changed<Position>();
            var changedCount = 0;

            foreach (var chunk in changedQuery.Chunks())
            {
                changedCount += chunk.Count;
            }

            // Query with optional components
            var optionalQuery = _world.Query().With<Position>().Optional<Velocity>();
            var optionalCount = 0;

            foreach (var chunk in optionalQuery.Chunks())
            {
                optionalCount += chunk.Count;
            }

            _world.NextFrame();

            // Verify reasonable counts
            changedCount.Should().BeGreaterThan(0);
            optionalCount.Should().BeGreaterThan(0);
        }

        var endTime = System.DateTime.UtcNow;
        var executionTime = endTime - startTime;

        // Assert - Should execute efficiently
        var timeThreshold = PlatformTestHelper.IsLinux || PlatformTestHelper.IsWindows ? 1000 : 500;
        executionTime.TotalMilliseconds.Should().BeLessThan(timeThreshold,
            $"Dynamic component queries with {entityCount} entities should execute efficiently on {PlatformTestHelper.PlatformDescription}");
    }
}
