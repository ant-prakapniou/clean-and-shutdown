using System.Diagnostics;
using CleanAndShutdown.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace CleanAndShutdown.Core.Services;

/// <summary>
/// Windows shutdown implementation backed by <c>shutdown.exe</c>.
/// </summary>
/// <remarks>
/// Works uniformly across Windows versions and avoids P/Invoke.
/// The command line is assembled in <see cref="ShutdownNow"/> and
/// executed by <see cref="RunShutdown"/>.
/// </remarks>
public sealed class WindowsShutdownService : IShutdownService
{
    private readonly ILogger<WindowsShutdownService> _logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WindowsShutdownService"/> class.
    /// </summary>
    /// <param name="logger">The logger. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public WindowsShutdownService(ILogger<WindowsShutdownService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Builds the <c>shutdown.exe</c> command line and executes it.
    /// If a reason is supplied, it is passed as the <c>/c</c> comment and
    /// recorded in the Windows event log.
    /// </remarks>
    public void ShutdownNow(string? reason = null)
    {
        // /s       — shut down.
        // /t 0     — no delay.
        // /d p:0:0 — reason code: planned shutdown, "other".
        // /c "..." — comment that ends up in the event log.
        var args = "/s /t 0 /d p:0:0";

        if (!string.IsNullOrWhiteSpace(reason))
        {
            // shutdown.exe limits the comment to 512 characters;
            // 500 leaves a small safety margin.
            var trimmed = reason.Length > 500 ? reason[..500] : reason;
            args += $" /c \"{trimmed}\"";
        }

        _logger.LogInformation("Shutting down now (reason: {Reason})", reason ?? "<none>");
        RunShutdown(args);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Waits for the requested delay before delegating to
    /// <see cref="ShutdownNow"/>. The delay is cancellable: if
    /// <paramref name="ct"/> fires during the wait, no shutdown command
    /// is issued.
    /// </remarks>
    public async Task ShutdownAsync(
        TimeSpan delay,
        string? reason,
        CancellationToken ct)
    {
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be non-negative.");

        _logger.LogInformation(
            "Shutdown scheduled in {Seconds:F1} second(s)",
            delay.TotalSeconds);

        // Wait for the delay. If cancelled, Task.Delay throws and
        // shutdown.exe is never launched.
        await Task.Delay(delay, ct).ConfigureAwait(false);

        // Double-check: ct could have been cancelled between Delay
        // completing and this line. Unlikely but theoretically possible.
        ct.ThrowIfCancellationRequested();

        ShutdownNow(reason);
    }

    /// <summary>
    /// Executes <c>shutdown.exe</c> with the specified arguments and
    /// inspects the exit code.
    /// </summary>
    /// <param name="arguments">The command-line arguments to pass.</param>
    /// <remarks>
    /// The process is given up to five seconds to exit. If it does not
    /// exit in time, the shutdown is assumed to be in progress and the
    /// method returns without error — the OS may already be terminating
    /// the current process.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>shutdown.exe</c> cannot be started, or when it
    /// exits with a non-zero exit code.
    /// </exception>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// Thrown when the process cannot be launched (for example, the
    /// executable is missing or access is denied).
    /// </exception>
    private void RunShutdown(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                throw new InvalidOperationException(
                    "Failed to start shutdown.exe (Process.Start returned null).");
            }

            // shutdown.exe /s /t 0 returns immediately, but we give it
            // a short window just in case. Waiting indefinitely is not
            // an option: once the command is accepted, the OS starts
            // shutting down and may terminate this process.
            if (!process.WaitForExit(milliseconds: 5000))
            {
                _logger.LogWarning(
                    "shutdown.exe did not exit within 5 seconds; assuming shutdown is in progress.");
                return;
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                var stdout = process.StandardOutput.ReadToEnd();
                _logger.LogError(
                    "shutdown.exe failed with exit code {ExitCode}. stderr: {Stderr} stdout: {Stdout}",
                    process.ExitCode, stderr, stdout);

                throw new InvalidOperationException(
                    $"shutdown.exe failed with exit code {process.ExitCode}: {stderr}");
            }

            _logger.LogInformation("shutdown.exe completed successfully.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // The most common cause is missing permissions (unlikely
            // here, given requireAdministrator) or shutdown.exe not
            // being available.
            _logger.LogError(ex, "Failed to launch shutdown.exe.");
            throw;
        }
    }
}