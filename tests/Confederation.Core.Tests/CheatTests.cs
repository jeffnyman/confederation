namespace Confederation.Core.Tests;

/// <summary>
/// The cheats Origin left in the games, turned on by the words <c>Origin -k</c> on the command
/// line. Run with and without them, the games' data differs in two flags: one <c>Origin</c> sets,
/// and one <c>-k</c> then clears. Found by comparing the whole memory of two runs, which differ
/// in nothing else; where the flags are is the GOG releases', the only ones this has seen.
/// </summary>
public sealed class CheatTests : IDisposable
{
    private readonly string _saves = Directory.CreateTempSubdirectory("confederation-saves").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    [Theory]
    [InlineData("Wing Commander", "WC.EXE", 0xB6)]
    [InlineData("Wing Commander", "SM2.EXE", 0xB6)]
    [InlineData("Wing Commander II", "WC2.EXE", 0xBC)]
    [InlineData("Wing Commander II", "SO1.EXE", 0xBC)]
    [InlineData("Wing Commander II", "SO2.EXE", 0xBC)]
    public void The_cheat_words_set_the_games_flags(string game, string program, int origin)
    {
        if (TestData.Game(game) is not { } folder)
        {
            return;
        }

        // the flag Origin sets, and four bytes on the one -k clears, in the game's data segment
        // once it's started
        (byte Origin, byte K) Flags(string arguments)
        {
            var machine = new Machine(folder, _saves);
            machine.Load(program, arguments);
            machine.Run(3L * machine.InstructionsPerSecond);
            uint data = (uint)machine.Cpu.Ds << 4;
            return (machine.Read(data + (uint)origin), machine.Read(data + (uint)origin + 4));
        }

        Assert.Equal((0, 1), Flags(""));
        Assert.Equal((1, 0), Flags("Origin -k"));
        Assert.Equal((1, 1), Flags("Origin"));

        // -k counts only after Origin, and Origin only with its capital
        Assert.Equal((0, 1), Flags("-k"));
        Assert.Equal((0, 1), Flags("origin -k"));
    }
}
