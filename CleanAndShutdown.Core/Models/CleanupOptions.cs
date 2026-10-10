namespace CleanAndShutdown.Core.Models;

/// <summary>
/// Configuration options that drive the cleanup process.
/// </summary>
/// <remarks>
/// Instances of this class are populated from the <c>Cleanup</c> section
/// of <c>appsettings.json</c> via the options binding infrastructure, or
/// created programmatically (for example, in unit tests).
/// </remarks>
public sealed class CleanupOptions
{
    /// <summary>
    /// Name of the configuration section that this options class binds to.
    /// </summary>
    /// <remarks>
    /// Exposed as a constant so that the same string is not duplicated
    /// across the DI registration, the JSON file, and the tests.
    /// </remarks>
    public const string SectionName = "Cleanup";

    /// <summary>
    /// Gets or sets the list of cleanup targets to process.
    /// </summary>
    /// <remarks>
    /// The order of items in this list determines the order in which targets
    /// are processed by the orchestrator. Each entry is matched against a
    /// registered <see cref="Abstractions.ICleanupTarget"/> by name.
    /// </remarks>
    public List<CleanupTargetOptions> Targets { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of seconds to wait before shutting down
    /// the machine after cleanup completes.
    /// </summary>
    /// <remarks>
    /// Used as a countdown window that gives the user a chance to cancel
    /// the shutdown with Ctrl+C.
    /// </remarks>
    public int ShutdownDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether the run is a dry run.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the application only reports what
    /// would be deleted without actually removing any files, and it does
    /// not initiate a shutdown.
    /// </remarks>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the machine should be
    /// shut down after the cleanup completes.
    /// </summary>
    /// <remarks>
    /// Set to <see langword="false"/> to perform a real cleanup without
    /// shutting the machine down. Useful for debugging and for scheduled
    /// maintenance runs.
    /// </remarks>
    public bool ShutdownAfterCleanup { get; set; } = true;
}