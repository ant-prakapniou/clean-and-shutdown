using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanAndShutdown.Core.Services;

/// <summary>
/// Coordinates the cleanup process: runs every configured target,
/// aggregates results, and optionally initiates a shutdown.
/// </summary>
/// <remarks>
/// Targets are resolved by name from the injected collection and are
/// processed sequentially in the order they appear in
/// <see cref="CleanupOptions.Targets"/>. A failure in one target is
/// isolated: the remaining targets are still processed, and the failed
/// target is recorded as a skipped result.
/// </remarks>
/// <param name="targets">
/// All registered cleanup targets. Must not be null.
/// </param>
/// <param name="shutdownService">
/// The shutdown service used after cleanup. Must not be null.
/// </param>
/// <param name="options">
/// The cleanup options loaded from configuration. Must not be null.
/// </param>
/// <param name="logger">
/// The logger for diagnostic messages. Must not be null.
/// </param>
/// <exception cref="ArgumentNullException">
/// Thrown when any of the arguments is <see langword="null"/>.
/// </exception>
public sealed class CleanupOrchestrator(
    IEnumerable<ICleanupTarget> targets,
    IShutdownService shutdownService,
    IOptions<CleanupOptions> options,
    ILogger<CleanupOrchestrator> logger)
{
    private readonly IEnumerable<ICleanupTarget> _targets = targets ?? throw new ArgumentNullException(nameof(targets));
    private readonly IShutdownService _shutdownService = shutdownService ?? throw new ArgumentNullException(nameof(shutdownService));
    private readonly CleanupOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<CleanupOrchestrator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Runs the cleanup across all configured targets and, unless the
    /// run is a dry run or shutdown is disabled, shuts down the machine
    /// after the configured countdown.
    /// </summary>
    /// <param name="progress">
    /// Optional progress sink that receives one event per processed
    /// entry, annotated with the target name. May be
    /// <see langword="null"/>, in which case no progress is reported.
    /// </param>
    /// <param name="ct">
    /// Cancellation token that aborts both the cleanup and the shutdown
    /// countdown.
    /// </param>
    /// <returns>
    /// A <see cref="CleanupSummary"/> describing the outcome of the run.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled during the cleanup
    /// or during the shutdown countdown.
    /// </exception>
    public async Task<CleanupSummary> RunAsync(
        IProgress<CleanupProgress>? progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting cleanup (dry-run: {DryRun})", _options.DryRun);

        var results = new List<CleanupResult>(_options.Targets.Count);
        var startTime = DateTime.UtcNow;

        foreach (var targetOptions in _options.Targets)
        {
            ct.ThrowIfCancellationRequested();

            var target = ResolveTarget(targetOptions.Name);
            if (target is null)
            {
                _logger.LogWarning(
                    "No ICleanupTarget registered for '{Name}' — skipping",
                    targetOptions.Name);
                continue;
            }

            _logger.LogInformation("--- {Target} ---", target.Name);

            try
            {
                // Wrap the raw (int, int) tuple reported by the target
                // into a CleanupProgress that carries the target name.
                // A synchronous IProgress implementation is used on
                // purpose: Progress<T> would dispatch callbacks
                // asynchronously, breaking the ordering guarantees that
                // consumers (and tests) rely on.
                var targetProgress = progress is null
                    ? null
                    : new SyncProgress<(int Deleted, int Total)>(p =>
                        progress.Report(new CleanupProgress(
                            targetOptions.Name,
                            p.Deleted,
                            p.Total)));

                var result = await target
                    .CleanAsync(targetOptions, _options.DryRun, targetProgress, ct)
                    .ConfigureAwait(false);

                results.Add(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Cleanup cancelled during '{Target}'", target.Name);
                throw;
            }
            catch (Exception ex)
            {
                // A single failing target must not abort the whole run.
                _logger.LogError(ex, "Target '{Target}' failed", target.Name);

                results.Add(new CleanupResult
                {
                    TargetName = target.Name,
                    Path = target.RawPath,
                    SkipReason = $"Failed: {ex.Message}"
                });
            }
        }

        var duration = DateTime.UtcNow - startTime;
        var summary = BuildSummary(results, duration);

        LogSummary(summary);

        // Shutdown phase.
        if (!_options.ShutdownAfterCleanup)
        {
            _logger.LogInformation("Shutdown disabled in options — exiting without shutdown");
            return summary;
        }

        if (_options.DryRun)
        {
            _logger.LogInformation("Dry-run — skipping shutdown");
            return summary;
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            await _shutdownService
                .ShutdownAsync(
                    TimeSpan.FromSeconds(_options.ShutdownDelaySeconds),
                    reason: "CleanAndShutdown: scheduled cleanup",
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Shutdown cancelled by user");
            throw;
        }

        return summary;
    }

    /// <summary>
    /// Resolves a registered target by its name, case-insensitively.
    /// </summary>
    /// <param name="name">The target name from configuration.</param>
    /// <returns>
    /// The matching target, or <see langword="null"/> if no registered
    /// target has the given name.
    /// </returns>
    private ICleanupTarget? ResolveTarget(string name)
    {
        return _targets.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the aggregated summary from the per-target results.
    /// </summary>
    /// <param name="results">The per-target results, in processing order.</param>
    /// <param name="duration">Wall-clock duration of the cleanup phase.</param>
    /// <returns>A fully populated <see cref="CleanupSummary"/>.</returns>
    private static CleanupSummary BuildSummary(
        IReadOnlyList<CleanupResult> results,
        TimeSpan duration)
    {
        return new CleanupSummary
        {
            Results = results,
            Duration = duration,
            TotalDeleted = results.Sum(r => r.DeletedCount),
            TotalSkipped = results.Sum(r => r.SkippedCount),
            TotalSizeBeforeBytes = results.Sum(r => r.SizeBeforeBytes)
        };
    }

    /// <summary>
    /// Logs a one-line summary of the run at the information level.
    /// </summary>
    /// <param name="summary">The summary to log.</param>
    private void LogSummary(CleanupSummary summary)
    {
        _logger.LogInformation(
            "Cleanup completed: {Deleted} deleted, {Skipped} skipped, {SizeMB:F2} MB before, {Seconds:F2}s",
            summary.TotalDeleted,
            summary.TotalSkipped,
            summary.TotalSizeBeforeBytes / 1024.0 / 1024.0,
            summary.Duration.TotalSeconds);
    }
}