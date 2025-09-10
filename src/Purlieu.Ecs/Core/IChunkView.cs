using System;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

/// <summary>
/// Interface for chunk-like containers that provide component access.
/// Allows both regular chunks and filtered chunks to be used interchangeably.
/// </summary>
public interface IChunkView
{
    ComponentSignature Signature { get; }
    int Count { get; }
    int Capacity { get; }

    Span<Entity> GetEntities();
    Entity GetEntity(int index);
    Span<T> GetSpan<T>() where T : struct;
    bool HasComponent<T>() where T : struct;
    void SetComponent<T>(int index, in T component) where T : struct;
}
