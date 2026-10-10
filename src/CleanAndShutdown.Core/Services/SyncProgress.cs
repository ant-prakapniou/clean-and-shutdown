namespace CleanAndShutdown.Core.Services;

/// <summary>
/// A synchronous implementation of <see cref="IProgress{T}"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="Progress{T}"/>, the callback is invoked directly
/// from <see cref="Report"/> — without capturing a
/// <see cref="SynchronizationContext"/> and without posting to a
/// thread pool queue.
/// </remarks>
/// <typeparam name="T">The type of the progress report value.</typeparam>
public sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncProgress{T}"/> class.
    /// </summary>
    /// <param name="handler">
    /// The callback invoked on every <see cref="Report"/> call. Must not
    /// be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    public SyncProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Invokes the configured callback synchronously on the calling thread.
    /// </summary>
    /// <param name="value">The progress value to report.</param>
    /// <remarks>
    /// Because the callback runs inline, the caller can rely on the
    /// ordering of reports and on the fact that all reports are
    /// delivered before <see cref="Report"/> returns.
    /// </remarks>
    public void Report(T value) => _handler(value);
}