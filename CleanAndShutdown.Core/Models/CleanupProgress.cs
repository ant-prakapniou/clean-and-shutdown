namespace CleanAndShutdown.Core.Models;

/// <summary>
/// Represents a single progress update emitted while a cleanup target
/// is being processed.
/// </summary>
/// <param name="TargetName">
/// The human-readable name of the target that produced this update.
/// </param>
/// <param name="Current">
/// The number of items processed so far (including skipped ones).
/// </param>
/// <param name="Total">
/// The total number of top-level items that will be processed.
/// </param>
/// <remarks>
/// Instances are reported through <see cref="System.IProgress{T}"/> by
/// <see cref="Services.CleanupOrchestrator"/>, which wraps the raw
/// <c>(int, int)</c> tuples emitted by individual targets and attaches
/// the target name.
/// </remarks>
public sealed record CleanupProgress(
    string TargetName,
    int Current,
    int Total);