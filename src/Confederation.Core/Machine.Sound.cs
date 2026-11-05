using Confederation.Core.Sound;
using Confederation.Core.Sound.Mt32;

namespace Confederation.Core;

// Sound: the devices that make it, mixed in step with the machine's time.
public sealed partial class Machine
{
    /// <summary>
    /// The sample rate of the machine's sound, in samples a second: 44,100, as GOG's DOSBox
    /// settings for both games use.
    /// </summary>
    public const int SampleRate = 44_100;

    // a second of sound waiting to be read; past that the oldest goes, as no one is listening
    private const int OutputCapacity = SampleRate * 2;

    private readonly Opl _opl = new(SampleRate);
    private Mpu401 _mpu = null!;
    private Mt32Module? _mt32;
    private readonly List<ISoundSource> _sources = [];
    private readonly short[] _output = new short[OutputCapacity];
    private int[] _mix = new int[1024];
    private int _outputStart, _outputCount;

    // the sample frames made so far, from the start
    private long _samples;

    /// <summary>
    /// Takes the sound made so far and not yet read: 16-bit samples, left and right
    /// interleaved, at <see cref="SampleRate"/>. The machine keeps the last second of sound
    /// for reading, and drops what's older.
    /// </summary>
    /// <returns>The number of samples written (twice the number of frames).</returns>
    public int ReadSamples(Span<short> samples)
    {
        int count = Math.Min(samples.Length & ~1, _outputCount);
        for (int i = 0; i < count; i++)
        {
            samples[i] = _output[(_outputStart + i) % OutputCapacity];
        }

        _outputStart = (_outputStart + count) % OutputCapacity;
        _outputCount -= count;
        return count;
    }

    /// <summary>
    /// The MT-32 plugged into the MPU-401, as in "CM-32L/LAPC-I Control v1.02"; null when none is.
    /// </summary>
    public string? Mt32 => _mt32?.Description;

    /// <summary>What the MT-32's display shows, or null without one.</summary>
    public string? Mt32Display => _mt32?.Display;

    /// <summary>
    /// Plugs a Roland MT-32 into the MPU-401, made from the ROMs found in a folder: a CM-32L's if
    /// they're there, as the games were written for it, otherwise an MT-32's. The files are
    /// recognized by their contents, whatever they're called.
    /// </summary>
    /// <returns>The module emulated, as in "CM-32L/LAPC-I Control v1.02".</returns>
    /// <exception cref="DirectoryNotFoundException">The folder isn't there.</exception>
    /// <exception cref="InvalidDataException">No control ROM and PCM ROM that go together are in it.</exception>
    public string UseMt32(string romFolder)
    {
        var roms = Mt32RomSet.FromDirectory(romFolder)
            ?? throw new InvalidDataException($"no MT-32 or CM-32L control and PCM ROMs that go together are in {romFolder}");
        if (_mt32 is not null)
        {
            _sources.Remove(_mt32);
        }

        UpdateSound();
        _mt32 = new Mt32Module(roms, SampleRate, line => Log?.Invoke(line));
        _sources.Add(_mt32);
        _mpu.Output = _mt32;
        return _mt32.Description;
    }

    private void InstallSound()
    {
        _sources.Add(_opl);
        _mpu = new Mpu401(line => Log?.Invoke(line));
    }

    /// <summary>The machine's time in seconds, as precisely as the instruction count gives it.</summary>
    private double Seconds => (double)_time / InstructionsPerSecond;

    /// <summary>
    /// Makes the sound for the time up to now. Called before a device's sound changes, so each
    /// change lands at the sample it happened at, and when a run ends.
    /// </summary>
    private void UpdateSound()
    {
        long due = (long)((double)_time * SampleRate / InstructionsPerSecond);
        int frames = (int)Math.Min(due - _samples, int.MaxValue / 4);
        if (frames <= 0)
        {
            return;
        }

        _samples += frames;
        if (_mix.Length < frames * 2)
        {
            _mix = new int[frames * 2];
        }

        var mix = _mix.AsSpan(0, frames * 2);
        mix.Clear();
        foreach (var source in _sources)
        {
            source.Render(mix);
        }

        foreach (int sample in mix)
        {
            short clipped = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
            if (_outputCount == OutputCapacity)
            {
                // nobody is reading: the oldest goes
                _outputStart = (_outputStart + 1) % OutputCapacity;
                _outputCount--;
            }

            _output[(_outputStart + _outputCount) % OutputCapacity] = clipped;
            _outputCount++;
        }
    }
}
