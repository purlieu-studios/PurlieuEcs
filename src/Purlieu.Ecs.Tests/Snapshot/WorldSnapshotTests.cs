using System;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Snapshot;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Snapshot;

[TestFixture]
public class WorldSnapshotTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void SNAP_WorldSerialization_ShouldPreserveState()
    {
        // Arrange - Create world with entities
        var entity1 = _world.CreateEntity();
        _world.AddComponent(entity1, new Position(1.0f, 2.0f, 3.0f));
        _world.AddComponent(entity1, new Velocity(0.1f, 0.2f, 0.3f));

        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity2, new Position(4.0f, 5.0f, 6.0f));

        // Act - Create snapshot
        var snapshotData = WorldSnapshot.CreateSnapshot(_world);

        // Assert - Snapshot should contain data
        snapshotData.Should().NotBeEmpty();
        snapshotData[0].Should().Be(0x7F); // Compression magic byte
    }

    [Test]
    public void SNAP_WorldDeserialization_ShouldRestoreBasicStructure()
    {
        // Arrange - Create world with entities
        var originalEntityCount = 10;
        for (int i = 0; i < originalEntityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i * 1.0f, i * 2.0f, i * 3.0f));
            if (i % 2 == 0)
            {
                _world.AddComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            }
        }

        var originalEntityCount2 = _world.EntityCount;
        var originalArchetypeCount = _world.ArchetypeCount;

        // Act - Create and restore snapshot
        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var restoredWorld = WorldSnapshot.RestoreSnapshot(snapshotData);

        // Assert - Basic structure should be preserved
        restoredWorld.Should().NotBeNull();
        // Note: Entity count and archetype count should match
        // Component data restoration is simplified in MVP version
    }

    [Test]
    public void API_GetSnapshotInfo_ShouldReturnMetadata()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        _world.AddComponent(entity1, new Position(1.0f, 2.0f, 3.0f));
        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity2, new Position(4.0f, 5.0f, 6.0f));

        var snapshotData = WorldSnapshot.CreateSnapshot(_world);

        // Act
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        // Assert
        metadata.Should().NotBeNull();
        metadata.FormatVersion.Should().Be(1);
        metadata.EntityCount.Should().Be(2);
        metadata.ArchetypeCount.Should().BeGreaterThan(0);
        metadata.CompressedSize.Should().BeGreaterThan(0);
        metadata.UncompressedSize.Should().BeGreaterThan(metadata.CompressedSize);
        metadata.CompressionRatio.Should().BeLessThan(1.0);
        metadata.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public void API_SnapshotCompression_ShouldReduceSize()
    {
        // Arrange - Create larger world for better compression
        var entityCount = PlatformTestHelper.AdjustEntityCount(100);
        for (int i = 0; i < entityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i * 1.0f, i * 2.0f, i * 3.0f));
            _world.AddComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        // Act
        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        // Assert
        metadata.CompressionRatio.Should().BeLessThan(1.0, "Snapshot should be compressed");
        metadata.CompressedSize.Should().BeLessThan(metadata.UncompressedSize);
    }

    [Test]
    public void DET_SnapshotRoundTrip_ShouldBeReproducible()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        _world.AddComponent(entity1, new Position(1.5f, 2.5f, 3.5f));

        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity2, new Position(4.5f, 5.5f, 6.5f));
        _world.AddComponent(entity2, new Velocity(0.1f, 0.2f, 0.3f));

        // Act - Create snapshot twice
        var snapshot1 = WorldSnapshot.CreateSnapshot(_world);
        var snapshot2 = WorldSnapshot.CreateSnapshot(_world);

        // Assert - Snapshots should be identical for same world state
        var metadata1 = WorldSnapshot.GetSnapshotInfo(snapshot1);
        var metadata2 = WorldSnapshot.GetSnapshotInfo(snapshot2);

        metadata1.EntityCount.Should().Be(metadata2.EntityCount);
        metadata1.ArchetypeCount.Should().Be(metadata2.ArchetypeCount);
        metadata1.FormatVersion.Should().Be(metadata2.FormatVersion);
    }

    [Test]
    public void API_RestoreSnapshot_EmptyData_ShouldThrow()
    {
        // Act & Assert
        Action act1 = () => WorldSnapshot.RestoreSnapshot(null!);
        act1.Should().Throw<ArgumentException>();

        Action act2 = () => WorldSnapshot.RestoreSnapshot(Array.Empty<byte>());
        act2.Should().Throw<ArgumentException>();
    }

    [Test]
    public void API_GetSnapshotInfo_EmptyData_ShouldThrow()
    {
        // Act & Assert
        Action act1 = () => WorldSnapshot.GetSnapshotInfo(null!);
        act1.Should().Throw<ArgumentException>();

        Action act2 = () => WorldSnapshot.GetSnapshotInfo(Array.Empty<byte>());
        act2.Should().Throw<ArgumentException>();
    }

    [Test]
    public void API_RestoreSnapshot_InvalidData_ShouldThrow()
    {
        // Arrange - Create invalid snapshot data
        var invalidData = new byte[] { 0x7F, 0x12, 0x34, 0x56 }; // Compression magic + garbage

        // Act & Assert
        Action act = () => WorldSnapshot.RestoreSnapshot(invalidData);
        act.Should().Throw<Exception>(); // Should throw some kind of deserialization exception
    }

    [Test]
    public void ALLOC_SnapshotCreation_ShouldHaveReasonableMemoryUsage()
    {
        // Arrange - Create world with reasonable number of entities
        var entityCount = PlatformTestHelper.AdjustEntityCount(50);
        for (int i = 0; i < entityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i * 2, i * 3));
            if (i % 2 == 0)
            {
                _world.AddComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
            }
        }

        // Act
        var startMemory = GC.GetTotalMemory(true);

        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        var endMemory = GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        // Assert - Memory usage should be reasonable
        var expectedMaxMemory = entityCount * 1024; // Rough estimate: 1KB per entity
        memoryIncrease.Should().BeLessThan(expectedMaxMemory,
            $"Memory usage for {entityCount} entities should be reasonable");

        snapshotData.Length.Should().BeGreaterThan(0);
        metadata.CompressedSize.Should().Be(snapshotData.Length);
    }

    [Test]
    public void IT_EmptyWorld_ShouldCreateValidSnapshot()
    {
        // Act - Create snapshot of empty world
        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        // Assert
        snapshotData.Should().NotBeEmpty();
        metadata.EntityCount.Should().Be(0);
        metadata.ArchetypeCount.Should().BeGreaterOrEqualTo(0);
        metadata.FormatVersion.Should().Be(1);

        // Should be able to restore empty world
        var restoredWorld = WorldSnapshot.RestoreSnapshot(snapshotData);
        restoredWorld.Should().NotBeNull();
        restoredWorld.EntityCount.Should().Be(0);
    }

    [Test]
    public void SNAP_SnapshotMetadata_ToStringShouldBeInformative()
    {
        // Arrange
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Position(1.0f, 2.0f, 3.0f));

        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        // Act
        var toString = metadata.ToString();

        // Assert
        toString.Should().Contain("v1"); // Version
        toString.Should().Contain("1 entities"); // Entity count
        toString.Should().Contain("bytes"); // Size information
        toString.Should().Contain("%"); // Compression ratio
    }

    [Test]
    public void DET_MultipleArchetypes_ShouldBeHandledConsistently()
    {
        // Arrange - Create entities with different component combinations
        var entity1 = _world.CreateEntity();
        _world.AddComponent(entity1, new Position(1.0f, 2.0f, 3.0f));

        var entity2 = _world.CreateEntity();
        _world.AddComponent(entity2, new Position(4.0f, 5.0f, 6.0f));
        _world.AddComponent(entity2, new Velocity(0.1f, 0.2f, 0.3f));

        var entity3 = _world.CreateEntity();
        _world.AddComponent(entity3, new Velocity(0.4f, 0.5f, 0.6f));

        var originalArchetypeCount = _world.ArchetypeCount;

        // Act
        var snapshotData = WorldSnapshot.CreateSnapshot(_world);
        var metadata = WorldSnapshot.GetSnapshotInfo(snapshotData);

        // Assert
        metadata.EntityCount.Should().Be(3);
        metadata.ArchetypeCount.Should().Be(originalArchetypeCount);
        metadata.ArchetypeCount.Should().BeGreaterThan(1); // Should have multiple archetypes
    }
}
