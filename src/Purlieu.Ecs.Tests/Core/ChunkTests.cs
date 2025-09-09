using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using System;
using System.Linq;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class ChunkTests
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
    public void API_ChunkCreation_ShouldInitializeCorrectly()
    {
        var chunk = new Chunk(_testSignature, 512);
        
        chunk.Signature.Should().Be(_testSignature);
        chunk.Capacity.Should().Be(512);
        chunk.Count.Should().Be(0);
        chunk.IsEmpty.Should().BeTrue();
        chunk.IsFull.Should().BeFalse();
    }

    [Test]
    public void API_AddEntity_ShouldIncreaseCount()
    {
        var chunk = new Chunk(_testSignature);
        var entity = new Entity(1, 1);
        
        var index = chunk.AddEntity(entity);
        
        index.Should().Be(0);
        chunk.Count.Should().Be(1);
        chunk.IsEmpty.Should().BeFalse();
        chunk.GetEntity(0).Should().Be(entity);
    }

    [Test]
    public void API_AddMultipleEntities_ShouldTrackCorrectly()
    {
        var chunk = new Chunk(_testSignature);
        var entities = new[]
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(3, 1)
        };
        
        foreach (var entity in entities)
        {
            chunk.AddEntity(entity);
        }
        
        chunk.Count.Should().Be(3);
        var retrievedEntities = chunk.GetEntities().ToArray();
        retrievedEntities.Should().BeEquivalentTo(entities);
    }

    [Test]
    public void API_AddEntityWhenFull_ShouldThrow()
    {
        var chunk = new Chunk(_testSignature, 2);
        chunk.AddEntity(new Entity(1, 1));
        chunk.AddEntity(new Entity(2, 1));
        
        chunk.IsFull.Should().BeTrue();
        
        var act = () => chunk.AddEntity(new Entity(3, 1));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Chunk is full");
    }

    [Test]
    public void API_RemoveEntity_ShouldSwapWithLast()
    {
        var chunk = new Chunk(_testSignature);
        var entities = new[]
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(3, 1)
        };
        
        foreach (var entity in entities)
        {
            chunk.AddEntity(entity);
        }
        
        // Remove middle entity
        chunk.RemoveEntity(1);
        
        chunk.Count.Should().Be(2);
        chunk.GetEntity(0).Should().Be(entities[0]); // First unchanged
        chunk.GetEntity(1).Should().Be(entities[2]); // Last moved to middle
    }

    [Test]
    public void API_RemoveInvalidIndex_ShouldThrow()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        
        var act1 = () => chunk.RemoveEntity(-1);
        var act2 = () => chunk.RemoveEntity(1);
        
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void API_FindEntity_ShouldReturnCorrectIndex()
    {
        var chunk = new Chunk(_testSignature);
        var entity1 = new Entity(1, 1);
        var entity2 = new Entity(2, 1);
        var entity3 = new Entity(3, 1);
        
        chunk.AddEntity(entity1);
        chunk.AddEntity(entity2);
        chunk.AddEntity(entity3);
        
        chunk.FindEntity(entity1).Should().Be(0);
        chunk.FindEntity(entity2).Should().Be(1);
        chunk.FindEntity(entity3).Should().Be(2);
        chunk.FindEntity(new Entity(999, 1)).Should().Be(-1);
    }

    [Test]
    public void API_GetSpan_ShouldCreateComponentArrayOnDemand()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        chunk.AddEntity(new Entity(2, 1));
        
        var positionSpan = chunk.GetSpan<Position>();
        
        positionSpan.Length.Should().Be(2);
        positionSpan[0].Should().Be(default(Position));
        positionSpan[1].Should().Be(default(Position));
    }

    [Test]
    public void API_GetSpanForInvalidComponent_ShouldThrow()
    {
        var chunk = new Chunk(_testSignature);
        
        Action act = () => chunk.GetSpan<Health>();
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Health*not part of this chunk's signature*");
    }

    [Test]
    public void API_SetAndGetComponent_ShouldWorkCorrectly()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        
        var position = new Position(10, 20, 30);
        chunk.SetComponent(0, position);
        
        var retrieved = chunk.GetComponent<Position>(0);
        retrieved.Should().Be(position);
    }

    [Test]
    public void API_GetComponentInvalidIndex_ShouldThrow()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        
        var act1 = () => chunk.GetComponent<Position>(-1);
        var act2 = () => chunk.GetComponent<Position>(1);
        
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void API_SetComponentInvalidIndex_ShouldThrow()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        
        var act1 = () => chunk.SetComponent(-1, new Position());
        var act2 = () => chunk.SetComponent(1, new Position());
        
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void API_EnsureComponentArray_ShouldPreallocateArray()
    {
        var chunk = new Chunk(_testSignature);
        
        // Should not throw
        chunk.EnsureComponentArray<Position>();
        chunk.EnsureComponentArray<Velocity>();
        
        Action act = () => chunk.EnsureComponentArray<Health>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ALLOC_ComponentAccess_ShouldUseSpans()
    {
        var chunk = new Chunk(_testSignature);
        for (int i = 0; i < 100; i++)
        {
            chunk.AddEntity(new Entity((uint)(i + 1), 1));
        }
        
        var startMemory = GC.GetTotalMemory(true);
        
        // Access components using spans - should not allocate
        for (int iteration = 0; iteration < 10; iteration++)
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();
            
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Position(i, i * 2, i * 3);
                velocities[i] = new Velocity(i * 0.1f, i * 0.2f, i * 0.3f);
            }
        }
        
        var endMemory = GC.GetTotalMemory(false);
        var allocated = endMemory - startMemory;
        
        allocated.Should().BeLessThan(50 * 1024, "Span-based component access should have minimal allocation");
    }

    [Test]
    public void DET_RemoveEntityPattern_ShouldBeConsistent()
    {
        var chunk = new Chunk(_testSignature);
        var entities = new[]
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(3, 1),
            new Entity(4, 1)
        };
        
        foreach (var entity in entities)
        {
            chunk.AddEntity(entity);
        }
        
        // Set component data
        for (int i = 0; i < entities.Length; i++)
        {
            chunk.SetComponent(i, new Position(i * 10, i * 20, i * 30));
        }
        
        // Remove entity at index 1 (entity 2)
        chunk.RemoveEntity(1);
        
        // Verify swap behavior
        chunk.Count.Should().Be(3);
        chunk.GetEntity(0).Should().Be(entities[0]); // Unchanged
        chunk.GetEntity(1).Should().Be(entities[3]); // Last entity moved here
        chunk.GetEntity(2).Should().Be(entities[2]); // Unchanged
        
        // Verify component data moved correctly
        chunk.GetComponent<Position>(1).Should().Be(new Position(30, 60, 90)); // Data from entity 4
    }

    [Test]
    public void IT_ChunkWithDefaultCapacity_ShouldUse512()
    {
        var chunk = new Chunk(_testSignature);
        
        chunk.Capacity.Should().Be(Chunk.DefaultCapacity);
        chunk.Capacity.Should().Be(512);
    }

    [Test]
    public void API_ToString_ShouldProvideUsefulOutput()
    {
        var chunk = new Chunk(_testSignature);
        chunk.AddEntity(new Entity(1, 1));
        
        var output = chunk.ToString();
        
        output.Should().Contain("Chunk");
        output.Should().Contain("signature");
        output.Should().Contain("count=1");
        output.Should().Contain("512");
    }
}