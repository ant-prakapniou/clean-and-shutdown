using CleanAndShutdown.Core.Abstractions;

namespace CleanAndShutdown.Tests.Fakes;

/// <summary>
/// Fake implementation of <see cref="IShutdownService"/> for tests.
/// </summary>
/// <remarks>
/// Never shuts anything down: instead, records the calls and their
/// parameters so that tests can assert on them.
/// </remarks>
public sealed class FakeShutdownService : IShutdownService
{
    /// <summary>
    /// Gets the number of times <see cref="ShutdownNow"/> was called.
    /// </summary>
    public int ShutdownNowCallCount { get; private set; }

    /// <summary>
    /// Gets the reason passed to the most recent shutdown call.
    /// </summary>
    public string? LastShutdownReason { get; private set; }

    /// <summary>
    /// Gets the number of times <see cref="ShutdownAsync"/> was called.
    /// </summary>
    public int ShutdownAsyncCallCount { get; private set; }

    /// <summary>
    /// Gets the delay passed to the most recent
    /// <see cref="ShutdownAsync"/> call.
    /// </summary>
    public TimeSpan? LastShutdownDelay { get; private set; }

    /// <summary>
    /// Gets or sets an exception that shutdown calls should throw,
    /// instead of completing successfully.
    /// </summary>
    /// <remarks>
    /// Used to verify how the orchestrator handles shutdown failures.
    /// </remarks>
    public Exception? ThrowOnShutdown { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether
    /// <see cref="ShutdownAsync"/> should actually wait for the
    /// specified delay.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/> so that tests do not sleep
    /// for the full delay. Set to <see langword="true"/> only when a
    /// test needs to exercise the cancellation path during the wait.
    /// </remarks>
    public bool HonorDelay { get; set; } = false;

    /// <inheritdoc />
    public void ShutdownNow(string? reason = null)
    {
        ShutdownNowCallCount++;
        LastShutdownReason = reason;

        if (ThrowOnShutdown is not null)
            throw ThrowOnShutdown;
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(
        TimeSpan delay,
        string? reason,
        CancellationToken ct)
    {
        ShutdownAsyncCallCount++;
        LastShutdownDelay = delay;
        LastShutdownReason = reason;

        if (HonorDelay)
        {
            // Real wait — only when the test explicitly asked for it.
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        else
        {
            // Instant emulation: honor cancellation but do not sleep.
            ct.ThrowIfCancellationRequested();
        }

        if (ThrowOnShutdown is not null)
            throw ThrowOnShutdown;

        // Mirror the production behavior: after the delay, the
        // shutdown itself is delegated to ShutdownNow.
        ShutdownNow(reason);
    }
}