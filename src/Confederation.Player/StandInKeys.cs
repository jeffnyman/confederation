namespace Confederation.Player;

/// <summary>
/// Keys the player hands the game in place of others: with Alt held, a key in the table goes to
/// the game as the key it stands in for. The cheats use it, so Alt+Q is the game's Alt+Del, keys
/// next to Alt rather than across the keyboard. The stand-in lasts as long as the key is held, so
/// letting go of it lets go of the key it stood in for, whether Alt was let go first or not.
/// </summary>
internal sealed class StandInKeys(IReadOnlyDictionary<byte, (byte Code, bool Extended)> table)
{
    private readonly Dictionary<(byte Code, bool Extended), (byte Code, bool Extended)> _down = [];

    public static StandInKeys None { get; } = new(new Dictionary<byte, (byte, bool)>());

    /// <summary>A key pressed, or repeating: the key the game is to see.</summary>
    public (byte Code, bool Extended) Down(byte code, bool extended, bool alt)
    {
        if (_down.TryGetValue((code, extended), out var standIn))
        {
            return standIn;
        }

        if (alt && !extended && table.TryGetValue(code, out standIn))
        {
            _down[(code, extended)] = standIn;
            return standIn;
        }

        return (code, extended);
    }

    /// <summary>A key let go: the key the game is to see let go.</summary>
    public (byte Code, bool Extended) Up(byte code, bool extended)
        => _down.Remove((code, extended), out var standIn) ? standIn : (code, extended);

    /// <summary>Forgets the keys held, when they've all been let go another way.</summary>
    public void Clear() => _down.Clear();
}
