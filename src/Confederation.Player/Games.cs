namespace Confederation.Player;

/// <summary>
/// A series: a main game and its mission disks, which share a folder, a configuration file
/// (written by the game's installer), a save folder, and a profile.
/// </summary>
/// <param name="Key">Its name in the settings and on the command line: <c>wc1</c> or <c>wc2</c>.</param>
/// <param name="Title">The main game's title.</param>
/// <param name="MainProgram">The main game's program, which identifies the series' folder.</param>
/// <param name="Config">The configuration file its installer writes.</param>
/// <param name="Cycles">Its speed in DOSBox's cycles, as GOG sets it.</param>
internal sealed record Series(string Key, string Title, string MainProgram, string Config, int Cycles);

/// <summary>A program the player knows by a short name, in a series.</summary>
/// <param name="Name">Its short name: <c>wc1</c>, <c>sm2</c>, <c>so1</c> and so on.</param>
/// <param name="Series">The series it's part of.</param>
/// <param name="Program">The program file.</param>
/// <param name="Title">Its title.</param>
/// <param name="Note">Something to know before playing it, if anything.</param>
internal sealed record KnownGame(string Name, Series Series, string Program, string Title, string? Note = null);

/// <summary>
/// A game to play: its folder, the program in it to run, how fast to run it, and the series it's
/// part of (null for a program the player doesn't know).
/// </summary>
internal sealed record Game(string Folder, string Program, string Title, int Cycles, Series? Series)
{
    /// <summary>The file its installer chose its devices in.</summary>
    public string? Config => Series?.Config;
}

/// <summary>The device the games play their music on.</summary>
internal enum Music
{
    /// <summary>The FM synthesizer of an AdLib or a Sound Blaster: GOG's setting.</summary>
    AdLib,

    /// <summary>A Roland MT-32 (or CM-32L, or LAPC-I), through an MPU-401.</summary>
    Roland,
}

/// <summary>
/// The games the player knows, and how it finds one from what's given on the command line: a
/// short name, a folder, or a program.
/// </summary>
/// <remarks>
/// <para>
/// The speeds are GOG's: its DOSBox settings run <i>Wing Commander</i> at 4,000 cycles and
/// <i>Wing Commander II</i> at 8,000. A DOSBox "cycle" is an instruction, and its count is per
/// millisecond, so 4,000 cycles is the machine's 4 million instructions a second.
/// </para>
/// <para>
/// <i>The Secret Missions</i> (the first) has no program of its own: once installed, it's a
/// choice in <i>Wing Commander</i>'s campaign menu, so <c>sm1</c> starts <c>WC.EXE</c>.
/// </para>
/// </remarks>
internal static class Games
{
    public static readonly Series Wc1 = new("wc1", "Wing Commander", "WC.EXE", "WINGCMDR.CFG", 4000);
    public static readonly Series Wc2 = new("wc2", "Wing Commander II", "WC2.EXE", "WC2.CFG", 8000);

    /// <summary>The series, the main game of each first.</summary>
    public static readonly Series[] AllSeries = [Wc1, Wc2];

    /// <summary>Every game the player knows, in the order the menu shows them.</summary>
    public static readonly KnownGame[] Known =
    [
        new("wc1", Wc1, "WC.EXE", "Wing Commander"),
        new("sm1", Wc1, "WC.EXE", "Wing Commander: The Secret Missions", "chosen in Wing Commander's campaign menu"),
        new("sm2", Wc1, "SM2.EXE", "Wing Commander: The Secret Missions 2"),
        new("wc2", Wc2, "WC2.EXE", "Wing Commander II"),
        new("so1", Wc2, "SO1.EXE", "Wing Commander II: Special Operations 1"),
        new("so2", Wc2, "SO2.EXE", "Wing Commander II: Special Operations 2"),
    ];

    /// <summary>The game with a short name, or null.</summary>
    public static KnownGame? Named(string name)
        => Known.FirstOrDefault(game => string.Equals(game.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The series whose main program is in a folder, or null.</summary>
    public static Series? SeriesIn(string folder)
        => AllSeries.FirstOrDefault(series => FileIn(folder, series.MainProgram) is not null);

    /// <summary>A known game in its series' folder.</summary>
    public static Game In(string folder, KnownGame game)
    {
        if (FileIn(folder, game.Program) is null)
        {
            throw new FileNotFoundException($"no {game.Program} in {folder}");
        }

        return new Game(Path.GetFullPath(folder), game.Program, game.Title, game.Series.Cycles, game.Series);
    }

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

        if (program is null)
        {
            // the main games first, before their mission disks
            var series = SeriesIn(folder)
                ?? throw new FileNotFoundException($"no game the player knows in {folder}: name its program, as in WC.EXE");
            program = series.MainProgram;
        }
        else if (FileIn(folder, program) is null)
        {
            throw new FileNotFoundException($"no {program} in {folder}");
        }

        var known = Known.FirstOrDefault(game => string.Equals(game.Program, program, StringComparison.OrdinalIgnoreCase));
        return known is null
            ? new Game(folder, program.ToUpperInvariant(), Path.GetFileNameWithoutExtension(program).ToUpperInvariant(), 4000, null)
            : new Game(folder, known.Program, known.Title, known.Series.Cycles, known.Series);
    }

    /// <summary>
    /// The words on the command line that turn on the cheats Origin left in both games, and in
    /// their mission disks: <c>Origin</c>, with a capital O, and <c>-k</c>. Each sets a flag in
    /// the game's data (observed; see docs/cheats.md). The fans' guides say they make the
    /// player's ship indestructible, and give keys in flight that destroy the target (Alt+Del)
    /// and the enemy wing (Alt+Ins).
    /// </summary>
    public const string CheatWords = "Origin -k";

    /// <summary>The program's command line: the cheats' words, when they're on, before any given.</summary>
    public static string Arguments(bool cheats, string given)
        => !cheats ? given : given.Length == 0 ? CheatWords : $"{CheatWords} {given}";

    /// <summary>
    /// The player's own folder in the user's application data: the settings, the saves and the
    /// MT-32's ROMs go there.
    /// </summary>
    public static string Home
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "Confederation");

    /// <summary>
    /// Where a game's saves go unless told otherwise: a folder of its own under the player's
    /// home, named for the game's folder, so the mission disks that share a folder share their
    /// saves, as they do on a real disk.
    /// </summary>
    public static string SaveFolder(Game game)
    {
        string name = string.Concat(Path.GetFileName(game.Folder).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(Home, "saves", name);
    }

    /// <summary>
    /// Where a game's saved state goes unless told otherwise: a file for each program, in a
    /// folder named as its save folder is, beside the saves. Not in the save folder itself, which
    /// is the game's drive C:, where the game would see it.
    /// </summary>
    public static string StateFile(Game game)
        => Path.Combine(Home, "states", Path.GetFileName(SaveFolder(game)), Path.ChangeExtension(game.Program, ".state"));

    /// <summary>Where the MT-32's ROMs are looked for unless told otherwise: a folder beside the saves.</summary>
    public static string Mt32RomFolder() => Path.Combine(Home, "mt32-roms");

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
}
