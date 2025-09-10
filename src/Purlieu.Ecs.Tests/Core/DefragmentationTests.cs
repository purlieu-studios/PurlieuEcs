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
        // Arrange - Create multiple archetypes with heavy fragmentation
        // Ensure we create enough entities to span multiple chunks (512 each) even on macOS
        var entityCount = Math.Max(1200, PlatformTestHelper.AdjustEntityCount(1500));

        // Archetype 1: Position only - create enough entities to span multiple chunks, then remove many
        var positionEntities = new Entity[entityCount];
        for (int i = 0; i < entityCount; i++)
        {
            positionEntities[i] = _world.CreateEntity();
            _world.AddComponent(positionEntities[i], new Position(i, i, i));
        }

        // Remove 60% of entities to create heavy fragmentation
        for (int i = 0; i < entityCount; i += 5)
        {
            if (i + 2 < entityCount)
            {
                _world.DestroyEntity(positionEntities[i]);
                _world.DestroyEntity(positionEntities[i + 1]);
                _world.DestroyEntity(positionEntities[i + 2]);
            }
        }

        // Archetype 2: Position + Velocity - create another fragmented archetype
        var velocityEntities = new Entity[entityCount / 2];
        for (int i = 0; i < velocityEntities.Length; i++)
        {
            velocityEntities[i] = _world.CreateEntity();
            _world.AddComponent(velocityEntities[i], new Position(i, i, i));
            _world.AddComponent(velocityEntities[i], new Velocity(1, 1, 1));
        }

        // Remove 50% of velocity entities
        for (int i = 0; i < velocityEntities.Length; i += 2)
        {
            _world.DestroyEntity(velocityEntities[i]);
        }

        // Act - Defragment all archetypes with permissive settings
        var config = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.7f, // Lower threshold to catch more fragmentation
            MinChunkCount = 2,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        var summary = _world.DefragmentAllArchetypes(config);

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
        // Arrange - Create entities for testing thresholds
        var entities = new Entity[900]; // Create entities to span multiple chunks

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Remove only a small number of entities to get ~85% utilization
        // With 900 entities across 2 chunks (1024 capacity), removing 35 entities gives ~84% utilization
        for (int i = 0; i < 35; i++)
        {
            _world.DestroyEntity(entities[i]);
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Position>());

        // Act & Assert - With high threshold (0.8), should not defragment (utilization ~84% > 80%)
        var highThresholdConfig = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.8f,
            MinChunkCount = 1,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        ArchetypeDefragmenter.ShouldDefragment(archetype, highThresholdConfig).Should().BeFalse();

        // With very high threshold (0.9), should defragment (utilization ~84% < 90%)
        var veryHighThresholdConfig = new DefragmentationConfig
        {
            MinUtilizationThreshold = 0.9f,
            MinChunkCount = 1,
            MaxChunksPerPass = 10,
            RemoveEmptyChunks = true
        };

        ArchetypeDefragmenter.ShouldDefragment(archetype, veryHighThresholdConfig).Should().BeTrue();
    }

    [Test]
    public void DEFRAG_EmptyChunkRemoval_ShouldCleanupEmptyChunks()
    {
        // Arrange - Create multiple chunks and then remove ALL entities to create empty chunks
        var entities = new Entity[1500]; // Ensure we have multiple chunks (3 chunks: 512+512+476)

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Health(100, 100));
        }

        var archetype = _world.GetArchetypes().First(a => a.Signature.Has<Health>());
        var initialChunkCount = archetype.ChunkCount;

        // Strategy: Remove all entities to create completely empty chunks
        // Due to swap-remove behavior, we need to remove all entities to guarantee empty chunks
        for (int i = 0; i < entities.Length; i++)
        {
            _world.DestroyEntity(entities[i]);
        }

        // At this point, all chunks should be empty
        archetype.EntityCount.Should().Be(0);
        archetype.ChunkCount.Should().BeGreaterThan(0); // But chunks still exist

        // Act - Call RemoveEmptyChunks to clean them up
        var removedChunks = archetype.RemoveEmptyChunks();

        // Assert - Should have removed empty chunks, leaving only 1 or 0 chunks
        removedChunks.Should().BeGreaterThan(0);
        archetype.ChunkCount.Should().BeLessOrEqualTo(1); // Should have at most 1 empty chunk remaining
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
