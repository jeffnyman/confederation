namespace Confederation.Player.Tests;

/// <summary>
/// W.C.A.T., found as its download lays it out: a <c>files</c> folder with its batch files, its
/// launcher, and an empty directory for the game's data, which the player fills from the
/// series' own folder rather than a copy.
/// </summary>
public sealed class OverhaulTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("confederation-wcat").FullName;

    public OverhaulTests()
    {
        Wcat = Directory.CreateDirectory(Path.Combine(_root, "W.C.A.T. for WC1")).FullName;
        Files = Directory.CreateDirectory(Path.Combine(Wcat, "files")).FullName;
        Directory.CreateDirectory(Path.Combine(Files, "LAUNCHER"));
        Directory.CreateDirectory(Path.Combine(Files, "GAMEWC1"));
        File.WriteAllBytes(Path.Combine(Files, "LAUNCHER", "launcher.exe"), []);
        File.WriteAllText(Path.Combine(Files, "WCAT.bat"), "");
        File.WriteAllText(Path.Combine(Files, "WC.bat"), "");

        Wc1 = Directory.CreateDirectory(Path.Combine(_root, "Wing Commander")).FullName;
        Directory.CreateDirectory(Path.Combine(Wc1, "GAMEDAT"));
        File.WriteAllBytes(Path.Combine(Wc1, "WC.EXE"), []);
    }

    private string Wcat { get; }

    private string Files { get; }

    private string Wc1 { get; }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Its_folder_runs_the_launcher_from_files()
    {
        var game = Games.Find(Wcat, null);

        Assert.Equal(Files, game.Folder);
        Assert.Equal("WCAT.BAT", game.Program);
        Assert.Same(Games.Overhauls[0], game.Overhaul);
        Assert.Null(game.Series);
        Assert.Equal(Games.OverhaulCycles, game.Cycles);
        Assert.Equal("W.C.A.T. for WC1", Path.GetFileName(Games.SaveFolder(game)));
    }

    [Fact]
    public void Its_batch_files_can_be_named()
    {
        Assert.Equal("WC.BAT", Games.Find(Wcat, "WC.bat").Program);
        Assert.Equal("WC.BAT", Games.Find(Path.Combine(Files, "WC.bat"), null).Program);
    }

    [Fact]
    public void The_game_data_comes_from_the_series_folder()
    {
        var game = Games.Find(Wcat, null);
        var settings = Settings.Load(Path.Combine(_root, "confederation.json"));

        Assert.Throws<FileNotFoundException>(() => Games.OverhaulData(game, settings));

        settings.Remember(Games.Wc1, Wc1);
        Assert.Equal(Path.Combine(Wc1, "GAMEDAT"), Games.OverhaulData(game, settings));

        // a copy in W.C.A.T.'s own directory, as its readme asks for, is used as it is
        File.WriteAllBytes(Path.Combine(Files, "GAMEWC1", "BRIEFING.VGA"), []);
        Assert.Null(Games.OverhaulData(game, settings));
    }
}
