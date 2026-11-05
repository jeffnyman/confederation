using System.Reflection;
using Confederation.Core;
using Confederation.Player.Sdl;

namespace Confederation.Player;

internal static class Program
{
    // exit codes: done, not found, a mistake on the command line, and the machine or window failed
    private const int Done = 0, NotFound = 1, BadCommandLine = 2, CannotRun = 3;

    /// <summary>A mistake on the command line, said in one line.</summary>
    private sealed class UsageException(string message) : Exception(message);

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (UsageException ex)
        {
            Error(ex.Message);
            Console.Error.WriteLine("Run 'confederation --help' for more.");
            return BadCommandLine;
        }
        catch (FileNotFoundException ex)
        {
            Error(ex.Message);
            return NotFound;
        }
        catch (Exception ex) when (ex is SdlException or InvalidDataException or IOException)
        {
            Error(ex.Message);
            return CannotRun;
        }
    }

    private static int Run(string[] args)
    {
        string? gamePath = null;
        string? program = null;
        string arguments = "";
        string? saves = null;
        string? logPath = null;
        string? screenshot = null;
        string? mt32Roms = null;
        Music? music = null;
        double? seconds = null;
        int? cycles = null;
        int scale = 3;
        bool fullscreen = false;
        bool squarePixels = false;
        bool joystick = true;
        bool mouse = true;
        bool sound = true;
        bool headless = false;

        int i = 0;

        // the value after an option, which must be there and not be another option
        string Next(string option, string what)
            => i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : throw new UsageException($"{option} needs {what}");

        int Number(string option, string what, int min, int max)
        {
            string value = Next(option, what);
            return int.TryParse(value, out int number) && number >= min && number <= max
                ? number
                : throw new UsageException($"{option} needs {what}, not '{value}'");
        }

        for (; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h" or "-?":
                    PrintHelp();
                    return Done;
                case "--version":
                    Console.WriteLine($"confederation {Version()}");
                    return Done;
                case "--args":
                    arguments = i + 1 < args.Length ? args[++i] : throw new UsageException("--args needs the program's arguments");
                    break;
                case "--saves":
                    saves = Next("--saves", "a folder");
                    break;
                case "--cycles":
                    cycles = Number("--cycles", "instructions a millisecond, as in 4000", 100, 1_000_000);
                    break;
                case "--scale":
                    scale = Number("--scale", "a scale from 1 to 8", 1, 8);
                    break;
                case "--fullscreen":
                    fullscreen = true;
                    break;
                case "--square-pixels":
                    squarePixels = true;
                    break;
                case "--no-joystick":
                    joystick = false;
                    break;
                case "--no-mouse":
                    mouse = false;
                    break;
                case "--no-sound":
                    sound = false;
                    break;
                case "--music":
                    string device = Next("--music", "adlib or roland");
                    music = device.ToLowerInvariant() switch
                    {
                        "adlib" => Music.AdLib,
                        "roland" or "mt32" or "mt-32" => Music.Roland,
                        _ => throw new UsageException($"--music needs adlib or roland, not '{device}'"),
                    };
                    break;
                case "--mt32-roms":
                    mt32Roms = Next("--mt32-roms", "a folder");
                    break;
                case "--log":
                    logPath = Next("--log", "a file name");
                    break;
                case "--seconds":
                    seconds = Number("--seconds", "a number of seconds", 1, int.MaxValue);
                    break;
                case "--screenshot":
                    screenshot = Next("--screenshot", "a file name");
                    break;
                case "--headless":
                    headless = true;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new UsageException($"unknown option {args[i]}");
                    }

                    if (gamePath is null)
                    {
                        gamePath = args[i];
                    }
                    else
                    {
                        program = program is null ? args[i] : throw new UsageException("one game at a time");
                    }

                    break;
            }
        }

        if (gamePath is null)
        {
            PrintHelp();
            return BadCommandLine;
        }

        if (headless && seconds is null)
        {
            throw new UsageException("--headless needs --seconds, or it would never end");
        }

        var game = Games.Find(gamePath, program);
        saves ??= Games.SaveFolder(game);
        Directory.CreateDirectory(saves);

        using var log = logPath is null ? null : new StreamWriter(logPath);
        var machine = new Machine(game.Folder, saves)
        {
            InstructionsPerSecond = (cycles ?? game.Cycles) * 1000,
            Log = log is null ? null : log.WriteLine,
            Mouse = mouse && !headless,
        };
        if (music is not null)
        {
            Games.SetMusic(game, saves, music.Value);
        }

        if (sound && Games.MusicOf(game, saves) == Music.Roland)
        {
            // the game will play its music to the MPU-401, so plug the Roland in
            string roms = mt32Roms ?? Games.Mt32RomFolder();
            if (Directory.Exists(roms))
            {
                Console.WriteLine($"confederation: music on a Roland, {machine.UseMt32(roms)}");
            }
            else if (music == Music.Roland || mt32Roms is not null)
            {
                throw new UsageException($"the Roland needs its ROMs, and there's no folder {roms}: give one with --mt32-roms");
            }
            else
            {
                Error($"the game is set for a Roland, but there are no MT-32 ROMs in {roms}, so there's no music (--music adlib sets it back)");
            }
        }

        machine.Load(game.Program, arguments);
        Console.WriteLine($"confederation: {game.Title}, {game.Program} at {machine.InstructionsPerSecond / 1000} cycles; saves in {saves}");

        bool running;
        try
        {
            if (headless)
            {
                running = machine.Run((long)(seconds!.Value * machine.InstructionsPerSecond));
            }
            else
            {
                var options = new PlayerOptions
                {
                    Title = $"Confederation - {game.Title}",
                    Scale = scale,
                    Fullscreen = fullscreen,
                    SquarePixels = squarePixels,
                    Joystick = joystick,
                    Sound = sound,
                    Mouse = mouse,
                };

                using var player = new SdlPlayer(machine, options);
                running = player.Run(seconds);
            }
        }
        catch (NotSupportedException ex)
        {
            // something the machine doesn't do yet: where the game was, for the log and the notes
            var cpu = machine.Cpu;
            Error($"{ex.Message} At {cpu.Cs:X4}:{cpu.Eip:X4}, {machine.Time.TotalSeconds:F1} s in.");
            return CannotRun;
        }

        if (screenshot is not null)
        {
            Screenshot.WritePng(machine, screenshot);
        }

        if (machine.Exited)
        {
            Console.WriteLine($"confederation: {game.Program} ended with code {machine.ExitCode}");
            return machine.ExitCode == 0 ? Done : CannotRun;
        }

        if (!running && !machine.Exited)
        {
            Error($"the processor stopped, halted with interrupts off, at {machine.Cpu.Cs:X4}:{machine.Cpu.Eip:X4}");
            return CannotRun;
        }

        return Done;
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            confederation {Version()}: plays the Wing Commander games, and only that.

            usage: confederation <game> [program] [options]

              <game>             the game's folder, or its program; in a folder, the main game is
                                 found (WC.EXE or WC2.EXE)
              [program]          another program in the folder, such as SM2.EXE or SO1.EXE

            options:
              --args "<text>"    the program's command line arguments
              --saves <folder>   where the game's writes go (by default a folder of its own in the
                                 user's application data); the game's folder is never written
              --cycles <n>       instructions a millisecond, as DOSBox counts cycles: the game's own
                                 by default, 4000 for Wing Commander and 8000 for Wing Commander II
              --scale <n>        the window's first size, n times the picture (3)
              --fullscreen       start in full screen
              --square-pixels    square pixels, not the 4:3 picture a monitor showed
              --no-joystick      no joystick on the game port, even with a gamepad plugged in
              --no-mouse         no mouse: the games' pointers move by keyboard or joystick
              --no-sound         no sound
              --music <device>   the music device, adlib or roland, written into the game's
                                 settings in the save folder, where it stays (GOG's is adlib)
              --mt32-roms <dir>  where the Roland MT-32's or CM-32L's ROMs are, for roland (by
                                 default a folder mt32-roms beside the saves); they're Roland's,
                                 and not included
              --log <file>       write every DOS, BIOS and EMS call, and unanswered port, to a file
              --seconds <n>      stop after n seconds of the game's time
              --screenshot <f>   save the picture as a PNG when stopping
              --headless         no window: run --seconds seconds and stop

            playing:
              the keyboard is the PC's, key for key
              a gamepad is the joystick: the left stick or d-pad moves it, the face buttons are its
              four buttons (plug it in before starting: the games look for a joystick as they start)
              the mouse is the PC's once a click in the window captures it; Ctrl+F10 frees it
              F11 full screen, Shift+F11 pixel shape, F12 full speed on and off, Pause pauses
            """);
    }

    private static string Version()
        => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    private static void Error(string message) => Console.Error.WriteLine($"confederation: {message}");
}
