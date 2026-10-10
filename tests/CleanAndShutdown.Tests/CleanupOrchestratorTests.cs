using CleanAndShutdown.Core.Abstractions;
using CleanAndShutdown.Core.Models;
using CleanAndShutdown.Core.Services;
using CleanAndShutdown.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CleanAndShutdown.Tests;

/// <summary>
/// Tests for <see cref="CleanupOrchestrator"/>.
/// </summary>
/// <remarks>
/// Covers ordering, target resolution, failure isolation, shutdown
/// policy, cancellation, result aggregation, and progress reporting.
/// </remarks>
public sealed class CleanupOrchestratorTests
{
    private const long MB = 1024L * 1024L;

    // ============================================================
    // Helpers for building the orchestrator
    // ============================================================

    /// <summary>
    /// Builds an orchestrator with the given options, targets, and
    /// shutdown service, using a no-op logger.
    /// </summary>
    private static CleanupOrchestrator CreateOrchestrator(
        CleanupOptions options,
        IEnumerable<ICleanupTarget> targets,
        IShutdownService shutdown)
    {
        return new CleanupOrchestrator(
            targets,
            shutdown,
            Options.Create(options),
            NullLogger<CleanupOrchestrator>.Instance);
    }

    /// <summary>
    /// Builds a <see cref="CleanupOptions"/> with sensible defaults:
    /// shutdown enabled, delay of 5 seconds, no dry run.
    /// </summary>
    private static CleanupOptions DefaultOptions(params CleanupTargetOptions[] targets)
    {
        return new CleanupOptions
        {
            Targets = targets.ToList(),
            ShutdownDelaySeconds = 5,
            DryRun = false,
            ShutdownAfterCleanup = true
        };
    }

    /// <summary>
    /// Builds a single target entry with the specified name, optional
    /// path and threshold.
    /// </summary>
    private static CleanupTargetOptions TargetOptions(string name, string path = "C:/Temp", int minSizeMB = 100)
        => new() { Name = name, Path = path, MinSizeMB = minSizeMB };

    // ============================================================
    // Target processing order
    // ============================================================

    [Fact]
    public async Task RunAsync_ProcessesTargetsInConfigOrder()
    {
        // Arrange
        var callLog = new List<string>();
        var t1 = new FakeCleanupTarget("A") { CallLog = callLog };
        var t2 = new FakeCleanupTarget("B") { CallLog = callLog };
        var t3 = new FakeCleanupTarget("C") { CallLog = callLog };

        // The config lists the targets in the order B → C → A.
        var options = DefaultOptions(
            TargetOptions("B"),
            TargetOptions("C"),
            TargetOptions("A"));

        var orchestrator = CreateOrchestrator(
            options,
            new[] { t1, t2, t3 },
            new FakeShutdownService());

        // Act
        await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        callLog.Should().ContainInOrder("B", "C", "A");
    }

    // ============================================================
    // Skipping targets that are not registered
    // ============================================================

    [Fact]
    public async Task RunAsync_UnknownTargetName_SkipsIt()
    {
        // Arrange
        var t1 = new FakeCleanupTarget("A");
        var options = DefaultOptions(
            TargetOptions("A"),
            TargetOptions("Unknown")); // no such target

        var orchestrator = CreateOrchestrator(
            options,
            new[] { t1 },
            new FakeShutdownService());

        // Act
        var summary = await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        summary.Results.Should().HaveCount(1); // only "A"
        summary.Results[0].TargetName.Should().Be("A");
    }

    // ============================================================
    // Failure isolation
    // ============================================================

