using CleanAndShutdown.Core.Abstractions;

namespace CleanAndShutdown.Tests.Fakes;

/// <summary>
/// In-memory implementation of <see cref="IFileSystem"/> for tests.
/// </summary>
/// <remarks>
/// Allows tests to define a tree of files and directories with
/// arbitrary sizes, and to mark specific paths as "locked" so that
/// <see cref="Delete"/> throws <see cref="IOException"/> for them —
/// mirroring the behavior of the real file system when a file is in use.
/// </remarks>
public sealed class FakeFileSystem : IFileSystem
{
    // All paths are normalized: forward slashes, no trailing slash.
    private readonly Dictionary<string, long> _files = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _directories = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _lockedPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, string> _envVars = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Gets or sets a value indicating whether
    /// <see cref="ExpandEnvironmentVariables"/> performs substitution.
    /// </summary>
    /// <remarks>
    /// Set to <see langword="false"/> to leave paths untouched, for
    /// example when a test wants to assert on the raw input path.
    /// </remarks>
    public bool ExpandVariables { get; set; } = true;

    // ----- Tree setup (Arrange) -----

    /// <summary>
    /// Adds a directory to the tree.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public FakeFileSystem AddDirectory(string path)
    {
        _directories.Add(Normalize(path));
        return this;
    }

    /// <summary>
    /// Adds a file with the specified size to the tree.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="sizeBytes">The file size, in bytes.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    /// <remarks>
    /// All parent directories are implicitly marked as existing, so
    /// tests do not need to call <see cref="AddDirectory"/> for each
    /// ancestor.
    /// </remarks>
    public FakeFileSystem AddFile(string path, long sizeBytes)
    {
        var normalized = Normalize(path);
        _files[normalized] = sizeBytes;

        // Walk up the tree and register every parent directory.
        var parent = GetParentDirectory(normalized);
        while (parent is not null)
        {
            _directories.Add(parent);
            parent = GetParentDirectory(parent);
        }

        return this;
    }

    /// <summary>
    /// Marks a path as locked, so that <see cref="Delete"/> throws
    /// <see cref="IOException"/> for it.
    /// </summary>
    /// <param name="path">The file or directory path to lock.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public FakeFileSystem LockPath(string path)
    {
        _lockedPaths.Add(Normalize(path));
        return this;
    }

    /// <summary>
    /// Defines an environment variable used by
    /// <see cref="ExpandEnvironmentVariables"/>.
    /// </summary>
    /// <param name="name">The variable name, without percent signs.</param>
    /// <param name="value">The value to substitute.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public FakeFileSystem SetEnvironmentVariable(string name, string value)
    {
        _envVars[name] = value;
        return this;
    }

    // ----- Assertions -----

    /// <summary>
    /// Determines whether a file with the given path exists in the tree.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>
    /// <see langword="true"/> if the file is present; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    /// <summary>
    /// Determines whether a directory with the given path exists in
    /// the tree.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>
    /// <see langword="true"/> if the directory is present; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Named differently from <see cref="DirectoryExists"/> on purpose:
    /// that method is part of <see cref="IFileSystem"/>, whereas this
    /// one is a test-only convenience that makes the intent explicit
    /// in assertions.
    /// </remarks>
    public bool DirectoryExistsInTree(string path) => _directories.Contains(Normalize(path));

    /// <summary>
    /// Gets the size of a file, or <see langword="null"/> if it does
    /// not exist.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file size in bytes, or <see langword="null"/>.</returns>
    public long? GetFileSizeOrNull(string path)
        => _files.TryGetValue(Normalize(path), out var size) ? size : null;

    // ----- IFileSystem implementation -----

    /// <inheritdoc />
    public bool DirectoryExists(string path)
        => _directories.Contains(Normalize(path));

    /// <inheritdoc />
    public IEnumerable<string> EnumerateTopLevelEntries(string path)
    {
        var normalized = Normalize(path);
        if (!_directories.Contains(normalized))
            yield break;

        // Files whose immediate parent is the requested directory.
        foreach (var file in _files.Keys)
        {
            if (string.Equals(GetParentDirectory(file), normalized, StringComparison.OrdinalIgnoreCase))
                yield return file;
        }

        // Subdirectories whose immediate parent is the requested directory.
        foreach (var dir in _directories)
        {
            if (string.Equals(GetParentDirectory(dir), normalized, StringComparison.OrdinalIgnoreCase))
                yield return dir;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFilesRecursive(string path)
    {
        var normalized = Normalize(path);
        if (!_directories.Contains(normalized))
            yield break;

        foreach (var file in _files.Keys)
        {
            if (IsUnder(file, normalized))
                yield return file;
        }
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
        => _files.TryGetValue(Normalize(path), out var size) ? size : 0;

    /// <inheritdoc />
    public bool IsDirectory(string path)
        => _directories.Contains(Normalize(path));

    /// <inheritdoc />
    /// <exception cref="IOException">
    /// Thrown when the path has been marked as locked via
    /// <see cref="LockPath"/>.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the path does not exist in the tree.
    /// </exception>
    public void Delete(string path)
    {
        var normalized = Normalize(path);

        if (_lockedPaths.Contains(normalized))
            throw new IOException($"File is locked: {normalized}");

        if (_directories.Contains(normalized))
        {
            // Recursively remove every file and subdirectory under it.
            var filesToRemove = _files.Keys.Where(f => IsUnder(f, normalized)).ToList();
            var dirsToRemove = _directories.Where(d => IsUnder(d, normalized) || d == normalized).ToList();

            foreach (var f in filesToRemove) _files.Remove(f);
            foreach (var d in dirsToRemove) _directories.Remove(d);
            return;
        }

        if (_files.ContainsKey(normalized))
        {
            _files.Remove(normalized);
            return;
        }

        throw new FileNotFoundException($"Not found: {normalized}");
    }

    /// <inheritdoc />
    public void ClearReadOnlyAttribute(string path)
    {
        // The fake file system has no attributes — this is a no-op.
    }

    /// <inheritdoc />
    public string ExpandEnvironmentVariables(string path)
    {
        if (!ExpandVariables) return path;

        var result = path;
        foreach (var (name, value) in _envVars)
        {
            result = result.Replace($"%{name}%", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    // ----- Helpers -----

    /// <summary>
    /// Normalizes a path to a canonical form used internally:
    /// forward slashes, no trailing slash.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    private static string Normalize(string path)
        => path.Replace('\\', '/').TrimEnd('/');

    /// <summary>
    /// Returns the immediate parent directory of a normalized path,
    /// or <see langword="null"/> if the path has no parent.
    /// </summary>
    /// <param name="normalizedPath">A path already processed by
    /// <see cref="Normalize"/>.</param>
    /// <returns>The parent path, or <see langword="null"/>.</returns>
    private static string? GetParentDirectory(string normalizedPath)
    {
        var idx = normalizedPath.LastIndexOf('/');
        return idx <= 0 ? null : normalizedPath[..idx];
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is equal to
    /// <paramref name="parent"/> or lies inside its subtree.
    /// </summary>
    /// <param name="candidate">The path to test.</param>
    /// <param name="parent">The presumed ancestor path.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="candidate"/> is the
    /// same as or nested under <paramref name="parent"/>.
    /// </returns>
    private static bool IsUnder(string candidate, string parent)
    {
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase))
            return true;

        return candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }
}