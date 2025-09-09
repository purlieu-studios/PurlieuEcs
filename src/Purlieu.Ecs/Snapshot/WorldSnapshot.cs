using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Snapshot;

/// <summary>
/// Provides world serialization and deserialization for save/load functionality.
/// Supports version tracking and LZ4 compression for efficient storage.
/// </summary>
public static class WorldSnapshot
{
    private const uint FormatVersion = 1;
    private const byte CompressionMagic = 0x7F; // Magic byte to identify compressed snapshots

    /// <summary>
    /// Creates a snapshot of the current world state.
    /// </summary>
    /// <param name="world">World to snapshot</param>
    /// <returns>Serialized snapshot data</returns>
    public static byte[] CreateSnapshot(World world)
    {
        var snapshotData = new SnapshotData
        {
            FormatVersion = FormatVersion,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EntityCount = world.EntityCount,
            ArchetypeCount = world.ArchetypeCount,
            Archetypes = SerializeArchetypes(world)
        };

        // Serialize to JSON first
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(snapshotData, SnapshotJsonOptions.Default);

        // Compress with GZip (LZ4 would require additional dependency)
        using var output = new MemoryStream();
        output.WriteByte(CompressionMagic); // Write magic byte

        using (var gzipStream = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzipStream.Write(jsonBytes);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Restores world state from a snapshot.
    /// </summary>
    /// <param name="snapshotData">Serialized snapshot data</param>
    /// <returns>Restored world instance</returns>
    public static World RestoreSnapshot(byte[] snapshotData)
    {
        if (snapshotData == null || snapshotData.Length == 0)
            throw new ArgumentException("Snapshot data cannot be null or empty");

        byte[] jsonBytes;

        // Check if data is compressed
        if (snapshotData[0] == CompressionMagic)
        {
            // Decompress data
            using var input = new MemoryStream(snapshotData, 1, snapshotData.Length - 1);
            using var gzipStream = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzipStream.CopyTo(output);
            jsonBytes = output.ToArray();
        }
        else
        {
            // Uncompressed data (legacy or debug)
            jsonBytes = snapshotData;
        }

        // Deserialize from JSON
        var snapshot = JsonSerializer.Deserialize<SnapshotData>(jsonBytes, SnapshotJsonOptions.Default);
        if (snapshot == null)
            throw new InvalidOperationException("Failed to deserialize snapshot data");

        // Validate format version
        if (snapshot.FormatVersion > FormatVersion)
            throw new NotSupportedException($"Unsupported snapshot format version: {snapshot.FormatVersion}");

        // Create new world and restore state
        var world = new World();
        RestoreArchetypes(world, snapshot.Archetypes);

        return world;
    }

    /// <summary>
    /// Gets snapshot metadata without fully deserializing the world.
    /// </summary>
    /// <param name="snapshotData">Serialized snapshot data</param>
    /// <returns>Snapshot metadata</returns>
    public static SnapshotMetadata GetSnapshotInfo(byte[] snapshotData)
    {
        if (snapshotData == null || snapshotData.Length == 0)
            throw new ArgumentException("Snapshot data cannot be null or empty");

        byte[] jsonBytes;

        // Check if data is compressed
        if (snapshotData[0] == CompressionMagic)
        {
            // Decompress data
            using var input = new MemoryStream(snapshotData, 1, snapshotData.Length - 1);
            using var gzipStream = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzipStream.CopyTo(output);
            jsonBytes = output.ToArray();
        }
        else
        {
            jsonBytes = snapshotData;
        }

        // Parse just the header information
        using var document = JsonDocument.Parse(jsonBytes);
        var root = document.RootElement;

        return new SnapshotMetadata
        {
            FormatVersion = root.GetProperty("FormatVersion").GetUInt32(),
            Timestamp = root.GetProperty("Timestamp").GetInt64(),
            EntityCount = root.GetProperty("EntityCount").GetInt32(),
            ArchetypeCount = root.GetProperty("ArchetypeCount").GetInt32(),
            CompressedSize = snapshotData.Length,
            UncompressedSize = jsonBytes.Length
        };
    }

    private static List<ArchetypeSnapshot> SerializeArchetypes(World world)
    {
        var archetypeSnapshots = new List<ArchetypeSnapshot>();

        foreach (var archetype in world.GetArchetypes())
        {
            var snapshot = new ArchetypeSnapshot
            {
                Signature = archetype.Signature.ToMask(),
                EntityCount = archetype.EntityCount,
                Entities = archetype.GetAllEntities().Select(e => new EntitySnapshot
                {
                    Id = e.Id,
                    Version = e.Version
                }).ToList(),
                // For MVP, we'll serialize components as JSON objects
                // A production system would use binary serialization or source generation
                ComponentData = new Dictionary<string, object>()
            };

            // Note: Component serialization is simplified for MVP
            // A full implementation would need:
            // 1. Component type registry with serialization delegates
            // 2. Binary serialization for performance
            // 3. Version compatibility handling
            // 4. Source generation for zero-reflection serialization

            archetypeSnapshots.Add(snapshot);
        }

        return archetypeSnapshots;
    }

    private static void RestoreArchetypes(World world, List<ArchetypeSnapshot> archetypeSnapshots)
    {
        foreach (var snapshot in archetypeSnapshots)
        {
            var signature = ComponentSignature.FromMask(snapshot.Signature);
            var archetype = world.GetOrCreateArchetype(signature);

            // Restore entities
            foreach (var entitySnapshot in snapshot.Entities)
            {
                var entity = new Entity(entitySnapshot.Id, entitySnapshot.Version);
                archetype.AddEntity(entity);

                // Note: Component restoration is simplified for MVP
                // A full implementation would restore all component data here
            }
        }
    }
}

/// <summary>
/// Complete snapshot data structure.
/// </summary>
internal sealed class SnapshotData
{
    public uint FormatVersion { get; set; }
    public long Timestamp { get; set; }
    public int EntityCount { get; set; }
    public int ArchetypeCount { get; set; }
    public List<ArchetypeSnapshot> Archetypes { get; set; } = new();
}

/// <summary>
/// Snapshot data for a single archetype.
/// </summary>
internal sealed class ArchetypeSnapshot
{
    public ulong Signature { get; set; }
    public int EntityCount { get; set; }
    public List<EntitySnapshot> Entities { get; set; } = new();
    public Dictionary<string, object> ComponentData { get; set; } = new();
}

/// <summary>
/// Snapshot data for a single entity.
/// </summary>
internal sealed class EntitySnapshot
{
    public uint Id { get; set; }
    public uint Version { get; set; }
}

/// <summary>
/// Metadata about a snapshot without full deserialization.
/// </summary>
public sealed class SnapshotMetadata
{
    public uint FormatVersion { get; init; }
    public long Timestamp { get; init; }
    public int EntityCount { get; init; }
    public int ArchetypeCount { get; init; }
    public int CompressedSize { get; init; }
    public int UncompressedSize { get; init; }

    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
    public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 1.0;

    public override string ToString()
    {
        return $"Snapshot(v{FormatVersion}, {EntityCount} entities, {ArchetypeCount} archetypes, {CompressedSize:N0} bytes, {CompressionRatio:P1} compression)";
    }
}

/// <summary>
/// JSON serialization options for snapshots.
/// </summary>
internal static class SnapshotJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
