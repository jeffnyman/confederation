using Confederation.Core.Devices;
using Confederation.Core.Dos;
using Confederation.Core.Processors;

namespace Confederation.Core;

/// <summary>
/// The PC a game runs on: a 386 in real mode, memory, the I/O ports, and a BIOS, DOS and
/// expanded-memory manager answered in C# instead of loaded as code.
/// </summary>
/// <remarks>
/// <para>
/// The memory map is the PC's. The first 640 KB is conventional memory, holding the interrupt
/// table, the BIOS data area, DOS's memory arena and the program. The video memory sits at
/// A0000h, expanded memory's 64 KB page frame at E0000h, and the BIOS at F0000h. The 64 KB above
/// 1 MB (the high memory area) is reachable only while the A20 gate is open; while it's closed,
/// addresses wrap at 1 MB as on the 8086.
/// </para>
/// <para>
/// Nothing in the machine is loaded from a real BIOS or DOS. Each interrupt they would answer
/// points at a four-byte stub in the BIOS area: the bytes <c>0F FF</c> and a service number,
/// which call into C# (see <see cref="Intel386.HostCall"/>), then <c>IRET</c>. A program sees
/// ordinary interrupt vectors, and can replace them or chain to them as it would on a real PC.
/// </para>
/// </remarks>
public sealed partial class Machine : ICpuBus
{
    /// <summary>The segment of the BIOS area, where the service stubs live.</summary>
    internal const ushort BiosSegment = 0xF000;

    private const uint HighMemoryEnd = 0x11_0000;
    private const uint PageFrame = 0xE_0000;
    private const uint VideoMemory = 0xA_0000;

    private readonly byte[] _memory = new byte[HighMemoryEnd];
    private readonly ExpandedMemory _ems;
    private readonly DosServices _dos;
    private readonly InterruptController _pic = new();
    private readonly IntervalTimer _timer = new();
    private readonly Vga _vga = new();
    private readonly KeyboardController _keyboard = new();
    private readonly GamePort _gamePort = new();

    // the time, counted in instructions, with those the processor would have run while halted
    private long _time;

    // when counter 0 next raises IRQ 0, in timer ticks
    private long _nextIrq0 = 0x10000;

    // port 61h: the speaker's gate and data bits as last written
    private byte _portB;

    /// <summary>
    /// Makes a machine that runs programs from a game's folder, mounted as drive C:. The folder
    /// is only read: anything a program writes, saved games included, goes to the save folder,
    /// and files there take the place of the game folder's from then on.
    /// </summary>
    public Machine(string gameFolder, string saveFolder)
    {
        Cpu = new Intel386(this) { HostCall = HostCall };
        _timer.Reloaded0 += () => _nextIrq0 = _timer.Start0 + _timer.Period0;
        _keyboard.Interrupt += () => _pic.Raise(1);
        _ems = new ExpandedMemory(this);
        _dos = new DosServices(this, new DosDrive(gameFolder, saveFolder));
        InstallBios();
        _ems.Install();
    }

    /// <summary>The processor.</summary>
    public Intel386 Cpu { get; }

    /// <summary>
    /// A line for each BIOS, DOS and EMS service a program calls, and each I/O port it uses that
    /// nothing answers yet. It's the record of what a game needs from the machine.
    /// </summary>
    public Action<string>? Log { get; set; }

    /// <summary>Whether the program has ended, by its DOS call or an <c>INT 20h</c>.</summary>
    public bool Exited { get; internal set; }

    /// <summary>The program's exit code, once it has ended.</summary>
    public int ExitCode { get; internal set; }

    /// <summary>The video mode the program last chose through the BIOS; 3, 80 by 25 text, at the start.</summary>
    public int VideoMode => _vga.Mode;

    /// <summary>The width of the picture the display shows, in pixels.</summary>
    public int DisplayWidth => _vga.Size.Width;

    /// <summary>The height of the picture the display shows, in pixels.</summary>
    public int DisplayHeight => _vga.Size.Height;

