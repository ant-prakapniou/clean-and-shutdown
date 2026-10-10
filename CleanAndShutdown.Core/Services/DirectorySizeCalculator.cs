using CleanAndShutdown.Core.Abstractions;

namespace CleanAndShutdown.Core.Services;

/// <summary>
/// Calculates the total size of a directory tree.
/// </summary>
/// <remarks>
/// Operates through <see cref="IFileSystem"/>, so it is fully testable
/// without touching a real disk.
/// </remarks>
public sealed class DirectorySizeCalculator
{
    private readonly IFileSystem _fs;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DirectorySizeCalculator"/> class.
    /// </summary>
    /// <param name="fs">The file system abstraction. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fs"/> is <see langword="null"/>.
    /// </exception>
    public DirectorySizeCalculator(IFileSystem fs)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
    }

    /// <summary>
    /// Calculates the total size of all files under the specified
    /// directory, recursively.
    /// </summary>
    /// <param name="path">The root directory to measure.</param>
    /// <returns>
    /// The combined size of all files in the tree, in bytes. Returns
    /// <c>0</c> if the directory does not exist.
    /// </returns>
    /// <remarks>
    /// Files that are inaccessible or disappear between enumeration and
    /// the size lookup are counted as zero rather than causing an error.
    /// This matches the behavior of the underlying <see cref="IFileSystem"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>,
    /// empty, or consists only of whitespace.
    /// </exception>
    public long CalculateBytes(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        if (!_fs.DirectoryExists(path))
            return 0;

        long total = 0;
        foreach (var file in _fs.EnumerateFilesRecursive(path))
        {
            total += _fs.GetFileSize(file);
        }

        return total;
    }

    /// <summary>
    /// Calculates the total size of the specified directory, in megabytes.
    /// </summary>
    /// <param name="path">The root directory to measure.</param>
    /// <returns>
    /// The combined size of all files in the tree, in megabytes. Returns
    /// <c>0</c> if the directory does not exist.
    /// </returns>
    /// <remarks>
    /// Convenience wrapper around <see cref="CalculateBytes"/> for
    /// display and threshold comparison. No rounding is applied.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>,
    /// empty, or consists only of whitespace.
    /// </exception>
    public double CalculateMB(string path)
        => CalculateBytes(path) / 1024.0 / 1024.0;
}