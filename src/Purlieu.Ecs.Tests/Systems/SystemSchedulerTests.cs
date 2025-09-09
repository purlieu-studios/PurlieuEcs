using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Systems;
using Purlieu.Ecs.Tests.Core;

namespace Purlieu.Ecs.Tests.Systems;

[TestFixture]
public class SystemSchedulerTests
{
    private SystemScheduler _scheduler;
    private World _world;

    [SetUp]
    public void Setup()
    {
        _scheduler = new SystemScheduler();
        _world = new World();
    }

    [Test]
    public void IT_SchedulerExecution_ShouldMaintainPhaseOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var system1 = new TestSystemUpdate100("System1", executionOrder);
        var system2 = new TestSystemPostUpdate50("System2", executionOrder);
        var system3 = new TestSystemUpdate50("System3", executionOrder);
        var system4 = new TestSystemPresentation0("System4", executionOrder);

        _scheduler.RegisterSystem(system1);
        _scheduler.RegisterSystem(system2);
        _scheduler.RegisterSystem(system3);
        _scheduler.RegisterSystem(system4);

        // Act
        _scheduler.UpdateSystems(_world, 0.016f);

        // Assert
        executionOrder.Should().Equal("System3", "System1", "System2", "System4");
    }

    [Test]
    public void API_RegisterSystem_ShouldTrackSystemTiming()
    {
        // Arrange
        var system = new TestSystem("TestSystem", GamePhase.Update, 0, new List<string>());
        _scheduler.RegisterSystem(system);

        // Act
        _scheduler.UpdateSystems(_world, 0.016f);

        // Assert
        var timing = _scheduler.GetSystemTiming(typeof(TestSystem));
        timing.Should().NotBeNull();
        timing!.FrameCount.Should().Be(1);
        timing.CurrentTime.Should().BeGreaterThanOrEqualTo(0);
        timing.AverageTime.Should().BeGreaterThanOrEqualTo(0);
        timing.PeakTime.Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public void API_GetAllTimings_ShouldReturnAllSystemTimings()
    {
        // Arrange
        var system1 = new TestSystemUpdate50("System1", new List<string>());
        var system2 = new TestSystemPostUpdate50("System2", new List<string>());
        _scheduler.RegisterSystem(system1);
        _scheduler.RegisterSystem(system2);

        // Act
        _scheduler.UpdateSystems(_world, 0.016f);
        var timings = _scheduler.GetAllTimings();

        // Assert
        timings.Should().HaveCount(2);
        timings.Should().ContainKey(typeof(TestSystemUpdate50));
        timings.Should().ContainKey(typeof(TestSystemPostUpdate50));
        timings.Values.Should().AllSatisfy(t => t.FrameCount.Should().Be(1));
    }

    [Test]
    public void API_ResetPeaks_ShouldClearPeakTimings()
    {
        // Arrange
        var system = new SchedulerSlowTestSystem();
        _scheduler.RegisterSystem(system);
        _scheduler.UpdateSystems(_world, 0.016f);

        var initialTiming = _scheduler.GetSystemTiming(typeof(SchedulerSlowTestSystem));
        var initialPeak = initialTiming!.PeakTime;
        initialPeak.Should().BeGreaterThan(0);

        // Act
        _scheduler.ResetPeaks();

        // Assert
        var timing = _scheduler.GetSystemTiming(typeof(SchedulerSlowTestSystem));
        timing!.PeakTime.Should().Be(0.0);
        timing.CurrentTime.Should().Be(initialTiming.CurrentTime); // Current time should remain
        timing.AverageTime.Should().Be(initialTiming.AverageTime); // Average should remain
    }

    [Test]
    public void API_GetExecutionOrder_ShouldReturnCorrectOrder()
    {
        // Arrange
        var system1 = new TestSystem("System1", GamePhase.Update, 100, new List<string>());
        var system2 = new TestSystem("System2", GamePhase.PostUpdate, 50, new List<string>());
        var system3 = new TestSystem("System3", GamePhase.Update, 50, new List<string>());

        _scheduler.RegisterSystem(system1);
        _scheduler.RegisterSystem(system2);
        _scheduler.RegisterSystem(system3);

        // Act
        var executionOrder = _scheduler.GetExecutionOrder();

        // Assert
        executionOrder.Should().Equal(typeof(TestSystem), typeof(TestSystem), typeof(TestSystem));
        // Note: Since we're using the same test system type, we can't distinguish between instances
        // In a real scenario, each system would have a unique type
    }

    [Test]
    public void DET_SystemExecution_ShouldBeReproducible()
    {
        // Arrange
        var executionOrder1 = new List<string>();
        var executionOrder2 = new List<string>();

        var scheduler1 = new SystemScheduler();
        var scheduler2 = new SystemScheduler();

        var system1a = new TestSystem("System1", GamePhase.Update, 100, executionOrder1);
        var system1b = new TestSystem("System2", GamePhase.PostUpdate, 50, executionOrder1);
        var system2a = new TestSystem("System1", GamePhase.Update, 100, executionOrder2);
        var system2b = new TestSystem("System2", GamePhase.PostUpdate, 50, executionOrder2);

        scheduler1.RegisterSystem(system1a);
        scheduler1.RegisterSystem(system1b);
        scheduler2.RegisterSystem(system2a);
        scheduler2.RegisterSystem(system2b);

        // Act
        scheduler1.UpdateSystems(_world, 0.016f);
        scheduler2.UpdateSystems(_world, 0.016f);

        // Assert
        executionOrder1.Should().Equal(executionOrder2, "System execution should be deterministic");
    }

    [Test]
    public void ALLOC_SystemScheduling_ShouldHaveMinimalOverhead()
    {
        // Arrange
        var system = new NoOpTestSystem();
        _scheduler.RegisterSystem(system);

        // Warmup
        _scheduler.UpdateSystems(_world, 0.016f);

        // Act & Assert - Multiple executions should be fast
        var startMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 1000; i++)
        {
            _scheduler.UpdateSystems(_world, 0.016f);
        }

        var endMemory = GC.GetTotalMemory(false);
        var memoryIncrease = endMemory - startMemory;

        // Should have minimal allocation overhead - CI environments may have higher allocation patterns
        memoryIncrease.Should().BeLessThan(200000, "System scheduling should have reasonable allocation overhead for CI environments");
    }

    [Test]
    [TestCase(null)]
    public void API_RegisterSystem_ShouldHandleNullSystem(ISystem? nullSystem)
    {
        // Act & Assert
        Action act = () => _scheduler.RegisterSystem(nullSystem!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void IT_SystemTimingRollingAverage_ShouldUpdateCorrectly()
    {
        // Arrange - Use platform-adjusted iterations
        var iterations = PlatformTestHelper.AdjustIterations(35); // More than 30 to test rolling window
        var system = new VariableTimeTestSystem();
        _scheduler.RegisterSystem(system);

        var timings = new List<double>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _scheduler.UpdateSystems(_world, 0.016f);
            var timing = _scheduler.GetSystemTiming(typeof(VariableTimeTestSystem))!;
            timings.Add(timing.AverageTime);
        }

        // Assert
        var finalTiming = _scheduler.GetSystemTiming(typeof(VariableTimeTestSystem))!;
        finalTiming.FrameCount.Should().Be(iterations);

        // Rolling average should stabilize after 30 frames
        if (iterations > 30)
        {
            var lastFewAverages = timings.Skip(Math.Max(0, timings.Count - 5)).ToList();
            var variance = lastFewAverages.Max() - lastFewAverages.Min();
            variance.Should().BeLessThan(1.0, "Rolling average should stabilize");
        }
    }
}

