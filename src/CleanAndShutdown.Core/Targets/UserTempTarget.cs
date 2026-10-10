using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Services;
using Microsoft.Extensions.Logging;

namespace CleanAndShutdown.Core.Targets;

/// <summary>
/// Cleanup target for the current user's temporary directory:
/// <c>%TEMP%</c> (usually <c>%LOCALAPPDATA%\Temp</c>).
/// </summary>
/// <remarks>
/// Inherits all deletion, threshold, and progress logic from
/// <see cref="CleanupTargetBase"/>; only the name and path differ.
/// </remarks>
public sealed class UserTempTarget : CleanupTargetBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserTempTarget"/> class.
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
    public UserTempTarget(
        IFileSystem fs,
        DirectorySizeCalculator sizeCalculator,
        ILogger<UserTempTarget> logger)
        : base(fs, sizeCalculator, logger)
    {
    }

    /// <inheritdoc />
    public override string Name => "User Temp";

    /// <inheritdoc />
    public override string RawPath => @"%TEMP%";
}