using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Systems;

/// <summary>
/// Base interface for all ECS systems. Systems must be stateless and deterministic.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// Updates the system logic for the current frame.
    /// </summary>
    /// <param name="world">The world to operate on</param>
    /// <param name="deltaTime">Time elapsed since last frame in seconds</param>
    void Update(World world, float deltaTime);
}
