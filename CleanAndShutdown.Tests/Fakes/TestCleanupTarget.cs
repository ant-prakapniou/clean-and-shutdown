using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Services;
using CleanAndShutdown.Core.Targets;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanAndShutdown.Tests.Fakes;

/// <summary>
/// Concrete <see cref="CleanupTargetBase"/> implementation for tests.
/// </summary>
/// <remarks>
/// Exists so that tests of the base class logic do not need to depend
/// on a specific production target. The <see cref="Name"/> and
/// <see cref="RawPath"/> values are supplied by the caller, and logging
/// is discarded via <see cref="NullLogger"/>.
/// </remarks>
public sealed class TestCleanupTarget : CleanupTargetBase
{
    private readonly string _name;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TestCleanupTarget"/> class.
    /// </summary>
    /// <param name="fs">The file system abstraction. Must not be null.</param>
    /// <param name="sizeCalculator">
    /// The directory size calculator. Must not be null.
    /// </param>
    /// <param name="name">
    /// The target name reported through <see cref="Name"/>.
    /// Defaults to <c>"Test Target"</c>.
    /// </param>
    /// <param name="path">
    /// The raw path reported through <see cref="RawPath"/> and used by
    /// the base class for cleanup. Defaults to <c>"C:/Temp"</c>.
    /// </param>
    public TestCleanupTarget(
        IFileSystem fs,
        DirectorySizeCalculator sizeCalculator,
        string name = "Test Target",
        string path = "C:/Temp")
        : base(fs, sizeCalculator, NullLogger.Instance)
    {
        _name = name;
        _path = path;
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override string RawPath => _path;
}