using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Core;

[TestFixture]
public class DefragmentationTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void DEFRAG_UtilizationCalculation_ShouldReturnCorrectRatio()
    {
        // Arrange - Create entities to fill chunks partially
        var entities = new Entity[300]; // Less than full chunk capacity (512)

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Act - Get utilization stats
        var stats = _world.GetArchetypeUtilizationStats();

        // Assert - Should have correct utilization calculation
        var positionArchetype = stats.Keys.FirstOrDefault(sig => sig.Has<Position>());
        positionArchetype.Should().NotBeNull();

        var utilizationStats = stats[positionArchetype];
        utilizationStats.EntityCount.Should().Be(300);
        utilizationStats.ChunkCount.Should().Be(1);
        utilizationStats.Utilization.Should().BeApproximately(300f / 512f, 0.01f);
        utilizationStats.TotalCapacity.Should().Be(512);
        utilizationStats.WastedCapacity.Should().Be(212);
    }

    [Test]
    public void DEFRAG_ShouldDefragment_ReturnsTrueForSparseArchetypes()
    {
        // Arrange - Create a sparse archetype by removing entities
        var entities = new Entity[1000];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Remove every other entity to create sparseness
        for (int i = 0; i < entities.Length; i += 2)
        {
            _world.DestroyEntity(entities[i]);
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Position>());
        var config = DefragmentationConfig.Default;

        // Act & Assert
        ArchetypeDefragmenter.ShouldDefragment(archetype, config).Should().BeTrue();
        ArchetypeDefragmenter.CalculateUtilization(archetype).Should().BeLessThan(0.5f);
    }

    [Test]
    public void DEFRAG_DefragmentArchetype_ShouldImproveUtilization()
    {
        // Arrange - Create multiple chunks with entities
        var entities = new Entity[1200]; // More than 2 chunks

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
            _world.AddComponent(entities[i], new Velocity(1, 1, 1));
        }

        // Remove entities to create fragmentation
        for (int i = 0; i < entities.Length; i += 3)
        {
            _world.DestroyEntity(entities[i]);
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Position>() && a.Signature.Has<Velocity>());
        var utilizationBefore = archetype.GetUtilization();
        var chunkCountBefore = archetype.ChunkCount;

        // Act - Perform defragmentation
        var result = ArchetypeDefragmenter.Defragment(archetype, DefragmentationConfig.Default);

        // Assert - Should improve utilization
        result.UtilizationBefore.Should().Be(utilizationBefore);
        result.UtilizationAfter.Should().BeGreaterOrEqualTo(utilizationBefore);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify archetype integrity
        archetype.EntityCount.Should().BeGreaterThan(0);
        archetype.ChunkCount.Should().BeLessOrEqualTo(chunkCountBefore);
    }

    [Test]
    public void DEFRAG_WorldDefragmentation_ShouldProcessMultipleArchetypes()
    {
        // Arrange - Create multiple archetypes with different fragmentation levels
        var entityCount = PlatformTestHelper.AdjustEntityCount(800);

        // Archetype 1: Position only
        for (int i = 0; i < entityCount / 2; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));

            // Remove some to create fragmentation
            if (i % 3 == 0)
            {
                _world.DestroyEntity(entity);
            }
        }

        // Archetype 2: Position + Velocity
        for (int i = 0; i < entityCount / 2; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
            _world.AddComponent(entity, new Velocity(1, 1, 1));

            // Remove some to create fragmentation
            if (i % 4 == 0)
            {
                _world.DestroyEntity(entity);
            }
        }

        // Act - Defragment all archetypes
        var summary = _world.DefragmentAllArchetypes();

        // Assert - Should process multiple archetypes
        summary.ArchetypesProcessed.Should().BeGreaterThan(0);
        summary.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify world integrity
        _world.EntityCount.Should().BeGreaterThan(0);
        _world.ArchetypeCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void DEFRAG_ConfigurableThresholds_ShouldRespectSettings()
    {
        // Arrange - Create archetype with moderate fragmentation
        var entities = new Entity[600];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Remove 30% of entities (70% utilization)
        for (int i = 0; i < entities.Length; i += 10)
        {
            if (i + 2 < entities.Length)
            {
                _world.DestroyEntity(entities[i]);
                _world.DestroyEntity(entities[i + 1]);
                _world.DestroyEntity(entities[i + 2]);
            }
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Position>());

        // Act & Assert - With high threshold (0.8), should not defragment
        var highThresholdConfig = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.8f,
            MinChunkCount = 1,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        ArchetypeDefragmenter.ShouldDefragment(archetype, highThresholdConfig).Should().BeFalse();

        // With low threshold (0.5), should defragment
        var lowThresholdConfig = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.5f,
            MinChunkCount = 1,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        ArchetypeDefragmenter.ShouldDefragment(archetype, lowThresholdConfig).Should().BeTrue();
    }

    [Test]
    public void DEFRAG_EmptyChunkRemoval_ShouldCleanupEmptyChunks()
    {
        // Arrange - Create multiple chunks and then empty some
        var entities = new Entity[1000];

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Health(100, 100));
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Health>());
        var initialChunkCount = archetype.ChunkCount;

        // Remove all entities from first chunk (assuming 512 capacity chunks)
        for (int i = 0; i < Math.Min(512, entities.Length); i++)
        {
            _world.DestroyEntity(entities[i]);
        }

        // Act - Defragment with empty chunk removal enabled
        var config = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.9f,
            MinChunkCount = 1,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        var result = ArchetypeDefragmenter.Defragment(archetype, config);

        // Assert - Should have removed empty chunks
        result.EmptyChunksRemoved.Should().BeGreaterThan(0);
        archetype.ChunkCount.Should().BeLessThan(initialChunkCount);
    }

    [Test]
    public void API_DefragmentationConfig_DefaultValuesShouldBeReasonable()
    {
        // Act
        var config = DefragmentationConfig.Default;

        // Assert - Default values should be reasonable for most use cases
        config.MinUtilizationThreshold.Should().Be(0.5f);
        config.MinChunkCount.Should().Be(2);
        config.MaxChunksPerPass.Should().Be(10);
        config.RemoveEmptyChunks.Should().BeTrue();
    }
}
