namespace Purlieu.Ecs.Core;

/// <summary>
/// Basic position component for 3D space.
/// </summary>
public struct Position
{
    public float X, Y, Z;

    public Position(float x, float y, float z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString() => $"Position({X},{Y},{Z})";
}

/// <summary>
/// Basic velocity component for 3D movement.
/// </summary>
public struct Velocity
{
    public float X, Y, Z;

    public Velocity(float x, float y, float z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString() => $"Velocity({X},{Y},{Z})";
}
