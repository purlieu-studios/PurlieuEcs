using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Query;

/// <summary>
/// Statistics for a single query execution.
/// </summary>
public struct QueryExecutionStats
{
    /// <summary>
    /// Total time spent executing the query.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Number of archetypes that matched the query.
    /// </summary>
    public int ArchetypesMatched { get; set; }

    /// <summary>
    /// Total number of chunks processed.
    /// </summary>
    public int ChunksProcessed { get; set; }

    /// <summary>
    /// Total number of entities processed.
    /// </summary>
    public int EntitiesProcessed { get; set; }

    /// <summary>
    /// Average chunk utilization (0.0 to 1.0).
    /// </summary>
    public float AverageChunkUtilization { get; set; }

    /// <summary>
    /// Number of empty chunks encountered.
    /// </summary>
    public int EmptyChunks { get; set; }

    /// <summary>
    /// Number of sparse chunks (below 50% utilization).
    /// </summary>
    public int SparseChunks { get; set; }

    /// <summary>
    /// Total wasted capacity across all processed chunks.
    /// </summary>
    public int WastedCapacity { get; set; }

    /// <summary>
    /// Query signature for debugging.
    /// </summary>
    public string QuerySignature { get; set; }
}

/// <summary>
/// Profiler for analyzing query performance and chunk utilization patterns.
/// </summary>
public static class QueryProfiler
{
    private static readonly Dictionary<string, List<QueryExecutionStats>> _queryStats = new();
    private static readonly object _lock = new();
    private static bool _enabled = false;

    /// <summary>
    /// Enables or disables query profiling.
    /// </summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Profiles the execution of a query and its chunk iteration.
    /// </summary>
    /// <param name="querySignature">String representation of the query for identification</param>
    /// <param name="queryExecution">Function that executes the query</param>
    /// <returns>Query execution statistics</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryExecutionStats ProfileQuery(string querySignature, Func<IEnumerable<IChunkView>> queryExecution)
    {
        if (!_enabled)
        {
            // If profiling is disabled, execute the query without profiling overhead
            var chunks = queryExecution();
            foreach (var _ in chunks) { } // Consume the enumerable
            return new QueryExecutionStats { QuerySignature = querySignature };
        }

        return ProfileQueryInternal(querySignature, queryExecution);
    }

    private static QueryExecutionStats ProfileQueryInternal(string querySignature, Func<IEnumerable<IChunkView>> queryExecution)
    {
        var stopwatch = Stopwatch.StartNew();
        var stats = new QueryExecutionStats
        {
            QuerySignature = querySignature
        };

        var chunks = queryExecution();
        var totalUtilization = 0f;
        var archetypeSet = new HashSet<ComponentSignature>();

        foreach (var chunk in chunks)
        {
            stats.ChunksProcessed++;
            stats.EntitiesProcessed += chunk.Count;

            // Track archetype diversity
            archetypeSet.Add(chunk.Signature);

            // Calculate chunk utilization
            var utilization = chunk.Count / (float)chunk.Capacity;
            totalUtilization += utilization;

            // Categorize chunks
            if (chunk.Count == 0)
            {
                stats.EmptyChunks++;
            }
            else if (utilization < 0.5f)
            {
                stats.SparseChunks++;
            }

            // Calculate wasted capacity
            stats.WastedCapacity += chunk.Capacity - chunk.Count;
        }

        stopwatch.Stop();

        stats.ExecutionTime = stopwatch.Elapsed;
        stats.ArchetypesMatched = archetypeSet.Count;
        stats.AverageChunkUtilization = stats.ChunksProcessed > 0 
            ? totalUtilization / stats.ChunksProcessed 
            : 0f;

        // Record stats for analysis
        RecordStats(querySignature, stats);

        return stats;
    }

