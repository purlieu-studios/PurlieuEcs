using System;
using System.Collections.Generic;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Query;

public interface IQuery
{
    IQuery With<T>() where T : struct;
    IQuery Without<T>() where T : struct;
    IQuery Changed<T>() where T : struct;
    IQuery Optional<T>() where T : struct;
    IEnumerable<IChunkView> Chunks();
}
