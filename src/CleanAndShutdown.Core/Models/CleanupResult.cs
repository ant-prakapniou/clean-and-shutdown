namespace CleanAndShutdown.Core.Models;

/// <summary>
/// Represents the outcome of cleaning a single target directory.
/// </summary>
/// <remarks>
/// One instance is produced per cleanup target per run and aggregated
/// into a <see cref="CleanupSummary"/> by the orchestrator.
/// </remarks>
public sealed record CleanupResult
{
    /// <summary>
    /// Gets the human-readable name of the cleanup target.
    /// </summary>
    /// <remarks>
    /// Used in logs and in the final report. Matches the name declared
    /// on the corresponding <see cref="Abstractions.ICleanupTarget"/>.
    /// </remarks>
    public required string TargetName { get; init; }

    /// <summary>
    /// Gets the fully expanded path of the directory that was processed.
    /// </summary>
    /// <remarks>
    /// Environment variables (for example, <c>%TEMP%</c>) are already
    /// resolved at this point.
    /// </remarks>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the total size of the directory before cleanup, in bytes.
    /// </summary>
    /// <remarks>
    /// Measured recursively, before any deletion. Zero if the directory
    /// did not exist or could not be measured.
    /// </remarks>
    public long SizeBeforeBytes { get; init; }

    /// <summary>
    /// Gets the number of top-level entries (files and directories)
    /// that were successfully deleted.
    /// </summary>
    public int DeletedCount { get; init; }

    /// <summary>
    /// Gets the number of top-level entries that could not be deleted.
    /// </summary>
    /// <remarks>
    /// Typical reasons include the file being locked by another process,
    /// insufficient permissions, or the file disappearing between
    /// enumeration and deletion.
    /// </remarks>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target was skipped because
    /// its size was below the configured threshold.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, no deletion was attempted and
    /// <see cref="SkipReason"/> describes the threshold.
    /// </remarks>
    public bool SkippedByThreshold { get; init; }

    /// <summary>
    /// Gets the reason the target was skipped, if any.
    /// </summary>
    /// <remarks>
    /// Non-null when the directory was not found, when its size was
    /// below the threshold, or when the target failed with an error.
    /// <see langword="null"/> on a normal, successful run.
    /// </remarks>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets the size of the directory before cleanup, in megabytes.
    /// </summary>
    /// <remarks>
    /// Convenience property for display and threshold comparison. Derived
    /// from <see cref="SizeBeforeBytes"/>; no rounding is applied.
    /// </remarks>
    public double SizeBeforeMB => SizeBeforeBytes / 1024.0 / 1024.0;
}