    /// <summary>
    /// Draws the picture the display shows, <see cref="DisplayWidth"/> by <see cref="DisplayHeight"/>
    /// pixels of 32-bit ARGB, row by row. Text modes are drawn black for now.
    /// </summary>
    public void Render(Span<uint> pixels) => _vga.Render(pixels);

    /// <summary>
    /// How fast the machine runs, in instructions per second. Time in the machine (the timer's
    /// count, and so the game's pace) follows the instructions run, so this is the emulated
    /// processor's speed. DOSBox's "cycles" setting is roughly this in thousands: GOG runs
    /// Wing Commander at 4000.
    /// </summary>
    public int InstructionsPerSecond { get; set; } = 4_000_000;

    /// <summary>The emulated time since the machine started.</summary>
    public TimeSpan Time => TimeSpan.FromSeconds((double)_time / InstructionsPerSecond);

    /// <summary>
    /// How many analog joysticks are plugged into the game port: 0, 1 or 2. Games look for them
    /// when they start, so this is best set before running.
    /// </summary>
    public int Joysticks
    {
        get => _gamePort.Sticks;
        set => _gamePort.Sticks = Math.Clamp(value, 0, 2);
    }

    /// <summary>
    /// Moves a joystick axis: 0 and 1 are the first stick's x and y, 2 and 3 the second's. The
    /// value runs from -1 (left, or up) through 0 (centred) to 1 (right, or down).
    /// </summary>
    public void SetJoystickAxis(int axis, float value) => _gamePort.SetAxis(axis & 3, value);

    /// <summary>Presses or lets go of a joystick button: 0 and 1 are the first stick's, 2 and 3 the second's.</summary>
    public void SetJoystickButton(int button, bool down) => _gamePort.SetButton(button & 3, down);

    /// <summary>Whether the A20 gate is open, so that addresses past 1 MB reach the high memory area.</summary>
    public bool A20 { get; set; }

    /// <summary>
    /// Loads a program from drive C: as DOS would, with a command tail, and readies the
    /// processor to run it.
    /// </summary>
    /// <param name="program">The program's name, as in <c>WC.EXE</c>.</param>
    /// <param name="arguments">What follows the program's name on the command line.</param>
    public void Load(string program, string arguments = "") => _dos.Load(program, arguments);

    /// <summary>
    /// Runs for the given number of instructions' time, or until the program exits or the
    /// processor halts with interrupts off, which nothing can end. A halted processor with
    /// interrupts on waits for the next one, and the time passes. A service the machine doesn't
    /// provide yet throws <see cref="NotSupportedException"/>.
    /// </summary>
    /// <returns>Whether the program is still running.</returns>
    public bool Run(long instructions)
    {
        long end = _time + instructions;
        while (!Exited && _time < end)
        {
            if (Cpu.Halted)
            {
                if (!Cpu.InterruptsEnabled)
                {
                    return false;
                }

                // nothing happens until the next interrupt, so the time skips to it
                long irq = (long)Math.Ceiling(_nextIrq0 * InstructionsPerSecond / IntervalTimer.Frequency);
                _time = Math.Clamp(irq, _time + 1, end);
            }
            else
            {
                Cpu.Step();
                _time++;
            }

            UpdateTimer();
            _keyboard.Update();
            if (_pic.HasInterrupt && Cpu.InterruptsEnabled)
            {
                Cpu.Interrupt(_pic.Acknowledge());
            }
        }

        return !Exited;
    }

    private long TimerTicks => (long)(_time * IntervalTimer.Frequency / InstructionsPerSecond);

    private void UpdateTimer()
    {
        long now = TimerTicks;
        _timer.Now = now;
        if (now >= _nextIrq0)
        {
            _pic.Raise(0);

            // if interrupts were off for a while, the ticks missed are lost, as on the PC
            long period = _timer.Period0;
            _nextIrq0 = _timer.Periodic0 ? _nextIrq0 + ((((now - _nextIrq0) / period) + 1) * period) : long.MaxValue;
        }
    }

