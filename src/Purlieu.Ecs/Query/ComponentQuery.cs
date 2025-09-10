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
        var querySignature = BuildQuerySignature();

        var chunks = ChunksInternal().ToList(); // Materialize to allow profiling
        QueryProfiler.ProfileQuery(querySignature, () => chunks);

        return chunks;
    }

    private IEnumerable<IChunkView> ChunksInternal()
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

    /// <summary>
    /// Builds a human-readable signature for this query for profiling purposes.
    /// </summary>
    private string BuildQuerySignature()
    {
        var parts = new List<string>();

        // Add With components
        var withComponents = GetComponentNames(_withSignature);
        if (withComponents.Count > 0)
        {
            parts.Add($"With<{string.Join(",", withComponents)}>");
        }

        // Add Without components
        var withoutComponents = GetComponentNames(_withoutSignature);
        if (withoutComponents.Count > 0)
        {
            parts.Add($"Without<{string.Join(",", withoutComponents)}>");
        }

        // Add Changed components
        var changedComponents = GetComponentNames(_changedSignature);
        if (changedComponents.Count > 0)
        {
            parts.Add($"Changed<{string.Join(",", changedComponents)}>");
        }

        // Add Optional components
        var optionalComponents = GetComponentNames(_optionalSignature);
        if (optionalComponents.Count > 0)
        {
            parts.Add($"Optional<{string.Join(",", optionalComponents)}>");
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// Gets component type names from a signature for debugging.
    /// Uses the component registry to get actual type names where possible.
    /// </summary>
    private List<string> GetComponentNames(ComponentSignature signature)
    {
        var names = new List<string>();

        for (int i = 0; i < 64; i++)
        {
            if (signature.HasComponentId(i))
            {
                // Try to get the actual type name from the registry
                var typeName = ComponentTypeRegistry.GetTypeName(i);
                names.Add(typeName ?? $"C{i}");
            }
        }

        return names;
    }
}