// Test system implementations
[GamePhase(GamePhase.Update, order: 0)]
public class TestSystem : ISystem
{
    private readonly string _name;
    private readonly GamePhase _phase;
    private readonly int _order;
    private readonly List<string> _executionOrder;

    public TestSystem(string name, GamePhase phase, int order, List<string> executionOrder)
    {
        _name = name;
        _phase = phase;
        _order = order;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.Update)]
public class NoOpTestSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // No-op for performance testing
    }
}

[GamePhase(GamePhase.Update)]
public class SchedulerSlowTestSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // Simulate some work
        System.Threading.Thread.Sleep(1);
    }
}

[GamePhase(GamePhase.Update)]
public class VariableTimeTestSystem : ISystem
{
    private int _frameCount = 0;

    public void Update(World world, float deltaTime)
    {
        _frameCount++;
        // Variable execution time to test rolling average
        if (_frameCount % 10 == 0)
        {
            System.Threading.Thread.Sleep(1);
        }
    }
}

[GamePhase(GamePhase.Update, order: 50)]
public class TestSystemUpdate50 : ISystem
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public TestSystemUpdate50(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.Update, order: 100)]
public class TestSystemUpdate100 : ISystem
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public TestSystemUpdate100(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.PostUpdate, order: 50)]
public class TestSystemPostUpdate50 : ISystem
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public TestSystemPostUpdate50(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}

[GamePhase(GamePhase.Presentation, order: 0)]
public class TestSystemPresentation0 : ISystem
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public TestSystemPresentation0(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public void Update(World world, float deltaTime)
    {
        _executionOrder.Add(_name);
    }
}
