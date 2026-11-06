namespace Confederation.Player;

/// <summary>
/// A series: a main game and its mission disks, which share a folder, a configuration file
/// (written by the game's installer), a save folder, and a profile.
/// </summary>
/// <param name="Key">Its name in the settings and on the command line: <c>wc1</c>, <c>wc2</c> or <c>wc3</c>.</param>
/// <param name="Title">The main game's title.</param>
/// <param name="MainPrograms">
/// The main game's program, which identifies the series' folder, unless it's on the CD: by its
/// name here, in the repository's <c>games</c>, and by GOG's.
/// </param>
/// <param name="Config">
/// The configuration file its installer writes, as one line of words the player can change the
/// music in; null when it's not that kind of file.
/// </param>
/// <param name="Cycles">Its speed in DOSBox's cycles, as GOG sets it.</param>
/// <param name="Cd">The CD image a game played from its CD comes as, which identifies its folder.</param>
internal sealed record Series(string Key, string Title, string[] MainPrograms, string? Config, int Cycles, Disc? Cd = null);

/// <summary>A CD, as an image in the game's folder, recognized by its volume's name.</summary>
/// <param name="Image">The image's file, as GOG names it.</param>
/// <param name="Label">The volume's name, in the image's primary volume descriptor.</param>
internal sealed record Disc(string Image, string Label);

/// <summary>A program the player knows by a short name, in a series.</summary>
/// <param name="Name">Its short name: <c>wc1</c>, <c>sm2</c>, <c>so1</c> and so on.</param>
/// <param name="Series">The series it's part of.</param>
/// <param name="Program">The program file, as it's named in the repository's <c>games</c>.</param>
/// <param name="Title">Its title.</param>
/// <param name="Note">Something to know before playing it, if anything.</param>
/// <param name="Original">The program's name as GOG installs it, when that's another.</param>
internal sealed record KnownGame(string Name, Series Series, string Program, string Title, string? Note = null, string? Original = null)
{
    /// <summary>The names its program goes by: the repository's, then GOG's.</summary>
    public string[] Programs => Original is null ? [Program] : [Program, Original];
}

/// <summary>
/// W.C.A.T., AllTinker's overhaul of a series: a launcher of its own, beside a directory it
/// expects the game's data in, which it installs its fixes over and starts the game from, by
/// batch files.
/// </summary>
/// <param name="Title">Its title.</param>
/// <param name="Series">The series it overhauls, whose data it needs.</param>
/// <param name="GameDirectory">The directory beside its launcher the game's data goes in.</param>
internal sealed record Overhaul(string Title, Series Series, string GameDirectory);

/// <summary>
/// A game to play: its folder, the program in it to run, how fast to run it, and the series it's
/// part of (null for a program the player doesn't know, and for W.C.A.T., which is played
/// through its own launcher and its own settings rather than the series'). A game with a CD
/// image has it as drive D:, and its program is run from there.
/// </summary>
internal sealed record Game(string Folder, string Program, string Title, int Cycles, Series? Series, Overhaul? Overhaul = null, string? Cd = null)
{
    /// <summary>The file its installer chose its devices in.</summary>
    public string? Config => Series?.Config;