    [Fact]
    public async Task RunAsync_OneTargetFails_OthersStillRun()
    {
        // Arrange
        var callLog = new List<string>();
        var t1 = new FakeCleanupTarget("A") { CallLog = callLog };
        var t2 = new FakeCleanupTarget("B")
        {
            CallLog = callLog,
            ExceptionToThrow = new InvalidOperationException("boom")
        };
        var t3 = new FakeCleanupTarget("C") { CallLog = callLog };

        var options = DefaultOptions(
            TargetOptions("A"),
            TargetOptions("B"),
            TargetOptions("C"));

        var orchestrator = CreateOrchestrator(
            options,
            new[] { t1, t2, t3 },
            new FakeShutdownService());

        // Act
        var summary = await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        callLog.Should().ContainInOrder("A", "B", "C"); // all invoked
        summary.Results.Should().HaveCount(3);

        // "B" is recorded with a SkipReason that mentions the error.
        var b = summary.Results.Single(r => r.TargetName == "B");
        b.SkipReason.Should().Contain("boom");
    }

    // ============================================================
    // Dry run
    // ============================================================

    [Fact]
    public async Task RunAsync_DryRun_DoesNotShutdown()
    {
        // Arrange
        var shutdown = new FakeShutdownService();
        var options = DefaultOptions(TargetOptions("A"));
        options.DryRun = true;

        var orchestrator = CreateOrchestrator(
            options,
            new[] { new FakeCleanupTarget("A") },
            shutdown);

        // Act
        await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        shutdown.ShutdownAsyncCallCount.Should().Be(0);
        shutdown.ShutdownNowCallCount.Should().Be(0);
    }

    // ============================================================
    // ShutdownAfterCleanup = false
    // ============================================================

    [Fact]
    public async Task RunAsync_ShutdownDisabled_DoesNotShutdown()
    {
        // Arrange
        var shutdown = new FakeShutdownService();
        var options = DefaultOptions(TargetOptions("A"));
        options.ShutdownAfterCleanup = false;

        var orchestrator = CreateOrchestrator(
            options,
            new[] { new FakeCleanupTarget("A") },
            shutdown);

        // Act
        await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        shutdown.ShutdownAsyncCallCount.Should().Be(0);
        shutdown.ShutdownNowCallCount.Should().Be(0);
    }

    // ============================================================
    // Shutdown with the configured delay
    // ============================================================

    [Fact]
    public async Task RunAsync_ShutdownEnabled_CallsShutdownWithConfiguredDelay()
    {
        // Arrange
        var shutdown = new FakeShutdownService();
        var options = DefaultOptions(TargetOptions("A"));
        options.ShutdownDelaySeconds = 7;

        var orchestrator = CreateOrchestrator(
            options,
            new[] { new FakeCleanupTarget("A") },
            shutdown);

        // Act
        await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        shutdown.ShutdownAsyncCallCount.Should().Be(1);
        shutdown.LastShutdownDelay.Should().Be(TimeSpan.FromSeconds(7));
        shutdown.LastShutdownReason.Should().Contain("CleanAndShutdown");
        // ShutdownAsync delegates to ShutdownNow after the delay.
        shutdown.ShutdownNowCallCount.Should().Be(1);
    }

    // ============================================================
    // Cancellation
    // ============================================================

