using System;
using System.Collections.Generic;

namespace Purlieu.Ecs.Events;

/// <summary>
/// Interface for visual system bridges that consume visual intents from the ECS.
/// Implementations handle forwarding intents to external visual systems (e.g., Godot, Unity, etc.).
/// This interface maintains complete separation between ECS and visual engine concerns.
/// </summary>
public interface IBridgeInterface
{
    /// <summary>
    /// Called when an entity's position has changed.
    /// </summary>
    void OnPositionChanged(in PositionChangedIntent intent);

    /// <summary>
    /// Called when a new entity should be spawned visually.
    /// </summary>
    void OnEntitySpawned(in EntitySpawnedIntent intent);

    /// <summary>
    /// Called when an entity should be removed visually.
    /// </summary>
    void OnEntityDestroyed(in EntityDestroyedIntent intent);

    /// <summary>
    /// Called when an entity's health has changed.
    /// </summary>
    void OnHealthChanged(in HealthChangedIntent intent);

    /// <summary>
    /// Called when an animation should be triggered.
    /// </summary>
    void OnAnimationTriggered(in AnimationTriggeredIntent intent);

    /// <summary>
    /// Called when a sound effect should be played.
    /// </summary>
    void OnSoundTriggered(in SoundTriggeredIntent intent);
}

/// <summary>
/// Null implementation of IBridgeInterface for testing and scenarios where no visual bridge is needed.
/// All intents are silently discarded.
/// </summary>
public sealed class NullBridgeInterface : IBridgeInterface
{
    public static readonly NullBridgeInterface Instance = new();

    private NullBridgeInterface() { }

    public void OnPositionChanged(in PositionChangedIntent intent) { }
    public void OnEntitySpawned(in EntitySpawnedIntent intent) { }
    public void OnEntityDestroyed(in EntityDestroyedIntent intent) { }
    public void OnHealthChanged(in HealthChangedIntent intent) { }
    public void OnAnimationTriggered(in AnimationTriggeredIntent intent) { }
    public void OnSoundTriggered(in SoundTriggeredIntent intent) { }
}

/// <summary>
/// Test implementation of IBridgeInterface that records all received intents for verification.
/// Used in unit tests to verify intent emission and processing.
/// </summary>
public sealed class RecordingBridgeInterface : IBridgeInterface
{
    public readonly List<PositionChangedIntent> PositionChangedIntents = new();
    public readonly List<EntitySpawnedIntent> EntitySpawnedIntents = new();
    public readonly List<EntityDestroyedIntent> EntityDestroyedIntents = new();
    public readonly List<HealthChangedIntent> HealthChangedIntents = new();
    public readonly List<AnimationTriggeredIntent> AnimationTriggeredIntents = new();
    public readonly List<SoundTriggeredIntent> SoundTriggeredIntents = new();

    public void OnPositionChanged(in PositionChangedIntent intent)
        => PositionChangedIntents.Add(intent);

    public void OnEntitySpawned(in EntitySpawnedIntent intent)
        => EntitySpawnedIntents.Add(intent);

    public void OnEntityDestroyed(in EntityDestroyedIntent intent)
        => EntityDestroyedIntents.Add(intent);

    public void OnHealthChanged(in HealthChangedIntent intent)
        => HealthChangedIntents.Add(intent);

    public void OnAnimationTriggered(in AnimationTriggeredIntent intent)
        => AnimationTriggeredIntents.Add(intent);

    public void OnSoundTriggered(in SoundTriggeredIntent intent)
        => SoundTriggeredIntents.Add(intent);

    public void Clear()
    {
        PositionChangedIntents.Clear();
        EntitySpawnedIntents.Clear();
        EntityDestroyedIntents.Clear();
        HealthChangedIntents.Clear();
        AnimationTriggeredIntents.Clear();
        SoundTriggeredIntents.Clear();
    }

    public int TotalIntents => 
        PositionChangedIntents.Count +
        EntitySpawnedIntents.Count +
        EntityDestroyedIntents.Count +
        HealthChangedIntents.Count +
        AnimationTriggeredIntents.Count +
        SoundTriggeredIntents.Count;
}