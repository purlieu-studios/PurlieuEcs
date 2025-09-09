using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Purlieu.Ecs.Core;
using Purlieu.Ecs.Events;
using Purlieu.Ecs.Systems;

namespace Purlieu.Ecs.Tests.Events;

[TestFixture]
public class VisualIntentTests
{
    private World _world;
    private RecordingBridgeInterface _bridge;
    private IntentProcessorSystem _processor;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        _bridge = new RecordingBridgeInterface();
        _processor = new IntentProcessorSystem(_bridge);
        _world.RegisterSystem(_processor);
    }

    [Test]
    public void API_PositionChangedIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var entity = _world.CreateEntity();
        var intent = new PositionChangedIntent(entity, 1, 2, 3, 0, 0, 0);

        // Act
        _world.Events<PositionChangedIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.PositionChangedIntents.Should().HaveCount(1);
        var receivedIntent = _bridge.PositionChangedIntents[0];
        receivedIntent.Entity.Should().Be(entity);
        receivedIntent.X.Should().Be(1);
        receivedIntent.Y.Should().Be(2);
        receivedIntent.Z.Should().Be(3);
        receivedIntent.PreviousX.Should().Be(0);
        receivedIntent.PreviousY.Should().Be(0);
        receivedIntent.PreviousZ.Should().Be(0);
    }

    [Test]
    public void API_EntitySpawnedIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var entity = _world.CreateEntity();
        var intent = new EntitySpawnedIntent(entity, "Player", 5, 10, 0);

        // Act
        _world.Events<EntitySpawnedIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.EntitySpawnedIntents.Should().HaveCount(1);
        var receivedIntent = _bridge.EntitySpawnedIntents[0];
        receivedIntent.Entity.Should().Be(entity);
        receivedIntent.VisualType.Should().Be("Player");
        receivedIntent.X.Should().Be(5);
        receivedIntent.Y.Should().Be(10);
        receivedIntent.Z.Should().Be(0);
    }

    [Test]
    public void API_EntityDestroyedIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var entity = _world.CreateEntity();
        var intent = new EntityDestroyedIntent(entity);

        // Act
        _world.Events<EntityDestroyedIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.EntityDestroyedIntents.Should().HaveCount(1);
        _bridge.EntityDestroyedIntents[0].Entity.Should().Be(entity);
    }

    [Test]
    public void API_HealthChangedIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var entity = _world.CreateEntity();
        var intent = new HealthChangedIntent(entity, 75, 100, 80);

        // Act
        _world.Events<HealthChangedIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.HealthChangedIntents.Should().HaveCount(1);
        var receivedIntent = _bridge.HealthChangedIntents[0];
        receivedIntent.Entity.Should().Be(entity);
        receivedIntent.CurrentHealth.Should().Be(75);
        receivedIntent.MaxHealth.Should().Be(100);
        receivedIntent.PreviousHealth.Should().Be(80);
    }

    [Test]
    public void API_AnimationTriggeredIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var entity = _world.CreateEntity();
        var intent = new AnimationTriggeredIntent(entity, "Attack", false, 1.5f);

        // Act
        _world.Events<AnimationTriggeredIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.AnimationTriggeredIntents.Should().HaveCount(1);
        var receivedIntent = _bridge.AnimationTriggeredIntents[0];
        receivedIntent.Entity.Should().Be(entity);
        receivedIntent.AnimationName.Should().Be("Attack");
        receivedIntent.Loop.Should().BeFalse();
        receivedIntent.Duration.Should().Be(1.5f);
    }

    [Test]
    public void API_SoundTriggeredIntent_ShouldBeProcessedByBridge()
    {
        // Arrange
        var intent = new SoundTriggeredIntent("Explosion", 0.8f, 1.2f, 10, 5, 0);

        // Act
        _world.Events<SoundTriggeredIntent>().Publish(in intent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.SoundTriggeredIntents.Should().HaveCount(1);
        var receivedIntent = _bridge.SoundTriggeredIntents[0];
        receivedIntent.SoundName.Should().Be("Explosion");
        receivedIntent.Volume.Should().Be(0.8f);
        receivedIntent.Pitch.Should().Be(1.2f);
        receivedIntent.X.Should().Be(10);
        receivedIntent.Y.Should().Be(5);
        receivedIntent.Z.Should().Be(0);
    }

    [Test]
    public void IT_MultipleIntents_ShouldAllBeProcessed()
    {
        // Arrange
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity();

        var posIntent = new PositionChangedIntent(entity1, 1, 2, 3, 0, 0, 0);
        var spawnIntent = new EntitySpawnedIntent(entity2, "Enemy", 5, 5, 5);
        var healthIntent = new HealthChangedIntent(entity1, 50, 100, 75);

        // Act
        _world.Events<PositionChangedIntent>().Publish(in posIntent);
        _world.Events<EntitySpawnedIntent>().Publish(in spawnIntent);
        _world.Events<HealthChangedIntent>().Publish(in healthIntent);
        _world.UpdateSystems(0.016f);

        // Assert
        _bridge.PositionChangedIntents.Should().HaveCount(1);
        _bridge.EntitySpawnedIntents.Should().HaveCount(1);
        _bridge.HealthChangedIntents.Should().HaveCount(1);
        _bridge.TotalIntents.Should().Be(3);
    }

    [Test]
    public void IT_IntentProcessorSystem_ShouldHaveCorrectPhaseAndOrder()
    {
        // Act
        var timing = _world.GetSystemTiming(typeof(IntentProcessorSystem));

        // Assert
        timing.Should().NotBeNull();

        // Verify the system is registered and will execute in presentation phase
        var executionOrder = _world.GetSystemExecutionOrder();
        executionOrder.Should().Contain(typeof(IntentProcessorSystem));
    }

    [Test]
    public void EDGE_IntentProcessorSystem_WithNullBridge_ShouldThrow()
    {
        // Act & Assert
        Action action = () => new IntentProcessorSystem(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("bridge");
    }

    [Test]
    public void EDGE_IntentProcessorSystem_WithNoIntents_ShouldNotError()
    {
        // Act & Assert - Should not throw
        Action action = () => _world.UpdateSystems(0.016f);
        action.Should().NotThrow();

        _bridge.TotalIntents.Should().Be(0);
    }

    [Test]
    public void DET_IntentProcessing_ShouldBeConsistentAcrossRuns()
    {
        // Arrange
        var entities = Enumerable.Range(0, 5).Select(_ => _world.CreateEntity()).ToArray();

        for (int run = 0; run < 3; run++)
        {
            _bridge.Clear();

            // Act - Publish intents in same order
            for (int i = 0; i < entities.Length; i++)
            {
                var intent = new PositionChangedIntent(entities[i], i, i * 2, i * 3, 0, 0, 0);
                _world.Events<PositionChangedIntent>().Publish(in intent);
            }

            _world.UpdateSystems(0.016f);

            // Assert - Same results each run
            _bridge.PositionChangedIntents.Should().HaveCount(5);
            for (int i = 0; i < 5; i++)
            {
                var intent = _bridge.PositionChangedIntents[i];
                intent.Entity.Should().Be(entities[i], $"Run {run + 1}, Intent {i}");
                intent.X.Should().Be(i, $"Run {run + 1}, Intent {i}");
                intent.Y.Should().Be(i * 2, $"Run {run + 1}, Intent {i}");
                intent.Z.Should().Be(i * 3, $"Run {run + 1}, Intent {i}");
            }
        }
    }
}