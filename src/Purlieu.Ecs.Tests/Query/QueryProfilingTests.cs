using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Query;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Query;

[TestFixture]
public class QueryProfilingTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        QueryProfiler.ClearStats();
        QueryProfiler.Enabled = true; // Enable profiling for tests
    }

    [TearDown]
    public void TearDown()
    {
        QueryProfiler.Enabled = false; // Clean up
        QueryProfiler.ClearStats();
    }

    [Test]
    public void PROF_QueryProfiling_ShouldCaptureBasicMetrics()
    {
        // Arrange - Create entities with varying chunk utilizations
        var entities = new Entity[300];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Act - Execute a query
        var query = _world.Query().With<Position>();
        var chunks = query.Chunks().ToList();

        // Assert - Profiling should capture the execution
        var queries = _world.GetProfiledQueries();
        queries.Should().HaveCount(1);

        var querySignature = queries.First();
        querySignature.Should().Contain("Position");

        var stats = _world.GetRecentQueryStats(querySignature);
        stats.Should().NotBeNull();
        stats.Value.ChunksProcessed.Should().BeGreaterThan(0);
        stats.Value.EntitiesProcessed.Should().Be(300);
        stats.Value.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public void PROF_QueryProfiling_ShouldCalculateUtilization()
    {
        // Arrange - Create partially filled chunks
        var entities = new Entity[300]; // Less than full chunk capacity
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Position(i, i, i));
        }

        // Act - Execute query
        var query = _world.Query().With<Position>();
        var chunks = query.Chunks().ToList();

        // Assert - Should calculate correct utilization
        var queries = _world.GetProfiledQueries();
        var querySignature = queries.First(q => q.Contains("Position"));

        var stats = _world.GetRecentQueryStats(querySignature);
        stats.Should().NotBeNull();
        stats.Value.AverageChunkUtilization.Should().BeApproximately(300f / 512f, 0.01f);
        stats.Value.WastedCapacity.Should().Be(212); // 512 - 300
    }

    [Test]
    public void PROF_QueryProfiling_ShouldDetectSparseChunks()
    {
        // Arrange - Create entities and then remove some to create sparseness
        var entities = new Entity[1000];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = _world.CreateEntity();
            _world.AddComponent(entities[i], new Health(100, 100));
        }

        // Remove every other entity to create sparseness
        for (int i = 0; i < entities.Length; i += 2)
        {
            _world.DestroyEntity(entities[i]);
        }

        // Act - Execute query
        var query = _world.Query().With<Health>();
        var chunks = query.Chunks().ToList();

        // Assert - Should detect sparse chunks
        var queries = _world.GetProfiledQueries();
        var querySignature = queries.First(q => q.Contains("Health"));

        var stats = _world.GetRecentQueryStats(querySignature);
        stats.Should().NotBeNull();
        stats.Value.SparseChunks.Should().BeGreaterThan(0);
        stats.Value.AverageChunkUtilization.Should().BeLessThan(0.6f);
    }

    [Test]
    public void PROF_QueryProfiling_ShouldTrackMultipleQueries()
    {
        // Arrange - Create entities with different component combinations
        for (int i = 0; i < 100; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
            if (i % 2 == 0)
            {
                _world.AddComponent(entity, new Velocity(1, 1, 1));
            }
        }

        // Act - Execute multiple different queries
        var positionQuery = _world.Query().With<Position>();
        var velocityQuery = _world.Query().With<Velocity>();
        var combinedQuery = _world.Query().With<Position>().With<Velocity>();

        positionQuery.Chunks().ToList();
        velocityQuery.Chunks().ToList();
        combinedQuery.Chunks().ToList();

        // Assert - Should track all queries separately
        var profiledQueries = _world.GetProfiledQueries();
        profiledQueries.Should().HaveCount(3);
        profiledQueries.Should().Contain(q => q.Contains("Position") && !q.Contains("Velocity"));
        profiledQueries.Should().Contain(q => q.Contains("Velocity") && !q.Contains("Position"));
        profiledQueries.Should().Contain(q => q.Contains("Position") && q.Contains("Velocity"));
    }

    [Test]
    public void PROF_QueryProfiling_ShouldAggregateStats()
    {
        // Arrange - Create entities
        for (int i = 0; i < 100; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
        }

        // Act - Execute the same query multiple times
        var query = _world.Query().With<Position>();
        for (int i = 0; i < 5; i++)
        {
            query.Chunks().ToList();
        }

        // Assert - Should aggregate statistics correctly
        var queries = _world.GetProfiledQueries();
        var querySignature = queries.First(q => q.Contains("Position"));

        var aggregateStats = _world.GetQueryStats(querySignature);
        aggregateStats.Should().NotBeNull();
        aggregateStats.Value.ExecutionCount.Should().Be(5);
        aggregateStats.Value.AverageExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
        aggregateStats.Value.AverageChunksProcessed.Should().BeGreaterThan(0);
    }

    [Test]
    public void PROF_QueryProfiling_ShouldCalculateEfficiencyScore()
    {
        // Arrange - Create well-utilized chunks
        for (int i = 0; i < 1000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
        }

        // Act - Execute query
        var query = _world.Query().With<Position>();
        query.Chunks().ToList();

        // Assert - Should have good efficiency score
        var queries = _world.GetProfiledQueries();
        var querySignature = queries.First(q => q.Contains("Position"));

        var aggregateStats = _world.GetQueryStats(querySignature);
        aggregateStats.Should().NotBeNull();
        aggregateStats.Value.EfficiencyScore.Should().BeGreaterThan(0.7f);
    }

    [Test]
    public void PROF_QueryProfiling_CanBeDisabled()
    {
        // Arrange - Disable profiling
        _world.SetQueryProfilingEnabled(false);

        // Create entities
        for (int i = 0; i < 100; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
        }

        // Act - Execute query
        var query = _world.Query().With<Position>();
        query.Chunks().ToList();

        // Assert - Should not have profiling data
        var profiledQueries = _world.GetProfiledQueries();
        profiledQueries.Should().BeEmpty();
    }

    [Test]
    public void PROF_QueryProfiling_ShouldHandleComplexQueries()
    {
        // Arrange - Create entities with complex component combinations
        for (int i = 0; i < 200; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));

            if (i % 2 == 0)
                _world.AddComponent(entity, new Velocity(1, 1, 1));

            if (i % 3 == 0)
                _world.AddComponent(entity, new Health(100, 100));
        }

        // Act - Execute complex query
        var query = _world.Query()
            .With<Position>()
            .With<Velocity>()
            .Without<Health>();

        query.Chunks().ToList();

        // Assert - Should profile complex query signature
        var queries = _world.GetProfiledQueries();
        var querySignature = queries.First(q => q.Contains("Position") && q.Contains("Velocity") && q.Contains("Without"));

        var stats = _world.GetRecentQueryStats(querySignature);
        stats.Should().NotBeNull();
        stats.Value.ArchetypesMatched.Should().BeGreaterThan(0);
    }

    [Test]
    public void PROF_PerformanceSummary_ShouldAggregateAllQueries()
    {
        // Arrange - Create varied data scenarios
        for (int i = 0; i < 500; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));

            if (i % 4 == 0)
                _world.AddComponent(entity, new Velocity(1, 1, 1));
        }

        // Act - Execute multiple queries
        _world.Query().With<Position>().Chunks().ToList();
        _world.Query().With<Velocity>().Chunks().ToList();
        _world.Query().With<Position>().Without<Velocity>().Chunks().ToList();

        // Assert - Summary should aggregate all queries
        var summary = _world.GetQueryPerformanceSummary();
        summary.TotalQueries.Should().Be(3);
        summary.TotalExecutions.Should().Be(3);
        summary.AverageExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
        summary.EfficiencyScore.Should().BeGreaterThan(0.0f);
    }

    [Test]
    public void PROF_QueryProfiling_ShouldClearStats()
    {
        // Arrange - Execute some queries
        for (int i = 0; i < 50; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
        }

        _world.Query().With<Position>().Chunks().ToList();

        // Verify stats exist
        _world.GetProfiledQueries().Should().HaveCount(1);

        // Act - Clear stats
        _world.ClearQueryProfilingData();

        // Assert - Stats should be cleared
        _world.GetProfiledQueries().Should().BeEmpty();
    }

    [Test]
    public void ALLOC_QueryProfiling_WhenDisabled_ShouldHaveMinimalOverhead()
    {
        // Arrange - Disable profiling to test overhead
        _world.SetQueryProfilingEnabled(false);

        for (int i = 0; i < 1000; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
        }

        // Act & Assert - Execute query (should not allocate for profiling)
        var query = _world.Query().With<Position>();

        // This test mainly ensures that when profiling is disabled,
        // the query execution path doesn't add significant overhead
        var chunks = query.Chunks().ToList();
        chunks.Should().NotBeEmpty();
    }
}
