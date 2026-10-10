namespace CleanAndShutdown.Core.Abstractions;

/// <summary>
/// Abstraction over the file system.
/// </summary>
/// <remarks>
/// Exists so that the cleanup logic can be unit tested without touching
/// a real disk. Production code uses <c>PhysicalFileSystem</c>; tests
/// use an in-memory fake.
/// </remarks>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>
    /// <see langword="true"/> if the directory exists;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Enumerates the top-level entries (files and subdirectories)
    /// directly inside the specified directory.
    /// </summary>
    /// <param name="path">The directory to enumerate.</param>
    /// <returns>
    /// A sequence of full paths to the top-level entries. Empty if the
    /// directory does not exist or cannot be read.
    /// </returns>
    /// <remarks>
    /// Non-recursive. Used when deleting the contents of a directory:
    /// each top-level entry is removed individually, and directories are
    /// deleted recursively by <see cref="Delete"/>.
    /// </remarks>
    IEnumerable<string> EnumerateTopLevelEntries(string path);

    /// <summary>
    /// Enumerates all files under the specified directory, recursively.
    /// </summary>
    /// <param name="path">The root directory to walk.</param>
    /// <returns>
    /// A sequence of full paths to every file in the tree. Empty if the
    /// directory does not exist or cannot be read.
    /// </returns>
    /// <remarks>
    /// Used for size calculation. Implementations should tolerate
    /// inaccessible subdirectories and continue walking rather than
    /// aborting the whole enumeration.
    /// </remarks>
    IEnumerable<string> EnumerateFilesRecursive(string path);

    /// <summary>
    /// Gets the size of the specified file, in bytes.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>
    /// The file size in bytes, or <c>0</c> if the file is missing,
    /// locked, or inaccessible for any reason.
    /// </returns>
    /// <remarks>
    /// Never throws. Files can disappear between enumeration and the
    /// size lookup, especially in temporary directories, so callers must
    /// be able to tolerate a zero result.
    /// </remarks>
    long GetFileSize(string path);

    /// <summary>
    /// Determines whether the specified path refers to a directory.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>
    /// <see langword="true"/> if the path exists and is a directory;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Used to decide between recursive directory deletion and plain
    /// file deletion.
    /// </remarks>
    bool IsDirectory(string path);

    /// <summary>
    /// Deletes the specified file or directory.
    /// </summary>
    /// <param name="path">The path to delete.</param>
    /// <remarks>
    /// If <paramref name="path"/> refers to a directory, its entire
    /// contents are removed recursively.
    /// </remarks>
    /// <exception cref="System.IO.IOException">
    /// Thrown when the item is locked or otherwise in use.
    /// </exception>
    /// <exception cref="System.UnauthorizedAccessException">
    /// Thrown when the caller does not have permission to delete
    /// the item.
    /// </exception>
    void Delete(string path);

    /// <summary>
    /// Removes the read-only attribute from the specified file, if set.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <remarks>
    /// Required because <c>File.Delete</c> throws on read-only files.
    /// Implementations should not throw if the attribute cannot be
    /// cleared; the subsequent delete attempt will surface the real
    /// error.
    /// </remarks>
    void ClearReadOnlyAttribute(string path);

    /// <summary>
    /// Expands environment-variable placeholders in the specified path.
    /// </summary>
    /// <param name="path">
    /// A path that may contain placeholders such as <c>%TEMP%</c> or
    /// <c>%SystemRoot%</c>.
    /// </param>
    /// <returns>The path with all placeholders replaced by their values.</returns>
    string ExpandEnvironmentVariables(string path);
}