using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Query;

public sealed class ComponentQuery : IQuery
{
    private readonly World _world;
    private ComponentSignature _withSignature;
    private ComponentSignature _withoutSignature;
    private ComponentSignature _changedSignature;
    private ComponentSignature _optionalSignature;

    internal ComponentQuery(World world)
    {
        _world = world;
        _withSignature = ComponentSignature.Empty;
        _withoutSignature = ComponentSignature.Empty;
        _changedSignature = ComponentSignature.Empty;
        _optionalSignature = ComponentSignature.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuery With<T>() where T : struct
    {
        _withSignature = _withSignature.With<T>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuery Without<T>() where T : struct
    {
        _withoutSignature = _withoutSignature.With<T>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuery Changed<T>() where T : struct
    {
        _changedSignature = _changedSignature.With<T>();
        _withSignature = _withSignature.With<T>(); // Must also have the component
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuery Optional<T>() where T : struct
    {
        _optionalSignature = _optionalSignature.With<T>();
        return this;
    }

    public IEnumerable<IChunkView> Chunks()
    {
        var archetypes = _world.GetArchetypes();

        foreach (var archetype in archetypes)
        {
            if (MatchesQuery(archetype.Signature))
            {
                foreach (var chunk in archetype.Chunks)
                {
                    if (chunk.Count > 0)
                    {
                        if (_changedSignature.IsEmpty)
                        {
                            // No change filtering, return regular chunk
                            yield return chunk;
                        }
                        else
                        {
                            // Apply change filtering
                            var filteredChunk = CreateFilteredChunk(chunk, _changedSignature);
                            if (filteredChunk != null && filteredChunk.Count > 0)
                            {
                                yield return filteredChunk;
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesQuery(ComponentSignature signature)
    {
        // Required components (including those marked as Changed)
        var requiredComponents = (ulong)_withSignature & ~(ulong)_optionalSignature;
        if (!signature.HasAll((ComponentSignature)requiredComponents))
            return false;

        // Excluded components
        if (!signature.HasNone(_withoutSignature))
            return false;

        // Optional components don't affect archetype matching
        return true;
    }

    private FilteredChunk? CreateFilteredChunk(Chunk chunk, ComponentSignature changedSignature)
    {
        var entities = chunk.GetEntities();
        var matchingIndices = new List<int>();

        // Find entities in the chunk that have changed components
        for (int i = 0; i < entities.Length; i++)
        {
            if (_world.HasAnyChanged(entities[i], changedSignature))
            {
                matchingIndices.Add(i);
            }
        }

        if (matchingIndices.Count == 0)
            return null;

        return new FilteredChunk(chunk, matchingIndices.ToArray(), matchingIndices.Count);
    }
}
