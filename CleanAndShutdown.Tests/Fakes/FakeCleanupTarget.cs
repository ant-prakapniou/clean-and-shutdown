using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Models;

namespace CleanAndShutdown.Tests.Fakes;

/// <summary>
/// A controllable <see cref="ICleanupTarget"/> for orchestrator tests.
/// </summary>
/// <remarks>
/// Supports three behaviors that the real targets do not need to model
/// in unit tests: returning a predefined result, throwing a specified
/// exception, and logging the order of invocation across multiple
/// targets.
/// </remarks>
public sealed class FakeCleanupTarget : ICleanupTarget
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FakeCleanupTarget"/> class.
    /// </summary>
    /// <param name="name">The target name.</param>
    public FakeCleanupTarget(string name)
    {
        Name = name;
        RawPath = $"%{name.Replace(" ", "")}%";
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string RawPath { get; }

    /// <summary>
    /// Gets the shared log that records the names of targets in the
    /// order their <see cref="CleanAsync"/> method was invoked.
    /// </summary>
    /// <remarks>
    /// Tests wire the same <see cref="List{T}"/> into multiple fakes to
    /// assert the order in which the orchestrator visits them.
    /// </remarks>
    public List<string> CallLog { get; init; } = new();

    /// <summary>
    /// Gets or sets the result returned from <see cref="CleanAsync"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, a minimal result is synthesized
    /// using the current target's name and the path from the options.
    /// </remarks>
    public CleanupResult? ResultToReturn { get; set; }

    /// <summary>
    /// Gets or sets an exception that <see cref="CleanAsync"/> should
    /// throw instead of returning a result.
    /// </summary>
    /// <remarks>
    /// Used to verify that the orchestrator isolates a failing target
    /// and continues with the remaining ones.
    /// </remarks>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="CleanAsync"/>
    /// should wait before returning, giving the cancellation token a
    /// chance to fire.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, the method returns immediately.
    /// The wait is short (500 ms) and only exists to exercise the
    /// cancellation path.
    /// </remarks>
    public bool HonorCancellation { get; set; } = false;

    /// <inheritdoc />
    public async Task<CleanupResult> CleanAsync(
        CleanupTargetOptions options,
        bool dryRun,
        IProgress<(int Deleted, int Total)>? progress,
        CancellationToken ct)
    {
        CallLog.Add(Name);

        if (HonorCancellation)
        {
            // Give the cancellation token a chance to fire before
            // returning a result.
            await Task.Delay(500, ct).ConfigureAwait(false);
        }
        else
        {
            ct.ThrowIfCancellationRequested();
        }

        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        return ResultToReturn ?? new CleanupResult
        {
            TargetName = Name,
            Path = options.Path
        };
    }
}