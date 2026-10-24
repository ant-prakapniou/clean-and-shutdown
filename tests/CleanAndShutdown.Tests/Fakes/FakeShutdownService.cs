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
    /// Gets or sets an exception that shutdown calls should throw,
    /// instead of completing successfully.
    /// </summary>
    /// <remarks>
    /// Used to verify how the orchestrator handles shutdown failures.
    /// </remarks>
    public Exception? ThrowOnShutdown { get; set; }

    /// <inheritdoc />
    public void ShutdownNow(string? reason = null)
    {
        ShutdownNowCallCount++;
        LastShutdownReason = reason;

        if (ThrowOnShutdown is not null)
            throw ThrowOnShutdown;
    }
}