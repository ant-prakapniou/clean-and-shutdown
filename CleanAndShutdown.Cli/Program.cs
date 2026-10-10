using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Models;
using CleanAndShutdown.Core.Services;
using CleanAndShutdown.Core.Targets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace CleanAndShutdown.Cli;

/// <summary>
/// Console entry point for the CleanAndShutdown utility.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>
    /// <c>0</c> on success; <c>1</c> on unhandled failure; <c>2</c> when
    /// not running with administrator privileges; <c>130</c> when the
    /// operation was cancelled by the user (Ctrl+C).
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        // The bootstrap configuration is read before Serilog is
        // configured: we need the "Serilog" section from appsettings.json
        // to capture any errors that occur during host and DI setup.
        var bootstrapConfig = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "CLEANANDSHUTDOWN_")
            .AddCommandLine(args)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(bootstrapConfig)
            .CreateLogger();

        try
        {
            Log.Information("CleanAndShutdown starting");

            var host = BuildHost(args);

            // Verify that the process is elevated. The manifest declares
            // requireAdministrator, so this should always pass when the
            // app is launched normally. The check is a safety net for
            // `dotnet run` and for cases where the manifest is not applied.
            if (!IsAdministrator())
            {
                Log.Error("This application must be run as Administrator.");
                return 2;
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                // Cancel the operation but do not kill the process
                // immediately: give the orchestrator a chance to finish
                // the current step cleanly.
                e.Cancel = true;
                Log.Warning("Cancellation requested (Ctrl+C). Finishing current step...");
                cts.Cancel();
            };

            var orchestrator = host.Services.GetRequiredService<CleanupOrchestrator>();
            var options = host.Services.GetRequiredService<IOptions<CleanupOptions>>().Value;

            PrintBanner(options);

            var progress = new Progress<CleanupProgress>(OnProgress);

            var summary = await orchestrator.RunAsync(progress, cts.Token);

            PrintSummary(summary);

            return 0;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Operation cancelled.");
            Console.WriteLine();
            Console.WriteLine("Cancelled. No shutdown performed.");
            return 130; // conventional exit code for SIGINT
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            Console.WriteLine();
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Required: flushes Serilog's buffers to disk.
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Builds and configures the generic host: application configuration,
    /// dependency injection, and logging integration.
    /// </summary>
    /// <param name="args">Command-line arguments, forwarded to the host.</param>
    /// <returns>A fully configured, unstarted <see cref="IHost"/>.</returns>
    private static IHost BuildHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            // .UseSerilog() without arguments reuses the already
            // configured Log.Logger. The configuration was read in
            // Main, so there is no need to read it again here.
            .UseSerilog()
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                cfg.AddEnvironmentVariables(prefix: "CLEANANDSHUTDOWN_");
                cfg.AddCommandLine(args);
            })
            .ConfigureServices((ctx, services) =>
            {
                // Options — bound from the "Cleanup" section.
                services.Configure<CleanupOptions>(
                    ctx.Configuration.GetSection(CleanupOptions.SectionName));

                // Infrastructure.
                services.AddSingleton<IFileSystem, PhysicalFileSystem>();
                services.AddSingleton<DirectorySizeCalculator>();
                services.AddSingleton<IShutdownService, WindowsShutdownService>();

                // Targets — all three are registered as ICleanupTarget.
                // The orchestrator receives IEnumerable<ICleanupTarget>;
                // which of them actually run is decided by the config.
                services.AddSingleton<ICleanupTarget, SystemTempTarget>();
                services.AddSingleton<ICleanupTarget, UserTempTarget>();
                services.AddSingleton<ICleanupTarget, WindowsUpdateCacheTarget>();

                // Orchestrator.
                services.AddSingleton<CleanupOrchestrator>();
            })
            .Build();
    }

    /// <summary>
    /// Determines whether the current process is running with
    /// administrator privileges.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the current user is a member of the
    /// local Administrators group; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Writes the startup banner to standard output.
    /// </summary>
    /// <param name="options">The resolved cleanup options.</param>
    private static void PrintBanner(CleanupOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine("  WINDOWS TEMP CLEANUP + SHUTDOWN");
        Console.WriteLine($"  Mode: {(options.DryRun ? "DRY-RUN (no changes)" : "LIVE")}");
        Console.WriteLine($"  Targets: {options.Targets.Count}");
        Console.WriteLine("============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Progress callback invoked once per processed entry.
    /// </summary>
    /// <param name="p">The progress event.</param>
    /// <remarks>
    /// The current line is overwritten using a carriage return so that
    /// the counter updates in place. When a target has no entries
    /// (<see cref="CleanupProgress.Total"/> is zero), nothing is printed.
    /// </remarks>
    private static void OnProgress(CleanupProgress p)
    {
        if (p.Total <= 0) return;

        var percent = (int)Math.Floor((double)p.Current / p.Total * 100);
        Console.Write($"\r  {p.TargetName}: {percent,3}% ({p.Current}/{p.Total})");

        if (p.Current >= p.Total)
        {
            // New line after the target finishes, so the next target's
            // progress does not overwrite it.
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Writes the final cleanup report to standard output.
    /// </summary>
    /// <param name="summary">The aggregated cleanup summary.</param>
    private static void PrintSummary(CleanupSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine("  CLEANUP COMPLETED");
        Console.WriteLine("============================================================");

        foreach (var r in summary.Results)
        {
            if (r.SkippedByThreshold)
            {
                Console.WriteLine(
                    $"  {r.TargetName,-22} SKIPPED ({r.SizeBeforeMB:F2} MB, below threshold)");
            }
            else if (r.SkipReason is not null)
            {
                Console.WriteLine(
                    $"  {r.TargetName,-22} SKIPPED ({r.SkipReason})");
            }
            else
            {
                Console.WriteLine(
                    $"  {r.TargetName,-22} deleted {r.DeletedCount}, skipped {r.SkippedCount} ({r.SizeBeforeMB:F2} MB before)");
            }
        }

        Console.WriteLine("------------------------------------------------------------");
        Console.WriteLine($"  Total deleted:   {summary.TotalDeleted}");
        Console.WriteLine($"  Total skipped:   {summary.TotalSkipped}");
        Console.WriteLine($"  Total before:    {summary.TotalSizeBeforeMB:F2} MB");
        Console.WriteLine($"  Duration:        {summary.Duration.TotalSeconds:F2} s");
        Console.WriteLine("============================================================");
        Console.WriteLine();
    }
}