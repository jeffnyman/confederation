namespace Confederation.Player;

/// <summary>
/// A game to play: its folder, the program in it to run, how fast to run it, and the file its
/// installer chose its devices in.
/// </summary>
internal sealed record Game(string Folder, string Program, string Title, int Cycles, string? Config);

/// <summary>The device the games play their music on.</summary>
internal enum Music
{
    /// <summary>The FM synthesizer of an AdLib or a Sound Blaster: GOG's setting.</summary>
    AdLib,

    /// <summary>A Roland MT-32 (or CM-32L, or LAPC-I), through an MPU-401.</summary>
    Roland,
}

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
    private const string Wc1Config = "WINGCMDR.CFG", Wc2Config = "WC2.CFG";

    private static readonly (string Program, string Title, int Cycles, string Config)[] Known =
    [
        ("WC2.EXE", "Wing Commander II", 8000, Wc2Config),
        ("SO1.EXE", "Wing Commander II: Special Operations 1", 8000, Wc2Config),
        ("SO2.EXE", "Wing Commander II: Special Operations 2", 8000, Wc2Config),
        ("WC.EXE", "Wing Commander", 4000, Wc1Config),
        ("SM2.EXE", "Wing Commander: The Secret Missions 2", 4000, Wc1Config),
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
            ? new Game(folder, program.ToUpperInvariant(), Path.GetFileNameWithoutExtension(program).ToUpperInvariant(), 4000, null)
            : new Game(folder, match.Program, match.Title, match.Cycles, match.Config);
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

    /// <summary>
    /// The music device the game's configuration file names, as the game reads it: from the save
    /// folder's copy if it has one, otherwise the game's own. Null when there's no file, or it
    /// names neither.
    /// </summary>
    /// <remarks>
    /// Both games' installers write one line of words, such as <c>v a904 z</c>: <c>v</c> for VGA,
    /// and a word for the music device, <c>a</c> and some digits for the AdLib, <c>r</c> for the
    /// Roland. That much was found by trying the letters (see docs/the-sound.md); what the digits
    /// and the other words mean isn't known. The last music word counts.
    /// </remarks>
    public static Music? MusicOf(Game game, string saves)
    {
        Music? music = null;
        foreach (string word in ConfigWords(game, saves))
        {
            music = word[0] switch { 'a' or 'A' => Music.AdLib, 'r' or 'R' => Music.Roland, _ => music };
        }

        return music;
    }

    /// <summary>
    /// Makes the game play its music on a device, by writing its configuration file into the
    /// save folder with that device's word, as running its installer again would. The game's own
    /// file isn't changed; the other words are kept.
    /// </summary>
    public static void SetMusic(Game game, string saves, Music music)
    {
        if (game.Config is null)
        {
            throw new InvalidOperationException($"the player doesn't know where {game.Program} keeps its settings");
        }

        var words = ConfigWords(game, saves);
        int at = words.FindIndex(IsMusic);
        string adlib = words.FirstOrDefault(word => word[0] is 'a' or 'A') ?? "a904";
        words.RemoveAll(IsMusic);
        words.Insert(at < 0 ? Math.Min(1, words.Count) : at, music == Music.Roland ? "r" : adlib);

        string path = Path.Combine(saves, Path.GetFileName(FileIn(saves, game.Config) ?? game.Config));
        File.WriteAllText(path, string.Join(' ', words) + "\r\n");

        static bool IsMusic(string word) => word[0] is 'a' or 'A' or 'r' or 'R';
    }

    private static List<string> ConfigWords(Game game, string saves)
    {
        if (game.Config is null || (FileIn(saves, game.Config) ?? FileIn(game.Folder, game.Config)) is not { } file)
        {
            return [];
        }

        // one line, which the installer ends with a zero byte
        string text = File.ReadAllText(file);
        int end = text.IndexOfAny(['\0', '\r', '\n']);
        return [.. (end < 0 ? text : text[..end]).Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    }

    // a file in a folder, whatever its case
    private static string? FileIn(string folder, string name)
        => Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>
    /// Where the MT-32's ROMs are looked for unless told otherwise: a folder under the user's
    /// application data, beside the saves.
    /// </summary>
    public static string Mt32RomFolder()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "Confederation", "mt32-roms");
}
