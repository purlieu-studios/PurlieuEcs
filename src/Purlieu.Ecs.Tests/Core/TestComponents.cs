namespace Purlieu.Ecs.Tests.Core;

// Test component structs for testing
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

public struct Health
{
    public int Current, Max;

    public Health(int current, int max)
    {
        Current = current;
        Max = max;
    }

    public override string ToString() => $"Health({Current}/{Max})";
}

public struct Name
{
    public string Value;

    public Name(string value)
    {
        Value = value;
    }

    public override string ToString() => $"Name({Value})";
}

// Tag components (no data)
public struct Player { }
public struct Enemy { }
public struct Dead { }
public struct Stunned { }