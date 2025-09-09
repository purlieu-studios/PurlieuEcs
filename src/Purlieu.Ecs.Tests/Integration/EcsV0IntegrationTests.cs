using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Events;
using Purlieu.Ecs.Systems;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Integration;

[TestFixture]
public class EcsV0IntegrationTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void E2E_MovementSystem_ShouldEmitIntentsOnChange()
    {
        // Arrange - Create entity with Position and Velocity
        var entity = _world.CreateEntity();

        // Add both components together to avoid archetype migration issues
        // (component copying is not yet implemented in the current version)
        _world.AddComponent(entity, new Position(0.0f, 0.0f, 0.0f)); // Start at origin
        _world.AddComponent(entity, new Velocity(1.0f, 2.0f, 3.0f));

        // Set the initial position after adding velocity to preserve it
        _world.SetComponent(entity, new Position(10.0f, 20.0f, 30.0f));

        // Register movement system
        var movementSystem = new MovementSystem();
        _world.RegisterSystem(movementSystem);

        // Track position change intents
        var positionIntents = new System.Collections.Generic.List<PositionChangedIntent>();
        var intentChannel = _world.Events<PositionChangedIntent>();

        // Act - Update system for one frame
        _world.UpdateSystems(1.0f); // 1 second delta time

        // Consume any position change intents
        intentChannel.ConsumeAll(intent => positionIntents.Add(intent));

        // Assert - Entity position should be updated
        var updatedPosition = _world.GetComponent<Position>(entity);
        updatedPosition.X.Should().BeApproximately(11.0f, 0.001f); // 10 + (1 * 1)
        updatedPosition.Y.Should().BeApproximately(22.0f, 0.001f); // 20 + (2 * 1)
        updatedPosition.Z.Should().BeApproximately(33.0f, 0.001f); // 30 + (3 * 1)

        // Assert - Position change intent should be emitted
        positionIntents.Should().HaveCount(1);
        var intent = positionIntents[0];
        intent.Entity.Should().Be(entity);
        intent.X.Should().BeApproximately(11.0f, 0.001f);
        intent.Y.Should().BeApproximately(22.0f, 0.001f);
        intent.Z.Should().BeApproximately(33.0f, 0.001f);
        intent.PreviousX.Should().BeApproximately(10.0f, 0.001f);
        intent.PreviousY.Should().BeApproximately(20.0f, 0.001f);
        intent.PreviousZ.Should().BeApproximately(30.0f, 0.001f);
    }

    [Test]
    public void IT_MultipleSystemsExecution_ShouldMaintainPhaseOrder()
    {
        // Arrange - Create test systems with different phases
        var executionOrder = new System.Collections.Generic.List<string>();

        var updateSystem = new UpdateTestExecutionOrderSystem("Update", executionOrder);
        var postUpdateSystem = new PostUpdateTestExecutionOrderSystem("PostUpdate", executionOrder);
        var presentationSystem = new PresentationTestExecutionOrderSystem("Presentation", executionOrder);

        _world.RegisterSystem(presentationSystem); // Register out of order
        _world.RegisterSystem(updateSystem);
        _world.RegisterSystem(postUpdateSystem);

        // Act - Update systems
        _world.UpdateSystems(0.016f);

        // Assert - Systems should execute in phase order
        executionOrder.Should().Equal("Update", "PostUpdate", "Presentation");
    }

    [Test]
    public void IT_EventChannelOneFrameCleanup_ShouldWork()
    {
        // Arrange - Create one-frame event
        var channel = _world.Events<TestOneFrameEvent>();
        channel.Publish(new TestOneFrameEvent { Message = "Frame 1" });
        channel.Publish(new TestOneFrameEvent { Message = "Frame 2" });

        channel.Count.Should().Be(2);

        // Act - Clear one-frame events
        _world.ClearOneFrameEvents();

        // Assert - Events should be cleared
        channel.Count.Should().Be(0);
        channel.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void IT_SystemTimingCollection_ShouldTrackPerformance()
    {
        // Arrange
        var slowSystem = new IntegrationSlowTestSystem();
        _world.RegisterSystem(slowSystem);

        // Act - Run system multiple times
        for (int i = 0; i < 5; i++)
        {
            _world.UpdateSystems(0.016f);
        }

        // Assert - Timing should be tracked
        var timing = _world.GetSystemTiming(typeof(IntegrationSlowTestSystem));
        timing.Should().NotBeNull();
        timing!.FrameCount.Should().Be(5);
        timing.CurrentTime.Should().BeGreaterThan(0);
        timing.AverageTime.Should().BeGreaterThan(0);
        timing.PeakTime.Should().BeGreaterThan(0);

        // Test peak reset
        var originalPeak = timing.PeakTime;
        _world.ResetSystemPeaks();

        var updatedTiming = _world.GetSystemTiming(typeof(IntegrationSlowTestSystem));
        updatedTiming!.PeakTime.Should().Be(0.0);
        updatedTiming.CurrentTime.Should().Be(timing.CurrentTime); // Current should remain
    }

    [Test]
    public void IT_QueryPerformanceWithSystems_ShouldBeEfficient()
    {
        // Arrange - Create many entities for performance testing
        var entityCount = PlatformTestHelper.AdjustEntityCount(1000);
        for (int i = 0; i < entityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i * 2, i * 3));
            _world.AddComponent(entity, new Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        var movementSystem = new MovementSystem();
        _world.RegisterSystem(movementSystem);

        // Act - Update system and measure performance
        var startTime = System.DateTime.UtcNow;
        _world.UpdateSystems(0.016f);
        var endTime = System.DateTime.UtcNow;

        var executionTime = endTime - startTime;

        // Assert - System should execute efficiently
        executionTime.TotalMilliseconds.Should().BeLessThan(100,
            $"MovementSystem with {entityCount} entities should execute quickly");

        // Verify timing is tracked
        var timing = _world.GetSystemTiming(typeof(MovementSystem));
        timing.Should().NotBeNull();
        timing!.CurrentTime.Should().BeGreaterThan(0);
    }

    [Test]
    public void DET_CompleteWorkflow_ShouldBeReproducible()
    {
        // Arrange - Create identical setups
        var world1 = new World();
        var world2 = new World();

        var setupWorlds = new[] { world1, world2 };
        var results = new System.Collections.Generic.List<Position>[2];

        for (int w = 0; w < 2; w++)
        {
            var world = setupWorlds[w];
            results[w] = new System.Collections.Generic.List<Position>();

            // Create identical entities
            for (int i = 0; i < 10; i++)
            {
                var entity = world.CreateEntity();
                world.AddComponent(entity, new Position(i * 1.0f, i * 2.0f, i * 3.0f));
                world.AddComponent(entity, new Velocity(0.1f, 0.2f, 0.3f));
            }

            // Register systems
            world.RegisterSystem(new MovementSystem());

            // Act - Update multiple frames
            for (int frame = 0; frame < 3; frame++)
            {
                world.UpdateSystems(0.016f);
            }

            // Collect final positions
            var query = world.Query().With<Position>();
            foreach (var chunk in query.Chunks())
            {
                var positions = chunk.GetSpan<Position>();
                for (int i = 0; i < positions.Length; i++)
                {
                    results[w].Add(positions[i]);
                }
            }

            results[w] = results[w].OrderBy(p => p.X).ToList(); // Sort for comparison
        }

        // Assert - Both worlds should have identical results
        results[0].Should().BeEquivalentTo(results[1], "ECS workflow should be deterministic");
    }

    [Test]
    public void ALLOC_CompleteWorkflow_ShouldHaveMinimalAllocations()
    {
        // Arrange
        var entityCount = PlatformTestHelper.AdjustEntityCount(100);
        for (int i = 0; i < entityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Position(i, i, i));
            _world.AddComponent(entity, new Velocity(1.0f, 1.0f, 1.0f));
        }

        _world.RegisterSystem(new MovementSystem());

        // Warmup
        _world.UpdateSystems(0.016f);
        _world.ClearOneFrameEvents();

        // Act - Multiple update cycles
        var startMemory = System.GC.GetTotalMemory(true);

        for (int frame = 0; frame < 10; frame++)
        {
            _world.UpdateSystems(0.016f);
            _world.ClearOneFrameEvents();
        }

        var endMemory = System.GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        // Assert - Should have minimal allocations per frame
        // Allow for reasonable memory allocation during ECS operations
        // This includes event publishing, archetype management, and system execution
        // CI environments may have higher allocation patterns due to different GC behavior
        var expectedMaxMemory = Math.Max(entityCount * 800, 80000L); // Realistic baseline for CI
        memoryIncrease.Should().BeLessThan(expectedMaxMemory,
            $"Complete ECS workflow with {entityCount} entities should have reasonable allocations");
    }
}

// Test systems for integration testing - each needs unique GamePhase attribute
[GamePhase(GamePhase.Update)]
public class UpdateTestExecutionOrderSystem : ISystem
{
    private readonly string _name;
    private readonly System.Collections.Generic.List<string> _executionOrder;

    public UpdateTestExecutionOrderSystem(string name, System.Collections.Generic.List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.PostUpdate)]
public class PostUpdateTestExecutionOrderSystem : ISystem
{
    private readonly string _name;
    private readonly System.Collections.Generic.List<string> _executionOrder;

    public PostUpdateTestExecutionOrderSystem(string name, System.Collections.Generic.List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.Presentation)]
public class PresentationTestExecutionOrderSystem : ISystem
{
    private readonly string _name;
    private readonly System.Collections.Generic.List<string> _executionOrder;

    public PresentationTestExecutionOrderSystem(string name, System.Collections.Generic.List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.Update)]
public class IntegrationSlowTestSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // Simulate some work for timing tests
        System.Threading.Thread.Sleep(1);
    }
}

// Test event structures
[OneFrame]
public struct TestOneFrameEvent
{
    public string Message { get; set; }
}
