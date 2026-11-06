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
        if (TestData.Game("wc1") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("CONFWC1.EXE");
        RunUntil(machine, () => machine.VideoMode == 0x13, 50_000_000);

        // it opens its data files for writing, but writes none of them, so nothing is copied
        Assert.Empty(Directory.EnumerateFileSystemEntries(_saves, "*", SearchOption.AllDirectories));
        Assert.Contains("console: Expanded Memory Detected.", _log);
    }

    [Fact]
    public void Wing_Commander_plays_its_intro()
    {
        if (TestData.Game("wc1") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("CONFWC1.EXE");
        string shots = Path.Combine(AppContext.BaseDirectory, "screenshots");

        // the conductor, the Origin FX logo, and the space battle after the opening words; the
        // times are emulated seconds from launch, and each screen is taken well inside its scene
        var sound = new List<short>();
        var buffer = new short[Machine.SampleRate * 2];
        foreach (var (second, name) in new[] { (12, "conductor"), (25, "origin-fx"), (36, "battle") })
        {
            // a tenth of a second at a time, taking the sound as it's made
            while (machine.Time.TotalSeconds < second && machine.Run(machine.InstructionsPerSecond / 10))
            {
                int count = machine.ReadSamples(buffer);
                sound.AddRange(buffer.AsSpan(0, count));
            }

            RunUntil(machine, () => machine.Time.TotalSeconds >= second, 0);
            Screenshot.WritePng(machine, Path.Combine(shots, $"wc-intro-{name}.png"));

            // a picture, not a blank or a single color
            var pixels = new uint[machine.DisplayWidth * machine.DisplayHeight];
            machine.Render(pixels);
            Assert.True(pixels.Distinct().Count() >= 16, $"the {name} screen has only {pixels.Distinct().Count()} colors");
        }

        // the score, on the AdLib: saved to listen to, and loud enough to be music, not silence
        WriteWav(Path.Combine(shots, "wc-intro.wav"), sound);
        Assert.InRange(sound.Count, 2 * 35 * Machine.SampleRate, 2 * 37 * Machine.SampleRate);
        double rms = Math.Sqrt(sound.Average(sample => (double)sample * sample));
        Assert.True(rms > 300, $"the intro is nearly silent: {rms:F0}");
    }

    [Fact]
    public void Wing_Commander_plays_its_intro_on_a_roland()
    {
        if (TestData.Game("wc1") is not { } folder || TestData.Mt32Roms() is not { } roms)
        {
            return;
        }

        // the installer's setting for a Roland, in the save folder where the player puts it
        File.WriteAllText(Path.Combine(_saves, "WINGCMDR.CFG"), "v r z\r\n");
        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.UseMt32(roms);
        machine.Load("CONFWC1.EXE");

        // the driver greets the player on the module's display, and sends its own instruments,
        // a SysEx message about every eighth of a second, which takes until about 10 s in; the
        // music starts at about 16 s
        var sound = new List<short>();
        var buffer = new short[Machine.SampleRate * 2];
        while (machine.Time.TotalSeconds < 24 && machine.Run(machine.InstructionsPerSecond / 10))
        {
            sound.AddRange(buffer.AsSpan(0, machine.ReadSamples(buffer)));
        }

        WriteWav(Path.Combine(AppContext.BaseDirectory, "screenshots", "wc-intro-mt32.wav"), sound);
        Assert.Equal("Origin Sound System!", machine.Mt32Display);
        Assert.Contains("mt32: \"Origin Sound System!\"", _log);

        var before = sound.Take(2 * 10 * Machine.SampleRate).ToList();
        var music = sound.Skip(2 * 17 * Machine.SampleRate).ToList();
        Assert.All(before, sample => Assert.Equal(0, sample));
        double rms = Math.Sqrt(music.Average(sample => (double)sample * sample));
        Assert.True(rms > 100, $"the music is nearly silent: {rms:F0}");
    }

    [Fact]
    public void Wing_Commander_II_speaks_on_the_sound_blaster()
    {
        if (TestData.Game("wc2") is not { } folder)
        {
            return;
        }

        // at GOG's speed for the game, 8,000 cycles: since a repeated string instruction takes
        // time for each repetition, as in DOSBox, the default 4,000 loads the speech too slowly
        var machine = new Machine(folder, _saves) { Log = _log.Add, InstructionsPerSecond = 8_000_000 };
        machine.Load("CONFWC2.EXE");

        void Press(byte code, double seconds)
        {
            machine.PressKey(code);
            machine.Run((long)(machine.InstructionsPerSecond * seconds));
            machine.ReleaseKey(code);
            machine.Run(machine.InstructionsPerSecond / 2);
        }

        // at the title menu, Up puts the pointer on "Start New Game" and Enter starts it; the
        // introduction's first scene, the Emperor and Prince Thrakhath, is the one with speech
        RunUntil(machine, () => machine.Time.TotalSeconds >= 65, 70L * machine.InstructionsPerSecond);
        Assert.Contains(_log, line => line.Contains("21/25 set vector 0Dh")); // its IRQ 5 handler
        Press(0x48, 0.3);
        Press(0x1C, 0.2);
        RunUntil(machine, () => _log.Any(line => line.StartsWith("sound blaster: ", StringComparison.Ordinal)), 70L * machine.InstructionsPerSecond);
        Assert.Contains(_log, line => line.Contains("open speech.s00", StringComparison.OrdinalIgnoreCase));
        Screenshot.WritePng(machine, Path.Combine(AppContext.BaseDirectory, "screenshots", "wc2-emperor.png"));

        // eight seconds of it: one block after another, each started by the game's interrupt
        // handler as the last ends, so the speech runs on without gaps
        var sound = new List<short>();
        var buffer = new short[Machine.SampleRate * 2];
        double start = machine.Time.TotalSeconds;
        while (machine.Time.TotalSeconds < start + 8 && machine.Run(machine.InstructionsPerSecond / 10))
        {
            sound.AddRange(buffer.AsSpan(0, machine.ReadSamples(buffer)));
        }

        WriteWav(Path.Combine(AppContext.BaseDirectory, "screenshots", "wc2-speech.wav"), sound);
        var blocks = _log.Where(line => line.StartsWith("sound blaster: ", StringComparison.Ordinal)).ToList();
        Assert.True(blocks.Count >= 15, $"only {blocks.Count} blocks in eight seconds");
        Assert.Contains("sound blaster: 4000 bytes at 10752 Hz", blocks);
        Assert.DoesNotContain(_log, line => line.Contains("is not done", StringComparison.Ordinal));
        double rms = Math.Sqrt(sound.Average(sample => (double)sample * sample));
        Assert.True(rms > 1000, $"the speech is nearly silent: {rms:F0}");
    }

    [Fact]
    public void Wing_Commander_II_starts_a_new_game_by_mouse()
    {
        if (TestData.Game("wc2") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add, Mouse = true };
        machine.Load("CONFWC2.EXE");
        RunUntil(machine, () => machine.Time.TotalSeconds >= 65, 70L * machine.InstructionsPerSecond);

        // the game polls the driver for the pointer's place (function 3) every frame: into the
        // top left corner, then onto "Start New Game", at (160, 135) on the screen, (320, 135) in
        // the driver's 640 by 200; a mickey a column across, two a row down
        machine.MoveMouse(-2000, -2000);
        machine.Run(machine.InstructionsPerSecond / 10);
        machine.MoveMouse(320, 270);
        machine.Run(machine.InstructionsPerSecond / 2);
        Screenshot.WritePng(machine, Path.Combine(AppContext.BaseDirectory, "screenshots", "wc2-mouse-on-button.png"));
        Assert.DoesNotContain(_log, line => line.Contains("open startglb.000", StringComparison.OrdinalIgnoreCase));

        machine.SetMouseButton(0, true);
        machine.Run(machine.InstructionsPerSecond / 5);
        machine.SetMouseButton(0, false);
        RunUntil(machine, () => _log.Any(line => line.Contains("open startglb.000", StringComparison.OrdinalIgnoreCase)), 5L * machine.InstructionsPerSecond);
        Assert.DoesNotContain(_log, line => line.StartsWith("33/", StringComparison.Ordinal) && line.Contains("not done", StringComparison.Ordinal));
    }

    [Fact]
    public void Wing_Commander_II_signs_on_a_pilot_and_flies()
    {
        if (TestData.Game("wc2") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("CONFWC2.EXE");

        void Wait(double seconds) => machine.Run((long)(machine.InstructionsPerSecond * seconds));
        void Press(byte code, double seconds = 0.15)
        {
            machine.PressKey(code);
            Wait(seconds);
            machine.ReleaseKey(code);
            Wait(0.3);
        }

        void Type(string text)
        {
            const string rows = "qwertyuiop....asdfghjkl.....zxcvbnm";
            foreach (char c in text)
            {
                int i = rows.IndexOf(c);
                Press((byte)(i < 14 ? 0x10 + i : i < 28 ? 0x1E + i - 14 : 0x2C + i - 28), 0.08);
            }

            Press(0x1C);
            Wait(2);
        }

        bool Logged(string text) => _log.Any(line => line.Contains(text, StringComparison.OrdinalIgnoreCase));

        // at the title menu, Up and Enter start a new game; Esc skips each scene of the
        // introduction, which ends at the personnel database
        Wait(65);
        Press(0x48, 0.3);
        Press(0x1C);
        while (!Logged("open options.v00") && machine.Time.TotalSeconds < 400)
        {
            Wait(14);
            if (!Logged("open options.v00"))
            {
                Press(0x01);
            }
        }

        Assert.True(Logged("open options.v00"), "the introduction didn't end at the personnel database");

        // the database takes no keys for its first few seconds; then C creates a pilot, whose
        // names are typed into prompts the game reads through DOS. That only works because the
        // BIOS says it has the keyboard intercept (INT 15h function C0h), so the game watches
        // keys through INT 15h function 4Fh and leaves the BIOS to buffer them for DOS.
        Wait(12);
        Press(0x2E);
        Wait(3);
        Type("christopher");
        Type("blair");
        Type("maverick");
        Assert.Contains(_log, line => line.Contains("21/25 set vector 15h", StringComparison.Ordinal));
        Assert.DoesNotContain(_log, line => line.Contains("21/25 set vector 09h", StringComparison.Ordinal));
        string save = Directory.EnumerateFiles(_saves, "SAVEGAME.WC2", SearchOption.AllDirectories).Single();
        Assert.Contains("maverick", File.ReadAllText(save, System.Text.Encoding.Latin1));

        // in the quarters, Enter on "Fly mission" goes to the story and the briefing, and Esc
        // skips each scene to the launch: the cockpit and the first nav point
        Wait(4);
        Press(0x1C);
        double launchBy = machine.Time.TotalSeconds + 120;
        while (!Logged("open pcship.v00") && machine.Time.TotalSeconds < launchBy)
        {
            Wait(5);
            if (!Logged("open pcship.v00"))
            {
                Press(0x01);
            }
        }

        Assert.True(Logged("open pcship.v00"), "no launch");
        Wait(8);
        Screenshot.WritePng(machine, Path.Combine(AppContext.BaseDirectory, "screenshots", "wc2-cockpit.png"));
        var pixels = new uint[machine.DisplayWidth * machine.DisplayHeight];
        machine.Render(pixels);
        Assert.True(pixels.Distinct().Count() >= 32, "the cockpit is nearly blank");
        Assert.DoesNotContain(_log, line => line.StartsWith("port ", StringComparison.Ordinal) || line.Contains("is not done", StringComparison.Ordinal));
    }

    private static void WriteWav(string path, List<short> samples)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new BinaryWriter(File.Create(path));
        int bytes = samples.Count * 2;
        writer.Write("RIFF"u8);
        writer.Write(36 + bytes);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)2); // stereo
        writer.Write(Machine.SampleRate);
        writer.Write(Machine.SampleRate * 4);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(bytes);
        foreach (short sample in samples)
        {
            writer.Write(sample);
        }
    }

    [Fact]
    public void Wing_Commander_goes_by_keyboard_to_the_barracks()
    {
        if (TestData.Game("wc1") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("CONFWC1.EXE");

        // a key goes down and, a tenth of a second later, comes up, as a quick press does
        void Press(byte code)
        {
            machine.PressKey(code);
            machine.Run(machine.InstructionsPerSecond / 10);
            machine.ReleaseKey(code);
        }

        // Space cuts the intro short and Enter ends it, at the campaign menu; Enter there starts
        // the Vega campaign, in the Tiger's Claw's rec room
        RunUntil(machine, () => machine.Time.TotalSeconds >= 10, 50_000_000);
        Press(0x39);
        machine.Run(12L * machine.InstructionsPerSecond);
        Press(0x1C);
        machine.Run(12L * machine.InstructionsPerSecond);
        Assert.DoesNotContain(_log, line => line.Contains("RECROOM.VGA", StringComparison.OrdinalIgnoreCase));
        Press(0x1C);
        RunUntil(machine, () => _log.Any(line => line.Contains("open RECROOM.VGA", StringComparison.OrdinalIgnoreCase)), 10L * machine.InstructionsPerSecond);
        machine.Run(2L * machine.InstructionsPerSecond);
        Screenshot.WritePng(machine, Path.Combine(AppContext.BaseDirectory, "screenshots", "wc-barracks.png"));
    }

    /// <summary>
    /// A new campaign starts in the simulator, and its end asks for the pilot's name. The prompt
    /// waits for each key with DOS's function 07h, called through the game's own INT 21h handler
    /// by a far call: the wait once broke that call's return, losing keys and then locking up.
    /// </summary>
    [Fact]
    public void Wing_Commander_takes_a_name_at_its_top_score_prompt()
    {
        if (TestData.Game("wc1") is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add };
        machine.Load("CONFWC1.EXE");

        // a quick press, then a moment before the next, as a typist's
        void Press(byte code)
        {
            machine.PressKey(code);
            machine.Run(machine.InstructionsPerSecond * 8 / 100);
            machine.ReleaseKey(code);
            machine.Run(machine.InstructionsPerSecond * 15 / 100);
        }

        // past the intro to the campaign menu, where the pointer starts on "Continue Campaign":
        // up to "Start Vega Campaign" and Enter, and the simulator ends at the prompt at once
        RunUntil(machine, () => machine.Time.TotalSeconds >= 10, 50_000_000);
        Press(0x39);
        machine.Run(12L * machine.InstructionsPerSecond);
        Press(0x1C);
        machine.Run(12L * machine.InstructionsPerSecond);
        for (int i = 0; i < 6; i++)
        {
            Press(0x48);
        }

        Press(0x1C);
        machine.Run(12L * machine.InstructionsPerSecond);

        // "smith", a backspace, and Enter: the game capitalizes it, and asks for the callsign
        foreach (byte key in new byte[] { 0x1F, 0x32, 0x17, 0x14, 0x23, 0x0E, 0x1C })
        {
            Press(key);
        }

        machine.Run(machine.InstructionsPerSecond / 2);
        Screenshot.WritePng(machine, Path.Combine(AppContext.BaseDirectory, "screenshots", "wc-top-score.png"));
        // kept as typed, and shown capitalized
        Assert.True(Contains(machine, "smit\0"u8), "the name typed isn't in memory");
        Assert.False(Contains(machine, "smith"u8), "the backspace didn't take");
    }

    private static bool Contains(Machine machine, ReadOnlySpan<byte> text)
    {
        var memory = new byte[0xA_0000];
        for (uint address = 0; address < memory.Length; address++)
        {
            memory[address] = machine.Read(address);
        }

        return memory.AsSpan().IndexOf(text) >= 0;
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