    /// <summary>The program as DOS is given it: from the CD, when there is one.</summary>
    public string Start => Cd is null ? Program : @"D:\" + Program;
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
/// The repository has <i>Wing Commander</i> and <i>Wing Commander II</i>, with their mission
/// disks, in <c>games</c>, by Electronic Arts' permission, with each program renamed as the
/// permission asks: <c>WC.EXE</c> is <c>CONFWC1.EXE</c> there, <c>WC2.EXE</c> is
/// <c>CONFWC2.EXE</c>, and so on. The player knows both names, so a GOG folder works as well.
/// The build copies <c>games</c> beside the player, and a series that isn't set up is played from
/// there.
/// </para>
/// <para>
/// <i>The Secret Missions</i> (the first) has no program of its own: once installed, it's a
/// choice in <i>Wing Commander</i>'s campaign menu, so <c>sm1</c> starts the main program.
/// </para>
/// <para>
/// <i>Wing Commander III</i> runs from its CD, as GOG sets it up: <c>DATA.DAT</c>, the four CDs in
/// one image, as drive D:, and <c>WC3.EXE</c> from there. Its folder holds no program, so it's
/// known by the image, whose volume is named <c>WC3_CD1</c>. GOG runs it at DOSBox's
/// <c>cycles=max</c>, as fast as the host goes. Here it's 30,000: the machine manages about 65
/// million steps a second in it (observed), so that leaves room for the window and the sound.
/// Its movies keep time by their sound, so they play in step at 20,000 as at 40,000, and a
/// simulator mission flies well at 30,000 (observed). Its installer's
/// <c>WC3.CFG</c> is binary, so the player leaves its music alone.
/// </para>
/// </remarks>
internal static class Games
{
    public static readonly Series Wc1 = new("wc1", "Wing Commander", ["CONFWC1.EXE", "WC.EXE"], "WINGCMDR.CFG", 4000);
    public static readonly Series Wc2 = new("wc2", "Wing Commander II", ["CONFWC2.EXE", "WC2.EXE"], "WC2.CFG", 8000);
    public static readonly Series Wc3 = new("wc3", "Wing Commander III", ["WC3.EXE"], null, 30000, new Disc("DATA.DAT", "WC3_CD1"));

    /// <summary>The series, the main game of each first.</summary>
    public static readonly Series[] AllSeries = [Wc1, Wc2, Wc3];

    /// <summary>Every game the player knows, in the order the menu shows them.</summary>
    public static readonly KnownGame[] Known =
    [
        new("wc1", Wc1, "CONFWC1.EXE", "Wing Commander", Original: "WC.EXE"),
        new("sm1", Wc1, "CONFWC1.EXE", "Wing Commander: The Secret Missions", "chosen in Wing Commander's campaign menu", "WC.EXE"),
        new("sm2", Wc1, "CONFSM2.EXE", "Wing Commander: The Secret Missions 2", Original: "SM2.EXE"),
        new("wc2", Wc2, "CONFWC2.EXE", "Wing Commander II", Original: "WC2.EXE"),
        new("so1", Wc2, "CONFSO1.EXE", "Wing Commander II: Special Operations 1", Original: "SO1.EXE"),
        new("so2", Wc2, "CONFSO2.EXE", "Wing Commander II: Special Operations 2", Original: "SO2.EXE"),
        new("wc3", Wc3, "WC3.EXE", "Wing Commander III: Heart of the Tiger", "new here: it plays to its first mission, and further is untried"),
    ];

    /// <summary>W.C.A.T. for each series, found by the directory its launcher expects the game in.</summary>
    public static readonly Overhaul[] Overhauls =
    [
        new("Wing Commander: AllTinker Overhaul", Wc1, "GAMEWC1"),
        new("Wing Commander II: AllTinker Overhaul", Wc2, "GAMEWC2"),
    ];

    /// <summary>
    /// W.C.A.T.'s speed, as its own DOSBox settings set it: 22,000 cycles, "a modest default so
    /// audio wouldn't skip on lower-end PCs". It limits the game's speed itself, by the display's
    /// vertical blank, so more does no harm.
    /// </summary>
    public const int OverhaulCycles = 22000;

    /// <summary>
    /// The batch file W.C.A.T.'s own DOSBox setup runs: its launcher, and then whatever it was
    /// told to start. Its others (<c>WC.BAT</c>, <c>SM2.BAT</c>, <c>WC2.BAT</c> and so on) start a
    /// game straight away once the launcher has been set up.
    /// </summary>
    public const string OverhaulProgram = "WCAT.BAT";

