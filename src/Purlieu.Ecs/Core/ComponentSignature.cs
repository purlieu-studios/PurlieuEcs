using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

public readonly struct ComponentSignature : IEquatable<ComponentSignature>
{
    private readonly ulong _bits;

    private ComponentSignature(ulong bits)
    {
        _bits = bits;
    }

    public static ComponentSignature Empty => new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentSignature With<T>() where T : struct
    {
        var typeId = ComponentTypeId<T>.Id;
        if (typeId >= 64)
            throw new InvalidOperationException($"Component type ID {typeId} exceeds maximum of 63");

        return new ComponentSignature(_bits | (1UL << typeId));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentSignature WithType(Type componentType)
    {
        var typeId = ComponentTypeRegistry.GetOrAssignId(componentType);
        if (typeId >= 64)
            throw new InvalidOperationException($"Component type ID {typeId} exceeds maximum of 63");

        return new ComponentSignature(_bits | (1UL << typeId));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentSignature Without<T>() where T : struct
    {
        var typeId = ComponentTypeId<T>.Id;
        if (typeId >= 64)
            throw new InvalidOperationException($"Component type ID {typeId} exceeds maximum of 63");

        return new ComponentSignature(_bits & ~(1UL << typeId));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : struct
    {
        var typeId = ComponentTypeId<T>.Id;
        if (typeId >= 64)
            return false;

        return (_bits & (1UL << typeId)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAll(ComponentSignature other)
    {
        return (_bits & other._bits) == other._bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAny(ComponentSignature other)
    {
        return (_bits & other._bits) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasNone(ComponentSignature other)
    {
        return (_bits & other._bits) == 0;
    }

    public int ComponentCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Numerics.BitOperations.PopCount(_bits);
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bits == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ComponentSignature other)
    {
        return _bits == other._bits;
    }

    public override bool Equals(object? obj)
    {
        return obj is ComponentSignature other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return _bits.GetHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ComponentSignature left, ComponentSignature right)
    {
        return left.Equals(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ComponentSignature left, ComponentSignature right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        if (IsEmpty)
            return "ComponentSignature(empty)";

        var components = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            if ((_bits & (1UL << i)) != 0)
                components.Add(i);
        }

        return $"ComponentSignature({string.Join(",", components)})";
    }

    public static implicit operator ulong(ComponentSignature signature)
    {
        return signature._bits;
    }

    public static explicit operator ComponentSignature(ulong bits)
    {
        return new ComponentSignature(bits);
    }
}

public static class ComponentTypeId<T> where T : struct
{
    private static int _cachedId = -1;
    private static int _registryVersion = -1;

    public static int Id
    {
        get
        {
            var currentVersion = ComponentTypeRegistry.Version;
            if (_registryVersion != currentVersion)
            {
                _cachedId = ComponentTypeRegistry.GetOrAssignId<T>();
                _registryVersion = currentVersion;
            }
            return _cachedId;
        }
    }
}

public static class ComponentTypeRegistry
{
    private static readonly Dictionary<Type, int> _typeToId = new();
    private static int _nextId = 0;
    private static int _version = 0;

    public static int Version => _version;

    public static int GetOrAssignId<T>() where T : struct
    {
        var type = typeof(T);

        if (_typeToId.TryGetValue(type, out var existingId))
            return existingId;

        if (_nextId >= 64)
            throw new InvalidOperationException("Maximum of 64 component types supported");

        var newId = _nextId++;
        _typeToId[type] = newId;
        return newId;
    }

    public static int GetId<T>() where T : struct
    {
        return _typeToId.TryGetValue(typeof(T), out var id) ? id : -1;
    }

    public static int GetOrAssignId(Type componentType)
    {
        if (!componentType.IsValueType)
            throw new ArgumentException($"Component type {componentType} must be a value type (struct)");

        if (_typeToId.TryGetValue(componentType, out var existingId))
            return existingId;

        if (_nextId >= 64)
            throw new InvalidOperationException("Maximum of 64 component types supported");

        var newId = _nextId++;
        _typeToId[componentType] = newId;
        return newId;
    }

    public static void Reset()
    {
        _typeToId.Clear();
        _nextId = 0;
        _version++;
    }
}
