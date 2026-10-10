using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Models;
using CleanAndShutdown.Core.Services;
using Microsoft.Extensions.Logging;

namespace CleanAndShutdown.Core.Targets;

/// <summary>
/// Base class for cleanup targets that operate on a single directory.
/// </summary>
/// <remarks>
/// Concrete targets (System Temp, User Temp, Windows Update cache)
/// inherit from this class and override only <see cref="Name"/> and
/// <see cref="RawPath"/>. All deletion, threshold, and progress logic
/// lives here.
/// </remarks>
public abstract class CleanupTargetBase : ICleanupTarget
{
    private readonly IFileSystem _fs;
    private readonly DirectorySizeCalculator _sizeCalculator;

    /// <summary>
    /// Logger shared with derived classes for target-specific messages.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupTargetBase"/> class.
    /// </summary>
    /// <param name="fs">The file system abstraction. Must not be null.</param>
    /// <param name="sizeCalculator">
    /// The directory size calculator. Must not be null.
    /// </param>
    /// <param name="logger">
    /// The logger used for diagnostic messages. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the arguments is <see langword="null"/>.
    /// </exception>
    protected CleanupTargetBase(
        IFileSystem fs,
        DirectorySizeCalculator sizeCalculator,
        ILogger logger)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string RawPath { get; }

    /// <inheritdoc />
    public Task<CleanupResult> CleanAsync(
        CleanupTargetOptions options,
        bool dryRun,
        IProgress<(int Deleted, int Total)>? progress,
        CancellationToken ct)
    {
        // All work is I/O-bound but the loop itself is synchronous.
        // Wrapping it in Task.Run keeps the async contract honest without
        // blocking the caller's thread.
        return Task.Run(
            () => CleanInternal(options, dryRun, progress, ct),
            ct);
    }

    private CleanupResult CleanInternal(
        CleanupTargetOptions options,
        bool dryRun,
        IProgress<(int Deleted, int Total)>? progress,
        CancellationToken ct)
    {
        var expandedPath = _fs.ExpandEnvironmentVariables(options.Path);

        // 1. Verify that the directory exists.
        if (!_fs.DirectoryExists(expandedPath))
        {
            Logger.LogWarning("{Target}: directory not found: {Path}", Name, expandedPath);
            return new CleanupResult
            {
                TargetName = Name,
                Path = expandedPath,
                SkipReason = "Directory not found"
            };
        }

        // 2. Measure the current size.
        var sizeBytes = _sizeCalculator.CalculateBytes(expandedPath);
        var sizeMB = sizeBytes / 1024.0 / 1024.0;

        Logger.LogInformation(
            "{Target}: size {SizeMB:F2} MB at {Path}",
            Name, sizeMB, expandedPath);

        // 3. Compare against the configured threshold.
        var thresholdBytes = (long)options.MinSizeMB * 1024L * 1024L;
        if (sizeBytes < thresholdBytes)
        {
            Logger.LogInformation(
                "{Target}: below threshold ({ThresholdMB} MB) — skipping",
                Name, options.MinSizeMB);

            return new CleanupResult
            {
                TargetName = Name,
                Path = expandedPath,
                SizeBeforeBytes = sizeBytes,
                SkippedByThreshold = true,
                SkipReason = $"Below threshold ({options.MinSizeMB} MB)"
            };
        }

        // 4. Snapshot the top-level entries.
        var entries = _fs.EnumerateTopLevelEntries(expandedPath).ToList();
        var total = entries.Count;

        if (total == 0)
        {
            Logger.LogInformation("{Target}: no items to delete", Name);
            return new CleanupResult
            {
                TargetName = Name,
                Path = expandedPath,
                SizeBeforeBytes = sizeBytes
            };
        }

        Logger.LogInformation("{Target}: cleaning {Count} item(s)...", Name, total);

        // 5. Delete entries one by one.
        var deleted = 0;
        var skipped = 0;
        var index = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            index++;

            if (dryRun)
            {
                // Dry run: report the entry as "would be deleted", but
                // do not touch the file system.
                deleted++;
                progress?.Report((index, total));
                continue;
            }

            try
            {
                _fs.ClearReadOnlyAttribute(entry);
                _fs.Delete(entry);
                deleted++;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogDebug(ex, "{Target}: access denied: {Entry}", Name, entry);
                skipped++;
            }
            catch (IOException ex)
            {
                Logger.LogDebug(ex, "{Target}: locked or in use: {Entry}", Name, entry);
                skipped++;
            }
            catch (Exception ex)
            {
                // Safety net: a single bad entry must not fail the whole run.
                Logger.LogWarning(ex, "{Target}: failed to delete: {Entry}", Name, entry);
                skipped++;
            }

            progress?.Report((index, total));
        }

        Logger.LogInformation(
            "{Target}: deleted {Deleted}, skipped {Skipped}",
            Name, deleted, skipped);

        return new CleanupResult
        {
            TargetName = Name,
            Path = expandedPath,
            SizeBeforeBytes = sizeBytes,
            DeletedCount = deleted,
            SkippedCount = skipped
        };
    }
}