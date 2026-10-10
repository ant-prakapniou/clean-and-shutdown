using CleanAndShutdown.Core.Abstractions;

namespace CleanAndShutdown.Core.Services;

/// <summary>
/// Production implementation of <see cref="IFileSystem"/> backed by
/// <see cref="System.IO"/>.
/// </summary>
/// <remarks>
/// This is the only place in the project that touches the real disk
/// directly. All other code goes through the <see cref="IFileSystem"/>
/// abstraction, which keeps the cleanup logic testable.
/// </remarks>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool DirectoryExists(string path)
        => Directory.Exists(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateTopLevelEntries(string path)
    {
        // The result is materialized into a list instead of using
        // yield return, because C# does not allow yield inside a
        // try block that has a catch clause. Top-level entries are
        // few, so the extra allocation is negligible.
        var entries = new List<string>();
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                entries.Add(entry);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }

        return entries;
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFilesRecursive(string path)
    {
        // A manual stack is used instead of
        // Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories):
        // the built-in overload throws as soon as it hits a single
        // inaccessible subdirectory, which is common in temp folders.
        var result = new List<string>();
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            // Files of the current directory first.
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    result.Add(file);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }

            // Then push subdirectories for later processing.
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    stack.Push(dir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
        }

        return result;
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (FileNotFoundException) { return 0; }
        catch (DirectoryNotFoundException) { return 0; }
        catch (IOException) { return 0; }          // locked or already gone
        catch (UnauthorizedAccessException) { return 0; }
    }

    /// <inheritdoc />
    public bool IsDirectory(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return attrs.HasFlag(FileAttributes.Directory);
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <inheritdoc />
    public void Delete(string path)
    {
        if (IsDirectory(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc />
    public void ClearReadOnlyAttribute(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <inheritdoc />
    public string ExpandEnvironmentVariables(string path)
        => Environment.ExpandEnvironmentVariables(path);
}