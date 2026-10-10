namespace CleanAndShutdown.Core.Abstractions;

/// <summary>
/// Abstraction over shutting down the computer.
/// </summary>
/// <remarks>
/// Exists so that tests never shut down the real machine. Production
/// code uses <c>WindowsShutdownService</c>, which invokes
/// <c>shutdown.exe</c>; tests use a fake that records the calls.
/// </remarks>
public interface IShutdownService
{
    /// <summary>
    /// Shuts down the computer immediately.
    /// </summary>
    /// <param name="reason">
    /// Optional human-readable reason for the shutdown. Recorded in the
    /// Windows event log as part of the shutdown reason code. May be
    /// <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// This method does not return in the usual sense: if the shutdown
    /// command succeeds, the operating system terminates the process
    /// shortly afterwards.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the underlying shutdown command fails to start or
    /// returns a non-zero exit code.
    /// </exception>
    void ShutdownNow(string? reason = null);

    /// <summary>
    /// Shuts down the computer after the specified delay.
    /// </summary>
    /// <param name="delay">
    /// The amount of time to wait before initiating the shutdown.
    /// Must be non-negative.
    /// </param>
    /// <param name="reason">
    /// Optional human-readable reason for the shutdown. May be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="ct">
    /// Cancellation token that aborts the countdown. If cancelled
    /// before the delay elapses, no shutdown is performed.
    /// </param>
    /// <returns>A task that completes when the shutdown has been initiated.</returns>
    /// <remarks>
    /// The delay is implemented as an awaitable wait. If
    /// <paramref name="ct"/> is cancelled during the wait, the returned
    /// task transitions to the cancelled state and
    /// <see cref="OperationCanceledException"/> is thrown; the shutdown
    /// command is not invoked in that case.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="delay"/> is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is cancelled before the delay
    /// elapses.
    /// </exception>
    Task ShutdownAsync(
        TimeSpan delay,
        string? reason,
        CancellationToken ct);
}