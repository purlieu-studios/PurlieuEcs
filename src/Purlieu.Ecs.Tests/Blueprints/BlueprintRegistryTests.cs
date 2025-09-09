using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Purlieu.Ecs.Blueprints;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Tests.Blueprints;

[TestFixture]
public class BlueprintRegistryTests
{
    private struct TestComponent
    {
        public int Value { get; set; }
        public TestComponent(int value) { Value = value; }
    }

    private string _tempDir;
    private BlueprintRegistry _registry;

    [SetUp]
    public void Setup()
    {
        ComponentTypeRegistry.Reset();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _registry = new BlueprintRegistry();
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
    public void IT_Registry_RegisterAndGet_WorksCorrectly()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(42));
        
        _registry.Register("TestBlueprint", blueprint);
        var retrieved = _registry.Get("TestBlueprint");
        
        Assert.AreEqual(1, retrieved.ComponentCount);
        Assert.IsTrue(retrieved.Has<TestComponent>());
        
        var component = retrieved.Get<TestComponent>();
        Assert.AreEqual(42, component.Value);
    }

    [Test]
    public void IT_Registry_Contains_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(123));
        
        Assert.IsFalse(_registry.Contains("TestBlueprint"));
        
        _registry.Register("TestBlueprint", blueprint);
        Assert.IsTrue(_registry.Contains("TestBlueprint"));
    }

    [Test]
    public void IT_Registry_TryGet_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(456));
        
        Assert.IsFalse(_registry.TryGet("TestBlueprint", out var notFound));
        Assert.AreEqual(EntityBlueprint.Empty.ComponentCount, notFound.ComponentCount);
        
        _registry.Register("TestBlueprint", blueprint);
        Assert.IsTrue(_registry.TryGet("TestBlueprint", out var found));
        Assert.AreEqual(1, found.ComponentCount);
    }

    [Test]
    public void IT_Registry_Remove_WorksCorrectly()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(789));
        
        _registry.Register("TestBlueprint", blueprint);
        Assert.IsTrue(_registry.Contains("TestBlueprint"));
        
        var removed = _registry.Remove("TestBlueprint");
        Assert.IsTrue(removed);
        Assert.IsFalse(_registry.Contains("TestBlueprint"));
        
        var removedAgain = _registry.Remove("TestBlueprint");
        Assert.IsFalse(removedAgain);
    }

    [Test]
    public void IT_Registry_Clear_RemovesAllBlueprints()
    {
        _registry.Register("Blueprint1", EntityBlueprint.Empty);
        _registry.Register("Blueprint2", EntityBlueprint.Empty);
        
        Assert.IsTrue(_registry.Contains("Blueprint1"));
        Assert.IsTrue(_registry.Contains("Blueprint2"));
        
        _registry.Clear();
        
        Assert.IsFalse(_registry.Contains("Blueprint1"));
        Assert.IsFalse(_registry.Contains("Blueprint2"));
        Assert.AreEqual(0, _registry.GetNames().Count());
    }

    [Test]
    public void IT_Registry_GetNames_ReturnsAllNames()
    {
        _registry.Register("First", EntityBlueprint.Empty);
        _registry.Register("Second", EntityBlueprint.Empty);
        _registry.Register("Third", EntityBlueprint.Empty);
        
        var names = _registry.GetNames().ToArray();
        
        Assert.AreEqual(3, names.Length);
        CollectionAssert.Contains(names, "First");
        CollectionAssert.Contains(names, "Second");
        CollectionAssert.Contains(names, "Third");
    }

    [Test]
    public void IT_Registry_RegisterFromFile_LoadsLazily()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(999));
        var filePath = Path.Combine(_tempDir, "lazy_blueprint.json");
        
        BlueprintSerializer.SaveToFile(blueprint, filePath);
        
        _registry.RegisterFromFile("LazyBlueprint", filePath);
        Assert.IsTrue(_registry.Contains("LazyBlueprint"));
        
        var loaded = _registry.Get("LazyBlueprint");
        Assert.AreEqual(1, loaded.ComponentCount);
        
        var component = loaded.Get<TestComponent>();
        Assert.AreEqual(999, component.Value);
    }

    [Test]
    public void IT_Registry_RegisterFromFile_CachesAfterFirstLoad()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(555));
        var filePath = Path.Combine(_tempDir, "cached_blueprint.json");
        
        BlueprintSerializer.SaveToFile(blueprint, filePath);
        _registry.RegisterFromFile("CachedBlueprint", filePath);
        
        var first = _registry.Get("CachedBlueprint");
        var second = _registry.Get("CachedBlueprint");
        
        // Should be cached and identical
        Assert.AreEqual(first.ComponentCount, second.ComponentCount);
        Assert.AreEqual(first.Signature, second.Signature);
    }

    [Test]
    public void IT_Registry_PreloadAll_LoadsAllFileBasedBlueprints()
    {
        var blueprint1 = EntityBlueprint.Empty.With(new TestComponent(111));
        var blueprint2 = EntityBlueprint.Empty.With(new TestComponent(222));
        
        var file1 = Path.Combine(_tempDir, "preload1.json");
        var file2 = Path.Combine(_tempDir, "preload2.json");
        
        BlueprintSerializer.SaveToFile(blueprint1, file1);
        BlueprintSerializer.SaveToFile(blueprint2, file2);
        
        _registry.RegisterFromFile("Preload1", file1);
        _registry.RegisterFromFile("Preload2", file2);
        
        _registry.PreloadAll();
        
        var stats = _registry.GetStats();
        Assert.AreEqual(2, stats.CachedCount);
        Assert.AreEqual(2, stats.FileBasedCount);
    }

    [Test]
    public void IT_Registry_Save_UpdatesFile()
    {
        var originalBlueprint = EntityBlueprint.Empty.With(new TestComponent(100));
        var filePath = Path.Combine(_tempDir, "save_test.json");
        
        BlueprintSerializer.SaveToFile(originalBlueprint, filePath);
        _registry.RegisterFromFile("SaveTest", filePath);
        
        // Load and modify
        var loaded = _registry.Get("SaveTest");
        var modified = loaded.Clone().With(new TestComponent(200));
        
        _registry.Register("SaveTest", modified);
        _registry.Save("SaveTest");
        
        // Verify file was updated
        var reloaded = BlueprintSerializer.LoadFromFile(filePath);
        var component = reloaded.Get<TestComponent>();
        Assert.AreEqual(200, component.Value);
    }

    [Test]
    public void IT_Registry_GetStats_ReturnsCorrectStats()
    {
        _registry.Register("InMemory", EntityBlueprint.Empty);
        
        var filePath = Path.Combine(_tempDir, "file_based.json");
        BlueprintSerializer.SaveToFile(EntityBlueprint.Empty, filePath);
        _registry.RegisterFromFile("FileBased", filePath);
        
        var stats = _registry.GetStats();
        
        Assert.AreEqual(1, stats.CachedCount);
        Assert.AreEqual(1, stats.FileBasedCount);
        Assert.AreEqual(2, stats.TotalCount);
    }

    [Test]
    public void IT_Registry_RegisterNullName_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _registry.Register(null, EntityBlueprint.Empty));
    }

    [Test]
    public void IT_Registry_RegisterEmptyName_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _registry.Register("", EntityBlueprint.Empty));
    }

    [Test]
    public void IT_Registry_RegisterNullBlueprint_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Register("Test", null));
    }

    [Test]
    public void IT_Registry_RegisterFromNonExistentFile_ThrowsException()
    {
        var nonExistentPath = Path.Combine(_tempDir, "does_not_exist.json");
        Assert.Throws<FileNotFoundException>(() => _registry.RegisterFromFile("Test", nonExistentPath));
    }

    [Test]
    public void IT_Registry_GetNonExistentBlueprint_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _registry.Get("DoesNotExist"));
    }

    [Test]
    public void IT_Registry_SaveNonCachedBlueprint_ThrowsException()
    {
        var filePath = Path.Combine(_tempDir, "not_cached.json");
        BlueprintSerializer.SaveToFile(EntityBlueprint.Empty, filePath);
        _registry.RegisterFromFile("NotCached", filePath);
        
        // Try to save without loading first
        Assert.Throws<ArgumentException>(() => _registry.Save("NotCached"));
    }
}