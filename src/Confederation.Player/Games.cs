namespace Confederation.Player;

/// <summary>A game to play: its folder, the program in it to run, and how fast to run it.</summary>
internal sealed record Game(string Folder, string Program, string Title, int Cycles);

/// <summary>
/// The programs the player knows, and how it finds one from what's given on the command line.
/// </summary>
/// <remarks>
/// The speeds are GOG's: its DOSBox settings run <i>Wing Commander</i> at 4,000 cycles and
/// <i>Wing Commander II</i> at 8,000. A DOSBox "cycle" is an instruction, and its count is per
/// millisecond, so 4,000 cycles is the machine's 4 million instructions a second.
/// </remarks>
internal static class Games
{
    private static readonly (string Program, string Title, int Cycles)[] Known =
    [
        ("WC2.EXE", "Wing Commander II", 8000),
        ("SO1.EXE", "Wing Commander II: Special Operations 1", 8000),
        ("SO2.EXE", "Wing Commander II: Special Operations 2", 8000),
        ("WC.EXE", "Wing Commander", 4000),
        ("SM2.EXE", "Wing Commander: The Secret Missions 2", 4000),
    ];

    /// <summary>
    /// The game at a path: a program file, or a folder holding one the player knows (the main
    /// game, when it holds several), or a folder and a program named apart.
    /// </summary>
    public static Game Find(string path, string? program)
    {
        string folder;
        if (File.Exists(path))
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
            program ??= Path.GetFileName(path);
        }
        else if (Directory.Exists(path))
        {
            folder = Path.GetFullPath(path);
        }
        else
        {
            throw new FileNotFoundException($"no game at {path}");
        }

        string[] files = [.. Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>()];
        bool Has(string name) => files.Any(file => string.Equals(file, name, StringComparison.OrdinalIgnoreCase));

        if (program is null)
        {
            // the main games first, before their mission disks
            program = Known.Select(known => known.Program).FirstOrDefault(Has)
                ?? throw new FileNotFoundException($"no game the player knows in {folder}: name its program, as in WC.EXE");
        }
        else if (!Has(program))
        {
            throw new FileNotFoundException($"no {program} in {folder}");
        }

        var match = Known.FirstOrDefault(known => string.Equals(known.Program, program, StringComparison.OrdinalIgnoreCase));
        return match.Program is null
            ? new Game(folder, program.ToUpperInvariant(), Path.GetFileNameWithoutExtension(program).ToUpperInvariant(), 4000)
            : new Game(folder, match.Program, match.Title, match.Cycles);
    }

    /// <summary>
    /// Where a game's saves go unless told otherwise: a folder of its own under the user's
    /// application data, named for the game's folder, so the mission disks that share a folder
    /// share their saves, as they do on a real disk.
    /// </summary>
    public static string SaveFolder(Game game)
    {
        string name = string.Concat(Path.GetFileName(game.Folder).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "Confederation", "saves", name);
    }
}
