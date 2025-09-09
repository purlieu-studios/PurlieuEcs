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

        Assert.That(retrieved.ComponentCount, Is.EqualTo(1));
        Assert.That(retrieved.Has<TestComponent>(), Is.True);

        var component = retrieved.Get<TestComponent>();
        Assert.That(component.Value, Is.EqualTo(42));
    }

    [Test]
    public void IT_Registry_Contains_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(123));

        Assert.That(_registry.Contains("TestBlueprint"), Is.False);

        _registry.Register("TestBlueprint", blueprint);
        Assert.That(_registry.Contains("TestBlueprint"), Is.True);
    }

    [Test]
    public void IT_Registry_TryGet_ReturnsCorrectValue()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(456));

        Assert.That(_registry.TryGet("TestBlueprint", out var notFound), Is.False);
        Assert.That(notFound.ComponentCount, Is.EqualTo(EntityBlueprint.Empty.ComponentCount));

        _registry.Register("TestBlueprint", blueprint);
        Assert.That(_registry.TryGet("TestBlueprint", out var found), Is.True);
        Assert.That(found.ComponentCount, Is.EqualTo(1));
    }

    [Test]
    public void IT_Registry_Remove_WorksCorrectly()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(789));

        _registry.Register("TestBlueprint", blueprint);
        Assert.That(_registry.Contains("TestBlueprint"), Is.True);

        var removed = _registry.Remove("TestBlueprint");
        Assert.That(removed, Is.True);
        Assert.That(_registry.Contains("TestBlueprint"), Is.False);

        var removedAgain = _registry.Remove("TestBlueprint");
        Assert.That(removedAgain, Is.False);
    }

    [Test]
    public void IT_Registry_Clear_RemovesAllBlueprints()
    {
        _registry.Register("Blueprint1", EntityBlueprint.Empty);
        _registry.Register("Blueprint2", EntityBlueprint.Empty);

        Assert.That(_registry.Contains("Blueprint1"), Is.True);
        Assert.That(_registry.Contains("Blueprint2"), Is.True);

        _registry.Clear();

        Assert.That(_registry.Contains("Blueprint1"), Is.False);
        Assert.That(_registry.Contains("Blueprint2"), Is.False);
        Assert.That(_registry.GetNames().Count(), Is.EqualTo(0));
    }

    [Test]
    public void IT_Registry_GetNames_ReturnsAllNames()
    {
        _registry.Register("First", EntityBlueprint.Empty);
        _registry.Register("Second", EntityBlueprint.Empty);
        _registry.Register("Third", EntityBlueprint.Empty);

        var names = _registry.GetNames().ToArray();

        Assert.That(names.Length, Is.EqualTo(3));
        Assert.That(names, Contains.Item("First"));
        Assert.That(names, Contains.Item("Second"));
        Assert.That(names, Contains.Item("Third"));
    }

    [Test]
    public void IT_Registry_RegisterFromFile_LoadsLazily()
    {
        var blueprint = EntityBlueprint.Empty.With(new TestComponent(999));
        var filePath = Path.Combine(_tempDir, "lazy_blueprint.json");

        BlueprintSerializer.SaveToFile(blueprint, filePath);

        _registry.RegisterFromFile("LazyBlueprint", filePath);
        Assert.That(_registry.Contains("LazyBlueprint"), Is.True);

        var loaded = _registry.Get("LazyBlueprint");
        Assert.That(loaded.ComponentCount, Is.EqualTo(1));

        var component = loaded.Get<TestComponent>();
        Assert.That(component.Value, Is.EqualTo(999));
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
        Assert.That(second.ComponentCount, Is.EqualTo(first.ComponentCount));
        Assert.That(second.Signature, Is.EqualTo(first.Signature));
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
        Assert.That(stats.CachedCount, Is.EqualTo(2));
        Assert.That(stats.FileBasedCount, Is.EqualTo(2));
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
        Assert.That(component.Value, Is.EqualTo(200));
    }

    [Test]
    public void IT_Registry_GetStats_ReturnsCorrectStats()
    {
        _registry.Register("InMemory", EntityBlueprint.Empty);

        var filePath = Path.Combine(_tempDir, "file_based.json");
        BlueprintSerializer.SaveToFile(EntityBlueprint.Empty, filePath);
        _registry.RegisterFromFile("FileBased", filePath);

        var stats = _registry.GetStats();

        Assert.That(stats.CachedCount, Is.EqualTo(1));
        Assert.That(stats.FileBasedCount, Is.EqualTo(1));
        Assert.That(stats.TotalCount, Is.EqualTo(2));
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
