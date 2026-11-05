using System.Text.Json;
using System.Text.Json.Serialization;

namespace Confederation.Player;

/// <summary>
/// How a series is played, where it differs from the defaults: each value is optional, and one
/// given on the command line wins over it for that run.
/// </summary>
internal sealed class Profile
{
    /// <summary>The speed, in DOSBox's cycles: instructions a millisecond.</summary>
    public int? Cycles { get; set; }

    /// <summary>The music device, written into the game's own settings in the save folder.</summary>
    public Music? Music { get; set; }

    public bool? Sound { get; set; }

    public bool? Joystick { get; set; }

    public bool? Mouse { get; set; }

    public int? Scale { get; set; }

    public bool? Fullscreen { get; set; }

    public bool? SquarePixels { get; set; }

    /// <summary>
    /// Whether the games' own cheats are on: the player starts the program with the words that
    /// turn them on (see <see cref="Games.CheatWords"/>).
    /// </summary>
    public bool? Cheats { get; set; }

    /// <summary>Whether nothing is set.</summary>
    [JsonIgnore]
    public bool IsEmpty => Cycles is null && Music is null && Sound is null && Joystick is null && Mouse is null
        && Scale is null && Fullscreen is null && SquarePixels is null && Cheats is null;

    /// <summary>The values set in another profile, over this one's.</summary>
    public void Take(Profile other)
    {
        Cycles = other.Cycles ?? Cycles;
        Music = other.Music ?? Music;
        Sound = other.Sound ?? Sound;
        Joystick = other.Joystick ?? Joystick;
        Mouse = other.Mouse ?? Mouse;
        Scale = other.Scale ?? Scale;
        Fullscreen = other.Fullscreen ?? Fullscreen;
        SquarePixels = other.SquarePixels ?? SquarePixels;
        Cheats = other.Cheats ?? Cheats;
    }

    /// <summary>What's set, in a line: "music roland, scale 4".</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Cycles is { } cycles)
        {
            parts.Add($"cycles {cycles}");
        }

        if (Music is { } music)
        {
            parts.Add($"music {music.ToString().ToLowerInvariant()}");
        }

        void Flag(bool? value, string name)
        {
            if (value is { } on)
            {
                parts.Add(on ? name : $"no {name}");
            }
        }

        Flag(Sound, "sound");
        Flag(Joystick, "joystick");
        Flag(Mouse, "mouse");
        if (Scale is { } scale)
        {
            parts.Add($"scale {scale}");
        }

        Flag(Fullscreen, "full screen");
        Flag(SquarePixels, "square pixels");
        Flag(Cheats, "cheats");
        return parts.Count == 0 ? "the defaults" : string.Join(", ", parts);
    }
}

/// <summary>
/// The player's settings, in one file in the user's application data (never in a game's folder,
/// which is only read): where each series is, where the MT-32's ROMs are, and each series'
/// profile. It's plain JSON, to be read and edited by hand as well as by the player.
/// </summary>
/// <remarks>
/// Profiles are per series rather than per program: a game and its mission disks share a folder,
/// the configuration file the game's installer wrote, and their saves, so they share how they're
/// played too.
/// </remarks>
internal sealed class Settings
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Where the settings are unless told otherwise.</summary>
    public static string DefaultPath => System.IO.Path.Combine(Games.Home, "confederation.json");

    /// <summary>The folder of the MT-32's ROMs, if one has been given.</summary>
    public string? Mt32Roms { get; set; }

    /// <summary>Each series' folder, by the series' key.</summary>
    public Dictionary<string, string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each series' profile, by the series' key.</summary>
    public Dictionary<string, Profile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The file they're kept in.</summary>
    [JsonIgnore]
    public string Path { get; private set; } = DefaultPath;

    /// <summary>The settings in a file; empty ones when there's no file yet.</summary>
    /// <exception cref="InvalidDataException">The file isn't settings the player can read.</exception>
    public static Settings Load(string path)
    {
        Settings settings;
        if (File.Exists(path))
        {
            try
            {
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new Settings();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"the settings in {path} can't be read: {ex.Message}");
            }

            // names are matched without regard to case, as on the command line
            settings.Folders = new(settings.Folders ?? [], StringComparer.OrdinalIgnoreCase);
            settings.Profiles = new(settings.Profiles ?? [], StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            settings = new Settings();
        }

        settings.Path = path;
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, Json) + Environment.NewLine);
    }

    /// <summary>A series' folder, if it's known and still there.</summary>
    public string? FolderOf(Series series)
        => Folders.TryGetValue(series.Key, out var folder) && Directory.Exists(folder) ? folder : null;

    /// <summary>Remembers where a series is. Returns whether that changed anything.</summary>
    public bool Remember(Series series, string folder)
    {
        folder = System.IO.Path.GetFullPath(folder);
        if (Folders.TryGetValue(series.Key, out var known) && string.Equals(known, folder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Folders[series.Key] = folder;
        return true;
    }

    /// <summary>A series' profile; an empty one when none is kept.</summary>
    public Profile ProfileOf(Series series) => Profiles.TryGetValue(series.Key, out var profile) ? profile : new Profile();
}
