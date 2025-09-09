using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Events;

namespace Purlieu.Ecs.Tests.Events;

[TestFixture]
public class WorldEventTests
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World();
    }

    [Test]
    public void API_WorldEvents_ShouldCreateAndReturnChannels()
    {
        // Act
        var channel1 = _world.Events<WorldTestEvent>();
        var channel2 = _world.Events<WorldTestEvent>();
        var channel3 = _world.Events<AnotherWorldTestEvent>();

        // Assert - Same type returns same channel
        channel1.Should().BeSameAs(channel2);

        // Assert - Different types return different channels
        channel1.Should().NotBeSameAs(channel3);

        // Assert - Channels have correct properties
        channel1.Capacity.Should().Be(1024); // Default capacity
        channel1.IsEmpty.Should().BeTrue();

        _world.EventChannelCount.Should().Be(2); // Two different event types
    }

    [Test]
    public void API_WorldEvents_WithCustomCapacity_ShouldCreateChannelWithCorrectCapacity()
    {
        // Act
        var channel = _world.Events<WorldTestEvent>(2048);

        // Assert
        channel.Capacity.Should().Be(2048);
        _world.EventChannelCount.Should().Be(1);
    }

    [Test]
    public void API_WorldEvents_PublishAndConsume_ShouldWorkCorrectly()
    {
        // Arrange
        var channel = _world.Events<WorldTestEvent>();
        var testEvents = new[]
        {
            new WorldTestEvent { Id = 1, Message = "First" },
            new WorldTestEvent { Id = 2, Message = "Second" }
        };

        // Act - Publish events
        foreach (var evt in testEvents)
        {
            channel.Publish(in evt);
        }

        // Act - Consume events
        var consumedEvents = new List<WorldTestEvent>();
        channel.ConsumeAll(evt => consumedEvents.Add(evt));

        // Assert
        consumedEvents.Should().HaveCount(2);
        consumedEvents[0].Should().BeEquivalentTo(testEvents[0]);
        consumedEvents[1].Should().BeEquivalentTo(testEvents[1]);
    }

    [Test]
    public void API_OneFrameEvents_ShouldBeDetectedAndCleared()
    {
        // Arrange
        var regularChannel = _world.Events<WorldTestEvent>();
        var oneFrameChannel = _world.Events<OneFrameWorldTestEvent>();

        // Act - Publish events to both channels
        regularChannel.Publish(new WorldTestEvent { Id = 1, Message = "Regular" });
        oneFrameChannel.Publish(new OneFrameWorldTestEvent { Id = 2, Message = "OneFrame" });

        // Verify events are present
        regularChannel.Count.Should().Be(1);
        oneFrameChannel.Count.Should().Be(1);

        // Act - Clear one-frame events
        _world.ClearOneFrameEvents();

        // Assert - OneFrame events cleared, regular events remain
        regularChannel.Count.Should().Be(1, "Regular events should not be cleared");
        oneFrameChannel.Count.Should().Be(0, "OneFrame events should be cleared");
    }

    [Test]
    public void API_EventChannelStats_ShouldReturnCorrectStats()
    {
        // Arrange
        var channel1 = _world.Events<WorldTestEvent>();
        var channel2 = _world.Events<AnotherWorldTestEvent>();

        // Populate channels
        channel1.Publish(new WorldTestEvent { Id = 1, Message = "Test" });
        channel2.Publish(new AnotherWorldTestEvent { Value = 42 });
        channel2.Publish(new AnotherWorldTestEvent { Value = 43 });

        // Act
        var stats = _world.GetEventChannelStats();

        // Assert
        stats.Should().HaveCount(2);
        stats.Should().ContainKey(typeof(WorldTestEvent));
        stats.Should().ContainKey(typeof(AnotherWorldTestEvent));

        // Verify stats are correct type (EventChannelStats)
        stats[typeof(WorldTestEvent)].Should().BeOfType<EventChannelStats>();
        stats[typeof(AnotherWorldTestEvent)].Should().BeOfType<EventChannelStats>();
    }

    [Test]
    public void IT_MultipleEventTypes_ShouldWorkIndependently()
    {
        // Arrange
        var testChannel = _world.Events<WorldTestEvent>();
        var anotherChannel = _world.Events<AnotherWorldTestEvent>();
        var oneFrameChannel = _world.Events<OneFrameWorldTestEvent>();

        // Act - Publish different events
        testChannel.Publish(new WorldTestEvent { Id = 1, Message = "Test" });
        anotherChannel.Publish(new AnotherWorldTestEvent { Value = 100 });
        anotherChannel.Publish(new AnotherWorldTestEvent { Value = 200 });
        oneFrameChannel.Publish(new OneFrameWorldTestEvent { Id = 3, Message = "OneFrame" });

        // Assert - Channels maintain independent state
        testChannel.Count.Should().Be(1);
        anotherChannel.Count.Should().Be(2);
        oneFrameChannel.Count.Should().Be(1);
        _world.EventChannelCount.Should().Be(3);

        // Act - Clear one-frame events
        _world.ClearOneFrameEvents();

        // Assert - Only OneFrame events cleared
        testChannel.Count.Should().Be(1);
        anotherChannel.Count.Should().Be(2);
        oneFrameChannel.Count.Should().Be(0);
    }

    [Test]
    public void DET_EventChannelCreation_ShouldBeConsistentAcrossRuns()
    {
        for (int run = 0; run < 5; run++)
        {
            var world = new World();

            // Create channels in consistent order
            var channel1 = world.Events<WorldTestEvent>();
            var channel2 = world.Events<AnotherWorldTestEvent>();
            var channel3 = world.Events<WorldTestEvent>(); // Same as channel1

            // Assert consistent behavior
            channel1.Should().BeSameAs(channel3, $"Run {run + 1}: Same event type should return same channel");
            channel1.Should().NotBeSameAs(channel2, $"Run {run + 1}: Different event types should return different channels");
            world.EventChannelCount.Should().Be(2, $"Run {run + 1}: Should have exactly 2 channel types");
        }
    }

    [Test]
    public void EDGE_ClearOneFrameEvents_WithNoEvents_ShouldNotThrow()
    {
        // Act & Assert - Should not throw even with no events
        Action action = () => _world.ClearOneFrameEvents();
        action.Should().NotThrow();
    }

    [Test]
    public void EDGE_ClearOneFrameEvents_WithOnlyRegularEvents_ShouldNotAffectThem()
    {
        // Arrange
        var channel = _world.Events<WorldTestEvent>();
        channel.Publish(new WorldTestEvent { Id = 1, Message = "Regular" });

        // Act
        _world.ClearOneFrameEvents();

        // Assert - Regular events unaffected
        channel.Count.Should().Be(1);
    }

    [Test]
    public void ALLOC_WorldEventManagement_ShouldNotAllocateExcessively()
    {
        // Act
        var startMemory = GC.GetTotalMemory(true);

        // Create channels and publish events
        for (int i = 0; i < 100; i++)
        {
            var channel = _world.Events<WorldTestEvent>();
            channel.Publish(new WorldTestEvent { Id = i, Message = $"Event {i}" });
        }

        // Clear one-frame events multiple times
        for (int i = 0; i < 50; i++)
        {
            _world.ClearOneFrameEvents();
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - World event management should have controlled allocation
        allocated.Should().BeLessThan(200 * 1024, "World event management should not allocate excessively");
    }
}

// Test event types for World event tests
public struct WorldTestEvent
{
    public int Id;
    public string Message;
}

public struct AnotherWorldTestEvent
{
    public int Value;
}

[OneFrame]
public struct OneFrameWorldTestEvent
{
    public int Id;
    public string Message;
}