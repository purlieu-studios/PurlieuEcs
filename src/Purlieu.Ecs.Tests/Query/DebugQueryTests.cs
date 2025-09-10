using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
public class DebugQueryTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void DEBUG_ChangeTracking_SimpleCase()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();

        _world.AddComponent(entity1, new Position(0, 0, 0));
        _world.AddComponent(entity2, new Position(10, 10, 10));

        Console.WriteLine("After adding components:");
        Console.WriteLine($"Entity1 has Position: {_world.HasComponent<Position>(entity1)}");
        Console.WriteLine($"Entity2 has Position: {_world.HasComponent<Position>(entity2)}");

        // Act - Modify only entity1
        _world.SetComponent(entity1, new Position(5, 5, 5));

        Console.WriteLine("After modifying entity1:");

        // Debug: Check which entities are in regular query
        var allEntities = new System.Collections.Generic.List<Entity>();
        var regularQuery = _world.Query().With<Position>();
        foreach (var chunk in regularQuery.Chunks())
        {
            var entities = chunk.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                allEntities.Add(entities[i]);
            }
        }
        Console.WriteLine($"Regular query found {allEntities.Count} entities: {string.Join(", ", allEntities)}");

        // Debug: Check which entities are in changed query
        var changedEntities = new System.Collections.Generic.List<Entity>();
        var changedQuery = _world.Query().Changed<Position>();
        foreach (var chunk in changedQuery.Chunks())
        {
            var entities = chunk.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                changedEntities.Add(entities[i]);
            }
        }
        Console.WriteLine($"Changed query found {changedEntities.Count} entities: {string.Join(", ", changedEntities)}");

        // Assert
        changedEntities.Should().HaveCount(1);
        changedEntities.Should().Contain(entity1);
    }
}
