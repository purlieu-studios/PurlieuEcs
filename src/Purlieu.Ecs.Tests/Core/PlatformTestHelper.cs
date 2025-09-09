using System.Runtime.InteropServices;

namespace Purlieu.Ecs.Tests.Core;

/// <summary>
/// Helper class for platform-specific test adjustments to handle different CI runner performance characteristics.
/// </summary>
public static class PlatformTestHelper
{
    /// <summary>
    /// Returns true if running on macOS (includes both x64 and ARM64).
    /// </summary>
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Returns true if running on Linux.
    /// </summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Returns true if running on Windows.
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Adjusts iteration count for performance tests based on platform performance characteristics.
    /// macOS CI runners (especially ARM64) are consistently 2-3x slower than Ubuntu x86 runners.
    /// </summary>
    /// <param name="baseIterations">Base iteration count for Ubuntu/Windows</param>
    /// <returns>Platform-adjusted iteration count</returns>
    public static int AdjustIterations(int baseIterations)
    {
        return IsMacOS ? (int)(baseIterations * 0.25) : baseIterations;
    }

    /// <summary>
    /// Adjusts entity count for allocation tests based on platform characteristics.
    /// Reduces memory pressure on slower macOS runners to prevent timeouts.
    /// </summary>
    /// <param name="baseEntityCount">Base entity count for Ubuntu/Windows</param>
    /// <returns>Platform-adjusted entity count</returns>
    public static int AdjustEntityCount(int baseEntityCount)
    {
        return IsMacOS ? Math.Max(100, (int)(baseEntityCount * 0.25)) : baseEntityCount;
    }

    /// <summary>
    /// Returns appropriate timeout multiplier for time-sensitive tests.
    /// </summary>
    public static double TimeoutMultiplier => IsMacOS ? 3.0 : 1.0;

    /// <summary>
    /// Returns a descriptive string about the current platform for test output.
    /// </summary>
    public static string PlatformDescription =>
        $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
}
