using System;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Events;

/// <summary>
/// Intent indicating that an entity's position has changed.
/// Emitted by movement systems when entity positions are updated.
/// </summary>
[OneFrame]
public readonly struct PositionChangedIntent
{
    public readonly Entity Entity;
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float PreviousX;
    public readonly float PreviousY;
    public readonly float PreviousZ;

    public PositionChangedIntent(Entity entity, float x, float y, float z, float prevX, float prevY, float prevZ)
    {
        Entity = entity;
        X = x;
        Y = y;
        Z = z;
        PreviousX = prevX;
        PreviousY = prevY;
        PreviousZ = prevZ;
    }
}

/// <summary>
/// Intent indicating that a new entity has been spawned and should be visually represented.
/// Emitted when entities are created and given visual components.
/// </summary>
[OneFrame]
public readonly struct EntitySpawnedIntent
{
    public readonly Entity Entity;
    public readonly string VisualType;
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public EntitySpawnedIntent(Entity entity, string visualType, float x, float y, float z)
    {
        Entity = entity;
        VisualType = visualType;
        X = x;
        Y = y;
        Z = z;
    }
}

/// <summary>
/// Intent indicating that an entity has been destroyed and its visual representation should be removed.
/// Emitted when entities are destroyed.
/// </summary>
[OneFrame]
public readonly struct EntityDestroyedIntent
{
    public readonly Entity Entity;

    public EntityDestroyedIntent(Entity entity)
    {
        Entity = entity;
    }
}

/// <summary>
/// Intent indicating that an entity's health has changed and UI should be updated.
/// Emitted by systems that modify health values.
/// </summary>
[OneFrame]
public readonly struct HealthChangedIntent
{
    public readonly Entity Entity;
    public readonly int CurrentHealth;
    public readonly int MaxHealth;
    public readonly int PreviousHealth;

    public HealthChangedIntent(Entity entity, int currentHealth, int maxHealth, int previousHealth)
    {
        Entity = entity;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        PreviousHealth = previousHealth;
    }
}

/// <summary>
/// Intent indicating that an entity has started or stopped an animation.
/// Emitted by systems that trigger visual state changes.
/// </summary>
[OneFrame]
public readonly struct AnimationTriggeredIntent
{
    public readonly Entity Entity;
    public readonly string AnimationName;
    public readonly bool Loop;
    public readonly float Duration;

    public AnimationTriggeredIntent(Entity entity, string animationName, bool loop, float duration)
    {
        Entity = entity;
        AnimationName = animationName;
        Loop = loop;
        Duration = duration;
    }
}

/// <summary>
/// Intent indicating that a sound effect should be played.
/// Emitted by game logic systems in response to events.
/// </summary>
[OneFrame]
public readonly struct SoundTriggeredIntent
{
    public readonly string SoundName;
    public readonly float Volume;
    public readonly float Pitch;
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public SoundTriggeredIntent(string soundName, float volume, float pitch, float x, float y, float z)
    {
        SoundName = soundName;
        Volume = volume;
        Pitch = pitch;
        X = x;
        Y = y;
        Z = z;
    }
}