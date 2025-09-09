using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Purlieu.Ecs.Events;
using Purlieu.Ecs.Query;
using Purlieu.Ecs.Systems;

namespace Purlieu.Ecs.Core;

public sealed class World
{
    private readonly Dictionary<ComponentSignature, Archetype> _archetypes;
    private readonly Dictionary<Entity, Archetype> _entityToArchetype;
    private uint _nextEntityId;
    private readonly Queue<uint> _freeEntityIds;
    private readonly SystemScheduler _scheduler;
    private readonly Dictionary<Type, object> _eventChannels;
    private readonly HashSet<Type> _oneFrameEventTypes;

    public World()
    {
        _archetypes = new Dictionary<ComponentSignature, Archetype>();
        _entityToArchetype = new Dictionary<Entity, Archetype>();
        _nextEntityId = 1; // Start from 1, reserve 0 for null
        _freeEntityIds = new Queue<uint>();
        _scheduler = new SystemScheduler();
        _eventChannels = new Dictionary<Type, object>();
        _oneFrameEventTypes = new HashSet<Type>();
    }

    public Entity CreateEntity()
    {
        uint id;
        if (_freeEntityIds.Count > 0)
        {
            id = _freeEntityIds.Dequeue();
        }
        else
        {
            id = _nextEntityId++;
        }

        var entity = new Entity(id, 1); // Start with version 1

        // Add to empty archetype initially
        var emptySignature = ComponentSignature.Empty;
        var archetype = GetOrCreateArchetype(emptySignature);
        archetype.AddEntity(entity);
        _entityToArchetype[entity] = archetype;

        return entity;
    }

