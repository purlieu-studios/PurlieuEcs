using Purlieu.Ecs.Core;

namespace Purlieu.Ecs.Systems;

/// <summary>
/// Example system demonstrating BVIP pattern - moves entities with Position and Velocity.
/// Systems never reference engine types and emit intents for visual updates.
/// </summary>
[GamePhase(GamePhase.Update, order: 100)]
public sealed class MovementSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        var query = world.Query()
            .With<Position>()
            .With<Velocity>();

        foreach (var chunk in query.Chunks())
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetSpan<Velocity>();

            for (int i = 0; i < chunk.Count; i++)
            {
                var oldPosition = positions[i];

                // Update position based on velocity
                positions[i] = new Position(
                    oldPosition.X + velocities[i].X * deltaTime,
                    oldPosition.Y + velocities[i].Y * deltaTime,
                    oldPosition.Z + velocities[i].Z * deltaTime
                );

                // BVIP pattern: emit intent only if position changed
                var newPosition = positions[i];
                if (oldPosition.X != newPosition.X ||
                    oldPosition.Y != newPosition.Y ||
                    oldPosition.Z != newPosition.Z)
                {
                    // In a full implementation, this would emit a PositionChangedIntent
                    // For now, this demonstrates the pattern without requiring the Events system

                    // Example: world.Events<PositionChangedIntent>().Publish(new PositionChangedIntent 
                    // { 
                    //     Entity = chunk.GetEntity(i), 
                    //     NewPosition = newPosition 
                    // });
                }
            }
        }
    }
}