    [Fact]
    public async Task RunAsync_CancelledBeforeStart_Throws()
    {
        // Arrange
        var options = DefaultOptions(TargetOptions("A"));
        var orchestrator = CreateOrchestrator(
            options,
            new[] { new FakeCleanupTarget("A") },
            new FakeShutdownService());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = () => orchestrator.RunAsync(progress: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_CancelledDuringShutdownDelay_Throws()
    {
        // Arrange
        var shutdown = new FakeShutdownService { HonorDelay = true };
        var options = DefaultOptions(TargetOptions("A"));
        options.ShutdownDelaySeconds = 10; // long enough to be cancelled

        var orchestrator = CreateOrchestrator(
            options,
            new[] { new FakeCleanupTarget("A") },
            shutdown);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // cancel well before the countdown ends

        // Act
        Func<Task> act = () => orchestrator.RunAsync(progress: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ============================================================
    // Result aggregation
    // ============================================================

    [Fact]
    public async Task RunAsync_AggregatesTotalsCorrectly()
    {
        // Arrange
        var t1 = new FakeCleanupTarget("A")
        {
            ResultToReturn = new CleanupResult
            {
                TargetName = "A",
                Path = "C:/A",
                SizeBeforeBytes = 100 * MB,
                DeletedCount = 10,
                SkippedCount = 2
            }
        };
        var t2 = new FakeCleanupTarget("B")
        {
            ResultToReturn = new CleanupResult
            {
                TargetName = "B",
                Path = "C:/B",
                SizeBeforeBytes = 200 * MB,
                DeletedCount = 20,
                SkippedCount = 3
            }
        };

        var options = DefaultOptions(TargetOptions("A"), TargetOptions("B"));
        options.ShutdownAfterCleanup = false;

        var orchestrator = CreateOrchestrator(
            options,
            new[] { t1, t2 },
            new FakeShutdownService());

        // Act
        var summary = await orchestrator.RunAsync(progress: null, TestContext.Current.CancellationToken);

        // Assert
        summary.TotalDeleted.Should().Be(30);
        summary.TotalSkipped.Should().Be(5);
        summary.TotalSizeBeforeBytes.Should().Be(300 * MB);
        summary.TotalSizeBeforeMB.Should().BeApproximately(300.0, 0.01);
        summary.Results.Should().HaveCount(2);
    }

    // ============================================================
    // Progress
    // ============================================================

    [Fact]
    public async Task RunAsync_ReportsProgressWithTargetName()
    {
        // Arrange
        var fakeProgressTarget = new ProgressReportingTarget("A");
        var options = DefaultOptions(TargetOptions("A"));
        options.ShutdownAfterCleanup = false;

        var orchestrator = CreateOrchestrator(
            options,
            new[] { fakeProgressTarget },
            new FakeShutdownService());

        var reports = new List<CleanupProgress>();
        var progress = new SyncProgress<CleanupProgress>(p => reports.Add(p));

        // Act
        await orchestrator.RunAsync(progress, TestContext.Current.CancellationToken);

        // Assert
        reports.Should().HaveCount(2);
        reports.Should().AllSatisfy(r => r.TargetName.Should().Be("A"));
        reports.Select(r => r.Current).Should().BeEquivalentTo(new[] { 1, 2 });
        reports.Should().AllSatisfy(r => r.Total.Should().Be(2));
    }

    /// <summary>
    /// A target that reports exactly two progress events, used to
    /// verify that the orchestrator annotates progress with the target
    /// name.
    /// </summary>
    private sealed class ProgressReportingTarget : ICleanupTarget
    {
        public ProgressReportingTarget(string name) { Name = name; }
        public string Name { get; }
        public string RawPath => "%PROGRESS%";

        public Task<CleanupResult> CleanAsync(
            CleanupTargetOptions options,
            bool dryRun,
            IProgress<(int Deleted, int Total)>? progress,
            CancellationToken ct)
        {
            progress?.Report((1, 2));
            progress?.Report((2, 2));
            return Task.FromResult(new CleanupResult
            {
                TargetName = Name,
                Path = options.Path
            });
        }
    }

    // ============================================================
    // Constructor guards
    // ============================================================

    [Fact]
    public void Constructor_NullTargets_Throws()
    {
        Action act = () => new CleanupOrchestrator(
            null!,
            new FakeShutdownService(),
            Options.Create(new CleanupOptions()),
            NullLogger<CleanupOrchestrator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullShutdown_Throws()
    {
        Action act = () => new CleanupOrchestrator(
            Array.Empty<ICleanupTarget>(),
            null!,
            Options.Create(new CleanupOptions()),
            NullLogger<CleanupOrchestrator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => new CleanupOrchestrator(
            Array.Empty<ICleanupTarget>(),
            new FakeShutdownService(),
            null!,
            NullLogger<CleanupOrchestrator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }
}