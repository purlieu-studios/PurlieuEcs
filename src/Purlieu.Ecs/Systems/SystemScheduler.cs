using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Systems;

/// <summary>
/// Schedules and executes systems in phase order with performance profiling.
/// </summary>
public sealed class SystemScheduler
{
    private readonly List<SystemEntry> _systems;
    private readonly Dictionary<Type, SystemTiming> _timings;

    public SystemScheduler()
    {
        _systems = new List<SystemEntry>();
        _timings = new Dictionary<Type, SystemTiming>();
    }

    /// <summary>
    /// Registers a system for execution.
    /// </summary>
    /// <param name="system">The system instance to register</param>
    public void RegisterSystem(ISystem system)
    {
        if (system == null)
            throw new ArgumentNullException(nameof(system));

        var systemType = system.GetType();
        var phaseAttr = systemType.GetCustomAttribute<GamePhaseAttribute>();

        var phase = phaseAttr?.Phase ?? GamePhase.Update;
        var order = phaseAttr?.Order ?? 0;

        var entry = new SystemEntry(system, phase, order, systemType);

        // Insert in sorted order
        var insertIndex = FindInsertionIndex(entry);
        _systems.Insert(insertIndex, entry);

        // Initialize timing data
        _timings[systemType] = new SystemTiming();
    }

    /// <summary>
    /// Executes all registered systems in phase order.
    /// </summary>
    /// <param name="world">The world to update</param>
    /// <param name="deltaTime">Time elapsed since last frame</param>
    public void UpdateSystems(World world, float deltaTime)
    {
        foreach (var entry in _systems)
        {
            var timing = _timings[entry.SystemType];
            var stopwatch = Stopwatch.StartNew();

            entry.System.Update(world, deltaTime);

            stopwatch.Stop();
            timing.UpdateTiming(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Gets timing information for a specific system type.
    /// </summary>
    /// <param name="systemType">The system type to get timing for</param>
    /// <returns>Timing information or null if system not found</returns>
    public SystemTiming? GetSystemTiming(Type systemType)
    {
        return _timings.TryGetValue(systemType, out var timing) ? timing : null;
    }

    /// <summary>
    /// Gets timing information for all registered systems.
    /// </summary>
    /// <returns>Dictionary of system types to their timing information</returns>
    public IReadOnlyDictionary<Type, SystemTiming> GetAllTimings()
    {
        return _timings;
    }

    /// <summary>
    /// Resets peak timing values for all systems.
    /// </summary>
    public void ResetPeaks()
    {
        foreach (var timing in _timings.Values)
        {
            timing.ResetPeak();
        }
    }

    /// <summary>
    /// Gets the execution order of registered systems.
    /// </summary>
    /// <returns>Ordered list of system types</returns>
    public IReadOnlyList<Type> GetExecutionOrder()
    {
        return _systems.Select(s => s.SystemType).ToList();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindInsertionIndex(SystemEntry newEntry)
    {
        for (int i = 0; i < _systems.Count; i++)
        {
            var existing = _systems[i];

            // Compare by phase first, then by order
            if (newEntry.Phase < existing.Phase ||
                (newEntry.Phase == existing.Phase && newEntry.Order < existing.Order))
            {
                return i;
            }
        }
        return _systems.Count;
    }

    private sealed class SystemEntry
    {
        public ISystem System { get; }
        public GamePhase Phase { get; }
        public int Order { get; }
        public Type SystemType { get; }

        public SystemEntry(ISystem system, GamePhase phase, int order, Type systemType)
        {
            System = system;
            Phase = phase;
            Order = order;
            SystemType = systemType;
        }
    }
}

/// <summary>
/// Timing information for a system.
/// </summary>
public sealed class SystemTiming
{
    private const int RollingAverageFrames = 30;
    private readonly Queue<double> _recentTimings = new();
    private double _totalTime;

    /// <summary>
    /// Current frame execution time in milliseconds.
    /// </summary>
    public double CurrentTime { get; private set; }

    /// <summary>
    /// Rolling average execution time over the last 30 frames in milliseconds.
    /// </summary>
    public double AverageTime { get; private set; }

    /// <summary>
    /// Peak execution time since last reset in milliseconds.
    /// </summary>
    public double PeakTime { get; private set; }

    /// <summary>
    /// Total number of frames this system has executed.
    /// </summary>
    public int FrameCount { get; private set; }

    internal void UpdateTiming(double timeMs)
    {
        CurrentTime = timeMs;
        FrameCount++;

        // Update peak
        if (timeMs > PeakTime)
        {
            PeakTime = timeMs;
        }

        // Update rolling average
        _recentTimings.Enqueue(timeMs);
        _totalTime += timeMs;

        if (_recentTimings.Count > RollingAverageFrames)
        {
            _totalTime -= _recentTimings.Dequeue();
        }

        AverageTime = _totalTime / _recentTimings.Count;
    }

    internal void ResetPeak()
    {
        PeakTime = 0.0;
    }

    public override string ToString()
    {
        return $"Current: {CurrentTime:F3}ms, Avg: {AverageTime:F3}ms, Peak: {PeakTime:F3}ms ({FrameCount} frames)";
    }
}
