using System;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Events;

namespace Purlieu.Ecs.Systems;

/// <summary>
/// System that processes all visual intents and forwards them to the registered bridge interface.
/// Runs in the Presentation phase to ensure all game logic has completed before visual updates.
/// This system maintains the separation between ECS logic and external visual systems.
/// </summary>
[GamePhase(GamePhase.Presentation, order: 1000)]
public sealed class IntentProcessorSystem : ISystem
{
    private readonly IBridgeInterface _bridge;

    public IntentProcessorSystem(IBridgeInterface bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public void Update(World world, float deltaTime)
    {
        // Process position changed intents
        ProcessPositionChangedIntents(world);

        // Process entity lifecycle intents
        ProcessEntitySpawnedIntents(world);
        ProcessEntityDestroyedIntents(world);

        // Process component change intents
        ProcessHealthChangedIntents(world);

        // Process animation and sound intents
        ProcessAnimationTriggeredIntents(world);
        ProcessSoundTriggeredIntents(world);
    }

    private void ProcessPositionChangedIntents(World world)
    {
        var channel = world.Events<PositionChangedIntent>();
        channel.ConsumeAll(intent => _bridge.OnPositionChanged(in intent));
    }

    private void ProcessEntitySpawnedIntents(World world)
    {
        var channel = world.Events<EntitySpawnedIntent>();
        channel.ConsumeAll(intent => _bridge.OnEntitySpawned(in intent));
    }

    private void ProcessEntityDestroyedIntents(World world)
    {
        var channel = world.Events<EntityDestroyedIntent>();
        channel.ConsumeAll(intent => _bridge.OnEntityDestroyed(in intent));
    }

    private void ProcessHealthChangedIntents(World world)
    {
        var channel = world.Events<HealthChangedIntent>();
        channel.ConsumeAll(intent => _bridge.OnHealthChanged(in intent));
    }

    private void ProcessAnimationTriggeredIntents(World world)
    {
        var channel = world.Events<AnimationTriggeredIntent>();
        channel.ConsumeAll(intent => _bridge.OnAnimationTriggered(in intent));
    }

    private void ProcessSoundTriggeredIntents(World world)
    {
        var channel = world.Events<SoundTriggeredIntent>();
        channel.ConsumeAll(intent => _bridge.OnSoundTriggered(in intent));
    }
}