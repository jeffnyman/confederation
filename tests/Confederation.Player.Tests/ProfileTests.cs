namespace Confederation.Player.Tests;

/// <summary>
/// Profiles and the command line, with no game needed: a temporary folder holds a stand-in
/// <c>WC.EXE</c>, a few bytes that end at once, and the settings live in a temporary file.
/// </summary>
public sealed class ProfileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("confederation-player").FullName;

    public ProfileTests()
    {
        Wc1 = Directory.CreateDirectory(Path.Combine(_root, "Wing Commander")).FullName;
        File.WriteAllBytes(Path.Combine(Wc1, "WC.EXE"), Exe([0xB8, 0x00, 0x4C, 0xCD, 0x21])); // mov ax, 4C00h; int 21h
        SettingsPath = Path.Combine(_root, "home", "confederation.json");
        Saves = Path.Combine(_root, "saves");
    }

    private string Wc1 { get; }

    private string SettingsPath { get; }

    private string Saves { get; }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private int Run(params string[] args)
        => Program.Run([.. args, "--settings", SettingsPath, "--saves", Saves, "--headless", "--seconds", "1"]);

    [Fact]
    public void No_settings_file_is_empty_settings()
    {
        var settings = Settings.Load(SettingsPath);
        Assert.Empty(settings.Folders);
        Assert.Null(settings.FolderOf(Games.Wc1));
        Assert.True(settings.ProfileOf(Games.Wc1).IsEmpty);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Settings_are_kept_and_read_back_whatever_the_case_of_their_names()
    {
        var settings = Settings.Load(SettingsPath);
        Assert.True(settings.Remember(Games.Wc1, Wc1));
        Assert.False(settings.Remember(Games.Wc1, Wc1));
        settings.Profiles["WC1"] = new Profile { Music = Music.Roland, Scale = 4, Joystick = false };
        settings.Save();

        var read = Settings.Load(SettingsPath);
        Assert.Equal(Wc1, read.FolderOf(Games.Wc1));
        var profile = read.ProfileOf(Games.Wc1);
        Assert.Equal(Music.Roland, profile.Music);
        Assert.Equal(4, profile.Scale);
        Assert.False(profile.Joystick);
        Assert.Null(profile.Cycles);
        Assert.Equal("music roland, no joystick, scale 4", profile.ToString());

        // plain JSON, with the music by name
        Assert.Contains("\"music\": \"roland\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Settings_that_cannot_be_read_say_so()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ not settings");
        var ex = Assert.Throws<InvalidDataException>(() => Settings.Load(SettingsPath));
        Assert.Contains(SettingsPath, ex.Message);
    }

    [Fact]
    public void A_profile_takes_only_what_another_sets()
    {
        var profile = new Profile { Cycles = 4000, Music = Music.AdLib, Sound = false };
        profile.Take(new Profile { Music = Music.Roland, Scale = 2 });
        Assert.Equal(4000, profile.Cycles);
        Assert.Equal(Music.Roland, profile.Music);
        Assert.False(profile.Sound);
        Assert.Equal(2, profile.Scale);
    }

    [Theory]
    [InlineData("wc1", "WC.EXE")]
    [InlineData("SM1", "WC.EXE")]
    [InlineData("sm2", "SM2.EXE")]
    [InlineData("so2", "SO2.EXE")]
    public void Games_are_known_by_their_short_names(string name, string program)
        => Assert.Equal(program, Games.Named(name)!.Program);

    [Fact]
    public void A_folder_is_known_by_its_main_program()
    {
        Assert.Equal(Games.Wc1, Games.SeriesIn(Wc1));
        Assert.Null(Games.SeriesIn(_root));

        var game = Games.Find(Wc1, null);
        Assert.Equal("WC.EXE", game.Program);
        Assert.Equal(Games.Wc1, game.Series);
        Assert.Equal(4000, game.Cycles);
        Assert.Throws<FileNotFoundException>(() => Games.In(Wc1, Games.Named("sm2")!));
    }

    [Fact]
    public void A_folder_played_once_is_remembered_for_its_short_name()
    {
        // the series isn't set up: its short name can't be played
        var ex = Assert.Throws<FileNotFoundException>(() => Run("wc1"));
        Assert.Contains("isn't set up", ex.Message);

        // played by its folder, it's remembered
        Assert.Equal(0, Run(Wc1));
        Assert.Equal(Wc1, Settings.Load(SettingsPath).FolderOf(Games.Wc1));

        // and then by its short name; The Secret Missions are the same program
        Assert.Equal(0, Run("wc1"));
        Assert.Equal(0, Run("sm1"));

        // a mission disk that isn't there
        Assert.Throws<FileNotFoundException>(() => Run("sm2"));
    }

    [Fact]
    public void Options_are_kept_only_when_asked_and_forgotten_when_asked()
    {
        Assert.Equal(0, Run(Wc1, "--cycles", "5000"));
        Assert.True(Settings.Load(SettingsPath).ProfileOf(Games.Wc1).IsEmpty);

        Assert.Equal(0, Run("wc1", "--cycles", "5000", "--no-sound", "--remember"));
        var profile = Settings.Load(SettingsPath).ProfileOf(Games.Wc1);
        Assert.Equal(5000, profile.Cycles);
        Assert.False(profile.Sound);

        // what's remembered is added to, not replaced
        Assert.Equal(0, Run("wc1", "--scale", "5", "--remember"));
        profile = Settings.Load(SettingsPath).ProfileOf(Games.Wc1);
        Assert.Equal(5000, profile.Cycles);
        Assert.Equal(5, profile.Scale);

        Assert.Equal(0, Run("wc1", "--mouse-speed", "50", "--remember"));
        profile = Settings.Load(SettingsPath).ProfileOf(Games.Wc1);
        Assert.Equal(50, profile.MouseSpeed);
        Assert.Equal("cycles 5000, no sound, mouse speed 50%, scale 5", profile.ToString());
        Assert.ThrowsAny<Exception>(() => Run("wc1", "--mouse-speed", "5"));

        Assert.Equal(0, Run("wc1", "--forget"));
        Assert.True(Settings.Load(SettingsPath).ProfileOf(Games.Wc1).IsEmpty);
        Assert.Equal(Wc1, Settings.Load(SettingsPath).FolderOf(Games.Wc1));
    }

    [Fact]
    public void Cheats_put_their_words_before_the_programs_arguments()
    {
        Assert.Equal("", Games.Arguments(cheats: false, ""));
        Assert.Equal("Origin -k", Games.Arguments(cheats: true, ""));
        Assert.Equal("Origin -k /x", Games.Arguments(cheats: true, "/x"));

        // and they're kept like any other option
        Assert.Equal(0, Run(Wc1, "--cheats", "--remember"));
        var profile = Settings.Load(SettingsPath).ProfileOf(Games.Wc1);
        Assert.True(profile.Cheats);
        Assert.Equal("cheats", profile.ToString());
        Assert.Equal(0, Run("wc1", "--no-cheats", "--remember"));
        Assert.False(Settings.Load(SettingsPath).ProfileOf(Games.Wc1).Cheats);
    }

    [Fact]
    public void Cheat_keys_beside_Alt_stand_in_for_the_games_own()
    {
        const byte q = 0x10, w = 0x11, del = 0x53, ins = 0x52;
        var keys = new StandInKeys(Games.CheatKeys(Games.Wc2));

        // without Alt, Q is Q
        Assert.Equal((q, false), keys.Down(q, false, alt: false));
        Assert.Equal((q, false), keys.Up(q, false));

        // with it, the extended Del, while it's held and repeats, and when let go after Alt is
        Assert.Equal((del, true), keys.Down(q, false, alt: true));
        Assert.Equal((del, true), keys.Down(q, false, alt: false));
        Assert.Equal((del, true), keys.Up(q, false));
        Assert.Equal((q, false), keys.Up(q, false));
        Assert.Equal((ins, true), keys.Down(w, false, alt: true));

        // Wing Commander has no Alt+Ins, and with the cheats off nothing stands in
        var wc1 = new StandInKeys(Games.CheatKeys(Games.Wc1));
        Assert.Equal((del, true), wc1.Down(q, false, alt: true));
        Assert.Equal((w, false), wc1.Down(w, false, alt: true));
        Assert.Equal((q, false), StandInKeys.None.Down(q, false, alt: true));
    }

    [Fact]
    public void A_saved_state_is_resumed_from_its_file()
    {
        // one for each program, beside the saves rather than in them, where the game would see it
        var game = Games.Find(Wc1, null);
        Assert.Equal(Path.Combine(Games.Home, "states", "Wing Commander", "WC.state"), Games.StateFile(game));

        string state = Path.Combine(_root, "wc.state");
        var ex = Assert.Throws<FileNotFoundException>(() => Run(Wc1, "--resume", "--state", state));
        Assert.Contains("no state saved", ex.Message);

        // a state of the program as loaded, before its first instruction
        var machine = new Core.Machine(Wc1, Saves);
        machine.Load("WC.EXE");
        using (var file = File.Create(state))
        {
            machine.SaveState(file);
        }

        Assert.Equal(0, Run("wc1", "--resume", "--state", state));

        File.WriteAllText(state, "not a state");
        Assert.Throws<InvalidDataException>(() => Run("wc1", "--resume", "--state", state));
    }

    [Fact]
    public void The_menu_lists_the_games_and_what_is_set_up()
    {
        var settings = Settings.Load(SettingsPath);
        settings.Remember(Games.Wc1, Wc1);
        string picture = Path.Combine(AppContext.BaseDirectory, "screenshots", "menu.bmp");
        Directory.CreateDirectory(Path.GetDirectoryName(picture)!);
        Sdl.SdlMenu.Snapshot(settings, picture, selected: 1, message: "Wing Commander is set up");

        // a 32-bit BMP: its pixels start where its header says
        byte[] bmp = File.ReadAllBytes(picture);
        Assert.Equal((byte)'B', bmp[0]);
        int start = BitConverter.ToInt32(bmp, 10);
        var colors = new HashSet<int>();
        for (int i = start; i + 4 <= bmp.Length; i += 4)
        {
            colors.Add(BitConverter.ToInt32(bmp, i) & 0xFFFFFF);
        }

        // the background, the bar, and text in four colors at least
        Assert.True(colors.Count >= 6, $"only {colors.Count} colors");
    }

    /// <summary>An MZ executable around code that starts at its first byte, as in the core's DOS tests.</summary>
    private static byte[] Exe(byte[] code)
    {
        int total = 32 + code.Length;
        var exe = new byte[total];
        void Word(int at, int value) => BitConverter.GetBytes((ushort)value).CopyTo(exe, at);
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        Word(0x02, total % 512);
        Word(0x04, (total + 511) / 512);
        Word(0x08, 2);
        Word(0x0A, 0x1000);
        Word(0x0C, 0xFFFF);
        Word(0x0E, 0x1000);
        Word(0x10, 0xFFFE);
        Word(0x18, 0x1C);
        code.CopyTo(exe, 32);
        return exe;
    }
}
