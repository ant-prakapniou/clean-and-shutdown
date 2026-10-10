using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Services;
using Microsoft.Extensions.Logging;

namespace CleanAndShutdown.Core.Targets;

/// <summary>
/// Cleanup target for the Windows Update download cache:
/// <c>%SystemRoot%\SoftwareDistribution\Download</c>.
/// </summary>
/// <remarks>
/// The Windows Update service (<c>wuauserv</c>) is intentionally not
/// stopped. Files locked by the service are skipped during the run and
/// will be removed on a subsequent execution once they are released.
/// Inherits all deletion, threshold, and progress logic from
/// <see cref="CleanupTargetBase"/>; only the name and path differ.
/// </remarks>
public sealed class WindowsUpdateCacheTarget : CleanupTargetBase
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WindowsUpdateCacheTarget"/> class.
    /// </summary>
    /// <param name="fs">The file system abstraction. Must not be null.</param>
    /// <param name="sizeCalculator">
    /// The directory size calculator. Must not be null.
    /// </param>
    /// <param name="logger">
    /// The logger for this target. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the arguments is <see langword="null"/>.
    /// </exception>
    public WindowsUpdateCacheTarget(
        IFileSystem fs,
        DirectorySizeCalculator sizeCalculator,
        ILogger<WindowsUpdateCacheTarget> logger)
        : base(fs, sizeCalculator, logger)
    {
    }

    /// <inheritdoc />
    public override string Name => "Windows Update cache";

    /// <inheritdoc />
    public override string RawPath => @"%SystemRoot%\SoftwareDistribution\Download";
}