using System;

namespace Purlieu.Ecs.Events;

/// <summary>
/// Marks an event or intent type as having one-frame lifetime.
/// Events marked with this attribute are automatically cleared at the end of each frame.
/// This ensures that events don't persist across multiple frames and maintains ECS purity.
/// </summary>
/// <example>
/// <code>
/// [OneFrame]
/// public struct PositionChangedIntent
/// {
///     public Entity Entity;
///     public Position NewPosition;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
public sealed class OneFrameAttribute : Attribute
{
    /// <summary>
    /// Creates a new OneFrame attribute instance.
    /// </summary>
    public OneFrameAttribute()
    {
    }
}
