using CleanAndShutdown.Core.Services;
using CleanAndShutdown.Tests.Fakes;
using FluentAssertions;

namespace CleanAndShutdown.Tests;

/// <summary>
/// Tests for <see cref="DirectorySizeCalculator"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="FakeFileSystem"/> as the in-memory file system.
/// Covers empty and missing directories, recursive summation,
/// isolation from unrelated directories, zero-size files, large trees,
/// unit conversion, and argument validation.
/// </remarks>
public sealed class DirectorySizeCalculatorTests
{
    private const long MB = 1024L * 1024L;

    [Fact]
    public void CalculateBytes_EmptyDirectory_ReturnsZero()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddDirectory("C:/Temp");
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public void CalculateBytes_NonExistentDirectory_ReturnsZero()
    {
        // Arrange
        var fs = new FakeFileSystem();
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/DoesNotExist");

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public void CalculateBytes_SingleFile_ReturnsFileSize()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 500);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(500);
    }

    [Fact]
    public void CalculateBytes_MultipleFilesInSameDirectory_SumsSizes()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 100);
        fs.AddFile("C:/Temp/b.tmp", 200);
        fs.AddFile("C:/Temp/c.tmp", 300);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(600);
    }

    [Fact]
    public void CalculateBytes_NestedDirectories_SumsRecursively()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 100);
        fs.AddFile("C:/Temp/sub/b.tmp", 200);
        fs.AddFile("C:/Temp/sub/deep/c.tmp", 300);
        fs.AddFile("C:/Temp/other/d.tmp", 400);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(1000);
    }

    [Fact]
    public void CalculateBytes_DoesNotCountFilesOutsideDirectory()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 100);
        fs.AddFile("C:/Other/b.tmp", 9999);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(100);
    }

    [Fact]
    public void CalculateBytes_IgnoresFilesWithZeroSize()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/empty.tmp", 0);
        fs.AddFile("C:/Temp/full.tmp", 500);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(500);
    }

    [Fact]
    public void CalculateBytes_LargeTree_NoOverflow()
    {
        // Arrange: 2000 files of 1 MB each (~2 GB total).
        // Verifies that the long accumulator does not overflow and
        // that the sum is correct.
        var fs = new FakeFileSystem();
        for (int i = 0; i < 2000; i++)
        {
            fs.AddFile($"C:/Temp/file{i}.tmp", MB);
        }
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var size = calc.CalculateBytes("C:/Temp");

        // Assert
        size.Should().Be(2000L * MB);
    }

    [Fact]
    public void CalculateMB_ConvertsBytesToMegabytes()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/Temp/a.tmp", 100 * MB);
        var calc = new DirectorySizeCalculator(fs);

        // Act
        var mb = calc.CalculateMB("C:/Temp");

        // Assert
        mb.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void CalculateBytes_EmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var fs = new FakeFileSystem();
        var calc = new DirectorySizeCalculator(fs);

        // Act
        Action act = () => calc.CalculateBytes("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("path");
    }

    [Fact]
    public void CalculateBytes_WhitespacePath_ThrowsArgumentException()
    {
        // Arrange
        var fs = new FakeFileSystem();
        var calc = new DirectorySizeCalculator(fs);

        // Act
        Action act = () => calc.CalculateBytes("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullFileSystem_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new DirectorySizeCalculator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fs");
    }
}