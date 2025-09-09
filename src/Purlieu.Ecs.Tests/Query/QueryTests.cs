using System;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Query;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
public class QueryTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void API_QueryCreation_ShouldReturnValidQuery()
    {
        var query = _world.Query();

        query.Should().NotBeNull();
        query.Should().BeAssignableTo<IQuery>();
    }

    [Test]
    public void API_QueryWithSingleComponent_ShouldReturnCorrectChunks()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        var entity3 = _world.CreateEntity();

        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Position(1, 2, 3));
        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Position(4, 5, 6));
        _world.AddComponent(entity3, new Purlieu.Ecs.Core.Velocity(1f, 2f, 3f)); // Different component

        // Act
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>();
        var chunks = query.Chunks().ToList();

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Count.Should().Be(2);

        var positions = chunks[0].GetSpan<Purlieu.Ecs.Core.Position>();
        positions.Length.Should().Be(2);
    }

    [Test]
    public void API_QueryWithoutSingleComponent_ShouldExcludeEntities()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        var entity3 = _world.CreateEntity();

        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Position(1, 2, 3));
        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Position(4, 5, 6));
        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Velocity(1f, 2f, 3f)); // entity2 has both
        _world.AddComponent(entity3, new Purlieu.Ecs.Core.Velocity(4f, 5f, 6f)); // entity3 only velocity

        // Act - query for Position but not Velocity
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>().Without<Purlieu.Ecs.Core.Velocity>();
        var chunks = query.Chunks().ToList();

        // Assert - should only find entity1
        chunks.Should().HaveCount(1);
        chunks[0].Count.Should().Be(1);
    }

    [Test]
    public void API_QueryMultipleWith_ShouldRequireAllComponents()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        var entity3 = _world.CreateEntity();

        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Position(1, 2, 3));
        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Velocity(1f, 2f, 3f));

        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Position(4, 5, 6));
        // entity2 missing Velocity

        _world.AddComponent(entity3, new Purlieu.Ecs.Core.Velocity(7f, 8f, 9f));
        // entity3 missing Position

        // Act
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>();
        var chunks = query.Chunks().ToList();

        // Assert - only entity1 has both components
        chunks.Should().HaveCount(1);
        chunks[0].Count.Should().Be(1);
    }

    [Test]
    public void API_QueryChaining_ShouldComposeFilters()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        var entity3 = _world.CreateEntity();

        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Position(1, 2, 3));
        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Velocity(1f, 2f, 3f));

        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Position(4, 5, 6));
        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Velocity(4f, 5f, 6f));
        _world.AddComponent(entity2, new Health(100, 100));

        _world.AddComponent(entity3, new Purlieu.Ecs.Core.Position(7, 8, 9));

        // Act - want Position and Velocity, but not Health
        var query = _world.Query()
            .With<Purlieu.Ecs.Core.Position>()
            .With<Purlieu.Ecs.Core.Velocity>()
            .Without<Health>();

        var chunks = query.Chunks().ToList();

        // Assert - only entity1 matches (has Pos+Vel, no Health)
        chunks.Should().HaveCount(1);
        chunks[0].Count.Should().Be(1);
    }

    [Test]
    public void IT_QueryWithWithout_FiltersCorrectly()
    {
        // Arrange - complex scenario with multiple archetypes
        var entities = new Entity[10];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
        }

        // Archetype 1: Position only (entities 0-2)
        for (int i = 0; i < 3; i++)
        {
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Position(i, i, i));
        }

        // Archetype 2: Position + Velocity (entities 3-5)
        for (int i = 3; i < 6; i++)
        {
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Position(i, i, i));
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Velocity(i, i, i));
        }

        // Archetype 3: Position + Velocity + Health (entities 6-8)
        for (int i = 6; i < 9; i++)
        {
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Position(i, i, i));
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Velocity(i, i, i));
            _world.AddComponent(entities[i], new Health(i * 10, i * 10));
        }

        // Archetype 4: Velocity only (entity 9)
        _world.AddComponent(entities[9], new Purlieu.Ecs.Core.Velocity(9, 9, 9));

        // Act & Assert different queries

        // Query 1: All entities with Position (should be 0-8)
        var positionQuery = _world.Query().With<Purlieu.Ecs.Core.Position>();
        var positionChunks = positionQuery.Chunks().ToList();
        var positionCount = positionChunks.Sum(c => c.Count);
        positionCount.Should().Be(9);

        // Query 2: Entities with Position and Velocity (should be 3-8)
        var posVelQuery = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>();
        var posVelChunks = posVelQuery.Chunks().ToList();
        var posVelCount = posVelChunks.Sum(c => c.Count);
        posVelCount.Should().Be(6);

        // Query 3: Entities with Position but without Health (should be 0-5)
        var posNoHealthQuery = _world.Query().With<Purlieu.Ecs.Core.Position>().Without<Health>();
        var posNoHealthChunks = posNoHealthQuery.Chunks().ToList();
        var posNoHealthCount = posNoHealthChunks.Sum(c => c.Count);
        posNoHealthCount.Should().Be(6);
    }

    [Test]
    public void DET_QueryResults_ConsistentAcrossRuns()
    {
        // Arrange - create deterministic scenario
        var entities = new Entity[5];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));

            if (i % 2 == 0)
            {
                _world.AddComponent(entities[i], new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            }
        }

        // Act - run query multiple times
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>().With<Purlieu.Ecs.Core.Velocity>();

        var results1 = query.Chunks().ToList();
        var results2 = query.Chunks().ToList();
        var results3 = query.Chunks().ToList();

        // Assert - results should be identical
        results1.Should().HaveCount(results2.Count);
        results1.Should().HaveCount(results3.Count);

        var count1 = results1.Sum(c => c.Count);
        var count2 = results2.Sum(c => c.Count);
        var count3 = results3.Sum(c => c.Count);

        count1.Should().Be(count2);
        count1.Should().Be(count3);
        count1.Should().Be(3); // entities 0, 2, 4 have both components
    }

    [Test]
    public void API_EmptyQuery_ShouldReturnAllNonEmptyChunks()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity1, new Purlieu.Ecs.Core.Position(1, 2, 3));
        _world.AddComponent(entity2, new Purlieu.Ecs.Core.Velocity(1f, 2f, 3f));

        // Act - query with no filters
        var query = _world.Query();
        var chunks = query.Chunks().ToList();

        // Assert - should return all chunks with entities
        chunks.Should().HaveCount(2); // Two different archetypes
        chunks.Sum(c => c.Count).Should().Be(2);
    }

    [Test]
    public void API_QueryOnEmptyWorld_ShouldReturnNoChunks()
    {
        // Act
        var query = _world.Query().With<Purlieu.Ecs.Core.Position>();
        var chunks = query.Chunks().ToList();

        // Assert
        chunks.Should().BeEmpty();
    }
}
