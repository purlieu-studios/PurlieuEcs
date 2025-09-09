using System;

namespace Purlieu.Ecs.Systems;

/// <summary>
/// Attribute to specify the execution phase and order of a system.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GamePhaseAttribute : Attribute
{
    /// <summary>
    /// The phase in which this system should execute.
    /// </summary>
    public GamePhase Phase { get; }

    /// <summary>
    /// The order within the phase (lower values execute first).
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Creates a new GamePhase attribute.
    /// </summary>
    /// <param name="phase">The execution phase</param>
    /// <param name="order">The order within the phase (default: 0)</param>
    public GamePhaseAttribute(GamePhase phase, int order = 0)
    {
        Phase = phase;
        Order = order;
    }
}

/// <summary>
/// Execution phases for systems.
/// </summary>
public enum GamePhase
{
    /// <summary>
    /// Main update phase for game logic.
    /// </summary>
    Update = 0,

    /// <summary>
    /// Post-update phase for cleanup and derived calculations.
    /// </summary>
    PostUpdate = 1,

    /// <summary>
    /// Presentation phase for visual updates and intent emission.
    /// </summary>
    Presentation = 2
}