    public void DestroyEntity(Entity entity)
    {
        if (!_entityToArchetype.TryGetValue(entity, out var archetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        archetype.RemoveEntity(entity);
        _entityToArchetype.Remove(entity);

        // Add ID to free list for reuse (with incremented version)
        _freeEntityIds.Enqueue(entity.Id);
    }

    public bool EntityExists(Entity entity)
    {
        return _entityToArchetype.ContainsKey(entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddComponent<T>(Entity entity, in T component) where T : struct
    {
        if (!_entityToArchetype.TryGetValue(entity, out var currentArchetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        var newSignature = currentArchetype.Signature.With<T>();

        // If signature hasn't changed, just update the component
        if (newSignature == currentArchetype.Signature)
        {
            currentArchetype.SetComponent(entity, component);
            return;
        }

        // Move entity to new archetype
        MoveEntityToArchetype(entity, currentArchetype, newSignature, component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveComponent<T>(Entity entity) where T : struct
    {
        if (!_entityToArchetype.TryGetValue(entity, out var currentArchetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        if (!currentArchetype.Signature.Has<T>())
            return; // Component not present

        var newSignature = currentArchetype.Signature.Without<T>();

        // Move entity to new archetype
        MoveEntityToArchetype(entity, currentArchetype, newSignature);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetComponent<T>(Entity entity) where T : struct
    {
        if (!_entityToArchetype.TryGetValue(entity, out var archetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        return ref archetype.GetComponent<T>(entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComponent<T>(Entity entity, in T component) where T : struct
    {
        if (!_entityToArchetype.TryGetValue(entity, out var archetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        if (!archetype.Signature.Has<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}");

        archetype.SetComponent(entity, component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>(Entity entity) where T : struct
    {
        if (!_entityToArchetype.TryGetValue(entity, out var archetype))
            return false;

        return archetype.HasComponent<T>(entity);
    }

    public Archetype GetOrCreateArchetype(ComponentSignature signature)
    {
        if (!_archetypes.TryGetValue(signature, out var archetype))
        {
            archetype = new Archetype(signature);
            _archetypes[signature] = archetype;
        }
        return archetype;
    }

    public IEnumerable<Archetype> GetArchetypes()
    {
        return _archetypes.Values;
    }

    public ComponentSignature GetEntitySignature(Entity entity)
    {
        if (!_entityToArchetype.TryGetValue(entity, out var archetype))
            throw new ArgumentException($"Entity {entity} does not exist");

        return archetype.Signature;
    }

    private void MoveEntityToArchetype<T>(Entity entity, Archetype fromArchetype, ComponentSignature newSignature, in T newComponent = default) where T : struct
    {
        var toArchetype = GetOrCreateArchetype(newSignature);

        // Copy all existing components except the one being added/removed
        var oldSignature = fromArchetype.Signature;

        // Remove from old archetype
        fromArchetype.RemoveEntity(entity);

        // Add to new archetype
        toArchetype.AddEntity(entity);

        // Copy shared components
        CopySharedComponents(entity, fromArchetype, toArchetype, oldSignature, newSignature);

        // Set new component if adding
        if (newSignature.Has<T>() && !oldSignature.Has<T>())
        {
            toArchetype.SetComponent(entity, newComponent);
        }

        // Update entity mapping
        _entityToArchetype[entity] = toArchetype;
    }

    private void MoveEntityToArchetype(Entity entity, Archetype fromArchetype, ComponentSignature newSignature)
    {
        MoveEntityToArchetype<byte>(entity, fromArchetype, newSignature);
    }

    private void CopySharedComponents(Entity entity, Archetype fromArchetype, Archetype toArchetype,
        ComponentSignature oldSignature, ComponentSignature newSignature)
    {
        // For now, we'll implement a limited version that copies known component types
        // In a full implementation, this would use reflection or source generation

        // For the MVP, we'll leave component copying as a TODO
        // This requires a more sophisticated approach with either:
        // 1. A component registry with type information
        // 2. Reflection-based copying
        // 3. Source generation
        // For now, components will need to be re-added after migration

        // Note: This is a temporary implementation. A production system would use:
        // 1. A component registry with generic copy delegates
        // 2. Source generation to create type-safe copy methods
        // 3. Reflection-based copying for flexibility (acceptable for archetype migration)
    }

    public int EntityCount => _entityToArchetype.Count;
    public int ArchetypeCount => _archetypes.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuery Query()
    {
        return new ComponentQuery(this);
    }

    /// <summary>
    /// Registers a system for execution.
    /// </summary>
    /// <param name="system">The system to register</param>
    public void RegisterSystem(ISystem system)
    {
        _scheduler.RegisterSystem(system);
    }

    /// <summary>
    /// Updates all registered systems in phase order.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame in seconds</param>
    public void UpdateSystems(float deltaTime)
    {
        _scheduler.UpdateSystems(this, deltaTime);
    }

    /// <summary>
    /// Gets timing information for a specific system type.
    /// </summary>
    /// <param name="systemType">The system type to get timing for</param>
    /// <returns>Timing information or null if system not found</returns>
    public SystemTiming? GetSystemTiming(Type systemType)
    {
        return _scheduler.GetSystemTiming(systemType);
    }

    /// <summary>
    /// Gets timing information for all registered systems.
    /// </summary>
    /// <returns>Dictionary of system types to their timing information</returns>
    public IReadOnlyDictionary<Type, SystemTiming> GetAllSystemTimings()
    {
        return _scheduler.GetAllTimings();
    }

    /// <summary>
    /// Resets peak timing values for all systems.
    /// </summary>
    public void ResetSystemPeaks()
    {
        _scheduler.ResetPeaks();
    }

    /// <summary>
    /// Gets the execution order of registered systems.
    /// </summary>
    /// <returns>Ordered list of system types</returns>
    public IReadOnlyList<Type> GetSystemExecutionOrder()
    {
        return _scheduler.GetExecutionOrder();
    }

    /// <summary>
    /// Gets or creates an event channel for the specified event type.
    /// </summary>
    /// <typeparam name="T">Event type - must be struct</typeparam>
    /// <param name="capacity">Channel capacity (default: 1024)</param>
    /// <returns>Event channel for the specified type</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventChannel<T> Events<T>(int capacity = 1024) where T : struct
    {
        var eventType = typeof(T);

        if (!_eventChannels.TryGetValue(eventType, out var channel))
        {
            channel = new EventChannel<T>(capacity);
            _eventChannels[eventType] = channel;

            // Check if this event type has OneFrame attribute
            if (eventType.GetCustomAttribute<OneFrameAttribute>() != null)
            {
                _oneFrameEventTypes.Add(eventType);
            }
        }

        return (EventChannel<T>)channel;
    }

    /// <summary>
    /// Clears all events marked with [OneFrame] attribute.
    /// This should be called at the end of each frame to maintain event lifetime semantics.
    /// </summary>
    public void ClearOneFrameEvents()
    {
        foreach (var eventType in _oneFrameEventTypes)
        {
            if (_eventChannels.TryGetValue(eventType, out var channel))
            {
                // Use reflection to call Clear() on the typed channel
                var clearMethod = channel.GetType().GetMethod("Clear");
                clearMethod?.Invoke(channel, null);
            }
        }
    }

    /// <summary>
    /// Gets statistics for all active event channels.
    /// </summary>
    /// <returns>Dictionary of event types to their channel statistics</returns>
    public IReadOnlyDictionary<Type, object> GetEventChannelStats()
    {
        var stats = new Dictionary<Type, object>();

        foreach (var kvp in _eventChannels)
        {
            // Use reflection to call GetStats() on the typed channel
            var getStatsMethod = kvp.Value.GetType().GetMethod("GetStats");
            if (getStatsMethod != null)
            {
                stats[kvp.Key] = getStatsMethod.Invoke(kvp.Value, null);
            }
        }

        return stats;
    }

    /// <summary>
    /// Gets the number of active event channels.
    /// </summary>
    public int EventChannelCount => _eventChannels.Count;

    public override string ToString()
    {
        return $"World(entities={EntityCount}, archetypes={ArchetypeCount})";
    }
}