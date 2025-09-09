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

    internal ComponentQuery(World world)
    {
        _world = world;
        _withSignature = ComponentSignature.Empty;
        _withoutSignature = ComponentSignature.Empty;
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

    public IEnumerable<Chunk> Chunks()
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
                        yield return chunk;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesQuery(ComponentSignature signature)
    {
        return signature.HasAll(_withSignature) && signature.HasNone(_withoutSignature);
    }
}