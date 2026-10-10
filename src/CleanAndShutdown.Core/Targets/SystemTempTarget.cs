using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Services;
using Microsoft.Extensions.Logging;

namespace CleanAndShutdown.Core.Targets;

/// <summary>
/// Cleanup target for the system temporary directory:
/// <c>%SystemRoot%\Temp</c> (usually <c>C:\Windows\Temp</c>).
/// </summary>
/// <remarks>
/// Inherits all deletion, threshold, and progress logic from
/// <see cref="CleanupTargetBase"/>; only the name and path differ.
/// </remarks>
public sealed class SystemTempTarget : CleanupTargetBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTempTarget"/> class.
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
    public SystemTempTarget(
        IFileSystem fs,
        DirectorySizeCalculator sizeCalculator,
        ILogger<SystemTempTarget> logger)
        : base(fs, sizeCalculator, logger)
    {
    }

    /// <inheritdoc />
    public override string Name => "System Temp";

    /// <inheritdoc />
    public override string RawPath => @"%SystemRoot%\Temp";
}