    // ---- the bus -----------------------------------------------------------------------------

    /// <inheritdoc/>
    public byte Read(uint address)
    {
        address = Wrap(address);
        if (address - VideoMemory < 0x2_0000)
        {
            return _vga.Read(address);
        }

        if (address - PageFrame < 0x1_0000)
        {
            return _ems.Read(address - PageFrame);
        }

        return _memory[address];
    }

    /// <inheritdoc/>
    public void Write(uint address, byte value)
    {
        address = Wrap(address);
        if (address - VideoMemory < 0x2_0000)
        {
            _vga.Write(address, value);
        }
        else if (address - PageFrame < 0x1_0000)
        {
            _ems.Write(address - PageFrame, value);
        }
        else if (address < 0xF_0000 || address >= 0x10_0000)
        {
            // the BIOS is ROM
            _memory[address] = value;
        }
    }

    /// <inheritdoc/>
    public byte In(ushort port)
    {
        _timer.Now = TimerTicks;
        switch (port)
        {
            case 0x20:
            case 0x21:
                return _pic.Read(port);
            case 0xA1:
                return _pic.SecondMask;
            case >= 0x40 and <= 0x43:
                return _timer.Read(port);
            case 0x61:
                // port B: the speaker bits as written, memory refresh toggling every 15 us (which
                // programs time short delays by), and counter 2's output
                return (byte)((_portB & 0x0F) | (((_timer.Now / 18) & 1) != 0 ? 0x10 : 0) | (_timer.Output2 ? 0x20 : 0));
            case 0x60:
            case 0x64:
                return _keyboard.In(port);
            case 0x92:
                return (byte)(A20 ? 0x02 : 0x00);
            case 0x201:
                return _gamePort.Read(Time.TotalSeconds);
            case >= 0x3C0 and <= 0x3CF:
            case 0x3D4:
            case 0x3D5:
            case 0x3DA:
                _vga.Time = Time.TotalSeconds;
                return _vga.In(port);
            default:
                LogPort(port, "in");
                return 0xFF;
        }
    }

    /// <inheritdoc/>
    public void Out(ushort port, byte value)
    {
        _timer.Now = TimerTicks;
        switch (port)
        {
            case 0x20:
            case 0x21:
                _pic.Write(port, value);
                break;
            case 0xA0:
                break;
            case 0xA1:
                _pic.SecondMask = value;
                break;
            case >= 0x40 and <= 0x43:
                _timer.Write(port, value);
                break;
            case 0x61:
                _portB = value;
                _timer.Gate2 = (value & 1) != 0;
                break;
            case 0x60:
            case 0x64:
                _keyboard.Out(port, value);
                A20 = _keyboard.A20;
                break;
            case 0x92:
                // the fast A20 gate of later machines
                A20 = (value & 0x02) != 0;
                break;
            case 0x201:
                _gamePort.Write(Time.TotalSeconds);
                break;
            case >= 0x3C0 and <= 0x3CF:
            case 0x3D4:
            case 0x3D5:
                _vga.Out(port, value);
                break;
            default:
                LogPort(port, $"out {value:X2}");
                break;
        }
    }

    private readonly HashSet<ushort> _portsSeen = [];

    private void LogPort(ushort port, string what)
    {
        // each port once: a game polling one would otherwise fill the log
        if (_portsSeen.Add(port))
        {
            Log?.Invoke($"port {port:X4} {what}: nothing answers yet");
        }
    }

    private uint Wrap(uint address) => A20 ? (address < HighMemoryEnd ? address : address & 0xF_FFFF) : address & 0xF_FFFF;

    // ---- memory helpers for the services -----------------------------------------------------

    internal byte ReadByte(ushort segment, uint offset) => Read(((uint)segment << 4) + (offset & 0xFFFF));

    internal ushort ReadWord(ushort segment, uint offset) =>
        (ushort)(ReadByte(segment, offset) | (ReadByte(segment, offset + 1) << 8));

