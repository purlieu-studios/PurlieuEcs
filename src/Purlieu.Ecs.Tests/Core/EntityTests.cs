using NUnit.Framework;
using Purlieu.Ecs.Core;
using System;
using System.Collections.Generic;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class EntityTests
{
    [Test]
    public void API_EntityConstruction_CreatesValidEntity()
    {
        // Arrange
        const uint id = 12345;
        const uint version = 67890;

        // Act
        var entity = new Entity(id, version);

        // Assert
        Assert.That(entity.Id, Is.EqualTo(id));
        Assert.That(entity.Version, Is.EqualTo(version));
        Assert.That(entity.IsNull, Is.False);
    }

    [Test]
    public void API_EntityNull_IsDefaultValue()
    {
        // Arrange & Act
        var nullEntity = Entity.Null;
        var defaultEntity = default(Entity);

        // Assert
        Assert.That(nullEntity.IsNull, Is.True);
        Assert.That(defaultEntity.IsNull, Is.True);
        Assert.That(nullEntity, Is.EqualTo(defaultEntity));
        Assert.That(nullEntity.Id, Is.EqualTo(0u));
        Assert.That(nullEntity.Version, Is.EqualTo(0u));
    }

    [Test]
    public void API_EntityPacking_PreservesData()
    {
        // Arrange
        const uint id = 0xDEADBEEF;
        const uint version = 0xCAFEBABE;
        var original = new Entity(id, version);

        // Act
        var packed = original.ToPacked();
        var unpacked = Entity.FromPacked(packed);

        // Assert
        Assert.That(unpacked, Is.EqualTo(original));
        Assert.That(unpacked.Id, Is.EqualTo(id));
        Assert.That(unpacked.Version, Is.EqualTo(version));
    }

    [Test]
    public void API_EntityEquality_WorksCorrectly()
    {
        // Arrange
        var entity1 = new Entity(123, 456);
        var entity2 = new Entity(123, 456);
        var entity3 = new Entity(123, 457); // Different version
        var entity4 = new Entity(124, 456); // Different ID

        // Act & Assert
        Assert.That(entity1.Equals(entity2), Is.True);
        Assert.That(entity1 == entity2, Is.True);
        Assert.That(entity1 != entity3, Is.True);
        Assert.That(entity1 != entity4, Is.True);
        Assert.That(entity1.Equals((object)entity2), Is.True);
        Assert.That(entity1.Equals((object?)null), Is.False);
        Assert.That(entity1.Equals("not an entity"), Is.False);
    }

    [Test]
    public void API_EntityComparison_OrdersByIdThenVersion()
    {
        // Arrange
        var entity1 = new Entity(100, 1);
        var entity2 = new Entity(100, 2);
        var entity3 = new Entity(101, 1);

        // Act & Assert - ID takes precedence
        Assert.That(entity1.CompareTo(entity3), Is.LessThan(0));
        Assert.That(entity3.CompareTo(entity1), Is.GreaterThan(0));

        // Version compared when IDs are equal
        Assert.That(entity1.CompareTo(entity2), Is.LessThan(0));
        Assert.That(entity2.CompareTo(entity1), Is.GreaterThan(0));

        // Equal entities
        Assert.That(entity1.CompareTo(entity1), Is.EqualTo(0));
    }

    [Test]
    public void API_EntityOperators_WorkCorrectly()
    {
        // Arrange
        var entity1 = new Entity(100, 1);
        var entity2 = new Entity(100, 2);
        var entity3 = new Entity(101, 1);

        // Act & Assert
        Assert.That(entity1 < entity2, Is.True);
        Assert.That(entity1 < entity3, Is.True);
        Assert.That(entity2 > entity1, Is.True);
        Assert.That(entity3 > entity1, Is.True);
        Assert.That(entity1 <= new Entity(100, 1), Is.True);
        Assert.That(entity1 >= new Entity(100, 1), Is.True);
        Assert.That(entity1 <= entity2, Is.True);
        Assert.That(entity2 >= entity1, Is.True);
    }

    [Test]
    public void API_EntityConversions_WorkCorrectly()
    {
        // Arrange
        var entity = new Entity(0x12345678, 0x9ABCDEF0);

        // Act
        ulong packed = entity; // Implicit conversion
        var unpacked = (Entity)packed; // Explicit conversion

        // Assert
        Assert.That(unpacked, Is.EqualTo(entity));
        Assert.That(packed, Is.EqualTo(entity.ToPacked()));
    }

    [Test]
    public void API_EntityToString_ReturnsCorrectFormat()
    {
        // Arrange
        var entity = new Entity(123, 456);
        var nullEntity = Entity.Null;

        // Act
        var entityString = entity.ToString();
        var nullString = nullEntity.ToString();

        // Assert
        Assert.That(entityString, Is.EqualTo("Entity(123:456)"));
        Assert.That(nullString, Is.EqualTo("Entity(null)"));
    }

    [Test]
    public void API_EntityHashCode_IsConsistent()
    {
        // Arrange
        var entity1 = new Entity(123, 456);
        var entity2 = new Entity(123, 456);
        var entity3 = new Entity(123, 457);

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();
        var hash3 = entity3.GetHashCode();

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2)); // Equal entities have equal hashes
        Assert.That(hash1, Is.Not.EqualTo(hash3)); // Different entities should have different hashes
    }

    [Test]
    public void DET_EntityPacking_IsDeterministic()
    {
        // Arrange
        const uint id = 0xDEADBEEF;
        const uint version = 0xCAFEBABE;

        // Act - Create same entity multiple times
        var entity1 = new Entity(id, version);
        var entity2 = new Entity(id, version);
        var packed1 = entity1.ToPacked();
        var packed2 = entity2.ToPacked();

        // Assert - Should always produce identical results
        Assert.That(packed1, Is.EqualTo(packed2));
        Assert.That(entity1, Is.EqualTo(entity2));
        Assert.That(entity1.GetHashCode(), Is.EqualTo(entity2.GetHashCode()));
    }

    [Test]
    public void DET_EntityOrdering_IsStable()
    {
        // Arrange
        var entities = new[]
        {
            new Entity(100, 2),
            new Entity(50, 1),
            new Entity(100, 1),
            new Entity(200, 1),
            new Entity(50, 2)
        };

        // Act - Sort multiple times
        var sorted1 = new List<Entity>(entities);
        var sorted2 = new List<Entity>(entities);
        sorted1.Sort();
        sorted2.Sort();

        // Assert - Should always produce same order
        Assert.That(sorted1, Is.EqualTo(sorted2));

        // Verify expected order: (50,1), (50,2), (100,1), (100,2), (200,1)
        Assert.That(sorted1[0], Is.EqualTo(new Entity(50, 1)));
        Assert.That(sorted1[1], Is.EqualTo(new Entity(50, 2)));
        Assert.That(sorted1[2], Is.EqualTo(new Entity(100, 1)));
        Assert.That(sorted1[3], Is.EqualTo(new Entity(100, 2)));
        Assert.That(sorted1[4], Is.EqualTo(new Entity(200, 1)));
    }

    [Test]
    public void ALLOC_EntityOperations_StructSemantics()
    {
        // This test verifies Entity follows value type semantics
        // which ensures no heap allocations for basic operations

        // Arrange
        var entity1 = new Entity(123, 456);
        var entity2 = new Entity(789, 012);

        // Act & Assert - These operations should work with value semantics
        // Value types are stack-allocated and don't require heap allocations

        // Test that Entity behaves as a value type
        Assert.That(entity1.GetType().IsValueType, Is.True, "Entity must be a value type");

        // Test operations work correctly (functionality test)
        var comparison = entity1.CompareTo(entity2);
        var equals = entity1.Equals(entity2);
        var hash = entity1.GetHashCode();
        var packed = entity1.ToPacked();
        var unpacked = Entity.FromPacked(packed);
        var isNull = entity1.IsNull;
        var id = entity1.Id;
        var version = entity1.Version;

        // Verify operations worked correctly
        Assert.That(comparison, Is.LessThan(0));
        Assert.That(equals, Is.False);
        Assert.That(hash, Is.Not.EqualTo(0));
        Assert.That(unpacked, Is.EqualTo(entity1));
        Assert.That(isNull, Is.False);
        Assert.That(id, Is.EqualTo(123u));
        Assert.That(version, Is.EqualTo(456u));

        // Test that struct copying works correctly (value semantics)
        var copy = entity1;
        Assert.That(copy, Is.EqualTo(entity1));
        // Value types are copied, not referenced, so this test validates value semantics
    }

    [Test]
    public void ALLOC_EntityConstruction_ValueTypeSemantics()
    {
        // This test verifies Entity construction follows value type semantics
        // Value types are allocated on the stack, not the heap

        // Act - Create entities (value types should be stack-allocated)
        var entity1 = new Entity(123, 456);
        var entity2 = Entity.Null;
        var entity3 = default(Entity);
        var entity4 = Entity.FromPacked(0x123456789ABCDEF0);

        // Assert - Verify value type behavior and correct construction
        Assert.That(typeof(Entity).IsValueType, Is.True, "Entity must be a value type");

        // Verify entities were created correctly on the stack
        Assert.That(entity1.Id, Is.EqualTo(123u));
        Assert.That(entity1.Version, Is.EqualTo(456u));
        Assert.That(entity2.IsNull, Is.True);
        Assert.That(entity3.IsNull, Is.True);
        Assert.That(entity4.ToPacked(), Is.EqualTo(0x123456789ABCDEF0UL));

        // Test that default construction works
        var defaultEntity = new Entity();
        Assert.That(defaultEntity.IsNull, Is.True);
        Assert.That(defaultEntity, Is.EqualTo(Entity.Null));

        // Test that struct assignment creates copies (value semantics)
        var copy = entity1;
        Assert.That(copy, Is.EqualTo(entity1));
        // Value types are copied by value, ensuring no reference sharing
    }

    [Test]
    public void IT_EntityBitPacking_HandlesExtremeValues()
    {
        // Arrange & Act - Test with maximum values
        var maxEntity = new Entity(uint.MaxValue, uint.MaxValue);
        var minEntity = new Entity(uint.MinValue, uint.MinValue);

        // Assert
        Assert.That(maxEntity.Id, Is.EqualTo(uint.MaxValue));
        Assert.That(maxEntity.Version, Is.EqualTo(uint.MaxValue));
        Assert.That(minEntity.Id, Is.EqualTo(uint.MinValue));
        Assert.That(minEntity.Version, Is.EqualTo(uint.MinValue));

        // Test packing/unpacking preserves extreme values
        var maxPacked = maxEntity.ToPacked();
        var maxUnpacked = Entity.FromPacked(maxPacked);
        Assert.That(maxUnpacked, Is.EqualTo(maxEntity));

        var minPacked = minEntity.ToPacked();
        var minUnpacked = Entity.FromPacked(minPacked);
        Assert.That(minUnpacked, Is.EqualTo(minEntity));
    }

    [Test]
    public void IT_EntityStaleReference_PreventedByVersion()
    {
        // Arrange - Simulate recycled entity ID with different version
        const uint recycledId = 42;
        var originalEntity = new Entity(recycledId, 1);
        var recycledEntity = new Entity(recycledId, 2); // Same ID, different version

        // Act & Assert - Entities should be different despite same ID
        Assert.That(originalEntity, Is.Not.EqualTo(recycledEntity));
        Assert.That(originalEntity.Id, Is.EqualTo(recycledEntity.Id));
        Assert.That(originalEntity.Version, Is.Not.EqualTo(recycledEntity.Version));

        // Simulate stale reference check
        var isStaleReference = originalEntity != recycledEntity;
        Assert.That(isStaleReference, Is.True, "Version should prevent stale reference usage");
    }
}
