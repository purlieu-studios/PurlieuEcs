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
        
        Assert.AreEqual(0, blueprint.ComponentCount);
        Assert.IsTrue(blueprint.Signature.IsEmpty);
        Assert.AreEqual(0, blueprint.Components.Count);
    }

    [Test]
    public void IT_Blueprint_WithComponent_AddsComponent()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20));

        Assert.AreEqual(1, blueprint.ComponentCount);
        Assert.IsTrue(blueprint.Has<TestPosition>());
        Assert.IsFalse(blueprint.Signature.IsEmpty);
        
        var pos = blueprint.Get<TestPosition>();
        Assert.AreEqual(10, pos.X);
        Assert.AreEqual(20, pos.Y);
    }

    [Test]
    public void IT_Blueprint_MultipleComponents_AddsAll()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(5, 15))
            .With(new TestVelocity(1.5f, -2.0f))
            .With(new TestTag());

        Assert.AreEqual(3, blueprint.ComponentCount);
        Assert.IsTrue(blueprint.Has<TestPosition>());
        Assert.IsTrue(blueprint.Has<TestVelocity>());
        Assert.IsTrue(blueprint.Has<TestTag>());
        
        var pos = blueprint.Get<TestPosition>();
        Assert.AreEqual(5, pos.X);
        Assert.AreEqual(15, pos.Y);
        
        var vel = blueprint.Get<TestVelocity>();
        Assert.AreEqual(1.5f, vel.VX, 0.001f);
        Assert.AreEqual(-2.0f, vel.VY, 0.001f);
    }

    [Test]
    public void IT_Blueprint_WithoutComponent_RemovesComponent()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20))
            .With(new TestVelocity(1.0f, 2.0f))
            .Without<TestPosition>();

        Assert.AreEqual(1, blueprint.ComponentCount);
        Assert.IsFalse(blueprint.Has<TestPosition>());
        Assert.IsTrue(blueprint.Has<TestVelocity>());
    }

    [Test]
    public void IT_Blueprint_TryGet_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(100, 200));

        Assert.IsTrue(blueprint.TryGet<TestPosition>(out var pos));
        Assert.AreEqual(100, pos.X);
        Assert.AreEqual(200, pos.Y);
        
        Assert.IsFalse(blueprint.TryGet<TestVelocity>(out var vel));
        Assert.AreEqual(default(TestVelocity), vel);
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

        Assert.AreEqual(1, original.ComponentCount);
        Assert.AreEqual(2, clone.ComponentCount);
        
        Assert.IsTrue(original.Has<TestPosition>());
        Assert.IsFalse(original.Has<TestVelocity>());
        
        Assert.IsTrue(clone.Has<TestPosition>());
        Assert.IsTrue(clone.Has<TestVelocity>());
    }

    [Test]
    public void IT_Blueprint_SignatureCaching_WorksCorrectly()
    {
        var blueprint = EntityBlueprint.Empty;
        var emptySignature = blueprint.Signature;
        var emptySignature2 = blueprint.Signature;
        
        Assert.AreEqual(emptySignature, emptySignature2);
        
        blueprint.With(new TestPosition(1, 2));
        var withPosSignature = blueprint.Signature;
        
        Assert.AreNotEqual(emptySignature, withPosSignature);
        Assert.IsFalse(withPosSignature.IsEmpty);
    }

    [Test]
    public void IT_WorldInstantiate_SingleBlueprint_CreatesEntity()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(42, 84))
            .With(new TestVelocity(3.14f, 2.71f));

        var entity = world.Instantiate(blueprint);

        Assert.IsTrue(world.EntityExists(entity));
        Assert.IsTrue(world.HasComponent<TestPosition>(entity));
        Assert.IsTrue(world.HasComponent<TestVelocity>(entity));
        
        var pos = world.GetComponent<TestPosition>(entity);
        Assert.AreEqual(42, pos.X);
        Assert.AreEqual(84, pos.Y);
        
        var vel = world.GetComponent<TestVelocity>(entity);
        Assert.AreEqual(3.14f, vel.VX, 0.001f);
        Assert.AreEqual(2.71f, vel.VY, 0.001f);
    }

    [Test]
    public void IT_WorldInstantiate_EmptyBlueprint_CreatesEmptyEntity()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty;

        var entity = world.Instantiate(blueprint);

        Assert.IsTrue(world.EntityExists(entity));
        Assert.IsFalse(world.HasComponent<TestPosition>(entity));
        Assert.IsFalse(world.HasComponent<TestVelocity>(entity));
    }

    [Test]
    public void IT_WorldInstantiateBatch_MultipleEntities_CreatesAll()
    {
        var world = new World();
        var blueprint = EntityBlueprint.Empty
            .With(new TestPosition(10, 20))
            .With(new TestTag());

        var entities = world.InstantiateBatch(blueprint, 5);

        Assert.AreEqual(5, entities.Length);
        Assert.AreEqual(5, world.EntityCount);
        
        foreach (var entity in entities)
        {
            Assert.IsTrue(world.EntityExists(entity));
            Assert.IsTrue(world.HasComponent<TestPosition>(entity));
            Assert.IsTrue(world.HasComponent<TestTag>(entity));
            
            var pos = world.GetComponent<TestPosition>(entity);
            Assert.AreEqual(10, pos.X);
            Assert.AreEqual(20, pos.Y);
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