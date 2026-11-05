namespace Confederation.Core.Dos;

/// <summary>
/// Drive C: as a program sees it: the game's folder, never written, with a save folder laid over
/// it. A file opened for writing is first copied into the save folder, and from then on the copy
/// is the file. Names are matched without regard to case, as DOS matches them, so the game's
/// files can be in any case on the host.
/// </summary>
internal sealed class DosDrive(string gameFolder, string saveFolder)
{
    // files a program deleted that exist in the game's folder, which isn't changed
    private readonly HashSet<string> _deleted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The current directory, as DOS names it after "C:\", uppercase; empty at the root.</summary>
    public string CurrentDirectory { get; private set; } = "";

    /// <summary>
    /// A DOS path as its components from the root, uppercase, with "." and ".." resolved. Null
    /// when it names another drive, or climbs above the root.
    /// </summary>
    public string[]? Parse(string path)
    {
        path = path.Trim().Replace('/', '\\').ToUpperInvariant();
        if (path.Length >= 2 && path[1] == ':')
        {
            if (path[0] != 'C')
            {
                return null;
            }

            path = path[2..];
        }

        var parts = new List<string>();
        if (!path.StartsWith('\\') && CurrentDirectory.Length > 0)
        {
            parts.AddRange(CurrentDirectory.Split('\\'));
        }

        foreach (var part in path.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return [.. parts];
    }

    public static string Join(string[] parts) => string.Join('\\', parts);

    /// <summary>The host file a DOS path names, the save folder's copy first; null if there's none.</summary>
    public string? FindFile(string[] parts)
    {
        if (parts.Length == 0)
        {
            return null;
        }

        if (Find(saveFolder, parts) is { } saved && File.Exists(saved))
        {
            return saved;
        }

        if (_deleted.Contains(Join(parts)))
        {
            return null;
        }

        return Find(gameFolder, parts) is { } original && File.Exists(original) ? original : null;
    }

    public bool DirectoryExists(string[] parts) =>
        parts.Length == 0 ||
        Find(saveFolder, parts) is { } saved && Directory.Exists(saved) ||
        Find(gameFolder, parts) is { } original && Directory.Exists(original);

    /// <summary>
    /// The host path to write a DOS path to, in the save folder, creating its directories. With
    /// <paramref name="keep"/>, a file that so far exists only in the game's folder is copied
    /// there first, so writing changes the copy, not the original.
    /// </summary>
    public string ForWriting(string[] parts, bool keep)
    {
        string directory = saveFolder;
        foreach (var part in parts[..^1])
        {
            directory = Find(directory, [part]) ?? Path.Combine(directory, part);
        }

        Directory.CreateDirectory(directory);
        string target = Find(directory, [parts[^1]]) ?? Path.Combine(directory, parts[^1]);

        if (keep && !File.Exists(target) && FindFile(parts) is { } original)
        {
            File.Copy(original, target);
        }

        _deleted.Remove(Join(parts));
        return target;
    }

    public bool Delete(string[] parts)
    {
        if (FindFile(parts) is not { } file)
        {
            return false;
        }

        if (file.StartsWith(saveFolder, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(file);
        }

        if (Find(gameFolder, parts) is { } original && File.Exists(original))
        {
            _deleted.Add(Join(parts));
        }

        return true;
    }

    public bool ChangeDirectory(string[] parts)
    {
        if (!DirectoryExists(parts))
        {
            return false;
        }

        CurrentDirectory = Join(parts);
        return true;
    }

    /// <summary>The entries of a directory, from both folders, the save folder's taking precedence.</summary>
    public IEnumerable<FileSystemInfo> List(string[] directory)
    {
        var seen = new Dictionary<string, FileSystemInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { saveFolder, gameFolder })
        {
            if ((directory.Length == 0 ? root : Find(root, directory)) is not { } host || !Directory.Exists(host))
            {
                continue;
            }

            foreach (var entry in new DirectoryInfo(host).EnumerateFileSystemInfos())
            {
                string name = entry.Name.ToUpperInvariant();
                if (root == gameFolder && _deleted.Contains(Join([.. directory, name])))
                {
                    continue;
                }

                seen.TryAdd(name, entry);
            }
        }

        return seen.Values;
    }

    /// <summary>The host path for DOS components under a root, matching each without regard to case.</summary>
    private static string? Find(string root, string[] parts)
    {
        string current = root;
        foreach (var part in parts)
        {
            if (!Directory.Exists(current))
            {
                return null;
            }

            var match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return null;
            }

            current = match;
        }

        return current;
    }
}