    /// <summary>The game with a short name, or null.</summary>
    public static KnownGame? Named(string name)
        => Known.FirstOrDefault(game => string.Equals(game.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The series whose main program, or CD image, is in a folder, or null.</summary>
    public static Series? SeriesIn(string folder)
        => AllSeries.FirstOrDefault(series => series.Cd is { } cd ? ImageIn(folder, cd) is not null : MainProgramIn(folder, series) is not null);

    /// <summary>Whether a known game is in a folder: its program, by either name, or its series' CD image.</summary>
    public static bool IsIn(string folder, KnownGame game)
        => game.Series.Cd is { } cd ? ImageIn(folder, cd) is not null : ProgramIn(folder, game) is not null;

    /// <summary>
    /// The folder a series is played from: where it was set up, or else the copy the build put
    /// beside the player, for the games the repository has. Null when there's neither.
    /// </summary>
    public static string? FolderOf(Settings settings, Series series) => settings.FolderOf(series) ?? Bundled(series);

    /// <summary>The repository's copy of a series, beside the player (<c>games/wc1</c> and <c>games/wc2</c>), or null.</summary>
    public static string? Bundled(Series series)
        => DirectoryIn(Path.Combine(AppContext.BaseDirectory, "games"), series.Key) is { } folder && SeriesIn(folder) == series ? folder : null;

    // the name a known game's program goes by in a folder, the repository's or GOG's
    private static string? ProgramIn(string folder, KnownGame game)
        => game.Programs.FirstOrDefault(program => FileIn(folder, program) is not null);

    // the name a series' main program goes by in a folder
    private static string? MainProgramIn(string folder, Series series)
        => series.MainPrograms.FirstOrDefault(program => FileIn(folder, program) is not null);

    /// <summary>A known game in its series' folder.</summary>
    public static Game In(string folder, KnownGame game)
    {
        if (!IsIn(folder, game))
        {
            throw new FileNotFoundException(game.Series.Cd is { } cd ? $"no {cd.Image} with the CD {cd.Label} in {folder}" : $"no {string.Join(" or ", game.Programs)} in {folder}");
        }

        return Make(Path.GetFullPath(folder), game);
    }

    // a known game, from its CD when its series has one
    private static Game Make(string folder, KnownGame game)
        => new(folder, ProgramIn(folder, game) ?? game.Program, game.Title, game.Series.Cycles, game.Series, Cd: game.Series.Cd is { } cd ? ImageIn(folder, cd) : null);

    /// <summary>
    /// The CD image in a folder, if it's there and is the right CD: its volume's name is in the
    /// primary volume descriptor, sector 16, at byte 40, 32 characters padded with spaces.
    /// </summary>
    private static string? ImageIn(string folder, Disc cd)
    {
        if (FileIn(folder, cd.Image) is not { } image)
        {
            return null;
        }

        var descriptor = new byte[72];
        using (var stream = File.OpenRead(image))
        {
            stream.Position = 16 * 2048;
            stream.ReadAtLeast(descriptor, descriptor.Length, throwOnEndOfStream: false);
        }

        bool primary = descriptor[0] == 1 && "CD001"u8.SequenceEqual(descriptor.AsSpan(1, 5));
        return primary && System.Text.Encoding.ASCII.GetString(descriptor, 40, 32).TrimEnd() == cd.Label ? Path.GetFullPath(image) : null;
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

            // a CD image stands for its game
            if (SeriesIn(folder) is not { Cd: { } cd } || !string.Equals(Path.GetFileName(path), cd.Image, StringComparison.OrdinalIgnoreCase))
            {
                program ??= Path.GetFileName(path);
            }
        }
        else if (Directory.Exists(path))
        {
            folder = Path.GetFullPath(path);
        }
        else
        {
            throw new FileNotFoundException($"no game at {path}");
        }

        if (OverhaulIn(folder) is var (files, overhaul))
        {
            program ??= OverhaulProgram;
            return FileIn(files, program) is null
                ? throw new FileNotFoundException($"no {program} in {files}")
                : new Game(files, program.ToUpperInvariant(), overhaul.Title, OverhaulCycles, null, overhaul);
        }

        if (program is null)
        {
            // the main games first, before their mission disks
            var series = SeriesIn(folder)
                ?? throw new FileNotFoundException($"no game the player knows in {folder}: name its program, as in WC.EXE");
            program = MainProgramIn(folder, series) ?? series.MainPrograms[0];
        }
        else if (FileIn(folder, program) is null)
        {
            throw new FileNotFoundException($"no {program} in {folder}");
        }

        var known = Known.FirstOrDefault(game => game.Programs.Contains(program, StringComparer.OrdinalIgnoreCase) && IsIn(folder, game));
        return known is null
            ? new Game(folder, program.ToUpperInvariant(), Path.GetFileNameWithoutExtension(program).ToUpperInvariant(), 4000, null)
            : Make(folder, known);
    }

    /// <summary>
    /// W.C.A.T. in a folder: the folder its batch files are in (its <c>files</c>, which W.C.A.T.'s
    /// DOSBox setup mounts as C:, or the folder itself), and which series it overhauls. Null
    /// when there's no W.C.A.T. there.
    /// </summary>
    public static (string Files, Overhaul Overhaul)? OverhaulIn(string folder)
    {
        foreach (string files in new[] { folder, Path.Combine(folder, "files") })
        {
            if (!Directory.Exists(files)
                || DirectoryIn(files, "LAUNCHER") is not { } launcher
                || FileIn(launcher, "LAUNCHER.EXE") is null)
            {
                continue;
            }

            if (Overhauls.FirstOrDefault(overhaul => DirectoryIn(files, overhaul.GameDirectory) is not null) is { } found)
            {
                return (Path.GetFullPath(files), found);
            }
        }

        return null;
    }

    /// <summary>
    /// Where W.C.A.T. finds the game's data, when the player is to provide it: the series' own
    /// folder's <c>GAMEDAT</c>, which W.C.A.T. asks for a copy of, or the folder itself when the
    /// data is there. Null when its directory already holds a copy, which is then used as it is.
    /// </summary>
    /// <exception cref="FileNotFoundException">There's no copy, and the series isn't set up.</exception>
    public static string? OverhaulData(Game game, Settings settings)
    {
        if (game.Overhaul is not { } overhaul
            || DirectoryIn(game.Folder, overhaul.GameDirectory) is { } copy && Directory.EnumerateFiles(copy).Any())
        {
            return null;
        }

        string folder = settings.FolderOf(overhaul.Series)
            ?? throw new FileNotFoundException(
                $"{overhaul.Title} needs {overhaul.Series.Title}'s data: set it up first (confederation \"<its folder>\"), or copy its GAMEDAT into {overhaul.GameDirectory}");
        return DirectoryIn(folder, "GAMEDAT") ?? folder;
    }

    /// <summary>
    /// The words on the command line that turn on the cheats Origin left in both games, and in
    /// their mission disks: <c>Origin</c>, with a capital O, and <c>-k</c>. Each sets a flag in
    /// the game's data, and in flight the games then take Alt+Del, and in Wing Commander II
    /// Alt+Ins, as cheats (observed; see docs/cheats.md).
    /// </summary>
    public const string CheatWords = "Origin -k";

    // scan codes: Q and W, and the extended Del and Ins
    private const byte Q = 0x10, W = 0x11, Del = 0x53, Ins = 0x52;

    /// <summary>The program's command line: the cheats' words, when they're on, before any given.</summary>
    public static string Arguments(bool cheats, string given)
        => !cheats ? given : given.Length == 0 ? CheatWords : $"{CheatWords} {given}";

    /// <summary>
    /// The cheats' keys, as keys beside Alt that stand in for them: Alt+Q for Alt+Del, which
    /// destroys the target, and in Wing Commander II Alt+W for Alt+Ins, which destroys the enemy
    /// fighters. Wing Commander has no Alt+Ins: its Ins is only a roll.
    /// </summary>
    public static IReadOnlyDictionary<byte, (byte Code, bool Extended)> CheatKeys(Series? series)
        => series == Wc1 ? new Dictionary<byte, (byte, bool)> { [Q] = (Del, true) }
            : series == Wc2 ? new Dictionary<byte, (byte, bool)> { [Q] = (Del, true), [W] = (Ins, true) }
            : new Dictionary<byte, (byte, bool)>();

    /// <summary>What the cheats' keys do, said when the game starts with them on.</summary>
    public static string CheatHelp(Series? series)
        => series == Wc1 ? "in flight, Alt+Q (or Alt+Del) destroys the target"
            : series == Wc2 ? "in flight, Alt+Q (or Alt+Del) destroys the target, Alt+W (or Alt+Ins) the enemy fighters"
            : "the games' own keys, Alt+Del and in Wing Commander II Alt+Ins";

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
        // W.C.A.T.'s batch files are in its "files", so its saves are named for the folder that's in
        string folder = game.Overhaul is not null && string.Equals(Path.GetFileName(game.Folder), "files", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(game.Folder)!
            : game.Folder;
        string name = string.Concat(Path.GetFileName(folder).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
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

    // a folder in a folder, whatever its case
    private static string? DirectoryIn(string folder, string name)
        => Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).FirstOrDefault(directory => string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase))
            : null;
}
