namespace Confederation.Core.Tests;

/// <summary>
/// The games themselves, from their own folders in the workspace's _wingc (or CONFEDERATION_GAMES).
/// Without them these pass without checking.
/// </summary>
public sealed class GameStartTests : IDisposable
{
    private readonly string _saves = Directory.CreateTempSubdirectory("confederation-saves").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    [Fact]
    public void Wing_Commander_starts_and_sets_the_vga_mode()
    {
        if (TestData.Game("Wing Commander") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("WC.EXE");
        RunUntil(machine, () => machine.VideoMode == 0x13, 50_000_000);

        // it opens its data files for writing, but writes none of them, so nothing is copied
        Assert.Empty(Directory.EnumerateFileSystemEntries(_saves, "*", SearchOption.AllDirectories));
        Assert.Contains("console: Expanded Memory Detected.", _log);
    }

    [Fact]
    public void Wing_Commander_plays_its_intro()
    {
        if (TestData.Game("Wing Commander") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("WC.EXE");
        string shots = Path.Combine(AppContext.BaseDirectory, "screenshots");

        // the conductor, the Origin FX logo, and the space battle after the opening words; the
        // times are emulated seconds from launch, and each screen is taken well inside its scene
        foreach (var (second, name) in new[] { (12, "conductor"), (25, "origin-fx"), (36, "battle") })
        {
            RunUntil(machine, () => machine.Time.TotalSeconds >= second, (long)second * machine.InstructionsPerSecond);
            Screenshot.WritePng(machine, Path.Combine(shots, $"wc-intro-{name}.png"));

            // a picture, not a blank or a single color
            var pixels = new uint[machine.DisplayWidth * machine.DisplayHeight];
            machine.Render(pixels);
            Assert.True(pixels.Distinct().Count() >= 16, $"the {name} screen has only {pixels.Distinct().Count()} colors");
        }
    }

    private void RunUntil(Machine machine, Func<bool> done, long limit)
    {
        string? error = null;
        try
        {
            for (long run = 0; run < limit && !done() && machine.Run(1_000_000); run += 1_000_000)
            {
            }
        }
        catch (NotSupportedException e)
        {
            error = e.Message;
        }

        string tail = string.Join('\n', _log.TakeLast(60));
        var cpu = machine.Cpu;
        Assert.True(done(),
            $"{error ?? (machine.Exited ? $"exited with {machine.ExitCode}" : "didn't get there")} at {cpu.Cs:X4}:{cpu.Eip:X4} " +
            $"after {cpu.Instructions} instructions, the last fault {cpu.LastFault}\n{tail}");
    }
}
