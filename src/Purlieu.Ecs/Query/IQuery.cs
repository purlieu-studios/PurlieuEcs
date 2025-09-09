using System;
using System.Collections.Generic;
using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Query;

public interface IQuery
{
    IQuery With<T>() where T : struct;
    IQuery Without<T>() where T : struct;
    IEnumerable<Chunk> Chunks();
}
