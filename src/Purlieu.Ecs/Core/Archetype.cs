using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

public sealed class Archetype
{
    private readonly List<Chunk> _chunks;
    private readonly Dictionary<Entity, (int chunkIndex, int entityIndex)> _entityLocations;

    public Archetype(ComponentSignature signature)
    {
        Signature = signature;
        _chunks = new List<Chunk>();
        _entityLocations = new Dictionary<Entity, (int, int)>();
    }

    public ComponentSignature Signature { get; }
    public int ChunkCount => _chunks.Count;
    public int EntityCount => _entityLocations.Count;

    public IReadOnlyList<Chunk> Chunks => _chunks;

    public bool Contains(Entity entity)
    {
        return _entityLocations.ContainsKey(entity);
    }

    public (Chunk chunk, int index) GetEntityLocation(Entity entity)
    {
        if (!_entityLocations.TryGetValue(entity, out var location))
            throw new ArgumentException($"Entity {entity} not found in archetype");

        return (_chunks[location.chunkIndex], location.entityIndex);
    }

    public int AddEntity(Entity entity)
    {
        if (_entityLocations.ContainsKey(entity))
            throw new ArgumentException($"Entity {entity} already exists in archetype");

        // Find or create a chunk with space
        Chunk targetChunk = null!;
        int chunkIndex = -1;

        for (int i = 0; i < _chunks.Count; i++)
        {
            if (!_chunks[i].IsFull)
            {
                targetChunk = _chunks[i];
                chunkIndex = i;
                break;
            }
        }

        if (targetChunk == null)
        {
            // Create new chunk
            targetChunk = new Chunk(Signature);
            _chunks.Add(targetChunk);
            chunkIndex = _chunks.Count - 1;
        }

        var entityIndex = targetChunk.AddEntity(entity);
        _entityLocations[entity] = (chunkIndex, entityIndex);

        return entityIndex;
    }

    public void RemoveEntity(Entity entity)
    {
        if (!_entityLocations.TryGetValue(entity, out var location))
            throw new ArgumentException($"Entity {entity} not found in archetype");

        var chunk = _chunks[location.chunkIndex];
        var entityIndex = location.entityIndex;

        // Get the entity that will be moved to fill the gap (if any)
        Entity? movedEntity = null;
        if (entityIndex < chunk.Count - 1)
        {
            movedEntity = chunk.GetEntity(chunk.Count - 1);
        }

        // Remove from chunk (this moves the last entity to the removed slot)
        chunk.RemoveEntity(entityIndex);

        // Update location tracking
        _entityLocations.Remove(entity);

        if (movedEntity.HasValue)
        {
            // Update the location of the moved entity
            _entityLocations[movedEntity.Value] = (location.chunkIndex, entityIndex);
        }

        // Remove empty chunks (optional optimization)
        if (chunk.IsEmpty && _chunks.Count > 1)
        {
            _chunks.RemoveAt(location.chunkIndex);

            // Update chunk indices for all entities in chunks after the removed one
            for (int i = location.chunkIndex; i < _chunks.Count; i++)
            {
                var chunkEntities = _chunks[i].GetEntities();
                for (int j = 0; j < chunkEntities.Length; j++)
                {
                    var chunkEntity = chunkEntities[j];
                    if (_entityLocations.TryGetValue(chunkEntity, out var loc))
                    {
                        _entityLocations[chunkEntity] = (i, loc.entityIndex);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetComponent<T>(Entity entity) where T : struct
    {
        var (chunk, index) = GetEntityLocation(entity);
        return ref chunk.GetComponent<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComponent<T>(Entity entity, in T component) where T : struct
    {
        var (chunk, index) = GetEntityLocation(entity);
        chunk.SetComponent(index, component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>(Entity entity) where T : struct
    {
        return Signature.Has<T>() && Contains(entity);
    }

    public void EnsureComponentArrays<T>() where T : struct
    {
        foreach (var chunk in _chunks)
        {
            chunk.EnsureComponentArray<T>();
        }
    }

    public IEnumerable<Entity> GetAllEntities()
    {
        return _entityLocations.Keys;
    }

    public override string ToString()
    {
        return $"Archetype(signature={Signature}, entities={EntityCount}, chunks={ChunkCount})";
    }
}
