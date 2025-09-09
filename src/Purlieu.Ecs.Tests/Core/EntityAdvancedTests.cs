using AutoFixture;
using Bogus;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class EntityAdvancedTests
{
    private IFixture _fixture;
    private Faker _faker;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _faker = new Faker();
    }

    [Test]
    public void API_EntityEquality_ShouldBeReflexive()
    {
        // Arrange
        var entity = new Entity(_faker.Random.UInt(), _faker.Random.UInt());

        // Act & Assert
        entity.Should().Be(entity);
        entity.Equals(entity).Should().BeTrue();
        (entity == entity).Should().BeTrue();
        (entity != entity).Should().BeFalse();
    }

    [Test]
    public void API_EntityEquality_ShouldBeSymmetric()
    {
        // Arrange
        var id = _faker.Random.UInt();
        var version = _faker.Random.UInt();
        var entity1 = new Entity(id, version);
        var entity2 = new Entity(id, version);

        // Act & Assert
        entity1.Should().Be(entity2);
        entity2.Should().Be(entity1);
        entity1.Equals(entity2).Should().Be(entity2.Equals(entity1));
        (entity1 == entity2).Should().Be(entity2 == entity1);
    }

    [Test]
    public void API_EntityEquality_ShouldBeTransitive()
    {
        // Arrange
        var id = _faker.Random.UInt();
        var version = _faker.Random.UInt();
        var entity1 = new Entity(id, version);
        var entity2 = new Entity(id, version);
        var entity3 = new Entity(id, version);

        // Act & Assert
        entity1.Should().Be(entity2);
        entity2.Should().Be(entity3);
        entity1.Should().Be(entity3);
    }

    [Test]
    public void API_EntityComparison_ShouldBeConsistentWithEquality()
    {
        // Arrange
        var entities = _fixture.CreateMany<Entity>(10).ToList();

        // Act & Assert
        foreach (var entity1 in entities)
        {
            foreach (var entity2 in entities)
            {
                var comparison = entity1.CompareTo(entity2);
                var equality = entity1.Equals(entity2);

                if (comparison == 0)
                {
                    equality.Should().BeTrue($"Equal entities should have CompareTo result of 0");
                }
                else
                {
                    equality.Should().BeFalse($"Unequal entities should not have CompareTo result of 0");
                }
            }
        }
    }

    [Test]
    public void API_EntityHashCode_ShouldBeConsistentWithEquality()
    {
        // Arrange
        var entities = GenerateTestEntities(100);

        // Act & Assert
        foreach (var entity1 in entities)
        {
            foreach (var entity2 in entities)
            {
                if (entity1.Equals(entity2))
                {
                    entity1.GetHashCode().Should().Be(entity2.GetHashCode(),
                        "Equal entities must have equal hash codes");
                }
            }
        }
    }

    [Test]
    public void ALLOC_EntityInCollections_ShouldHaveGoodHashDistribution()
    {
        // Arrange
        var entities = GenerateTestEntities(1000);
        var hashCodes = entities.Select(e => e.GetHashCode()).ToList();

        // Act
        var uniqueHashes = hashCodes.Distinct().Count();
        var collisionRate = 1.0 - ((double)uniqueHashes / hashCodes.Count);

        // Assert
        collisionRate.Should().BeLessThan(0.1, "Hash collision rate should be less than 10%");
        uniqueHashes.Should().BeGreaterThan(900, "Should have good hash distribution");
    }

    [Test]
    public void IT_EntityInHashSet_ShouldWorkCorrectly()
    {
        // Arrange
        var entities = GenerateTestEntities(100);
        var hashSet = new HashSet<Entity>();

        // Act
        foreach (var entity in entities)
        {
            hashSet.Add(entity);
        }

        // Assert
        hashSet.Count.Should().Be(entities.Count, "All unique entities should be added to HashSet");

        foreach (var entity in entities)
        {
            hashSet.Should().Contain(entity, "HashSet should contain all added entities");
        }
    }

    [Test]
    public void IT_EntityInSortedSet_ShouldMaintainOrder()
    {
        // Arrange
        var entities = GenerateTestEntities(50);
        var sortedSet = new SortedSet<Entity>(entities);

        // Act
        var sortedList = sortedSet.ToList();
        var manuallySorted = entities.OrderBy(e => e).ToList();

        // Assert
        sortedList.Should().BeEquivalentTo(manuallySorted, options => options.WithStrictOrdering(),
            "SortedSet should maintain the same order as manual sorting");
    }

    [Test]
    public void DET_EntitySorting_ShouldBeStableAcrossRuns()
    {
        // Arrange
        var entities = new[]
        {
            new Entity(100, 1),
            new Entity(50, 3),
            new Entity(100, 2),
            new Entity(50, 1),
            new Entity(200, 1)
        };

        // Act - Sort multiple times
        var sorted1 = entities.OrderBy(e => e).ToList();
        var sorted2 = entities.OrderBy(e => e).ToList();
        var sorted3 = entities.OrderBy(e => e).ToList();

        // Assert
        sorted1.Should().BeEquivalentTo(sorted2, options => options.WithStrictOrdering());
        sorted2.Should().BeEquivalentTo(sorted3, options => options.WithStrictOrdering());

        // Verify expected order: ID first, then version
        sorted1[0].Should().Be(new Entity(50, 1));
        sorted1[1].Should().Be(new Entity(50, 3));
        sorted1[2].Should().Be(new Entity(100, 1));
        sorted1[3].Should().Be(new Entity(100, 2));
        sorted1[4].Should().Be(new Entity(200, 1));
    }

    [Test]
    public void DET_EntityPacking_ShouldBeReversible()
    {
        // Arrange
        var testCases = new[]
        {
            (0u, 0u),
            (uint.MaxValue, uint.MaxValue),
            (uint.MaxValue, 0u),
            (0u, uint.MaxValue),
            (_faker.Random.UInt(), _faker.Random.UInt())
        };

        foreach (var (id, version) in testCases)
        {
            // Act
            var entity = new Entity(id, version);
            var packed = entity.ToPacked();
            var unpacked = Entity.FromPacked(packed);

            // Assert
            unpacked.Should().Be(entity, $"Packing/unpacking should be reversible for ID={id}, Version={version}");
            unpacked.Id.Should().Be(id);
            unpacked.Version.Should().Be(version);
        }
    }

    [Test]
    public void API_EntityConversions_ShouldWorkBidirectionally()
    {
        // Arrange
        var entities = GenerateTestEntities(20);

        foreach (var entity in entities)
        {
            // Act
            ulong packed = entity; // Implicit conversion
            var converted = (Entity)packed; // Explicit conversion

            // Assert
            converted.Should().Be(entity, "Implicit and explicit conversions should be reversible");
        }
    }

    [Test]
    public void IT_EntityVersioning_ShouldPreventStaleReferences()
    {
        // Arrange
        const uint entityId = 42;
        var generations = new[]
        {
            new Entity(entityId, 1),
            new Entity(entityId, 2),
            new Entity(entityId, 3)
        };

        // Act & Assert
        for (int i = 0; i < generations.Length; i++)
        {
            for (int j = 0; j < generations.Length; j++)
            {
                if (i == j)
                {
                    generations[i].Should().Be(generations[j], "Same generation should be equal");
                }
                else
                {
                    generations[i].Should().NotBe(generations[j],
                        $"Different generations should not be equal (gen {i + 1} vs gen {j + 1})");
                }
            }
        }
    }

    [Test]
    [TestCase(1000)]
    [TestCase(10000)]
    public void ALLOC_EntityOperations_ShouldHandleLargeVolumes(int entityCount)
    {
        // Arrange
        var entities = GenerateTestEntities(entityCount);
        var startMemory = GC.GetTotalMemory(true);

        // Act - Perform operations that should be efficient
        var hashSet = new HashSet<Entity>(entities);
        var sortedEntities = entities.OrderBy(e => e).ToArray();
        var packed = entities.Select(e => e.ToPacked()).ToArray();
        var unpacked = packed.Select(Entity.FromPacked).ToArray();

        // Assert
        var endMemory = GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        // Allow reasonable memory usage for collections
        var expectedMaxMemory = entityCount * 64; // Rough estimate: 64 bytes per entity in collections
        memoryIncrease.Should().BeLessThan(expectedMaxMemory * 2,
            $"Memory usage should be reasonable for {entityCount} entities");

        hashSet.Count.Should().Be(entityCount);
        sortedEntities.Length.Should().Be(entityCount);
        unpacked.Should().BeEquivalentTo(entities);
    }

    [Test]
    public void API_EntityNullChecks_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var nullEntity1 = Entity.Null;
        var nullEntity2 = default(Entity);
        var nullEntity3 = new Entity(0, 0);
        var validEntity = new Entity(1, 0);

        // Assert
        nullEntity1.IsNull.Should().BeTrue();
        nullEntity2.IsNull.Should().BeTrue();
        nullEntity3.IsNull.Should().BeTrue();
        validEntity.IsNull.Should().BeFalse();

        nullEntity1.Should().Be(nullEntity2);
        nullEntity2.Should().Be(nullEntity3);
        validEntity.Should().NotBe(nullEntity1);
    }

    [Test]
    public void API_EntityToString_ShouldProvideUsefulOutput()
    {
        // Arrange
        var entity = new Entity(12345, 67890);
        var nullEntity = Entity.Null;

        // Act
        var entityString = entity.ToString();
        var nullString = nullEntity.ToString();

        // Assert
        entityString.Should().Be("Entity(12345:67890)");
        nullString.Should().Be("Entity(null)");

        entityString.Should().Contain("12345");
        entityString.Should().Contain("67890");
        nullString.Should().Contain("null");
    }

    private List<Entity> GenerateTestEntities(int count)
    {
        var entities = new List<Entity>();
        var usedCombinations = new HashSet<(uint, uint)>();

        while (entities.Count < count)
        {
            var id = _faker.Random.UInt();
            var version = _faker.Random.UInt();

            if (usedCombinations.Add((id, version)))
            {
                entities.Add(new Entity(id, version));
            }
        }

        return entities;
    }
}