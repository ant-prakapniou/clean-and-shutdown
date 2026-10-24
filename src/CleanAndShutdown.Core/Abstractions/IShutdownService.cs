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
}