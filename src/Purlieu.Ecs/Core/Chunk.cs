using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

public sealed class Chunk
{
    public const int DefaultCapacity = 512;

    private readonly Entity[] _entities;
    private readonly Dictionary<Type, Array> _componentArrays;
    private int _count;

    public Chunk(ComponentSignature signature, int capacity = DefaultCapacity)
    {
        Signature = signature;
        Capacity = capacity;
        _entities = new Entity[capacity];
        _componentArrays = new Dictionary<Type, Array>();
        _count = 0;
    }

    public ComponentSignature Signature { get; }
    public int Capacity { get; }
    public int Count => _count;
    public bool IsFull => _count >= Capacity;
    public bool IsEmpty => _count == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Entity> GetEntities()
    {
        return _entities.AsSpan(0, _count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity GetEntity(int index)
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _entities[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan<T>() where T : struct
    {
        var componentType = typeof(T);

        if (!_componentArrays.TryGetValue(componentType, out var array))
        {
            // Create array on first access if this component is in the signature
            if (!Signature.Has<T>())
                throw new InvalidOperationException($"Component type {typeof(T).Name} is not part of this chunk's signature");

            array = new T[Capacity];
            _componentArrays[componentType] = array;
        }

        return ((T[])array).AsSpan(0, _count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetComponent<T>(int index) where T : struct
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var span = GetSpan<T>();
        return ref span[index];
    }

    public int AddEntity(Entity entity)
    {
        if (IsFull)
            throw new InvalidOperationException("Chunk is full");

        var index = _count;
        _entities[index] = entity;
        _count++;

        return index;
    }

    public void RemoveEntity(int index)
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        // Move last entity to the removed slot (swap-remove)
        var lastIndex = _count - 1;

        if (index != lastIndex)
        {
            _entities[index] = _entities[lastIndex];

            // Move component data for all component types
            foreach (var (componentType, array) in _componentArrays)
            {
                Array.Copy(array, lastIndex, array, index, 1);
            }
        }

        _count--;
    }

    public int FindEntity(Entity entity)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_entities[i] == entity)
                return i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComponent<T>(int index, in T component) where T : struct
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var span = GetSpan<T>();
        span[index] = component;
    }

    public void EnsureComponentArray<T>() where T : struct
    {
        var componentType = typeof(T);

        if (!_componentArrays.ContainsKey(componentType))
        {
            if (!Signature.Has<T>())
                throw new InvalidOperationException($"Component type {typeof(T).Name} is not part of this chunk's signature");

            _componentArrays[componentType] = new T[Capacity];
        }
    }

    public override string ToString()
    {
        return $"Chunk(signature={Signature}, count={Count}/{Capacity})";
    }
}