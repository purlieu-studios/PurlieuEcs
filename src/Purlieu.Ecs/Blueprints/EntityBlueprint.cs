using System;
using System.Collections.Generic;
using System.Linq;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Blueprints;

/// <summary>
/// Declarative entity definition for fast instantiation and serialization.
/// Blueprints define entities as component collections without requiring individual AddComponent calls.
/// </summary>
public sealed class EntityBlueprint
{
    private readonly List<ComponentData> _components;
    private ComponentSignature? _cachedSignature;

    public EntityBlueprint()
    {
        _components = new List<ComponentData>();
    }

    public EntityBlueprint(IEnumerable<ComponentData> components)
    {
        _components = new List<ComponentData>(components);
    }

    /// <summary>
    /// Gets all components defined in this blueprint.
    /// </summary>
    public IReadOnlyList<ComponentData> Components => _components;

    /// <summary>
    /// Gets the component signature for this blueprint (cached after first calculation).
    /// </summary>
    public ComponentSignature Signature
    {
        get
        {
            if (_cachedSignature == null)
            {
                var signature = ComponentSignature.Empty;
                foreach (var component in _components)
                {
                    signature = signature.WithType(component.ComponentType);
                }
                _cachedSignature = signature;
            }
            return _cachedSignature.Value;
        }
    }

    /// <summary>
    /// Add or replace a component in this blueprint.
    /// </summary>
    public EntityBlueprint With<T>(in T component) where T : struct
    {
        var componentType = typeof(T);

        // Remove existing component of same type first
        _components.RemoveAll(c => c.ComponentType == componentType);

        // Add the new component
        var componentData = new ComponentData(componentType, component);
        _components.Add(componentData);
        _cachedSignature = null; // Invalidate cache
        return this;
    }

    /// <summary>
    /// Remove all components of the specified type from this blueprint.
    /// </summary>
    public EntityBlueprint Without<T>() where T : struct
    {
        var componentType = typeof(T);
        _components.RemoveAll(c => c.ComponentType == componentType);
        _cachedSignature = null; // Invalidate cache
        return this;
    }

    /// <summary>
    /// Check if this blueprint contains a component of the specified type.
    /// </summary>
    public bool Has<T>() where T : struct
    {
        var componentType = typeof(T);
        return _components.Any(c => c.ComponentType == componentType);
    }

    /// <summary>
    /// Get a component value from this blueprint.
    /// </summary>
    public T Get<T>() where T : struct
    {
        var componentType = typeof(T);
        var componentData = _components.FirstOrDefault(c => c.ComponentType == componentType);
        if (componentData.ComponentType == null)
            throw new ArgumentException($"Blueprint does not contain component of type {typeof(T)}");

        return (T)componentData.Value;
    }

    /// <summary>
    /// Try to get a component value from this blueprint.
    /// </summary>
    public bool TryGet<T>(out T component) where T : struct
    {
        var componentType = typeof(T);
        var componentData = _components.FirstOrDefault(c => c.ComponentType == componentType);
        if (componentData.ComponentType != null)
        {
            component = (T)componentData.Value;
            return true;
        }

        component = default;
        return false;
    }

    /// <summary>
    /// Create a copy of this blueprint.
    /// </summary>
    public EntityBlueprint Clone()
    {
        return new EntityBlueprint(_components.ToList());
    }

    /// <summary>
    /// Get the number of components in this blueprint.
    /// </summary>
    public int ComponentCount => _components.Count;

    /// <summary>
    /// Create an empty blueprint.
    /// </summary>
    public static EntityBlueprint Empty => new EntityBlueprint();

    public override string ToString()
    {
        var componentNames = _components.Select(c => c.ComponentType.Name);
        return $"EntityBlueprint({string.Join(", ", componentNames)})";
    }
}

/// <summary>
/// Container for component type and value data in blueprints.
/// </summary>
public readonly struct ComponentData
{
    public readonly Type ComponentType;
    public readonly object Value;

    public ComponentData(Type componentType, object value)
    {
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        Value = value ?? throw new ArgumentNullException(nameof(value));

        if (!componentType.IsValueType)
            throw new ArgumentException($"Component type {componentType} must be a value type (struct)");
    }

    public override string ToString() => $"{ComponentType.Name}: {Value}";
}
