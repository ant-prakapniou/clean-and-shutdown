using CleanAndShutdown.Core.Models;
using CleanAndShutdown.Core.Services;
using CleanAndShutdown.Tests.Fakes;
using FluentAssertions;

namespace CleanAndShutdown.Tests;

/// <summary>
/// Tests for <see cref="CleanAndShutdown.Core.Targets.CleanupTargetBase"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="TestCleanupTarget"/> as a concrete subclass and
/// <see cref="FakeFileSystem"/> as the in-memory file system.
/// Covers threshold handling, missing directories, empty directories,
/// locked entries, dry runs, environment-variable expansion,
/// cancellation, progress reporting, and size reporting.
/// </remarks>
public sealed class CleanupTargetBaseTests
{
    private const long MB = 1024L * 1024L;

    // ============================================================
    // Directory existence
    // ============================================================

    [Fact]
    public async Task CleanAsync_NonExistentDirectory_ReturnsResultWithSkipReason()
    {
        // Arrange
        var fs = new FakeFileSystem();
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs), path: "C:/Nope");
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Nope", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.TargetName.Should().Be("Test Target");
        result.Path.Should().Be("C:/Nope");
        result.SkipReason.Should().Be("Directory not found");
        result.DeletedCount.Should().Be(0);
        result.SkippedByThreshold.Should().BeFalse();
    }

    // ============================================================
    // Size threshold
    // ============================================================

    [Fact]
    public async Task CleanAsync_BelowThreshold_SkipsWithoutDeleting()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 50 * MB); // 50 MB < 100 MB threshold
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.SkippedByThreshold.Should().BeTrue();
        result.DeletedCount.Should().Be(0);
        result.SkipReason.Should().Contain("100");
        fs.FileExists("C:/Temp/a.tmp").Should().BeTrue(); // file still in place
    }

    [Fact]
    public async Task CleanAsync_ExactlyAtThreshold_Cleans()
    {
        // Arrange: exactly 100 MB with a 100 MB threshold — the
        // comparison is strict less-than, so cleanup proceeds.
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 100 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.SkippedByThreshold.Should().BeFalse();
        result.DeletedCount.Should().Be(1);
        fs.FileExists("C:/Temp/a.tmp").Should().BeFalse();
    }

    [Fact]
    public async Task CleanAsync_AboveThreshold_DeletesAllEntries()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 60 * MB);
        fs.AddFile("C:/Temp/b.tmp", 60 * MB);
        fs.AddFile("C:/Temp/sub/c.tmp", 60 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.DeletedCount.Should().Be(3); // a.tmp, b.tmp, sub
        result.SkippedCount.Should().Be(0);
        fs.FileExists("C:/Temp/a.tmp").Should().BeFalse();
        fs.FileExists("C:/Temp/b.tmp").Should().BeFalse();
        fs.FileExists("C:/Temp/sub/c.tmp").Should().BeFalse();
    }

    // ============================================================
    // Empty directory
    // ============================================================

    [Fact]
    public async Task CleanAsync_EmptyDirectory_ReturnsZeroCounts()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddDirectory("C:/Temp");
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 0 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.DeletedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);
        result.SkippedByThreshold.Should().BeFalse();
        result.SkipReason.Should().BeNull();
    }

    // ============================================================
    // Locked files
    // ============================================================

    [Fact]
    public async Task CleanAsync_LockedFile_SkipsItAndContinues()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/locked.tmp", 60 * MB);
        fs.AddFile("C:/Temp/free.tmp", 60 * MB);
        fs.LockPath("C:/Temp/locked.tmp");
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.DeletedCount.Should().Be(1);  // free.tmp was deleted
        result.SkippedCount.Should().Be(1);  // locked.tmp was skipped
        fs.FileExists("C:/Temp/free.tmp").Should().BeFalse();
        fs.FileExists("C:/Temp/locked.tmp").Should().BeTrue();
    }

    [Fact]
    public async Task CleanAsync_AllLocked_DeletesNothingButDoesNotThrow()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 60 * MB);
        fs.AddFile("C:/Temp/b.tmp", 60 * MB);
        fs.LockPath("C:/Temp/a.tmp");
        fs.LockPath("C:/Temp/b.tmp");
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.DeletedCount.Should().Be(0);
        result.SkippedCount.Should().Be(2);
        fs.FileExists("C:/Temp/a.tmp").Should().BeTrue();
        fs.FileExists("C:/Temp/b.tmp").Should().BeTrue();
    }

    // ============================================================
    // Dry run
    // ============================================================

    [Fact]
    public async Task CleanAsync_DryRun_DoesNotDeleteButCountsAsDeleted()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 60 * MB);
        fs.AddFile("C:/Temp/b.tmp", 60 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: true, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.DeletedCount.Should().Be(2); // counted as "would have been deleted"
        result.SkippedCount.Should().Be(0);
        fs.FileExists("C:/Temp/a.tmp").Should().BeTrue(); // but nothing was actually removed
        fs.FileExists("C:/Temp/b.tmp").Should().BeTrue();
    }

    [Fact]
    public async Task CleanAsync_DryRunBelowThreshold_StillSkipsByThreshold()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 50 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: true, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.SkippedByThreshold.Should().BeTrue();
        result.DeletedCount.Should().Be(0);
    }

    // ============================================================
    // Environment variable expansion
    // ============================================================

    [Fact]
    public async Task CleanAsync_ExpandsEnvironmentVariables()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.SetEnvironmentVariable("TEMP", "C:/Users/test/AppData/Local/Temp");
        fs.AddFile("C:/Users/test/AppData/Local/Temp/a.tmp", 150 * MB);
        var target = new TestCleanupTarget(
            fs,
            new DirectorySizeCalculator(fs),
            path: "%TEMP%");
        var options = new CleanupTargetOptions
        {
            Name = "Test",
            Path = "%TEMP%",
            MinSizeMB = 100
        };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.Path.Should().Be("C:/Users/test/AppData/Local/Temp");
        result.DeletedCount.Should().Be(1);
    }

    // ============================================================
    // Cancellation
    // ============================================================

    [Fact]
    public async Task CleanAsync_CancelledBeforeStart_Throws()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 150 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = () => target.CleanAsync(options, dryRun: false, progress: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ============================================================
    // Progress
    // ============================================================

    [Fact]
    public async Task CleanAsync_ReportsProgress()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 60 * MB);
        fs.AddFile("C:/Temp/b.tmp", 60 * MB);
        fs.AddFile("C:/Temp/c.tmp", 60 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        var reports = new List<(int Deleted, int Total)>();
        var progress = new SyncProgress<(int, int)>(p => reports.Add(p));

        // Act
        await target.CleanAsync(options, dryRun: false, progress: progress, TestContext.Current.CancellationToken);

        // Assert
        reports.Should().HaveCount(3);
        reports.Select(r => r.Total).Should().AllBeEquivalentTo(3);
        reports.Select(r => r.Deleted).Should().BeEquivalentTo([1, 2, 3]);
    }

    // ============================================================
    // Size before cleanup
    // ============================================================

    [Fact]
    public async Task CleanAsync_ReportsSizeBeforeCleanup()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 80 * MB);
        fs.AddFile("C:/Temp/b.tmp", 40 * MB);
        var target = new TestCleanupTarget(fs, new DirectorySizeCalculator(fs));
        var options = new CleanupTargetOptions { Name = "Test", Path = "C:/Temp", MinSizeMB = 100 };

        // Act
        var result = await target.CleanAsync(options, dryRun: false, progress: null, TestContext.Current.CancellationToken);

        // Assert
        result.SizeBeforeBytes.Should().Be(120 * MB);
        result.SizeBeforeMB.Should().BeApproximately(120.0, 0.01);
    }
}