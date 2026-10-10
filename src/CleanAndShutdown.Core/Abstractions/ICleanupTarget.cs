using CleanAndShutdown.Core.Models;

namespace CleanAndShutdown.Core.Abstractions;

/// <summary>
/// Represents a single cleanup target — one directory that the
/// application knows how to clean.
/// </summary>
/// <remarks>
/// Built-in implementations:
/// <list type="bullet">
///   <item><description><c>SystemTempTarget</c> — <c>%SystemRoot%\Temp</c>.</description></item>
///   <item><description><c>UserTempTarget</c> — <c>%TEMP%</c>.</description></item>
///   <item><description><c>WindowsUpdateCacheTarget</c> — <c>%SystemRoot%\SoftwareDistribution\Download</c>.</description></item>
/// </list>
/// The orchestrator resolves a target by matching its <see cref="Name"/>
/// against the <see cref="CleanupTargetOptions.Name"/> of each configured
/// entry.
/// </remarks>
public interface ICleanupTarget
{
    /// <summary>
    /// Gets the human-readable name of the target.
    /// </summary>
    /// <remarks>
    /// Used for logging, reporting, and matching against configuration.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the raw path to the target directory, before expansion.
    /// </summary>
    /// <remarks>
    /// May contain environment-variable placeholders such as
    /// <c>%SystemRoot%</c> or <c>%TEMP%</c>. The path is expanded at
    /// runtime by <see cref="CleanAsync"/>.
    /// </remarks>
    string RawPath { get; }

    /// <summary>
    /// Cleans the target directory, subject to the configured threshold.
    /// </summary>
    /// <param name="options">
    /// The per-target options: resolved path and minimum size threshold.
    /// Must not be null.
    /// </param>
    /// <param name="dryRun">
    /// When <see langword="true"/>, the directory is inspected but no
    /// files are deleted. The returned result still reports what would
    /// have been deleted.
    /// </param>
    /// <param name="progress">
    /// Optional progress sink invoked after each processed entry. The
    /// tuple contains the running count of processed entries and the
    /// total number of top-level entries. May be <see langword="null"/>.
    /// </param>
    /// <param name="ct">
    /// Cancellation token. When cancelled, the operation stops as soon
    /// as possible and throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CleanupResult"/> describing the outcome. If the
    /// directory size is below <see cref="CleanupTargetOptions.MinSizeMB"/>,
    /// the result has <see cref="CleanupResult.SkippedByThreshold"/> set
    /// to <see langword="true"/> and no deletion is performed.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled before or during
    /// the operation.
    /// </exception>
    Task<CleanupResult> CleanAsync(
        CleanupTargetOptions options,
        bool dryRun,
        IProgress<(int Deleted, int Total)>? progress,
        CancellationToken ct);
}