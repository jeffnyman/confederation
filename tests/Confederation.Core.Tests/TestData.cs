namespace Confederation.Core.Tests;

internal static class TestData
{
    /// <summary>
    /// The folder at that path, its case aside, in CONFEDERATION_TESTDATA, or in a _testdata folder
    /// (or _wingc/_testdata) in a folder above the test binary; null when there is none. Tests that
    /// need it pass without checking then.
    /// </summary>
    public static string? Find(string relativePath) =>
        Search("CONFEDERATION_TESTDATA", ["_testdata", "_wingc/_testdata"], relativePath);

    /// <summary>A game's folder, in CONFEDERATION_GAMES or in a _wingc folder above the test binary; null when there is none.</summary>
    public static string? Game(string folder) => Search("CONFEDERATION_GAMES", ["_wingc"], folder);

    private static string? Search(string variable, string[] rootNames, string relativePath)
    {
        var fromEnv = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(fromEnv) && In(fromEnv, relativePath) is { } found)
        {
            return found;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var rootName in rootNames)
            {
                if (In(Path.Combine(dir.FullName, rootName), relativePath) is { } candidate)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? In(string root, string relativePath)
    {
        string current = root;
        foreach (var part in relativePath.Split('/'))
        {
            if (!Directory.Exists(current))
            {
                return null;
            }

            current = Directory.EnumerateDirectories(current).FirstOrDefault(d => string.Equals(Path.GetFileName(d), part, StringComparison.OrdinalIgnoreCase))!;
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }
}
