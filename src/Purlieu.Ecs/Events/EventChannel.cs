using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Events;

/// <summary>
/// High-performance event channel with ring buffer storage for zero-allocation event publishing and consuming.
/// Designed for one-frame event lifetime with automatic clearing support.
/// </summary>
/// <typeparam name="T">Event type - must be struct for zero-allocation semantics</typeparam>
public sealed class EventChannel<T> where T : struct
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private int _head;
    private int _tail;
    private int _count;
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a new event channel with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of events that can be stored (default: 1024)</param>
    public EventChannel(int capacity = 1024)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

        _capacity = capacity;
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the current number of events in the channel.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the channel.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets whether the channel is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets whether the channel is at capacity.
    /// </summary>
    public bool IsFull => Count == _capacity;

    /// <summary>
    /// Publishes an event to the channel. If the channel is full, the oldest event is overwritten.
    /// This method is zero-allocation for struct events.
    /// </summary>
    /// <param name="eventData">The event data to publish</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(in T eventData)
    {
        lock (_lock)
        {
            _buffer[_tail] = eventData;
            _tail = (_tail + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                // Buffer is full, advance head to overwrite oldest event
                _head = (_head + 1) % _capacity;
            }
        }
    }

    /// <summary>
    /// Consumes all events in the channel, calling the provided action for each event.
    /// Events are consumed in FIFO order. This method is zero-allocation.
    /// </summary>
    /// <param name="consumer">Action to call for each event</param>
    public void ConsumeAll(Action<T> consumer)
    {
        if (consumer == null)
            throw new ArgumentNullException(nameof(consumer));

        lock (_lock)
        {
            while (_count > 0)
            {
                var eventData = _buffer[_head];
                _head = (_head + 1) % _capacity;
                _count--;

                // Call consumer outside of critical section for the event data
                // (but we still hold the lock to maintain consistency)
                consumer(eventData);
            }
        }
    }

    /// <summary>
    /// Consumes all events in the channel, calling the provided action for each event by reference.
    /// Events are consumed in FIFO order. This method is zero-allocation and allows in-place processing.
    /// </summary>
    /// <param name="consumer">Action to call for each event by reference</param>
    public void ConsumeAll(RefAction<T> consumer)
    {
        if (consumer == null)
            throw new ArgumentNullException(nameof(consumer));

        lock (_lock)
        {
            while (_count > 0)
            {
                ref var eventData = ref _buffer[_head];
                _head = (_head + 1) % _capacity;
                _count--;

                // Call consumer with ref to avoid copying
                consumer(ref eventData);
            }
        }
    }

    /// <summary>
    /// Clears all events from the channel.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;

            // Clear the buffer to avoid holding references to old events
            Array.Clear(_buffer, 0, _capacity);
        }
    }

    /// <summary>
    /// Tries to peek at the next event without consuming it.
    /// </summary>
    /// <param name="eventData">The next event data if available</param>
    /// <returns>True if an event was available, false if the channel is empty</returns>
    public bool TryPeek(out T eventData)
    {
        lock (_lock)
        {
            if (_count > 0)
            {
                eventData = _buffer[_head];
                return true;
            }

            eventData = default;
            return false;
        }
    }

    /// <summary>
    /// Tries to consume a single event from the channel.
    /// </summary>
    /// <param name="eventData">The consumed event data if available</param>
    /// <returns>True if an event was consumed, false if the channel is empty</returns>
    public bool TryConsume(out T eventData)
    {
        lock (_lock)
        {
            if (_count > 0)
            {
                eventData = _buffer[_head];
                _head = (_head + 1) % _capacity;
                _count--;
                return true;
            }

            eventData = default;
            return false;
        }
    }

    /// <summary>
    /// Returns a snapshot of all current events without consuming them.
    /// This allocates a new array and should be used sparingly.
    /// </summary>
    /// <returns>Array containing all current events</returns>
    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0)
                return Array.Empty<T>();

            var result = new T[_count];
            var sourceIndex = _head;

            for (int i = 0; i < _count; i++)
            {
                result[i] = _buffer[sourceIndex];
                sourceIndex = (sourceIndex + 1) % _capacity;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets statistics about the event channel for debugging and monitoring.
    /// </summary>
    /// <returns>Channel statistics</returns>
    public EventChannelStats GetStats()
    {
        lock (_lock)
        {
            return new EventChannelStats
            {
                Count = _count,
                Capacity = _capacity,
                IsEmpty = _count == 0,
                IsFull = _count == _capacity,
                UtilizationPercentage = _capacity > 0 ? (_count * 100.0f) / _capacity : 0f
            };
        }
    }
}

/// <summary>
/// Delegate for consuming events by reference to avoid copying.
/// </summary>
/// <typeparam name="T">Event type</typeparam>
/// <param name="eventData">Event data by reference</param>
public delegate void RefAction<T>(ref T eventData);

/// <summary>
/// Statistics about an event channel's current state.
/// </summary>
public readonly struct EventChannelStats
{
    /// <summary>
    /// Current number of events in the channel.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Maximum capacity of the channel.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Whether the channel is empty.
    /// </summary>
    public bool IsEmpty { get; init; }

    /// <summary>
    /// Whether the channel is at capacity.
    /// </summary>
    public bool IsFull { get; init; }

    /// <summary>
    /// Current utilization as a percentage (0-100).
    /// </summary>
    public float UtilizationPercentage { get; init; }

    public override string ToString()
    {
        return $"EventChannel: {Count}/{Capacity} ({UtilizationPercentage:F1}%)";
    }
}