    internal void WriteByte(ushort segment, uint offset, byte value) => Write(((uint)segment << 4) + (offset & 0xFFFF), value);

    internal void WriteWord(ushort segment, uint offset, ushort value)
    {
        WriteByte(segment, offset, (byte)value);
        WriteByte(segment, offset + 1, (byte)(value >> 8));
    }

    /// <summary>Writes into the BIOS area, which programs can't.</summary>
    internal void WriteRom(uint address, ReadOnlySpan<byte> bytes) => bytes.CopyTo(_memory.AsSpan((int)address));

    /// <summary>A string ending in a zero byte, as DOS takes file names.</summary>
    internal string ReadString(ushort segment, uint offset, int max = 128)
    {
        var chars = new List<char>();
        for (int i = 0; i < max; i++)
        {
            byte b = ReadByte(segment, offset + (uint)i);
            if (b == 0)
            {
                break;
            }

            chars.Add((char)b);
        }

        return new string([.. chars]);
    }

    /// <summary>
    /// Writes bytes from a segment and offset onward, by linear address: a block longer than
    /// 64 KB, like a program's image, carries on past the segment's end instead of wrapping.
    /// </summary>
    internal void WriteBytes(ushort segment, uint offset, ReadOnlySpan<byte> bytes)
    {
        uint address = ((uint)segment << 4) + offset;
        for (int i = 0; i < bytes.Length; i++)
        {
            Write(address + (uint)i, bytes[i]);
        }
    }

    internal void Trace(string line) => Log?.Invoke(line);

    // ---- the services ------------------------------------------------------------------------

    /// <summary>
    /// Sets or clears carry in the flags an interrupt will return with. A service runs between the
    /// <c>INT</c> and the stub's <c>IRET</c>, which restores the flags the <c>INT</c> pushed, so a
    /// result reported in a flag has to be written into that pushed copy.
    /// </summary>
    internal void SetReturnFlag(uint flag, bool on)
    {
        uint sp = Cpu.Esp & 0xFFFF;
        ushort flags = ReadWord(Cpu.Ss, sp + 4);
        flags = on ? (ushort)(flags | flag) : (ushort)(flags & ~flag);
        WriteWord(Cpu.Ss, sp + 4, flags);
    }

    internal void SetReturnCarry(bool on) => SetReturnFlag(Intel386.Flag.Carry, on);

    internal void SetReturnZero(bool on) => SetReturnFlag(Intel386.Flag.Zero, on);

    /// <summary>
    /// Makes the interrupt run again when it returns, by moving the return address back over the
    /// two-byte <c>INT</c>. A service that must wait (for a key, say) does this, and the program
    /// keeps calling it, with interrupts able to come in between, until it can answer.
    /// </summary>
    internal void RepeatInterrupt()
    {
        uint sp = Cpu.Esp & 0xFFFF;
        WriteWord(Cpu.Ss, sp, (ushort)(ReadWord(Cpu.Ss, sp) - 2));
    }

    private void HostCall(byte service)
    {
        switch (service)
        {
            case 0x08: TimerTick(); break;
            case 0x09: KeyboardInterrupt(); break;
            case >= 0x0A and <= 0x0F: _pic.EndOfInterrupt(); break;
            case 0x10: VideoService(); break;
            case 0x11: Cpu.Eax = (Cpu.Eax & 0xFFFF_0000) | ReadWord(0x40, 0x10); break;
            case 0x12: Cpu.Eax = (Cpu.Eax & 0xFFFF_0000) | ReadWord(0x40, 0x13); break;
            case 0x15: SystemService(); break;
            case 0x16: KeyboardService(); break;
            case 0x1A: ClockService(); break;
            case 0x20: _dos.Terminate(0); break;
            case 0x21: _dos.Call(); break;
            case 0x2F: MultiplexService(); break;
            case 0x33: MouseService(); break;
            case 0x67: _ems.Call(); break;
            default: throw new NotSupportedException($"No service {service:X2}h.");
        }
    }
}
