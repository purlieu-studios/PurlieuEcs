using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Blueprints;

/// <summary>
/// Handles serialization and deserialization of EntityBlueprint instances.
/// Supports JSON format for human-readable blueprints and binary format for performance.
/// </summary>
public static class BlueprintSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serialize a blueprint to JSON format.
    /// </summary>
    public static string SerializeToJson(EntityBlueprint blueprint)
    {
        var data = new BlueprintData
        {
            Components = blueprint.Components.Select(c => new SerializedComponent
            {
                TypeName = c.ComponentType.AssemblyQualifiedName!,
                ValueJson = JsonSerializer.Serialize(c.Value)
            }).ToArray()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// Deserialize a blueprint from JSON format.
    /// </summary>
    public static EntityBlueprint DeserializeFromJson(string json)
    {
        var data = JsonSerializer.Deserialize<BlueprintData>(json, JsonOptions);
        if (data == null)
            throw new InvalidOperationException("Failed to deserialize blueprint data");

        var blueprint = EntityBlueprint.Empty;
        foreach (var component in data.Components)
        {
            var componentType = Type.GetType(component.TypeName);
            if (componentType == null)
                throw new InvalidOperationException($"Could not resolve component type: {component.TypeName}");

            if (!componentType.IsValueType)
                throw new InvalidOperationException($"Component type {componentType} must be a value type (struct)");

            var value = JsonSerializer.Deserialize(component.ValueJson, componentType);
            if (value == null)
                throw new InvalidOperationException($"Failed to deserialize component value for type {componentType}");

            // Use reflection to call the generic With method
            var withMethod = typeof(EntityBlueprint).GetMethod("With");
            var genericWithMethod = withMethod!.MakeGenericMethod(componentType);
            blueprint = (EntityBlueprint)genericWithMethod.Invoke(blueprint, new[] { value })!;
        }

        return blueprint;
    }

    /// <summary>
    /// Serialize a blueprint to binary format for efficient storage.
    /// </summary>
    public static byte[] SerializeToBinary(EntityBlueprint blueprint)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        // Write header
        writer.Write((byte)1); // Version
        writer.Write(blueprint.ComponentCount);

        // Write components
        foreach (var component in blueprint.Components)
        {
            // Write type name
            writer.Write(component.ComponentType.AssemblyQualifiedName!);

            // Serialize component to JSON for now (could be optimized further)
            var json = JsonSerializer.Serialize(component.Value);
            writer.Write(json);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Deserialize a blueprint from binary format.
    /// </summary>
    public static EntityBlueprint DeserializeFromBinary(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        // Read header
        var version = reader.ReadByte();
        if (version != 1)
            throw new InvalidOperationException($"Unsupported blueprint binary format version: {version}");

        var componentCount = reader.ReadInt32();
        var blueprint = EntityBlueprint.Empty;

        // Read components
        for (int i = 0; i < componentCount; i++)
        {
            // Read type name
            var typeName = reader.ReadString();
            var componentType = Type.GetType(typeName);
            if (componentType == null)
                throw new InvalidOperationException($"Could not resolve component type: {typeName}");

            // Read and deserialize component value
            var json = reader.ReadString();
            var value = JsonSerializer.Deserialize(json, componentType);
            if (value == null)
                throw new InvalidOperationException($"Failed to deserialize component value for type {componentType}");

            // Use reflection to call the generic With method
            var withMethod = typeof(EntityBlueprint).GetMethod("With");
            var genericWithMethod = withMethod!.MakeGenericMethod(componentType);
            blueprint = (EntityBlueprint)genericWithMethod.Invoke(blueprint, new[] { value })!;
        }

        return blueprint;
    }

    /// <summary>
    /// Save a blueprint to file in JSON format.
    /// </summary>
    public static void SaveToFile(EntityBlueprint blueprint, string filePath)
    {
        var json = SerializeToJson(blueprint);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Load a blueprint from JSON file.
    /// </summary>
    public static EntityBlueprint LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Blueprint file not found: {filePath}");

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return DeserializeFromJson(json);
    }

    /// <summary>
    /// Save a blueprint to file in binary format.
    /// </summary>
    public static void SaveToBinaryFile(EntityBlueprint blueprint, string filePath)
    {
        var data = SerializeToBinary(blueprint);
        File.WriteAllBytes(filePath, data);
    }

    /// <summary>
    /// Load a blueprint from binary file.
    /// </summary>
    public static EntityBlueprint LoadFromBinaryFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Blueprint file not found: {filePath}");

        var data = File.ReadAllBytes(filePath);
        return DeserializeFromBinary(data);
    }
}

/// <summary>
/// Internal data structure for JSON serialization.
/// </summary>
internal class BlueprintData
{
    public SerializedComponent[] Components { get; set; } = Array.Empty<SerializedComponent>();
}

/// <summary>
/// Internal data structure for component serialization.
/// </summary>
internal class SerializedComponent
{
    public string TypeName { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
}
