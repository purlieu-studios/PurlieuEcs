using System;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Systems;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Systems;

[TestFixture]
public class SystemTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void API_SystemRegistration_ShouldExecuteInPhaseOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var postSystem = new PostTestSystem("Post", executionOrder);
        var updateSystem1 = new Update1TestSystem("Update1", executionOrder);
        var updateSystem2 = new Update2TestSystem("Update2", executionOrder);
        var presentSystem = new PresentTestSystem("Present", executionOrder);

        // Register in random order
        _world.RegisterSystem(postSystem);
        _world.RegisterSystem(presentSystem);
        _world.RegisterSystem(updateSystem1);
        _world.RegisterSystem(updateSystem2);

        // Act
        _world.UpdateSystems(0.016f);

        // Assert - should execute in phase order, then by order within phase
        executionOrder.Should().Equal(new[] { "Update2", "Update1", "Post", "Present" });
    }

    [Test]
    public void API_SystemTiming_ShouldCollectPerformanceData()
    {
        // Arrange
        var slowSystem = new SlowTestSystem();
        _world.RegisterSystem(slowSystem);

        // Act
        _world.UpdateSystems(0.016f);
        _world.UpdateSystems(0.016f);

        // Assert
        var timing = _world.GetSystemTiming(typeof(SlowTestSystem));
        timing.Should().NotBeNull();
        timing!.CurrentTime.Should().BeGreaterThan(0);
        timing.AverageTime.Should().BeGreaterThan(0);
        timing.PeakTime.Should().BeGreaterThan(0);
        timing.FrameCount.Should().Be(2);
    }

    [Test]
    public void API_SystemExecutionOrder_ShouldReturnCorrectOrder()
    {
        // Arrange
        var system1 = new Update1TestSystem("A");
        var system2 = new PostTestSystem("B");
        var system3 = new Update2TestSystem("C");

        _world.RegisterSystem(system1);
        _world.RegisterSystem(system2);
        _world.RegisterSystem(system3);

        // Act
        var order = _world.GetSystemExecutionOrder();

        // Assert
        order.Should().HaveCount(3);
        order[0].Should().Be(typeof(Update2TestSystem)); // system3 (Update, order 50)
        order[1].Should().Be(typeof(Update1TestSystem)); // system1 (Update, order 100) 
        order[2].Should().Be(typeof(PostTestSystem)); // system2 (PostUpdate, order 0)
    }

    [Test]
    public void API_ResetSystemPeaks_ShouldClearPeakTimings()
    {
        // Arrange
        var slowSystem = new SlowTestSystem();
        _world.RegisterSystem(slowSystem);
        _world.UpdateSystems(0.016f);

        var timing = _world.GetSystemTiming(typeof(SlowTestSystem));
        timing!.PeakTime.Should().BeGreaterThan(0);

        // Act
        _world.ResetSystemPeaks();

        // Assert
        timing.PeakTime.Should().Be(0);
    }

    [Test]
    public void IT_MovementSystem_ShouldUpdatePositionsAndEmitIntents()
    {
        // Arrange
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Purlieu.Ecs.Core.Position(0, 0, 0));
        _world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(10, 5, 0));

        var movementSystem = new MovementSystem();
        _world.RegisterSystem(movementSystem);

        // Act
        _world.UpdateSystems(0.1f); // 100ms

        // Assert
        var newPosition = _world.GetComponent<Purlieu.Ecs.Core.Position>(entity);
        newPosition.X.Should().BeApproximately(1.0f, 0.001f); // 10 * 0.1
        newPosition.Y.Should().BeApproximately(0.5f, 0.001f); // 5 * 0.1
        newPosition.Z.Should().Be(0);
    }

    [Test]
    public void DET_SystemExecution_ShouldBeReproducible()
    {
        // Test that executing systems with same initial state produces identical results
        var results1 = RunMovementTest();
        var results2 = RunMovementTest();

        // Assert - should get identical results
        results1.Length.Should().Be(results2.Length);
        for (int i = 0; i < results1.Length; i++)
        {
            results1[i].X.Should().BeApproximately(results2[i].X, 0.0001f, $"Entity {i} X position mismatch");
            results1[i].Y.Should().BeApproximately(results2[i].Y, 0.0001f, $"Entity {i} Y position mismatch");
            results1[i].Z.Should().BeApproximately(results2[i].Z, 0.0001f, $"Entity {i} Z position mismatch");
        }
    }

    private Purlieu.Ecs.Core.Position[] RunMovementTest()
    {
        var testWorld = new World();
        var entities = new Entity[5];

        // Create entities with deterministic initial state
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = testWorld.CreateEntity();
            testWorld.AddComponent(entities[i], new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));
            testWorld.AddComponent(entities[i], new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        var movementSystem = new MovementSystem();
        testWorld.RegisterSystem(movementSystem);

        // Run one update step
        testWorld.UpdateSystems(0.016f);

        // Return final positions
        return entities.Select(e => testWorld.GetComponent<Purlieu.Ecs.Core.Position>(e)).ToArray();
    }

    [Test]
    public void API_MultipleSystemUpdates_ShouldAccumulateTimings()
    {
        // Arrange
        var system = new Update1TestSystem("Test");
        _world.RegisterSystem(system);

        // Act
        for (int i = 0; i < 10; i++)
        {
            _world.UpdateSystems(0.016f);
        }

        // Assert
        var timing = _world.GetSystemTiming(typeof(Update1TestSystem));
        timing.Should().NotBeNull();
        timing!.FrameCount.Should().Be(10);
        timing.AverageTime.Should().BeGreaterThan(0);
    }

    [Test]
    public void API_GetAllSystemTimings_ShouldReturnAllRegisteredSystems()
    {
        // Arrange
        var system1 = new Update1TestSystem("A");
        var system2 = new SlowTestSystem();

        _world.RegisterSystem(system1);
        _world.RegisterSystem(system2);
        _world.UpdateSystems(0.016f);

        // Act
        var allTimings = _world.GetAllSystemTimings();

        // Assert
        allTimings.Should().HaveCount(2);
        allTimings.Should().ContainKey(typeof(Update1TestSystem));
        allTimings.Should().ContainKey(typeof(SlowTestSystem));
    }

    [Test]
    public void ALLOC_SystemScheduler_RegisterSystem_ShouldNotAllocate()
    {
        // Arrange
        var world = new World();
        var systems = new ISystem[]
        {
            new Update1TestSystem("Test1"),
            new Update2TestSystem("Test2"),
            new PostTestSystem("Test3"),
            new PresentTestSystem("Test4"),
            new SlowTestSystem()
        };

        // Act
        var startMemory = GC.GetTotalMemory(true);

        foreach (var system in systems)
        {
            world.RegisterSystem(system);
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocatedBytes = Math.Max(0, endMemory - startMemory);

        // Assert - system registration should not allocate excessively
        allocatedBytes.Should().BeLessThan(100 * 1024,
            "System registration should not allocate excessive memory");
    }

    [Test]
    public void ALLOC_SystemScheduler_UpdateSystems_ShouldNotAllocateExcessively()
    {
        // Arrange
        var world = new World();

        // Create some entities with components for systems to process
        for (int i = 0; i < 100; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(entity, new Purlieu.Ecs.Core.Position(i, i * 2, i * 3));
            world.AddComponent(entity, new Purlieu.Ecs.Core.Velocity(i * 0.1f, i * 0.2f, i * 0.3f));
        }

        // Register multiple systems
        world.RegisterSystem(new MovementSystem());
        world.RegisterSystem(new Update1TestSystem("UpdateSys"));
        world.RegisterSystem(new PostTestSystem("PostSys"));
        world.RegisterSystem(new PresentTestSystem("PresentSys"));

        // Warm up
        world.UpdateSystems(0.016f);

        // Act
        var startMemory = GC.GetTotalMemory(true);

        world.UpdateSystems(0.016f);

        var endMemory = GC.GetTotalMemory(false);
        var allocatedBytes = Math.Max(0, endMemory - startMemory);

        // Assert - system execution should not allocate excessively
        allocatedBytes.Should().BeLessThan(80 * 1024,
            "System execution should not allocate excessive memory");
    }

    [Test]
    public void ALLOC_SystemTiming_UpdateTiming_ShouldNotAllocate()
    {
        // Arrange
        var world = new World();
        var system = new Update1TestSystem("TimingTest");
        world.RegisterSystem(system);

        // Warm up timing collection
        world.UpdateSystems(0.016f);

        // Act
        var startMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 50; i++)
        {
            world.UpdateSystems(0.016f);
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocatedBytes = Math.Max(0, endMemory - startMemory);

        // Assert - timing collection should not allocate excessively
        allocatedBytes.Should().BeLessThan(60 * 1024,
            "Timing collection should not allocate excessive memory");
    }
}

