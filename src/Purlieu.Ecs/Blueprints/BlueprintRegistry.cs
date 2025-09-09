using System;
using System.Collections.Generic;
using System.IO;

namespace Purlieu.Ecs.Blueprints;

/// <summary>
/// Registry for managing named entity blueprints with caching and batch operations.
/// Supports loading blueprints from files and managing them by string identifiers.
/// </summary>
public sealed class BlueprintRegistry
{
    private readonly Dictionary<string, EntityBlueprint> _blueprints;
    private readonly Dictionary<string, string> _filePaths;

    public BlueprintRegistry()
    {
        _blueprints = new Dictionary<string, EntityBlueprint>();
        _filePaths = new Dictionary<string, string>();
    }

    /// <summary>
    /// Register a blueprint with a given name.
    /// </summary>
    public void Register(string name, EntityBlueprint blueprint)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Blueprint name cannot be null or empty", nameof(name));
        
        if (blueprint == null)
            throw new ArgumentNullException(nameof(blueprint));

        _blueprints[name] = blueprint;
    }

    /// <summary>
    /// Register a blueprint from a file path. The blueprint will be loaded when first accessed.
    /// </summary>
    public void RegisterFromFile(string name, string filePath)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Blueprint name cannot be null or empty", nameof(name));
        
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Blueprint file not found: {filePath}");

        _filePaths[name] = filePath;
    }

    /// <summary>
    /// Get a blueprint by name. Loads from file if necessary.
    /// </summary>
    public EntityBlueprint Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Blueprint name cannot be null or empty", nameof(name));

        // Check cache first
        if (_blueprints.TryGetValue(name, out var cachedBlueprint))
            return cachedBlueprint;

        // Try to load from file
        if (_filePaths.TryGetValue(name, out var filePath))
        {
            var blueprint = BlueprintSerializer.LoadFromFile(filePath);
            _blueprints[name] = blueprint; // Cache it
            return blueprint;
        }

        throw new ArgumentException($"Blueprint '{name}' not found in registry");
    }

    /// <summary>
    /// Try to get a blueprint by name. Returns false if not found.
    /// </summary>
    public bool TryGet(string name, out EntityBlueprint blueprint)
    {
        try
        {
            blueprint = Get(name);
            return true;
        }
        catch (ArgumentException)
        {
            blueprint = EntityBlueprint.Empty;
            return false;
        }
    }

    /// <summary>
    /// Check if a blueprint with the given name is registered.
    /// </summary>
    public bool Contains(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return _blueprints.ContainsKey(name) || _filePaths.ContainsKey(name);
    }

    /// <summary>
    /// Remove a blueprint from the registry.
    /// </summary>
    public bool Remove(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var removedFromCache = _blueprints.Remove(name);
        var removedFromFiles = _filePaths.Remove(name);
        
        return removedFromCache || removedFromFiles;
    }

    /// <summary>
    /// Clear all registered blueprints.
    /// </summary>
    public void Clear()
    {
        _blueprints.Clear();
        _filePaths.Clear();
    }

    /// <summary>
    /// Get all registered blueprint names.
    /// </summary>
    public IEnumerable<string> GetNames()
    {
        var names = new HashSet<string>();
        
        foreach (var name in _blueprints.Keys)
            names.Add(name);
            
        foreach (var name in _filePaths.Keys)
            names.Add(name);

        return names;
    }

    /// <summary>
    /// Load all file-based blueprints into memory cache.
    /// Useful for preloading during startup.
    /// </summary>
    public void PreloadAll()
    {
        foreach (var (name, filePath) in _filePaths)
        {
            if (!_blueprints.ContainsKey(name))
            {
                var blueprint = BlueprintSerializer.LoadFromFile(filePath);
                _blueprints[name] = blueprint;
            }
        }
    }

    /// <summary>
    /// Save a cached blueprint to its associated file path.
    /// </summary>
    public void Save(string name)
    {
        if (!_blueprints.TryGetValue(name, out var blueprint))
            throw new ArgumentException($"Blueprint '{name}' not found in cache");

        if (!_filePaths.TryGetValue(name, out var filePath))
            throw new ArgumentException($"No file path associated with blueprint '{name}'");

        BlueprintSerializer.SaveToFile(blueprint, filePath);
    }

    /// <summary>
    /// Save all cached blueprints to their associated file paths.
    /// </summary>
    public void SaveAll()
    {
        foreach (var name in _blueprints.Keys)
        {
            if (_filePaths.ContainsKey(name))
            {
                Save(name);
            }
        }
    }

    /// <summary>
    /// Get statistics about the registry.
    /// </summary>
    public BlueprintRegistryStats GetStats()
    {
        return new BlueprintRegistryStats
        {
            CachedCount = _blueprints.Count,
            FileBasedCount = _filePaths.Count,
            TotalCount = GetNames().Count()
        };
    }

    public override string ToString()
    {
        var stats = GetStats();
        return $"BlueprintRegistry(cached={stats.CachedCount}, files={stats.FileBasedCount}, total={stats.TotalCount})";
    }
}

/// <summary>
/// Statistics information for a BlueprintRegistry.
/// </summary>
public readonly struct BlueprintRegistryStats
{
    public readonly int CachedCount;
    public readonly int FileBasedCount;
    public readonly int TotalCount;

    public BlueprintRegistryStats(int cachedCount, int fileBasedCount, int totalCount)
    {
        CachedCount = cachedCount;
        FileBasedCount = fileBasedCount;
        TotalCount = totalCount;
    }
}