using System;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Events;

namespace Purlieu.Ecs.Tests.Events;

[TestFixture]
public class EventChannelAllocationTests
{
    [Test]
    public void ALLOC_EventChannelPublish_ShouldNotAllocate()
    {
        // Arrange
        var channel = new EventChannel<TestEvent>(1000);
        var testEvent = new TestEvent { Id = 42, Message = "Test Event" };

        // Warm up
        channel.Publish(in testEvent);
        channel.Clear();

        // Act
        var startMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 100; i++)
        {
            var evt = new TestEvent { Id = i, Message = $"Event {i}" };
            channel.Publish(in evt);
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - Event publishing should have minimal allocation
        allocated.Should().BeLessThan(25 * 1024, "Event publishing should not allocate significantly");
    }

    [Test]
    public void ALLOC_EventChannelConsume_ShouldNotAllocate()
    {
        // Arrange
        var channel = new EventChannel<TestEvent>(1000);

        // Pre-populate channel
        for (int i = 0; i < 500; i++)
        {
            channel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }

        // Warm up
        var consumeCount = 0;
        channel.ConsumeAll(evt => consumeCount++);

        // Repopulate
        for (int i = 0; i < 500; i++)
        {
            channel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }

        // Act
        var startMemory = GC.GetTotalMemory(true);

        consumeCount = 0;
        channel.ConsumeAll(evt =>
        {
            consumeCount++;
            // Do minimal work to prevent optimization
            _ = evt.Id + evt.Message?.Length ?? 0;
        });

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - Event consumption should have minimal allocation
        allocated.Should().BeLessThan(25 * 1024, "Event consumption should not allocate significantly");
        consumeCount.Should().Be(500);
    }

    [Test]
    public void ALLOC_EventChannelTryOperations_ShouldNotAllocate()
    {
        // Arrange
        var channel = new EventChannel<TestEvent>(100);

        // Pre-populate
        for (int i = 0; i < 50; i++)
        {
            channel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }

        // Act
        var startMemory = GC.GetTotalMemory(true);

        // Perform many try operations
        for (int i = 0; i < 100; i++)
        {
            channel.TryPeek(out var peekedEvent);
            _ = peekedEvent.Id; // Use the result

            if (i % 2 == 0 && channel.TryConsume(out var consumedEvent))
            {
                _ = consumedEvent.Message; // Use the result
            }
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - Try operations should have minimal allocation
        allocated.Should().BeLessThan(100 * 1024, "Try operations should not allocate excessively");
    }

    [Test]
    public void ALLOC_EventChannelRingBufferReuse_ShouldNotGrowMemory()
    {
        // Arrange
        var channel = new EventChannel<TestEvent>(100);

        // Act - Simulate many cycles of fill and empty
        var startMemory = GC.GetTotalMemory(true);

        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Fill channel
            for (int i = 0; i < 150; i++) // Overfill to test ring buffer
            {
                channel.Publish(new TestEvent { Id = i, Message = $"Cycle {cycle} Event {i}" });
            }

            // Empty channel
            var consumedCount = 0;
            channel.ConsumeAll(evt => consumedCount++);
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - Ring buffer reuse should not grow memory significantly
        allocated.Should().BeLessThan(200 * 1024, "Ring buffer reuse should not cause excessive memory growth");
    }

    [Test]
    public void ALLOC_EventChannelStats_ShouldNotAllocateExcessively()
    {
        // Arrange
        var channel = new EventChannel<TestEvent>(1000);

        // Populate with some events
        for (int i = 0; i < 100; i++)
        {
            channel.Publish(new TestEvent { Id = i, Message = $"Event {i}" });
        }

        // Act
        var startMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 1000; i++)
        {
            var stats = channel.GetStats();
            _ = stats.Count; // Use the result
            _ = stats.ToString(); // This will allocate string but should be minimal
        }

        var endMemory = GC.GetTotalMemory(false);
        var allocated = Math.Max(0, endMemory - startMemory);

        // Assert - Stats collection should have controlled allocation
        allocated.Should().BeLessThan(150 * 1024, "Stats collection should have controlled allocation");
    }
}