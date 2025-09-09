using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Events;

namespace Purlieu.Ecs.Tests.Events;

[TestFixture]
public class EventChannelTests
{
    private EventChannel<TestEvent> _channel;

    [SetUp]
    public void Setup()
    {
        _channel = new EventChannel<TestEvent>(4); // Small capacity for testing
    }

    [Test]
    public void API_EventChannel_Construction_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var channel = new EventChannel<TestEvent>(10);

        // Assert
        channel.Count.Should().Be(0);
        channel.Capacity.Should().Be(10);
        channel.IsEmpty.Should().BeTrue();
        channel.IsFull.Should().BeFalse();
    }

    [Test]
    public void API_EventChannel_Construction_WithInvalidCapacity_ShouldThrow()
    {
        // Act & Assert
        Action action = () => new EventChannel<TestEvent>(0);
        action.Should().Throw<ArgumentException>();

        Action action2 = () => new EventChannel<TestEvent>(-1);
        action2.Should().Throw<ArgumentException>();
    }

    [Test]
    public void API_EventChannel_PublishConsume_ShouldWorkCorrectly()
    {
        // Arrange
        var events = new[]
        {
            new TestEvent { Id = 1, Message = "First" },
            new TestEvent { Id = 2, Message = "Second" },
            new TestEvent { Id = 3, Message = "Third" }
        };

        // Act - Publish events
        foreach (var evt in events)
        {
            _channel.Publish(in evt);
        }

        // Assert - Channel state
        _channel.Count.Should().Be(3);
        _channel.IsEmpty.Should().BeFalse();
        _channel.IsFull.Should().BeFalse();

        // Act - Consume events
        var consumedEvents = new List<TestEvent>();
        _channel.ConsumeAll(evt => consumedEvents.Add(evt));

        // Assert - Events consumed in FIFO order
        consumedEvents.Should().HaveCount(3);
        consumedEvents[0].Should().BeEquivalentTo(events[0]);
        consumedEvents[1].Should().BeEquivalentTo(events[1]);
        consumedEvents[2].Should().BeEquivalentTo(events[2]);

        // Assert - Channel is empty after consumption
        _channel.Count.Should().Be(0);
        _channel.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void API_EventChannel_TryConsume_ShouldWorkCorrectly()
    {
        // Arrange
        var testEvent = new TestEvent { Id = 42, Message = "Test" };
        _channel.Publish(in testEvent);

        // Act & Assert - Successful consumption
        _channel.TryConsume(out var consumedEvent).Should().BeTrue();
        consumedEvent.Should().BeEquivalentTo(testEvent);

        // Act & Assert - Empty channel
        _channel.TryConsume(out var emptyEvent).Should().BeFalse();
        emptyEvent.Should().BeEquivalentTo(default(TestEvent));
    }

    [Test]
    public void API_EventChannel_TryPeek_ShouldWorkCorrectly()
    {
        // Arrange
        var testEvent = new TestEvent { Id = 123, Message = "Peek Test" };
        _channel.Publish(in testEvent);

        // Act & Assert - Successful peek
        _channel.TryPeek(out var peekedEvent).Should().BeTrue();
        peekedEvent.Should().BeEquivalentTo(testEvent);
        _channel.Count.Should().Be(1); // Should not consume

        // Act & Assert - Peek again
        _channel.TryPeek(out var peekedEvent2).Should().BeTrue();
        peekedEvent2.Should().BeEquivalentTo(testEvent);
        _channel.Count.Should().Be(1); // Still should not consume

        // Act & Assert - Consume and then peek empty
        _channel.TryConsume(out _);
        _channel.TryPeek(out var emptyPeek).Should().BeFalse();
        emptyPeek.Should().BeEquivalentTo(default(TestEvent));
    }

    [Test]
    public void API_EventChannel_Clear_ShouldRemoveAllEvents()
    {
        // Arrange
        _channel.Publish(new TestEvent { Id = 1, Message = "First" });
        _channel.Publish(new TestEvent { Id = 2, Message = "Second" });
        _channel.Count.Should().Be(2);

        // Act
        _channel.Clear();

        // Assert
        _channel.Count.Should().Be(0);
        _channel.IsEmpty.Should().BeTrue();
        _channel.TryConsume(out _).Should().BeFalse();
    }

    [Test]
    public void API_EventChannel_RingBufferOverflow_ShouldOverwriteOldest()
    {
        // Arrange - Fill channel to capacity (4 events)
        for (int i = 1; i <= 4; i++)
        {
            _channel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }
        _channel.IsFull.Should().BeTrue();

        // Act - Add one more event (should overwrite the first)
        _channel.Publish(new TestEvent { Id = 5, Message = "Event 5" });

        // Assert - Still at capacity, oldest event overwritten
        _channel.Count.Should().Be(4);
        _channel.IsFull.Should().BeTrue();

        // Act - Consume all events
        var consumedEvents = new List<TestEvent>();
        _channel.ConsumeAll(evt => consumedEvents.Add(evt));

        // Assert - Should have events 2, 3, 4, 5 (event 1 was overwritten)
        consumedEvents.Should().HaveCount(4);
        consumedEvents[0].Id.Should().Be(2);
        consumedEvents[1].Id.Should().Be(3);
        consumedEvents[2].Id.Should().Be(4);
        consumedEvents[3].Id.Should().Be(5);
    }

    [Test]
    public void API_EventChannel_ConsumeAllWithRefAction_ShouldWorkCorrectly()
    {
        // Arrange
        _channel.Publish(new TestEvent { Id = 1, Message = "Test" });
        _channel.Publish(new TestEvent { Id = 2, Message = "Test" });

        // Act - Consume with ref action (allows modification)
        var consumedCount = 0;
        _channel.ConsumeAll((ref TestEvent evt) =>
        {
            evt.Message = $"Modified {evt.Id}"; // Modify the event
            consumedCount++;
        });

        // Assert
        consumedCount.Should().Be(2);
        _channel.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void API_EventChannel_ToArray_ShouldReturnSnapshot()
    {
        // Arrange
        var events = new[]
        {
            new TestEvent { Id = 1, Message = "First" },
            new TestEvent { Id = 2, Message = "Second" }
        };

        foreach (var evt in events)
        {
            _channel.Publish(in evt);
        }

        // Act
        var snapshot = _channel.ToArray();

        // Assert
        snapshot.Should().HaveCount(2);
        snapshot[0].Should().BeEquivalentTo(events[0]);
        snapshot[1].Should().BeEquivalentTo(events[1]);

        // Events should still be in channel
        _channel.Count.Should().Be(2);
    }

    [Test]
    public void API_EventChannel_ToArrayEmpty_ShouldReturnEmptyArray()
    {
        // Act
        var snapshot = _channel.ToArray();

        // Assert
        snapshot.Should().BeEmpty();
        snapshot.Should().BeSameAs(Array.Empty<TestEvent>());
    }

    [Test]
    public void API_EventChannel_GetStats_ShouldReturnCorrectStats()
    {
        // Arrange - Add 2 events to channel with capacity 4
        _channel.Publish(new TestEvent { Id = 1, Message = "First" });
        _channel.Publish(new TestEvent { Id = 2, Message = "Second" });

        // Act
        var stats = _channel.GetStats();

        // Assert
        stats.Count.Should().Be(2);
        stats.Capacity.Should().Be(4);
        stats.IsEmpty.Should().BeFalse();
        stats.IsFull.Should().BeFalse();
        stats.UtilizationPercentage.Should().BeApproximately(50.0f, 0.1f);
        stats.ToString().Should().Contain("2/4").And.Contain("50.0%");
    }

    [Test]
    public void API_EventChannel_ConsumeAllWithNullAction_ShouldThrow()
    {
        // Act & Assert
        Action action1 = () => _channel.ConsumeAll((Action<TestEvent>)null);
        action1.Should().Throw<ArgumentNullException>();

        Action action2 = () => _channel.ConsumeAll((RefAction<TestEvent>)null);
        action2.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void PERF_EventChannel_LargeVolumePublishConsume_ShouldBeEfficient()
    {
        // Arrange
        var largeChannel = new EventChannel<TestEvent>(10000);
        var eventCount = 5000;

        // Act - Publish many events
        var publishStart = DateTime.UtcNow;
        for (int i = 0; i < eventCount; i++)
        {
            largeChannel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }
        var publishTime = DateTime.UtcNow - publishStart;

        // Act - Consume all events
        var consumeStart = DateTime.UtcNow;
        var consumedCount = 0;
        largeChannel.ConsumeAll(evt => consumedCount++);
        var consumeTime = DateTime.UtcNow - consumeStart;

        // Assert - Performance should be reasonable
        publishTime.TotalMilliseconds.Should().BeLessThan(100, "Publishing should be fast");
        consumeTime.TotalMilliseconds.Should().BeLessThan(50, "Consuming should be fast");
        consumedCount.Should().Be(eventCount);
    }

    [Test]
    public void DET_EventChannel_OrderingConsistency_ShouldBeDeterministic()
    {
        // Test multiple times to ensure consistent ordering
        for (int run = 0; run < 5; run++)
        {
            var channel = new EventChannel<TestEvent>(100);
            var expectedOrder = new List<int>();

            // Publish events in order
            for (int i = 0; i < 50; i++)
            {
                var eventId = i * 2; // Use even numbers
                channel.Publish(new TestEvent { Id = eventId, Message = $"Event {eventId}" });
                expectedOrder.Add(eventId);
            }

            // Consume and verify order
            var actualOrder = new List<int>();
            channel.ConsumeAll(evt => actualOrder.Add(evt.Id));

            actualOrder.Should().Equal(expectedOrder, $"Run {run + 1} should maintain FIFO order");
        }
    }
}

// Test event struct for testing
public struct TestEvent
{
    public int Id;
    public string Message;
}