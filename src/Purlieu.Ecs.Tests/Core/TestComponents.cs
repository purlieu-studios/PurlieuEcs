namespace Purlieu.Ecs.Tests.Core;

// Test component structs for testing

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