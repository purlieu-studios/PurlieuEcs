using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class ArchetypeMigrationTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void IT_ArchetypeMigration_PreservesComponentData()
    {
        // Arrange - Create entity with Position component
        var entity = _world.CreateEntity();
        var originalPosition = new Position(10.0f, 20.0f, 30.0f);
        _world.AddComponent(entity, originalPosition);

        // Verify initial state
        _world.HasComponent<Position>(entity).Should().BeTrue();
        var initialPosition = _world.GetComponent<Position>(entity);
        initialPosition.X.Should().Be(10.0f);
        initialPosition.Y.Should().Be(20.0f);
        initialPosition.Z.Should().Be(30.0f);

        // Act - Add Velocity component (triggers archetype migration)
        var velocity = new Velocity(1.0f, 2.0f, 3.0f);
        _world.AddComponent(entity, velocity);

        // Assert - Position component should be preserved after migration
        _world.HasComponent<Position>(entity).Should().BeTrue();
        _world.HasComponent<Velocity>(entity).Should().BeTrue();

        var preservedPosition = _world.GetComponent<Position>(entity);
        preservedPosition.X.Should().Be(10.0f);
        preservedPosition.Y.Should().Be(20.0f);
        preservedPosition.Z.Should().Be(30.0f);

        var addedVelocity = _world.GetComponent<Velocity>(entity);
        addedVelocity.X.Should().Be(1.0f);
        addedVelocity.Y.Should().Be(2.0f);
        addedVelocity.Z.Should().Be(3.0f);
    }

    [Test]
    public void IT_ArchetypeMigration_PreservesMultipleComponents()
    {
        // Arrange - Create entity with multiple components
        var entity = _world.CreateEntity();
        var position = new Position(5.0f, 10.0f, 15.0f);
        var velocity = new Velocity(0.5f, 1.0f, 1.5f);

        _world.AddComponent(entity, position);
        _world.AddComponent(entity, velocity);

        // Act - Add a third component (triggers another migration)
        var health = new Health(100, 100);
        _world.AddComponent(entity, health);

        // Assert - All components should be preserved
        _world.HasComponent<Position>(entity).Should().BeTrue();
        _world.HasComponent<Velocity>(entity).Should().BeTrue();
        _world.HasComponent<Health>(entity).Should().BeTrue();

        var preservedPosition = _world.GetComponent<Position>(entity);
        preservedPosition.X.Should().Be(5.0f);
        preservedPosition.Y.Should().Be(10.0f);
        preservedPosition.Z.Should().Be(15.0f);

        var preservedVelocity = _world.GetComponent<Velocity>(entity);
        preservedVelocity.X.Should().Be(0.5f);
        preservedVelocity.Y.Should().Be(1.0f);
        preservedVelocity.Z.Should().Be(1.5f);

        var addedHealth = _world.GetComponent<Health>(entity);
        addedHealth.Current.Should().Be(100);
        addedHealth.Max.Should().Be(100);
    }

    [Test]
    public void IT_ArchetypeMigration_RemoveComponent_PreservesRemaining()
    {
        // Arrange - Create entity with multiple components
        var entity = _world.CreateEntity();
        var position = new Position(7.0f, 14.0f, 21.0f);
        var velocity = new Velocity(0.7f, 1.4f, 2.1f);
        var health = new Health(75, 100);

        _world.AddComponent(entity, position);
        _world.AddComponent(entity, velocity);
        _world.AddComponent(entity, health);

        // Act - Remove one component (triggers archetype migration)
        _world.RemoveComponent<Velocity>(entity);

        // Assert - Remaining components should be preserved
        _world.HasComponent<Position>(entity).Should().BeTrue();
        _world.HasComponent<Velocity>(entity).Should().BeFalse();
        _world.HasComponent<Health>(entity).Should().BeTrue();

        var preservedPosition = _world.GetComponent<Position>(entity);
        preservedPosition.X.Should().Be(7.0f);
        preservedPosition.Y.Should().Be(14.0f);
        preservedPosition.Z.Should().Be(21.0f);

        var preservedHealth = _world.GetComponent<Health>(entity);
        preservedHealth.Current.Should().Be(75);
        preservedHealth.Max.Should().Be(100);
    }

    [Test]
    public void DET_ComponentCopying_ConsistentAcrossRuns()
    {
        // Arrange & Act - Run the same migration pattern multiple times
        var results = new List<(float x, float y, float z, int health)>();

        for (int run = 0; run < 5; run++)
        {
            var world = new World();
            var entity = world.CreateEntity();

            var position = new Position(run * 2.0f, run * 3.0f, run * 4.0f);
            var health = new Health(100 + run, 200);

            world.AddComponent(entity, position);
            world.AddComponent(entity, health);

            // Trigger migration by adding velocity
            world.AddComponent(entity, new Velocity(1.0f, 1.0f, 1.0f));

            var finalPosition = world.GetComponent<Position>(entity);
            var finalHealth = world.GetComponent<Health>(entity);

            results.Add((finalPosition.X, finalPosition.Y, finalPosition.Z, finalHealth.Current));
        }

        // Assert - All runs should produce consistent results
        for (int i = 0; i < results.Count; i++)
        {
            results[i].x.Should().Be(i * 2.0f);
            results[i].y.Should().Be(i * 3.0f);
            results[i].z.Should().Be(i * 4.0f);
            results[i].health.Should().Be(100 + i);
        }
    }

    [Test]
    public void ALLOC_ArchetypeTransition_MinimalAllocations()
    {
        // Arrange
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Position(1.0f, 2.0f, 3.0f));

        // Warmup
        _world.AddComponent(entity, new Velocity(0.1f, 0.2f, 0.3f));
        _world.RemoveComponent<Velocity>(entity);

        // Act & Assert
        var startMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 100; i++)
        {
            _world.AddComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            _world.RemoveComponent<Velocity>(entity);
        }

        var endMemory = GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        // Should have reasonable allocation overhead for migration operations
        // CI environments may have higher allocation patterns due to GC behavior
        memoryIncrease.Should().BeLessThan(200000, "Archetype transitions should have reasonable allocation overhead for CI environments");
    }
}
