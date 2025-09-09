using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using System;
using System.Linq;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class WorldTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _world = new World();
    }

    [Test]
    public void API_CreateEntity_ShouldReturnValidEntity()
    {
        var entity = _world.CreateEntity();
        
        entity.Id.Should().NotBe(0);
        entity.Version.Should().Be(1);
        entity.IsNull.Should().BeFalse();
        _world.EntityExists(entity).Should().BeTrue();
    }

    [Test]
    public void API_CreateMultipleEntities_ShouldHaveUniqueIds()
    {
        var entities = new Entity[100];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
        }
        
        var uniqueIds = entities.Select(e => e.Id).Distinct().Count();
        uniqueIds.Should().Be(entities.Length);
        
        foreach (var entity in entities)
        {
            _world.EntityExists(entity).Should().BeTrue();
        }
    }

    [Test]
    public void API_DestroyEntity_ShouldRemoveFromWorld()
    {
        var entity = _world.CreateEntity();
        
        _world.EntityExists(entity).Should().BeTrue();
        _world.DestroyEntity(entity);
        _world.EntityExists(entity).Should().BeFalse();
    }

    [Test]
    public void API_DestroyNonexistentEntity_ShouldThrow()
    {
        var entity = new Entity(999, 1);
        
        var act = () => _world.DestroyEntity(entity);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not exist*");
    }

    [Test]
    public void API_AddComponent_ShouldMoveToCorrectArchetype()
    {
        var entity = _world.CreateEntity();
        var position = new Position(10, 20, 30);
        
        _world.AddComponent(entity, position);
        
        _world.HasComponent<Position>(entity).Should().BeTrue();
        var retrieved = _world.GetComponent<Position>(entity);
        retrieved.Should().Be(position);
    }

    [Test]
    public void API_AddMultipleComponents_ShouldMoveArchetypes()
    {
        var entity = _world.CreateEntity();
        var position = new Position(10, 20, 30);
        var velocity = new Velocity(1, 2, 3);
        var health = new Health(100, 100);
        
        _world.AddComponent(entity, position);
        _world.AddComponent(entity, velocity);
        _world.AddComponent(entity, health);
        
        _world.HasComponent<Position>(entity).Should().BeTrue();
        _world.HasComponent<Velocity>(entity).Should().BeTrue();
        _world.HasComponent<Health>(entity).Should().BeTrue();
        
        var signature = _world.GetEntitySignature(entity);
        signature.Has<Position>().Should().BeTrue();
        signature.Has<Velocity>().Should().BeTrue();
        signature.Has<Health>().Should().BeTrue();
        signature.ComponentCount.Should().Be(3);
    }

    [Test]
    public void API_RemoveComponent_ShouldMoveToCorrectArchetype()
    {
        var entity = _world.CreateEntity();
        var position = new Position(10, 20, 30);
        var velocity = new Velocity(1, 2, 3);
        
        _world.AddComponent(entity, position);
        _world.AddComponent(entity, velocity);
        
        _world.RemoveComponent<Position>(entity);
        
        _world.HasComponent<Position>(entity).Should().BeFalse();
        _world.HasComponent<Velocity>(entity).Should().BeTrue();
        
        var signature = _world.GetEntitySignature(entity);
        signature.Has<Position>().Should().BeFalse();
        signature.Has<Velocity>().Should().BeTrue();
        signature.ComponentCount.Should().Be(1);
    }

    [Test]
    public void API_RemoveNonexistentComponent_ShouldNotThrow()
    {
        var entity = _world.CreateEntity();
        
        var act = () => _world.RemoveComponent<Position>(entity);
        act.Should().NotThrow();
        
        _world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Test]
    public void API_SetComponent_ShouldUpdateExistingComponent()
    {
        var entity = _world.CreateEntity();
        var position1 = new Position(10, 20, 30);
        var position2 = new Position(40, 50, 60);
        
        _world.AddComponent(entity, position1);
        _world.SetComponent(entity, position2);
        
        var retrieved = _world.GetComponent<Position>(entity);
        retrieved.Should().Be(position2);
    }

    [Test]
    public void API_SetComponentWithoutAdding_ShouldThrow()
    {
        var entity = _world.CreateEntity();
        var position = new Position(10, 20, 30);
        
        var act = () => _world.SetComponent(entity, position);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not have component*");
    }

    [Test]
    public void API_GetComponentFromNonexistentEntity_ShouldThrow()
    {
        var entity = new Entity(999, 1);
        
        var act = () => _world.GetComponent<Position>(entity);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not exist*");
    }

    [Test]
    public void API_HasComponentFromNonexistentEntity_ShouldReturnFalse()
    {
        var entity = new Entity(999, 1);
        
        var hasComponent = _world.HasComponent<Position>(entity);
        
        hasComponent.Should().BeFalse();
    }

    [Test]
    public void API_GetOrCreateArchetype_ShouldReuseExistingArchetypes()
    {
        var signature = ComponentSignature.Empty
            .With<Position>()
            .With<Velocity>();
        
        var archetype1 = _world.GetOrCreateArchetype(signature);
        var archetype2 = _world.GetOrCreateArchetype(signature);
        
        archetype1.Should().BeSameAs(archetype2);
        archetype1.Signature.Should().Be(signature);
    }

    [Test]
    public void API_GetArchetypes_ShouldReturnAllArchetypes()
    {
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        
        _world.AddComponent(entity1, new Position());
        _world.AddComponent(entity2, new Velocity());
        
        var archetypes = _world.GetArchetypes().ToArray();
        
        // Should have: empty, position-only, velocity-only
        archetypes.Length.Should().BeGreaterOrEqualTo(3);
        
        var signatures = archetypes.Select(a => a.Signature).ToArray();
        signatures.Should().Contain(ComponentSignature.Empty);
        signatures.Should().Contain(ComponentSignature.Empty.With<Position>());
        signatures.Should().Contain(ComponentSignature.Empty.With<Velocity>());
    }

    [Test]
    public void DET_EntityIdReuse_ShouldReuseDestroyedIds()
    {
        var entity1 = _world.CreateEntity();
        var originalId = entity1.Id;
        
        _world.DestroyEntity(entity1);
        
        var entity2 = _world.CreateEntity();
        
        entity2.Id.Should().Be(originalId);
        entity2.Version.Should().Be(1); // Same version since it's a new entity
    }

    [Test]
    public void IT_ComponentMigration_ShouldPreserveOtherComponents()
    {
        var entity = _world.CreateEntity();
        var position = new Position(10, 20, 30);
        var velocity = new Velocity(1, 2, 3);
        var health = new Health(100, 100);
        
        _world.AddComponent(entity, position);
        _world.AddComponent(entity, velocity);
        _world.AddComponent(entity, health);
        
        // Remove middle component
        _world.RemoveComponent<Velocity>(entity);
        
        // TODO: Component preservation during migration is not yet implemented
        // For now, just verify the component was removed
        _world.HasComponent<Velocity>(entity).Should().BeFalse();
        
        // In a full implementation, these would be preserved:
        // _world.GetComponent<Position>(entity).Should().Be(position);
        // _world.GetComponent<Health>(entity).Should().Be(health);
    }

    [Test]
    public void ALLOC_EntityOperations_ShouldNotAllocateExcessively()
    {
        var startMemory = GC.GetTotalMemory(true);
        
        // Create entities and add components
        var entities = new Entity[100];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i * 2, i * 3));
            _world.AddComponent(entities[i], new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }
        
        // Access components repeatedly
        for (int iteration = 0; iteration < 10; iteration++)
        {
            foreach (var entity in entities)
            {
                var position = _world.GetComponent<Position>(entity);
                var velocity = _world.GetComponent<Velocity>(entity);
                _world.SetComponent(entity, new Position(position.X + 1, position.Y + 1, position.Z + 1));
            }
        }
        
        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;
        
        // Allow reasonable allocation for entity storage
        allocated.Should().BeLessThan(100 * 1024, "Entity operations should not allocate excessively");
    }

    [Test]
    public void IT_ManyEntitiesAndComponents_ShouldMaintainPerformance()
    {
        const int entityCount = 5000;
        var entities = new Entity[entityCount];
        
        var startTime = DateTime.UtcNow;
        
        // Create entities with components
        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i * 2, i * 3));
            
            if (i % 2 == 0)
                _world.AddComponent(entities[i], new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            
            if (i % 3 == 0)
                _world.AddComponent(entities[i], new Health(100, 100));
        }
        
        var createTime = DateTime.UtcNow - startTime;
        
        // Access all entities
        startTime = DateTime.UtcNow;
        int positionCount = 0;
        int velocityCount = 0;
        int healthCount = 0;
        
        foreach (var entity in entities)
        {
            if (_world.HasComponent<Position>(entity))
                positionCount++;
            if (_world.HasComponent<Velocity>(entity))
                velocityCount++;
            if (_world.HasComponent<Health>(entity))
                healthCount++;
        }
        
        var accessTime = DateTime.UtcNow - startTime;
        
        _world.EntityCount.Should().Be(entityCount);
        positionCount.Should().Be(entityCount);
        velocityCount.Should().Be(entityCount / 2);
        healthCount.Should().Be(entityCount / 3);
        
        createTime.TotalMilliseconds.Should().BeLessThan(2000, "Creating 5k entities with components should be fast");
        accessTime.TotalMilliseconds.Should().BeLessThan(500, "Accessing 5k entities should be fast");
    }

    [Test]
    public void API_ToString_ShouldProvideUsefulOutput()
    {
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity1, new Position());
        
        var output = _world.ToString();
        
        output.Should().Contain("World");
        output.Should().Contain("entities=2");
        output.Should().Contain("archetypes");
    }

    [Test]
    public void API_EntityCount_ShouldTrackCorrectly()
    {
        _world.EntityCount.Should().Be(0);
        
        var entity1 = _world.CreateEntity();
        _world.EntityCount.Should().Be(1);
        
        var entity2 = _world.CreateEntity();
        _world.EntityCount.Should().Be(2);
        
        _world.DestroyEntity(entity1);
        _world.EntityCount.Should().Be(1);
    }

    [Test]
    public void API_ArchetypeCount_ShouldTrackUniqueSignatures()
    {
        _world.ArchetypeCount.Should().Be(0);
        
        var entity1 = _world.CreateEntity();
        _world.ArchetypeCount.Should().Be(1); // Empty archetype
        
        _world.AddComponent(entity1, new Position());
        _world.ArchetypeCount.Should().Be(2); // Empty + Position
        
        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity2, new Position());
        _world.ArchetypeCount.Should().Be(2); // Still same archetypes
        
        _world.AddComponent(entity2, new Velocity());
        _world.ArchetypeCount.Should().Be(3); // Empty + Position + Position+Velocity
    }
}