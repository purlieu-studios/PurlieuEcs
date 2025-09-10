using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

/// <summary>
/// Tracks component changes for efficient Changed<T> queries.
/// Uses frame-based dirty flags to identify entities with modified components.
/// </summary>
public sealed class ChangeTracker
{
    private readonly Dictionary<int, ulong> _changedComponents;
    private readonly Dictionary<Entity, ulong> _entityChanges;
    private ulong _currentFrame;

    public ChangeTracker()
    {
        _changedComponents = new Dictionary<int, ulong>();
        _entityChanges = new Dictionary<Entity, ulong>();
        _currentFrame = 1; // Start at 1 to avoid default(ulong) confusion
    }

    /// <summary>
    /// Marks a component as changed for the given entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkChanged<T>(Entity entity) where T : struct
    {
        var componentId = ComponentTypeId<T>.Id;
        var componentMask = 1UL << componentId;

        // Track global component changes
        _changedComponents[componentId] = _currentFrame;

        // Track per-entity changes
        if (!_entityChanges.TryGetValue(entity, out var entityMask))
        {
            entityMask = 0;
        }
        _entityChanges[entity] = entityMask | componentMask;
    }

    /// <summary>
    /// Checks if a component has changed for the given entity since the last frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasChanged<T>(Entity entity) where T : struct
    {
        var componentId = ComponentTypeId<T>.Id;
        var componentMask = 1UL << componentId;

        return _entityChanges.TryGetValue(entity, out var entityMask) &&
               (entityMask & componentMask) != 0;
    }

    /// <summary>
    /// Checks if any component of the given type has changed this frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasChangedAny<T>() where T : struct
    {
        var componentId = ComponentTypeId<T>.Id;
        return _changedComponents.TryGetValue(componentId, out var lastChanged) &&
               lastChanged == _currentFrame;
    }

    /// <summary>
    /// Advances to the next frame and clears per-entity change flags.
    /// Should be called at the end of each update cycle.
    /// </summary>
    public void NextFrame()
    {
        _currentFrame++;
        _entityChanges.Clear();
    }

    /// <summary>
    /// Removes tracking for the given entity (called when entity is destroyed).
    /// </summary>
    public void RemoveEntity(Entity entity)
    {
        _entityChanges.Remove(entity);
    }

    /// <summary>
    /// Gets the current frame number for debugging.
    /// </summary>
    public ulong CurrentFrame => _currentFrame;

    /// <summary>
    /// Checks if an entity has any changed components matching the signature.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasChangedAny(Entity entity, ComponentSignature changedSignature)
    {
        if (!_entityChanges.TryGetValue(entity, out var entityMask))
            return false;

        // Check if any of the requested component types have changed
        var signatureMask = (ulong)changedSignature;
        return (entityMask & signatureMask) != 0;
    }
}
