using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Purlieu.Ecs.Blueprints;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Tests.Blueprints;

[TestFixture]
public class BlueprintSerializationTests
{
    private struct SerializablePosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public SerializablePosition(int x, int y) { X = x; Y = y; }
    }

    private struct SerializableVelocity
    {
        public float VX { get; set; }
        public float VY { get; set; }
        public SerializableVelocity(float vx, float vy) { VX = vx; VY = vy; }
    }

    private struct SerializableTag { }

    private string _tempDir;

    [SetUp]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void SNAP_Serialization_EmptyBlueprint_RoundTrip()
    {
        var original = EntityBlueprint.Empty;

        var json = BlueprintSerializer.SerializeToJson(original);
        var deserialized = BlueprintSerializer.DeserializeFromJson(json);

        Assert.That(deserialized.ComponentCount, Is.EqualTo(0));
        Assert.That(deserialized.Signature.IsEmpty, Is.True);
    }

    [Test]
    public void SNAP_Serialization_SingleComponent_RoundTrip()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(100, 200));

        var json = BlueprintSerializer.SerializeToJson(original);
        var deserialized = BlueprintSerializer.DeserializeFromJson(json);

        Assert.That(deserialized.ComponentCount, Is.EqualTo(1));
        Assert.That(deserialized.Has<SerializablePosition>(), Is.True);

        var pos = deserialized.Get<SerializablePosition>();
        Assert.That(pos.X, Is.EqualTo(100));
        Assert.That(pos.Y, Is.EqualTo(200));
    }

    [Test]
    public void SNAP_Serialization_MultipleComponents_RoundTrip()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(50, 75))
            .With(new SerializableVelocity(1.5f, -2.5f))
            .With(new SerializableTag());

        var json = BlueprintSerializer.SerializeToJson(original);
        var deserialized = BlueprintSerializer.DeserializeFromJson(json);

        Assert.That(deserialized.ComponentCount, Is.EqualTo(3));
        Assert.That(deserialized.Has<SerializablePosition>(), Is.True);
        Assert.That(deserialized.Has<SerializableVelocity>(), Is.True);
        Assert.That(deserialized.Has<SerializableTag>(), Is.True);

        var pos = deserialized.Get<SerializablePosition>();
        Assert.That(pos.X, Is.EqualTo(50));
        Assert.That(pos.Y, Is.EqualTo(75));

        var vel = deserialized.Get<SerializableVelocity>();
        Assert.That(vel.VX, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(vel.VY, Is.EqualTo(-2.5f).Within(0.001f));
    }

    [Test]
    public void SNAP_BinarySerializer_RoundTrip()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(123, 456))
            .With(new SerializableVelocity(7.89f, 12.34f));

        var binary = BlueprintSerializer.SerializeToBinary(original);
        var deserialized = BlueprintSerializer.DeserializeFromBinary(binary);

        Assert.That(deserialized.ComponentCount, Is.EqualTo(2));
        Assert.That(deserialized.Has<SerializablePosition>(), Is.True);
        Assert.That(deserialized.Has<SerializableVelocity>(), Is.True);

        var pos = deserialized.Get<SerializablePosition>();
        Assert.That(pos.X, Is.EqualTo(123));
        Assert.That(pos.Y, Is.EqualTo(456));

        var vel = deserialized.Get<SerializableVelocity>();
        Assert.That(vel.VX, Is.EqualTo(7.89f).Within(0.001f));
        Assert.That(vel.VY, Is.EqualTo(12.34f).Within(0.001f));
    }

    [Test]
    public void SNAP_FileOperations_SaveAndLoad_WorksCorrectly()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(999, 888));

        var filePath = Path.Combine(_tempDir, "test_blueprint.json");

        BlueprintSerializer.SaveToFile(original, filePath);
        Assert.That(File.Exists(filePath), Is.True);

        var loaded = BlueprintSerializer.LoadFromFile(filePath);

        Assert.That(loaded.ComponentCount, Is.EqualTo(1));
        Assert.That(loaded.Has<SerializablePosition>(), Is.True);

        var pos = loaded.Get<SerializablePosition>();
        Assert.That(pos.X, Is.EqualTo(999));
        Assert.That(pos.Y, Is.EqualTo(888));
    }

    [Test]
    public void SNAP_BinaryFileOperations_SaveAndLoad_WorksCorrectly()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(777, 666))
            .With(new SerializableVelocity(-1.23f, 4.56f));

        var filePath = Path.Combine(_tempDir, "test_blueprint.bin");

        BlueprintSerializer.SaveToBinaryFile(original, filePath);
        Assert.That(File.Exists(filePath), Is.True);

        var loaded = BlueprintSerializer.LoadFromBinaryFile(filePath);

        Assert.That(loaded.ComponentCount, Is.EqualTo(2));
        Assert.That(loaded.Has<SerializablePosition>(), Is.True);
        Assert.That(loaded.Has<SerializableVelocity>(), Is.True);

        var pos = loaded.Get<SerializablePosition>();
        Assert.That(pos.X, Is.EqualTo(777));
        Assert.That(pos.Y, Is.EqualTo(666));

        var vel = loaded.Get<SerializableVelocity>();
        Assert.That(vel.VX, Is.EqualTo(-1.23f).Within(0.001f));
        Assert.That(vel.VY, Is.EqualTo(4.56f).Within(0.001f));
    }

    [Test]
    public void SNAP_LoadFromFile_NonExistentFile_ThrowsException()
    {
        var nonExistentPath = Path.Combine(_tempDir, "does_not_exist.json");
        Assert.Throws<FileNotFoundException>(() => BlueprintSerializer.LoadFromFile(nonExistentPath));
    }

    [Test]
    public void SNAP_LoadFromBinaryFile_NonExistentFile_ThrowsException()
    {
        var nonExistentPath = Path.Combine(_tempDir, "does_not_exist.bin");
        Assert.Throws<FileNotFoundException>(() => BlueprintSerializer.LoadFromBinaryFile(nonExistentPath));
    }

    [Test]
    public void SNAP_DeserializeFromJson_InvalidJson_ThrowsException()
    {
        var invalidJson = "{ invalid json }";
        Assert.Throws<JsonException>(() => BlueprintSerializer.DeserializeFromJson(invalidJson));
    }

    [Test]
    public void SNAP_DeserializeFromBinary_InvalidVersion_ThrowsException()
    {
        var invalidBinary = new byte[] { 99, 0, 0, 0, 0 }; // Version 99
        Assert.Throws<InvalidOperationException>(() => BlueprintSerializer.DeserializeFromBinary(invalidBinary));
    }

    [Test]
    public void SNAP_Serialization_PreservesSignatures()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(1, 2))
            .With(new SerializableVelocity(3.0f, 4.0f));

        var originalSignature = original.Signature;

        var json = BlueprintSerializer.SerializeToJson(original);
        var deserialized = BlueprintSerializer.DeserializeFromJson(json);

        Assert.That(deserialized.Signature, Is.EqualTo(originalSignature));
    }
}