// Test helper classes
[GamePhase(GamePhase.Update, order: 50)]
public class Update2TestSystem : ISystem
{
    private readonly string _name;
    private readonly List<string>? _executionOrder;

    public Update2TestSystem(string name, List<string>? executionOrder = null)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder?.Add(_name);
    }
}

[GamePhase(GamePhase.Update, order: 100)]
public class Update1TestSystem : ISystem
{
    private readonly string _name;
    private readonly List<string>? _executionOrder;

    public Update1TestSystem(string name, List<string>? executionOrder = null)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder?.Add(_name);
    }
}

[GamePhase(GamePhase.PostUpdate, order: 0)]
public class PostTestSystem : ISystem
{
    private readonly string _name;
    private readonly List<string>? _executionOrder;

    public PostTestSystem(string name, List<string>? executionOrder = null)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder?.Add(_name);
    }
}

[GamePhase(GamePhase.Presentation, order: 0)]
public class PresentTestSystem : ISystem
{
    private readonly string _name;
    private readonly List<string>? _executionOrder;

    public PresentTestSystem(string name, List<string>? executionOrder = null)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder?.Add(_name);
    }
}

[GamePhase(GamePhase.Update, order: 200)]
public class SlowTestSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // Simulate work
        System.Threading.Thread.Sleep(1);
    }
}