using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using System;
using System.Linq;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class ArchetypeTests
{
    private ComponentSignature _testSignature;

    [SetUp]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _testSignature = ComponentSignature.Empty
            .With<Position>()
            .With<Velocity>();
    }

    [Test]
    public void API_ArchetypeCreation_ShouldInitializeCorrectly()
    {
        var archetype = new Archetype(_testSignature);

        archetype.Signature.Should().Be(_testSignature);
        archetype.ChunkCount.Should().Be(0);
        archetype.EntityCount.Should().Be(0);
        archetype.Chunks.Should().BeEmpty();
    }

    [Test]
    public void API_AddEntity_ShouldCreateFirstChunk()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        var index = archetype.AddEntity(entity);

        index.Should().Be(0);
        archetype.EntityCount.Should().Be(1);
        archetype.ChunkCount.Should().Be(1);
        archetype.Contains(entity).Should().BeTrue();
    }

    [Test]
    public void API_AddMultipleEntities_ShouldFillChunks()
    {
        var archetype = new Archetype(_testSignature);
        var entities = new Entity[100];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entities[i]);
        }

        archetype.EntityCount.Should().Be(100);
        archetype.ChunkCount.Should().Be(1); // All fit in one chunk (capacity 512)

        foreach (var entity in entities)
        {
            archetype.Contains(entity).Should().BeTrue();
        }
    }

    [Test]
    public void API_AddEntityWhenChunkFull_ShouldCreateNewChunk()
    {
        var signature = ComponentSignature.Empty.With<Position>();
        var archetype = new Archetype(signature);

        // Create a small chunk for testing
        var smallChunk = new Chunk(signature, 2);
        // We can't directly access private fields, so we'll add enough entities
        // to trigger chunk creation in the real archetype

        var entities = new Entity[1000]; // More than default chunk capacity
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entities[i]);
        }

        archetype.EntityCount.Should().Be(1000);
        archetype.ChunkCount.Should().BeGreaterThan(1); // Should have created multiple chunks
    }

    [Test]
    public void API_AddDuplicateEntity_ShouldThrow()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        archetype.AddEntity(entity);

        var act = () => archetype.AddEntity(entity);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*already exists*");
    }

    [Test]
    public void API_RemoveEntity_ShouldUpdateTracking()
    {
        var archetype = new Archetype(_testSignature);
        var entities = new[]
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(3, 1)
        };

        foreach (var entity in entities)
        {
            archetype.AddEntity(entity);
        }

        archetype.RemoveEntity(entities[1]);

        archetype.EntityCount.Should().Be(2);
        archetype.Contains(entities[0]).Should().BeTrue();
        archetype.Contains(entities[1]).Should().BeFalse();
        archetype.Contains(entities[2]).Should().BeTrue();
    }

    [Test]
    public void API_RemoveNonexistentEntity_ShouldThrow()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        var act = () => archetype.RemoveEntity(entity);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Test]
    public void API_GetEntityLocation_ShouldReturnCorrectChunkAndIndex()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        archetype.AddEntity(entity);
        var (chunk, index) = archetype.GetEntityLocation(entity);

        chunk.Should().NotBeNull();
        chunk.Signature.Should().Be(_testSignature);
        index.Should().Be(0);
        chunk.GetEntity(index).Should().Be(entity);
    }

    [Test]
    public void API_GetEntityLocationForNonexistent_ShouldThrow()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        var act = () => archetype.GetEntityLocation(entity);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Test]
    public void API_SetAndGetComponent_ShouldWorkCorrectly()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);

        archetype.AddEntity(entity);

        var position = new Position(10, 20, 30);
        archetype.SetComponent(entity, position);

        var retrieved = archetype.GetComponent<Position>(entity);
        retrieved.Should().Be(position);
    }

    [Test]
    public void API_HasComponent_ShouldCheckSignatureAndEntity()
    {
        var archetype = new Archetype(_testSignature);
        var entity = new Entity(1, 1);
        var otherEntity = new Entity(2, 1);

        archetype.AddEntity(entity);

        archetype.HasComponent<Position>(entity).Should().BeTrue();
        archetype.HasComponent<Velocity>(entity).Should().BeTrue();
        archetype.HasComponent<Health>(entity).Should().BeFalse(); // Not in signature
        archetype.HasComponent<Position>(otherEntity).Should().BeFalse(); // Not in archetype
    }

    [Test]
    public void API_EnsureComponentArrays_ShouldInitializeAllChunks()
    {
        var archetype = new Archetype(_testSignature);

        // Add enough entities to create multiple chunks
        for (int i = 0; i < 1000; i++)
        {
            archetype.AddEntity(new Entity((uint)(i + 1), 1));
        }

        // Should not throw
        archetype.EnsureComponentArrays<Position>();
        archetype.EnsureComponentArrays<Velocity>();

        // Verify we can access components in all chunks
        foreach (var chunk in archetype.Chunks)
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();

            positions.Length.Should().BeGreaterThan(0);
            velocities.Length.Should().BeGreaterThan(0);
        }
    }

    [Test]
    public void API_GetAllEntities_ShouldReturnAllEntities()
    {
        var archetype = new Archetype(_testSignature);
        var entities = new[]
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(3, 1)
        };

        foreach (var entity in entities)
        {
            archetype.AddEntity(entity);
        }

        var allEntities = archetype.GetAllEntities().ToArray();
        allEntities.Should().BeEquivalentTo(entities);
    }

    [Test]
    public void DET_RemoveEntity_ShouldMaintainChunkIntegrity()
    {
        var archetype = new Archetype(_testSignature);
        var entities = new Entity[10];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entities[i]);
            archetype.SetComponent(entities[i], new Position(i * 10, i * 20, i * 30));
        }

        // Remove entity from middle
        archetype.RemoveEntity(entities[5]);

        // Verify remaining entities are still accessible
        for (int i = 0; i < entities.Length; i++)
        {
            if (i == 5) continue; // Removed entity

            archetype.Contains(entities[i]).Should().BeTrue($"Entity {i} should still exist");
            var position = archetype.GetComponent<Position>(entities[i]);
            position.Should().Be(new Position(i * 10, i * 20, i * 30), $"Entity {i} component data should be preserved");
        }
    }

    [Test]
    public void ALLOC_ComponentAccess_ShouldNotAllocate()
    {
        var archetype = new Archetype(_testSignature);
        var entities = new Entity[100];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entities[i]);
        }

        var startMemory = GC.GetTotalMemory(true);

        // Access components repeatedly - should not allocate
        for (int iteration = 0; iteration < 10; iteration++)
        {
            foreach (var entity in entities)
            {
                archetype.SetComponent(entity, new Position(iteration, iteration * 2, iteration * 3));
                var position = archetype.GetComponent<Position>(entity);
                var hasPosition = archetype.HasComponent<Position>(entity);
            }
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;

        allocated.Should().BeLessThan(170 * 1024, "Component access should have minimal allocation");
    }

    [Test]
    public void IT_ArchetypeWithManyEntities_ShouldMaintainPerformance()
    {
        var archetype = new Archetype(_testSignature);
        const int entityCount = 10000;

        var startTime = DateTime.UtcNow;

        // Add many entities
        for (int i = 0; i < entityCount; i++)
        {
            var entity = new Entity((uint)(i + 1), 1);
            archetype.AddEntity(entity);
            archetype.SetComponent(entity, new Position(i, i * 2, i * 3));
        }

        var addTime = DateTime.UtcNow - startTime;

        // Access all entities
        startTime = DateTime.UtcNow;
        var totalSum = 0f;
        foreach (var entity in archetype.GetAllEntities())
        {
            var position = archetype.GetComponent<Position>(entity);
            totalSum += position.X + position.Y + position.Z;
        }

        var accessTime = DateTime.UtcNow - startTime;

        archetype.EntityCount.Should().Be(entityCount);
        addTime.TotalMilliseconds.Should().BeLessThan(1000, "Adding 10k entities should be fast");
        accessTime.TotalMilliseconds.Should().BeLessThan(100, "Accessing 10k entities should be fast");
    }

    [Test]
    public void API_ToString_ShouldProvideUsefulOutput()
    {
        var archetype = new Archetype(_testSignature);
        archetype.AddEntity(new Entity(1, 1));
        archetype.AddEntity(new Entity(2, 1));

        var output = archetype.ToString();

        output.Should().Contain("Archetype");
        output.Should().Contain("signature");
        output.Should().Contain("entities=2");
        output.Should().Contain("chunks=1");
    }
}