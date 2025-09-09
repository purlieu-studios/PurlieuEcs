using System;
using System.Runtime.CompilerServices;

namespace Purlieu.Ecs.Core;

/// <summary>
/// Represents a unique entity in the ECS world.
/// Packed as (uint id, uint version) into ulong to prevent stale references.
/// </summary>
/// <remarks>
/// Design principles:
/// - Immutable value type for thread safety
/// - Version prevents reuse of old entity IDs
/// - Packed into 64-bit value for efficient storage
/// - Zero-cost abstractions with aggressive inlining
/// </remarks>
public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
{
    private readonly ulong _packed;

    private const int IdShift = 0;
    private const int VersionShift = 32;
    private const uint IdMask = 0xFFFFFFFF;
    private const uint VersionMask = 0xFFFFFFFF;

    /// <summary>
    /// Gets the unique identifier component of this entity.
    /// </summary>
    public uint Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)((_packed >> IdShift) & IdMask);
    }

    /// <summary>
    /// Gets the version component of this entity.
    /// Used to prevent stale references when entity IDs are recycled.
    /// </summary>
    public uint Version
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)((_packed >> VersionShift) & VersionMask);
    }

    /// <summary>
    /// Gets a value indicating whether this entity is null (uninitialized).
    /// </summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _packed == 0;
    }

    /// <summary>
    /// Represents a null/invalid entity.
    /// </summary>
    public static readonly Entity Null = default;

    /// <summary>
    /// Initializes a new entity with the specified ID and version.
    /// </summary>
    /// <param name="id">The unique identifier for this entity.</param>
    /// <param name="version">The version to prevent stale references.</param>
    public Entity(uint id, uint version)
    {
        _packed = ((ulong)version << VersionShift) | ((ulong)id << IdShift);
    }

    /// <summary>
    /// Creates an entity from a packed ulong value.
    /// </summary>
    /// <param name="packed">The packed entity data.</param>
    /// <returns>An entity with the unpacked ID and version.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity FromPacked(ulong packed)
    {
        var id = (uint)((packed >> IdShift) & IdMask);
        var version = (uint)((packed >> VersionShift) & VersionMask);
        return new Entity(id, version);
    }

    /// <summary>
    /// Gets the packed representation of this entity as a ulong.
    /// </summary>
    /// <returns>The packed entity data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ToPacked() => _packed;

    /// <summary>
    /// Determines whether this entity is equal to another entity.
    /// </summary>
    /// <param name="other">The other entity to compare.</param>
    /// <returns>True if the entities are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Entity other) => _packed == other._packed;

    /// <summary>
    /// Determines whether this entity is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the object is an entity and equal to this entity; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <summary>
    /// Gets the hash code for this entity.
    /// </summary>
    /// <returns>A hash code for this entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => _packed.GetHashCode();

    /// <summary>
    /// Compares this entity to another entity for ordering.
    /// Entities are ordered first by ID, then by version.
    /// </summary>
    /// <param name="other">The other entity to compare.</param>
    /// <returns>A value indicating the relative order of the entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Entity other)
    {
        var idComparison = Id.CompareTo(other.Id);
        return idComparison != 0 ? idComparison : Version.CompareTo(other.Version);
    }

    /// <summary>
    /// Returns a string representation of this entity.
    /// </summary>
    /// <returns>A string in the format "Entity(id:version)".</returns>
    public override string ToString() => IsNull ? "Entity(null)" : $"Entity({Id}:{Version})";

    /// <summary>
    /// Determines whether two entities are equal.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the entities are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    /// <summary>
    /// Determines whether two entities are not equal.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the entities are not equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    /// <summary>
    /// Determines whether the first entity is less than the second entity.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the first entity is less than the second; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Entity left, Entity right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether the first entity is greater than the second entity.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the first entity is greater than the second; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Entity left, Entity right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether the first entity is less than or equal to the second entity.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the first entity is less than or equal to the second; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Entity left, Entity right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether the first entity is greater than or equal to the second entity.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns>True if the first entity is greater than or equal to the second; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Entity left, Entity right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Implicitly converts an entity to its packed ulong representation.
    /// </summary>
    /// <param name="entity">The entity to convert.</param>
    /// <returns>The packed entity data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ulong(Entity entity) => entity._packed;

    /// <summary>
    /// Explicitly converts a packed ulong to an entity.
    /// </summary>
    /// <param name="packed">The packed entity data.</param>
    /// <returns>An entity with the unpacked data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Entity(ulong packed) => FromPacked(packed);
}
