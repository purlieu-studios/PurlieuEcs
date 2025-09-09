using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Purlieu.Ecs.Blueprints;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Tests.Blueprints;

[TestFixture]
public class BlueprintTests
{
    private struct TestPosition
    {
        public int X, Y;
        public TestPosition(int x, int y) { X = x; Y = y; }
    }

    private struct TestVelocity
    {
        public float VX, VY;
        public TestVelocity(float vx, float vy) { VX = vx; VY = vy; }
    }

    private struct TestTag { }

    [SetUp]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
    }

    [Test]
    public void IT_Blueprint_EmptyBlueprint_CreatesCorrectly()
    {
        var blueprint = EntityBlueprint.Empty;

        Assert.That(blueprint.ComponentCount, Is.EqualTo(0));
        Assert.That(blueprint.Signature.IsEmpty, Is.True);
        Assert.That(blueprint.Components.Count, Is.EqualTo(0));
    }

    [Test]
    public void IT_Blueprint_WithComponent_AddsComponent()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20));

        Assert.That(blueprint.ComponentCount, Is.EqualTo(1));
        Assert.That(blueprint.Has<TestPosition>(), Is.True);
        Assert.That(blueprint.Signature.IsEmpty, Is.False);

        var pos = blueprint.Get<TestPosition>();
        Assert.That(pos.X, Is.EqualTo(10));
        Assert.That(pos.Y, Is.EqualTo(20));
    }

    [Test]
    public void IT_Blueprint_MultipleComponents_AddsAll()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(5, 15))
            .With(new TestVelocity(1.5f, -2.0f))
            .With(new TestTag());

        Assert.That(blueprint.ComponentCount, Is.EqualTo(3));
        Assert.That(blueprint.Has<TestPosition>(), Is.True);
        Assert.That(blueprint.Has<TestVelocity>(), Is.True);
        Assert.That(blueprint.Has<TestTag>(), Is.True);

        var pos = blueprint.Get<TestPosition>();
        Assert.That(pos.X, Is.EqualTo(5));
        Assert.That(pos.Y, Is.EqualTo(15));

        var vel = blueprint.Get<TestVelocity>();
        Assert.That(vel.VX, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(vel.VY, Is.EqualTo(-2.0f).Within(0.001f));
    }

    [Test]
    public void IT_Blueprint_WithoutComponent_RemovesComponent()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20))
            .With(new TestVelocity(1.0f, 2.0f))
            .Without<TestPosition>();

        Assert.That(blueprint.ComponentCount, Is.EqualTo(1));
        Assert.That(blueprint.Has<TestPosition>(), Is.False);
        Assert.That(blueprint.Has<TestVelocity>(), Is.True);
    }

    [Test]
    public void IT_Blueprint_TryGet_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(100, 200));

        Assert.That(blueprint.TryGet<TestPosition>(out var pos), Is.True);
        Assert.That(pos.X, Is.EqualTo(100));
        Assert.That(pos.Y, Is.EqualTo(200));

        Assert.That(blueprint.TryGet<TestVelocity>(out var vel), Is.False);
        Assert.That(vel, Is.EqualTo(default(TestVelocity)));
    }

    [Test]
    public void IT_Blueprint_GetNonExistent_ThrowsException()
    {
        var blueprint = EntityBlueprint.Empty;
        Assert.Throws<ArgumentException>(() => blueprint.Get<TestPosition>());
    }

    [Test]
    public void IT_Blueprint_Clone_CreatesIndependentCopy()
    {
        var original = EntityBlueprint.Empty
            .With(new TestPosition(10, 20));

        var clone = original.Clone();
        clone.With(new TestVelocity(1.0f, 2.0f));

        Assert.That(original.ComponentCount, Is.EqualTo(1));
        Assert.That(clone.ComponentCount, Is.EqualTo(2));

        Assert.That(original.Has<TestPosition>(), Is.True);
        Assert.That(original.Has<TestVelocity>(), Is.False);

        Assert.That(clone.Has<TestPosition>(), Is.True);
        Assert.That(clone.Has<TestVelocity>(), Is.True);
    }

    [Test]
    public void IT_Blueprint_SignatureCaching_WorksCorrectly()
    {
        var blueprint = EntityBlueprint.Empty;
        var emptySignature = blueprint.Signature;
        var emptySignature2 = blueprint.Signature;

        Assert.That(emptySignature2, Is.EqualTo(emptySignature));

        blueprint.With(new TestPosition(1, 2));
        var withPosSignature = blueprint.Signature;

        Assert.That(withPosSignature, Is.Not.EqualTo(emptySignature));
        Assert.That(withPosSignature.IsEmpty, Is.False);
    }

    [Test]
    public void IT_WorldInstantiate_SingleBlueprint_CreatesEntity()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(42, 84))
            .With(new TestVelocity(3.14f, 2.71f));

        var entity = world.Instantiate(blueprint);

        Assert.That(world.EntityExists(entity), Is.True);
        Assert.That(world.HasComponent<TestPosition>(entity), Is.True);
        Assert.That(world.HasComponent<TestVelocity>(entity), Is.True);

        var pos = world.GetComponent<TestPosition>(entity);
        Assert.That(pos.X, Is.EqualTo(42));
        Assert.That(pos.Y, Is.EqualTo(84));

        var vel = world.GetComponent<TestVelocity>(entity);
        Assert.That(vel.VX, Is.EqualTo(3.14f).Within(0.001f));
        Assert.That(vel.VY, Is.EqualTo(2.71f).Within(0.001f));
    }

    [Test]
    public void IT_WorldInstantiate_EmptyBlueprint_CreatesEmptyEntity()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty;

        var entity = world.Instantiate(blueprint);

        Assert.That(world.EntityExists(entity), Is.True);
        Assert.That(world.HasComponent<TestPosition>(entity), Is.False);
        Assert.That(world.HasComponent<TestVelocity>(entity), Is.False);
    }

    [Test]
    public void IT_WorldInstantiateBatch_MultipleEntities_CreatesAll()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20))
            .With(new TestTag());

        var entities = world.InstantiateBatch(blueprint, 5);

        Assert.That(entities.Length, Is.EqualTo(5));
        Assert.That(world.EntityCount, Is.EqualTo(5));

        foreach (var entity in entities)
        {
            Assert.That(world.EntityExists(entity), Is.True);
            Assert.That(world.HasComponent<TestPosition>(entity), Is.True);
            Assert.That(world.HasComponent<TestTag>(entity), Is.True);

            var pos = world.GetComponent<TestPosition>(entity);
            Assert.That(pos.X, Is.EqualTo(10));
            Assert.That(pos.Y, Is.EqualTo(20));
        }
    }

    [Test]
    public void IT_WorldInstantiateBatch_ZeroCount_ThrowsException()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty;

        Assert.Throws<ArgumentException>(() => world.InstantiateBatch(blueprint, 0));
    }

    [Test]
    public void IT_WorldInstantiateBatch_NegativeCount_ThrowsException()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty;

        Assert.Throws<ArgumentException>(() => world.InstantiateBatch(blueprint, -5));
    }
}