    /// <summary>
    /// Records query execution statistics for later analysis.
    /// </summary>
    private static void RecordStats(string querySignature, QueryExecutionStats stats)
    {
        lock (_lock)
        {
            if (!_queryStats.TryGetValue(querySignature, out var statsList))
            {
                statsList = new List<QueryExecutionStats>();
                _queryStats[querySignature] = statsList;
            }

            statsList.Add(stats);

            // Limit history to prevent memory growth
            const int maxHistorySize = 100;
            if (statsList.Count > maxHistorySize)
            {
                statsList.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets aggregated statistics for a specific query signature.
    /// </summary>
    /// <param name="querySignature">Query signature to analyze</param>
    /// <returns>Aggregated statistics or null if no data exists</returns>
    public static QueryAggregateStats? GetAggregateStats(string querySignature)
    {
        lock (_lock)
        {
            if (!_queryStats.TryGetValue(querySignature, out var statsList) || statsList.Count == 0)
                return null;

            var aggregate = new QueryAggregateStats
            {
                QuerySignature = querySignature,
                ExecutionCount = statsList.Count
            };

            var totalTime = TimeSpan.Zero;
            var totalChunks = 0;
            var totalEntities = 0;
            var totalUtilization = 0f;
            var totalWasted = 0;
            var totalSparse = 0;
            var totalEmpty = 0;

            foreach (var stats in statsList)
            {
                totalTime += stats.ExecutionTime;
                totalChunks += stats.ChunksProcessed;
                totalEntities += stats.EntitiesProcessed;
                totalUtilization += stats.AverageChunkUtilization;
                totalWasted += stats.WastedCapacity;
                totalSparse += stats.SparseChunks;
                totalEmpty += stats.EmptyChunks;
            }

            aggregate.AverageExecutionTime = new TimeSpan(totalTime.Ticks / statsList.Count);
            aggregate.AverageChunksProcessed = (float)totalChunks / statsList.Count;
            aggregate.AverageEntitiesProcessed = (float)totalEntities / statsList.Count;
            aggregate.AverageUtilization = totalUtilization / statsList.Count;
            aggregate.AverageWastedCapacity = (float)totalWasted / statsList.Count;
            aggregate.AverageSparseChunks = (float)totalSparse / statsList.Count;
            aggregate.AverageEmptyChunks = (float)totalEmpty / statsList.Count;

            return aggregate;
        }
    }

    /// <summary>
    /// Gets all recorded query signatures.
    /// </summary>
    /// <returns>List of query signatures that have been profiled</returns>
    public static IReadOnlyList<string> GetProfiledQueries()
    {
        lock (_lock)
        {
            return new List<string>(_queryStats.Keys);
        }
    }

    /// <summary>
    /// Clears all recorded profiling data.
    /// </summary>
    public static void ClearStats()
    {
        lock (_lock)
        {
            _queryStats.Clear();
        }
    }

    /// <summary>
    /// Gets the most recent execution stats for a query.
    /// </summary>
    /// <param name="querySignature">Query signature to look up</param>
    /// <returns>Most recent stats or null if no data exists</returns>
    public static QueryExecutionStats? GetRecentStats(string querySignature)
    {
        lock (_lock)
        {
            if (!_queryStats.TryGetValue(querySignature, out var statsList) || statsList.Count == 0)
                return null;

            return statsList[statsList.Count - 1];
        }
    }
}

/// <summary>
/// Aggregated statistics for a query across multiple executions.
/// </summary>
public struct QueryAggregateStats
{
    /// <summary>
    /// Query signature for identification.
    /// </summary>
    public string QuerySignature { get; set; }

    /// <summary>
    /// Total number of times this query has been executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Average execution time across all runs.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Average number of chunks processed per execution.
    /// </summary>
    public float AverageChunksProcessed { get; set; }

    /// <summary>
    /// Average number of entities processed per execution.
    /// </summary>
    public float AverageEntitiesProcessed { get; set; }

    /// <summary>
    /// Average chunk utilization across all executions.
    /// </summary>
    public float AverageUtilization { get; set; }

    /// <summary>
    /// Average wasted capacity per execution.
    /// </summary>
    public float AverageWastedCapacity { get; set; }

    /// <summary>
    /// Average number of sparse chunks encountered.
    /// </summary>
    public float AverageSparseChunks { get; set; }

    /// <summary>
    /// Average number of empty chunks encountered.
    /// </summary>
    public float AverageEmptyChunks { get; set; }

    /// <summary>
    /// Efficiency score (0.0 to 1.0) based on utilization and waste metrics.
    /// </summary>
    public float EfficiencyScore => AverageUtilization * 0.7f + (1.0f - AverageSparseChunks / Math.Max(1, AverageChunksProcessed)) * 0.3f;
}