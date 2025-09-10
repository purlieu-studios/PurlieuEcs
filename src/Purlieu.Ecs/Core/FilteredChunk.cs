using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

/// <summary>
/// A filtered view of a chunk that only exposes entities matching specific criteria.
/// Used for Changed<T> queries to provide entity-level filtering while maintaining chunk-based APIs.
/// </summary>
public sealed class FilteredChunk : IChunkView
{
    private readonly Chunk _sourceChunk;
    private readonly int[] _entityIndices;
    private readonly int _count;

    public FilteredChunk(Chunk sourceChunk, int[] entityIndices, int count)
    {
        _sourceChunk = sourceChunk;
        _entityIndices = entityIndices;
        _count = count;
    }

    public ComponentSignature Signature => _sourceChunk.Signature;
    public int Count => _count;
    public int Capacity => _sourceChunk.Capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Entity> GetEntities()
    {
        var entities = new Entity[_count];
        for (int i = 0; i < _count; i++)
        {
            entities[i] = _sourceChunk.GetEntity(_entityIndices[i]);
        }
        return entities.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity GetEntity(int index)
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _sourceChunk.GetEntity(_entityIndices[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan<T>() where T : struct
    {
        var sourceSpan = _sourceChunk.GetSpan<T>();
        var filteredArray = new T[_count];

        for (int i = 0; i < _count; i++)
        {
            filteredArray[i] = sourceSpan[_entityIndices[i]];
        }

        return filteredArray.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>() where T : struct
    {
        return _sourceChunk.HasComponent<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComponent<T>(int index, in T component) where T : struct
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _sourceChunk.SetComponent(_entityIndices[index], component);
    }
}
