namespace CleanAndShutdown.Core.Models;

/// <summary>
/// Aggregated outcome of a full cleanup run.
/// </summary>
/// <remarks>
/// Combines per-target results produced by <see cref="CleanupResult"/>
/// with run-level totals. Returned by
/// <see cref="Services.CleanupOrchestrator.RunAsync"/> and consumed by
/// the CLI for the final report.
/// </remarks>
public sealed record CleanupSummary
{
    /// <summary>
    /// Gets the per-target results in the order the targets were processed.
    /// </summary>
    /// <remarks>
    /// Includes entries for targets that were skipped, failed, or had
    /// no work to do. The order matches the order of items in
    /// <see cref="CleanupOptions.Targets"/>.
    /// </remarks>
    public required IReadOnlyList<CleanupResult> Results { get; init; }

    /// <summary>
    /// Gets the wall-clock time spent on the cleanup itself.
    /// </summary>
    /// <remarks>
    /// Does not include the shutdown countdown or the shutdown itself.
    /// </remarks>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the total number of entries deleted across all targets.
    /// </summary>
    /// <remarks>
    /// Sum of <see cref="CleanupResult.DeletedCount"/> over all results.
    /// </remarks>
    public int TotalDeleted { get; init; }

    /// <summary>
    /// Gets the total number of entries skipped across all targets.
    /// </summary>
    /// <remarks>
    /// Sum of <see cref="CleanupResult.SkippedCount"/> over all results.
    /// </remarks>
    public int TotalSkipped { get; init; }

    /// <summary>
    /// Gets the combined size of all targets before cleanup, in bytes.
    /// </summary>
    /// <remarks>
    /// Sum of <see cref="CleanupResult.SizeBeforeBytes"/> over all
    /// results. Targets that were skipped still contribute their
    /// measured size.
    /// </remarks>
    public long TotalSizeBeforeBytes { get; init; }

    /// <summary>
    /// Gets the combined size of all targets before cleanup, in megabytes.
    /// </summary>
    /// <remarks>
    /// Convenience property for display. Derived from
    /// <see cref="TotalSizeBeforeBytes"/>; no rounding is applied.
    /// </remarks>
    public double TotalSizeBeforeMB => TotalSizeBeforeBytes / 1024.0 / 1024.0;
}