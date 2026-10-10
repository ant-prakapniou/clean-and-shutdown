namespace CleanAndShutdown.Core.Models;

/// <summary>
/// Configuration for a single cleanup target (one directory).
/// </summary>
/// <remarks>
/// Each entry in <see cref="CleanupOptions.Targets"/> is an instance of
/// this class. The orchestrator matches <see cref="Name"/> against the
/// names of registered <see cref="Abstractions.ICleanupTarget"/>
/// implementations to decide which code actually performs the cleanup.
/// </remarks>
public sealed class CleanupTargetOptions
{
    /// <summary>
    /// Gets or sets the human-readable name of the target.
    /// </summary>
    /// <remarks>
    /// Used in logs and in the final report. Must match the
    /// <see cref="Abstractions.ICleanupTarget.Name"/> of a registered
    /// target; otherwise, the entry is skipped and a warning is logged.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the directory to clean.
    /// </summary>
    /// <remarks>
    /// Environment variables are supported and expanded at runtime,
    /// for example <c>"%SystemRoot%\\Temp"</c> or <c>"%TEMP%"</c>.
    /// </remarks>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum size, in megabytes, that the directory
    /// must reach before it is cleaned.
    /// </summary>
    /// <remarks>
    /// If the measured size is below this threshold, the target is
    /// skipped and no files are deleted. A value of <c>0</c> disables
    /// the threshold check.
    /// </remarks>
    public int MinSizeMB { get; set; } = 100;
}