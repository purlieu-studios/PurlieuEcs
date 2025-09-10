using System;
using System.Collections.Generic;
using System.Linq;

namespace Purlieu.Ecs.Core;

/// <summary>
/// Configuration for archetype defragmentation operations.
/// </summary>
public struct DefragmentationConfig
{
    /// <summary>
    /// Minimum utilization threshold (0.0 to 1.0) below which a chunk is considered sparse.
    /// Default: 0.5 (50% utilization)
    /// </summary>
    public float MinUtilizationThreshold { get; set; }

    /// <summary>
    /// Minimum number of chunks required before defragmentation is considered.
    /// Default: 2
    /// </summary>
    public int MinChunkCount { get; set; }

    /// <summary>
    /// Maximum number of chunks to process in a single defragmentation pass.
    /// Default: 10
    /// </summary>
    public int MaxChunksPerPass { get; set; }

    /// <summary>
    /// Whether to remove completely empty chunks after defragmentation.
    /// Default: true
    /// </summary>
    public bool RemoveEmptyChunks { get; set; }

    public static DefragmentationConfig Default => new()
    {
        MinUtilizationThreshold = 0.5f,
        MinChunkCount = 2,
        MaxChunksPerPass = 10,
        RemoveEmptyChunks = true
    };
}

/// <summary>
/// Result of a defragmentation operation.
/// </summary>
public struct DefragmentationResult
{
    /// <summary>
    /// Number of entities that were moved during defragmentation.
    /// </summary>
    public int EntitiesMoved { get; set; }

    /// <summary>
    /// Number of chunks that were consolidated.
    /// </summary>
    public int ChunksConsolidated { get; set; }

    /// <summary>
    /// Number of empty chunks that were removed.
    /// </summary>
    public int EmptyChunksRemoved { get; set; }

    /// <summary>
    /// Utilization before defragmentation (0.0 to 1.0).
    /// </summary>
    public float UtilizationBefore { get; set; }

    /// <summary>
    /// Utilization after defragmentation (0.0 to 1.0).
    /// </summary>
    public float UtilizationAfter { get; set; }

    /// <summary>
    /// Time taken for the defragmentation operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Utility class for defragmenting archetype chunks to improve memory utilization.
/// Consolidates entities from sparse chunks to reduce memory fragmentation.
/// </summary>
public static class ArchetypeDefragmenter
{
    /// <summary>
    /// Analyzes an archetype and determines if it would benefit from defragmentation.
    /// </summary>
    /// <param name="archetype">The archetype to analyze</param>
    /// <param name="config">Defragmentation configuration</param>
    /// <returns>True if defragmentation would be beneficial</returns>
    public static bool ShouldDefragment(Archetype archetype, DefragmentationConfig config)
    {
        if (archetype.ChunkCount < config.MinChunkCount)
            return false;

        var utilization = CalculateUtilization(archetype);
        return utilization < config.MinUtilizationThreshold;
    }

    /// <summary>
    /// Calculates the current utilization of an archetype (0.0 to 1.0).
    /// </summary>
    /// <param name="archetype">The archetype to analyze</param>
    /// <returns>Utilization ratio where 1.0 is fully utilized</returns>
    public static float CalculateUtilization(Archetype archetype)
    {
        return archetype.GetUtilization();
    }

    /// <summary>
    /// Performs defragmentation on an archetype, consolidating entities from sparse chunks.
    /// </summary>
    /// <param name="archetype">The archetype to defragment</param>
    /// <param name="config">Defragmentation configuration</param>
    /// <returns>Result of the defragmentation operation</returns>
    public static DefragmentationResult Defragment(Archetype archetype, DefragmentationConfig config)
    {
        var startTime = DateTime.UtcNow;
        var utilizationBefore = CalculateUtilization(archetype);

        var result = new DefragmentationResult
        {
            UtilizationBefore = utilizationBefore,
            EntitiesMoved = 0,
            ChunksConsolidated = 0,
            EmptyChunksRemoved = 0
        };

        if (!ShouldDefragment(archetype, config))
        {
            result.UtilizationAfter = utilizationBefore;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        // Get chunks sorted by utilization (most sparse first)
        var chunkInfos = archetype.Chunks
            .Select((chunk, index) => new { Chunk = chunk, Index = index, Utilization = chunk.Count / (float)chunk.Capacity })
            .OrderBy(x => x.Utilization)
            .Take(config.MaxChunksPerPass)
            .ToList();

        // Find sparse chunks that need consolidation
        var sparseChunks = chunkInfos
            .Where(x => x.Utilization < config.MinUtilizationThreshold && x.Chunk.Count > 0)
            .ToList();

        // Find target chunks that can accept more entities
        var targetChunks = chunkInfos
            .Where(x => !x.Chunk.IsFull && !sparseChunks.Contains(x))
            .OrderByDescending(x => x.Utilization)
            .ToList();

        // Consolidate entities from sparse chunks to target chunks
        foreach (var sparseInfo in sparseChunks)
        {
            var entitiesToMove = sparseInfo.Chunk.GetEntities().ToArray();

            foreach (var entity in entitiesToMove)
            {
                // Find a target chunk with space
                var targetInfo = targetChunks.FirstOrDefault(x => !x.Chunk.IsFull);
                if (targetInfo == null)
                {
                    // No more targets available, stop processing
                    break;
                }

                // Move entity from sparse chunk to target chunk
                if (archetype.MoveEntityToChunk(entity, targetInfo.Index))
                {
                    result.EntitiesMoved++;
                }
            }

            if (sparseInfo.Chunk.IsEmpty)
            {
                result.ChunksConsolidated++;
            }
        }

        // Remove empty chunks if configured
        if (config.RemoveEmptyChunks)
        {
            result.EmptyChunksRemoved = archetype.RemoveEmptyChunks();
        }

        result.UtilizationAfter = CalculateUtilization(archetype);
        result.Duration = DateTime.UtcNow - startTime;

        return result;
    }


    /// <summary>
    /// Gets defragmentation statistics for all archetypes in a world.
    /// </summary>
    /// <param name="world">The world to analyze</param>
    /// <param name="config">Defragmentation configuration</param>
    /// <returns>Dictionary of archetype signatures to their utilization stats</returns>
    public static Dictionary<ComponentSignature, ArchetypeUtilizationStats> GetUtilizationStats(World world, DefragmentationConfig config)
    {
        var stats = new Dictionary<ComponentSignature, ArchetypeUtilizationStats>();

        foreach (var archetype in world.GetArchetypes())
        {
            var utilization = CalculateUtilization(archetype);
            var shouldDefrag = ShouldDefragment(archetype, config);

            stats[archetype.Signature] = new ArchetypeUtilizationStats
            {
                EntityCount = archetype.EntityCount,
                ChunkCount = archetype.ChunkCount,
                Utilization = utilization,
                ShouldDefragment = shouldDefrag,
                TotalCapacity = archetype.ChunkCount * Chunk.DefaultCapacity
            };
        }

        return stats;
    }
}

/// <summary>
/// Statistics about archetype utilization.
/// </summary>
public struct ArchetypeUtilizationStats
{
    public int EntityCount { get; set; }
    public int ChunkCount { get; set; }
    public float Utilization { get; set; }
    public bool ShouldDefragment { get; set; }
    public int TotalCapacity { get; set; }
    public int WastedCapacity => TotalCapacity - EntityCount;
}
