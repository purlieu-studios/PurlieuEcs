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

        Assert.AreEqual(0, deserialized.ComponentCount);
        Assert.IsTrue(deserialized.Signature.IsEmpty);
    }

    [Test]
    public void SNAP_Serialization_SingleComponent_RoundTrip()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(100, 200));
        
        var json = BlueprintSerializer.SerializeToJson(original);
        var deserialized = BlueprintSerializer.DeserializeFromJson(json);

        Assert.AreEqual(1, deserialized.ComponentCount);
        Assert.IsTrue(deserialized.Has<SerializablePosition>());
        
        var pos = deserialized.Get<SerializablePosition>();
        Assert.AreEqual(100, pos.X);
        Assert.AreEqual(200, pos.Y);
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

        Assert.AreEqual(3, deserialized.ComponentCount);
        Assert.IsTrue(deserialized.Has<SerializablePosition>());
        Assert.IsTrue(deserialized.Has<SerializableVelocity>());
        Assert.IsTrue(deserialized.Has<SerializableTag>());
        
        var pos = deserialized.Get<SerializablePosition>();
        Assert.AreEqual(50, pos.X);
        Assert.AreEqual(75, pos.Y);
        
        var vel = deserialized.Get<SerializableVelocity>();
        Assert.AreEqual(1.5f, vel.VX, 0.001f);
        Assert.AreEqual(-2.5f, vel.VY, 0.001f);
    }

    [Test]
    public void SNAP_BinarySerializer_RoundTrip()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(123, 456))
            .With(new SerializableVelocity(7.89f, 12.34f));
        
        var binary = BlueprintSerializer.SerializeToBinary(original);
        var deserialized = BlueprintSerializer.DeserializeFromBinary(binary);

        Assert.AreEqual(2, deserialized.ComponentCount);
        Assert.IsTrue(deserialized.Has<SerializablePosition>());
        Assert.IsTrue(deserialized.Has<SerializableVelocity>());
        
        var pos = deserialized.Get<SerializablePosition>();
        Assert.AreEqual(123, pos.X);
        Assert.AreEqual(456, pos.Y);
        
        var vel = deserialized.Get<SerializableVelocity>();
        Assert.AreEqual(7.89f, vel.VX, 0.001f);
        Assert.AreEqual(12.34f, vel.VY, 0.001f);
    }

    [Test]
    public void SNAP_FileOperations_SaveAndLoad_WorksCorrectly()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(999, 888));

        var filePath = Path.Combine(_tempDir, "test_blueprint.json");
        
        BlueprintSerializer.SaveToFile(original, filePath);
        Assert.IsTrue(File.Exists(filePath));
        
        var loaded = BlueprintSerializer.LoadFromFile(filePath);
        
        Assert.AreEqual(1, loaded.ComponentCount);
        Assert.IsTrue(loaded.Has<SerializablePosition>());
        
        var pos = loaded.Get<SerializablePosition>();
        Assert.AreEqual(999, pos.X);
        Assert.AreEqual(888, pos.Y);
    }

    [Test]
    public void SNAP_BinaryFileOperations_SaveAndLoad_WorksCorrectly()
    {
        var original = EntityBlueprint.Empty
            .With(new SerializablePosition(777, 666))
            .With(new SerializableVelocity(-1.23f, 4.56f));

        var filePath = Path.Combine(_tempDir, "test_blueprint.bin");
        
        BlueprintSerializer.SaveToBinaryFile(original, filePath);
        Assert.IsTrue(File.Exists(filePath));
        
        var loaded = BlueprintSerializer.LoadFromBinaryFile(filePath);
        
        Assert.AreEqual(2, loaded.ComponentCount);
        Assert.IsTrue(loaded.Has<SerializablePosition>());
        Assert.IsTrue(loaded.Has<SerializableVelocity>());
        
        var pos = loaded.Get<SerializablePosition>();
        Assert.AreEqual(777, pos.X);
        Assert.AreEqual(666, pos.Y);
        
        var vel = loaded.Get<SerializableVelocity>();
        Assert.AreEqual(-1.23f, vel.VX, 0.001f);
        Assert.AreEqual(4.56f, vel.VY, 0.001f);
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
        Assert.Throws<InvalidOperationException>(() => BlueprintSerializer.DeserializeFromJson(invalidJson));
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
        
        Assert.AreEqual(originalSignature, deserialized.Signature);
    }
}