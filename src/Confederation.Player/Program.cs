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

    /// <summary>The command line, run: what <see cref="Main"/> does, without its error reporting.</summary>
    internal static int Run(string[] args)
    {
        string? gameName = null;
        string? program = null;
        string arguments = "";
        string? saves = null;
        string? logPath = null;
        string? screenshot = null;
        string? mt32Roms = null;
        string? statePath = null;
        bool resume = false;
        string settingsPath = Settings.DefaultPath;
        double? seconds = null;
        bool headless = false;
        bool list = false;
        bool remember = false;
        bool forget = false;
        bool forgetFolder = false;

        // the options a profile can keep, as given here: they win over the profile for this run
        var given = new Profile();

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
                case "--list":
                    list = true;
                    break;
                case "--settings":
                    settingsPath = Path.GetFullPath(Next("--settings", "a file name"));
                    break;
                case "--remember":
                    remember = true;
                    break;
                case "--forget":
                    forget = true;
                    break;
                case "--forget-folder":
                    forgetFolder = true;
                    break;
                case "--args":
                    arguments = i + 1 < args.Length ? args[++i] : throw new UsageException("--args needs the program's arguments");
                    break;
                case "--saves":
                    saves = Next("--saves", "a folder");
                    break;
                case "--cycles":
                    given.Cycles = Number("--cycles", "instructions a millisecond, as in 4000", 100, 1_000_000);
                    break;
                case "--scale":
                    given.Scale = Number("--scale", "a scale from 1 to 8", 1, 8);
                    break;
                case "--fullscreen":
                    given.Fullscreen = true;
                    break;
                case "--windowed":
                    given.Fullscreen = false;
                    break;
                case "--square-pixels":
                    given.SquarePixels = true;
                    break;
                case "--no-square-pixels":
                    given.SquarePixels = false;
                    break;
                case "--joystick" or "--no-joystick":
                    given.Joystick = args[i] == "--joystick";
                    break;
                case "--mouse" or "--no-mouse":
                    given.Mouse = args[i] == "--mouse";
                    break;
                case "--mouse-speed":
                    given.MouseSpeed = Number("--mouse-speed", "a speed in percent, from 10 to 400", 10, 400);
                    break;
                case "--cheats" or "--no-cheats":
                    given.Cheats = args[i] == "--cheats";
                    break;
                case "--sound" or "--no-sound":
                    given.Sound = args[i] == "--sound";
                    break;
                case "--music":
                    string device = Next("--music", "adlib or roland");
                    given.Music = device.ToLowerInvariant() switch
                    {
                        "adlib" => Music.AdLib,
                        "roland" or "mt32" or "mt-32" => Music.Roland,
                        _ => throw new UsageException($"--music needs adlib or roland, not '{device}'"),
                    };
                    break;
                case "--mt32-roms":
                    mt32Roms = Path.GetFullPath(Next("--mt32-roms", "a folder"));
                    break;
                case "--state":
                    statePath = Path.GetFullPath(Next("--state", "a file name"));
                    break;
                case "--resume":
                    resume = true;
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

                    if (gameName is null)
                    {
                        gameName = args[i];
                    }
                    else
                    {
                        program = program is null ? args[i] : throw new UsageException("one game at a time");
                    }

                    break;
            }
        }

        var settings = Settings.Load(settingsPath);
        bool changed = false;
        if (mt32Roms is not null && mt32Roms != settings.Mt32Roms)
        {
            // the ROMs' folder is remembered once given, as the games' folders are
            settings.Mt32Roms = mt32Roms;
            changed = true;
        }

        if (forgetFolder)
        {
            // only by a short name: a folder given here would be remembered again at once
            var forgetting = (gameName is not null ? Games.Named(gameName)?.Series : null)
                ?? throw new UsageException("--forget-folder needs a game's short name, such as wc1");
            string from = Games.Bundled(forgetting) is not null ? $"; {forgetting.Title} plays the copy included with Confederation" : "";
            if (settings.Forget(forgetting) is { } forgotten)
            {
                changed = true;
                Console.WriteLine($"confederation: forgot {forgetting.Title}'s folder, {forgotten}{from}");
            }
            else
            {
                Console.WriteLine($"confederation: no folder was kept for {forgetting.Title}{from}");
            }
        }

        if (list)
        {
            if (changed)
            {
                settings.Save();
            }

            List(settings);
            return Done;
        }

        if (headless && seconds is null)
        {
            throw new UsageException("--headless needs --seconds, or it would never end");
        }

        Game game;
        if (gameName is null)
        {
            if (headless)
            {
                throw new UsageException("--headless needs a game");
            }

            // no game named: the menu, which can also set games up
            if (Sdl.SdlMenu.Choose(settings) is not { } chosen)
            {
                return Done;
            }

            game = chosen;
        }
        else if (Games.Named(gameName) is { } known)
        {
            game = Games.In(
                Games.FolderOf(settings, known.Series)
                    ?? throw new FileNotFoundException($"{known.Series.Title} isn't set up yet: run confederation \"<its folder>\" once, or drop the folder on the menu's window"),
                known);
            if (program is not null)
            {
                throw new UsageException($"{gameName} names its program already");
            }
        }
        else
        {
            game = Games.Find(gameName, program);
            if (game.Series is { } found)
            {
                // a folder given once is remembered, so the game can be named by its short name
                changed |= settings.Remember(found, game.Folder);
            }
        }

        // the series' profile, with what was given here over it
        var profile = new Profile();
        if (game.Series is { } series)
        {
            if (forget && settings.Profiles.Remove(series.Key))
            {
                changed = true;
            }

            profile.Take(settings.ProfileOf(series));
            if (remember && !given.IsEmpty)
            {
                var kept = settings.ProfileOf(series);
                kept.Take(given);
                settings.Profiles[series.Key] = kept;
                changed = true;
            }
        }
        else if (remember || forget)
        {
            throw new UsageException($"the player keeps no profile for {game.Program}, a program it doesn't know");
        }

        profile.Take(given);
        if (changed)
        {
            settings.Save();
        }

        // W.C.A.T. without a copy of the game's data: the series' own folder, where it looks
        string? overhaulData = Games.OverhaulData(game, settings);

        bool sound = profile.Sound ?? true;
        saves ??= Games.SaveFolder(game);
        Directory.CreateDirectory(saves);

        using var log = logPath is null ? null : new StreamWriter(logPath);
        var machine = new Machine(game.Folder, saves)
        {
            InstructionsPerSecond = (profile.Cycles ?? game.Cycles) * 1000,
            Log = log is null ? null : log.WriteLine,
            Mouse = (profile.Mouse ?? true) && !headless,
        };
        if (game.Cd is not null)
        {
            Console.WriteLine($"confederation: the CD, {machine.InsertCd(game.Cd)}, from {game.Cd}, as D:");
        }

        if (overhaulData is not null)
        {
            machine.Mount(game.Overhaul!.GameDirectory, overhaulData);
            Console.WriteLine($"confederation: {game.Overhaul.Series.Title}'s data from {overhaulData}, as C:\\{game.Overhaul.GameDirectory} (read only)");
        }

        if (profile.Music is { } music)
        {
            if (game.Config is null)
            {
                // Wing Commander III chooses its music in its own setup
                Error($"the player can't set {game.Title}'s music: it's chosen in the game's own setup");
            }
            else
            {
                Games.SetMusic(game, saves, music);
            }
        }

        if (sound && Games.MusicOf(game, saves) == Music.Roland)
        {
            // the game will play its music to the MPU-401, so plug the Roland in
            string roms = settings.Mt32Roms ?? Games.Mt32RomFolder();
            if (Directory.Exists(roms))
            {
                Console.WriteLine($"confederation: music on a Roland, {machine.UseMt32(roms)}");
            }
            else if (given.Music == Music.Roland)
            {
                throw new UsageException($"the Roland needs its ROMs, and there's no folder {roms}: give one with --mt32-roms");
            }
            else
            {
                Error($"the game is set for a Roland, but there are no MT-32 ROMs in {roms}, so there's no music (--music adlib sets it back)");
            }
        }

        bool cheats = profile.Cheats ?? false;
        machine.Load(game.Start, Games.Arguments(cheats, arguments));
        statePath ??= Games.StateFile(game);
        if (resume)
        {
            // on from where Ctrl+F5 last saved, the machine as it was then
            if (!File.Exists(statePath))
            {
                throw new FileNotFoundException($"no state saved at {statePath}: Ctrl+F5 saves one while playing");
            }

            using var state = File.OpenRead(statePath);
            machine.LoadState(state);
            Console.WriteLine($"confederation: on from the state saved {File.GetLastWriteTime(statePath):g}");
        }
        Console.WriteLine($"confederation: {game.Title}, {game.Program} at {machine.InstructionsPerSecond / 1000} cycles; saves in {saves}");
        if (gameName is not null && Games.Named(gameName) is { Note: { } note } named)
        {
            Console.WriteLine($"confederation: {named.Title} is {note}");
        }

        if (cheats)
        {
            Console.WriteLine($"confederation: cheats on ({Games.CheatWords}): {Games.CheatHelp(game.Series)}");
        }

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
                    Scale = profile.Scale ?? 3,
                    Fullscreen = profile.Fullscreen ?? false,
                    SquarePixels = profile.SquarePixels ?? false,
                    Joystick = profile.Joystick ?? true,
                    Sound = sound,
                    Mouse = profile.Mouse ?? true,
                    MouseSpeed = (profile.MouseSpeed ?? 100) / 100.0,
                    StatePath = statePath,
                    Icon = GameIcons.For(game),
                    StandIns = cheats ? new StandInKeys(Games.CheatKeys(game.Series)) : StandInKeys.None,
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

    /// <summary>What's set up: each game, where it is, and how its series is played.</summary>
    private static void List(Settings settings)
    {
        Console.WriteLine($"settings: {settings.Path}");
        Console.WriteLine($"MT-32 ROMs: {settings.Mt32Roms ?? $"{Games.Mt32RomFolder()} (the default)"}");
        foreach (var series in Games.AllSeries)
        {
            Console.WriteLine();
            string? folder = Games.FolderOf(settings, series);
            string from = settings.FolderOf(series) is null && folder is not null ? " (the copy included with Confederation)" : "";
            Console.WriteLine($"{series.Title}: {folder ?? "not set up (run confederation \"<its folder>\" once)"}{from}");
            if (folder is null)
            {
                continue;
            }

            Console.WriteLine($"  played with {settings.ProfileOf(series)}");
            foreach (var game in Games.Known.Where(game => game.Series == series))
            {
                string missing = series.Cd is { } cd ? cd.Image : game.Program;
                string note = !Games.IsIn(folder, game) ? $"  (no {missing})" : game.Note is { } n ? $"  ({n})" : "";
                Console.WriteLine($"  {game.Name}  {game.Title}{note}");
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            confederation {Version()}: plays the Wing Commander games, and only that.

            usage: confederation [game] [program] [options]

              [game]             a game's short name: wc1, sm1 or sm2 (Wing Commander and its
                                 Secret Missions), wc2, so1 or so2 (Wing Commander II and its
                                 Special Operations), wc3 (Wing Commander III, from GOG's CD
                                 image, DATA.DAT); or its folder, or its program. Wing Commander
                                 and Wing Commander II are included, and play by their short
                                 names as they are. A folder given once is remembered, so the
                                 short names find it after.
                                 Or W.C.A.T.'s folder, which runs its launcher, with the series
                                 set up for its data. With no game, a menu in a window lists them.
              [program]          another program in the folder, such as SM2.EXE or SO1.EXE, or
                                 one of W.C.A.T.'s batch files, such as WC.BAT

            options:
              --list             what's set up: each game's folder, and how it's played
              --remember         keep this run's options below as the game's own, for the game
                                 and its mission disks
              --forget           go back to the defaults for the game and its mission disks
              --forget-folder    forget the folder the game was set up from, for the game and its
                                 mission disks: Wing Commander and Wing Commander II then play
                                 the copies included with Confederation (with --list, forget
                                 and list rather than play)
              --settings <file>  the settings file (by default confederation.json in the user's
                                 application data, beside the saves)
              --args "<text>"    the program's command line arguments
              --saves <folder>   where the game's writes go (by default a folder of its own in the
                                 user's application data); the game's folder is never written
              --cycles <n>       instructions a millisecond, as DOSBox counts cycles: the game's own
                                 by default, 4000 for Wing Commander, 8000 for Wing Commander II,
                                 30000 for Wing Commander III
              --scale <n>        the window's first size, n times the picture (3)
              --fullscreen       start in full screen (--windowed: in a window)
              --square-pixels    square pixels, not the 4:3 picture a monitor showed
                                 (--no-square-pixels: 4:3)
              --no-joystick      no joystick on the game port, even with a gamepad plugged in
              --no-mouse         no mouse: the games' pointers move by keyboard or joystick
              --no-sound         no sound
              --mouse-speed <n>  the mouse's speed in percent (100): at 100, moving across the
                                 picture moves the game's pointer across its screen
                                 (--joystick, --mouse, --sound: back on)
              --cheats           the games' own cheats: the ship can't be destroyed, Alt+Q (Alt+Del)
                                 destroys the target, and in WC2 Alt+W (Alt+Ins) the enemy
                                 fighters (--no-cheats: off)
              --music <device>   the music device, adlib or roland, written into the game's
                                 settings in the save folder, where it stays (GOG's is adlib)
              --mt32-roms <dir>  where the Roland MT-32's or CM-32L's ROMs are, for roland (by
                                 default a folder mt32-roms beside the saves), remembered once
                                 given; they're Roland's, and not included
              --resume           start from the state Ctrl+F5 last saved
              --state <file>     the file Ctrl+F5 saves the state to, Ctrl+F9 and --resume load
                                 it from (by default one for each program, in a folder states
                                 beside the saves)
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
              Ctrl+F5 saves the whole machine's state, Ctrl+F9 loads it back
            """);
    }

    private static string Version()
        => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    private static void Error(string message) => Console.Error.WriteLine($"confederation: {message}");
}
