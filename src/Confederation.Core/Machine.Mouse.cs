namespace Confederation.Core;

// The mouse: a Microsoft-compatible mouse driver answering INT 33h, and the mouse it reads.
public sealed partial class Machine
{
    private const int MouseButtons = 2;

    private bool _mouse;
    private double _mouseX, _mouseY;
    private int _mouseMinX, _mouseMaxX, _mouseMinY, _mouseMaxY;
    private int _mickeysPer8X, _mickeysPer8Y;
    private int _mickeyCountX, _mickeyCountY;
    private int _mouseButtons;
    private int _mouseHidden;
    private readonly int[] _presses = new int[MouseButtons], _releases = new int[MouseButtons];
    private readonly (int X, int Y)[] _lastPress = new (int, int)[MouseButtons], _lastRelease = new (int, int)[MouseButtons];

    /// <summary>
    /// Whether a mouse and its driver are installed. Without one, <c>INT 33h</c> reports no mouse,
    /// as on a PC with none; the games then steer their pointers by keyboard or joystick. Set it
    /// before the program starts: games look for a mouse once.
    /// </summary>
    public bool Mouse
    {
        get => _mouse;
        set
        {
            _mouse = value;
            ResetMouse();
        }
    }

    /// <summary>
    /// The mouse moves, by a number of "mickeys", the mouse's own steps (a two hundredth of an
    /// inch on Microsoft's mouse): right and down are positive.
    /// </summary>
    public void MoveMouse(int dx, int dy)
    {
        if (!_mouse)
        {
            return;
        }

        _mickeyCountX += dx;
        _mickeyCountY += dy;

        // the driver turns mickeys into its pixels by its ratio: by default 8 to 8 across and
        // 16 to 8 down, so the pointer moves half as fast down as across
        _mouseX = Math.Clamp(_mouseX + (dx * 8.0 / _mickeysPer8X), _mouseMinX, _mouseMaxX);
        _mouseY = Math.Clamp(_mouseY + (dy * 8.0 / _mickeysPer8Y), _mouseMinY, _mouseMaxY);
    }

    /// <summary>A mouse button goes down or up: 0 the left, 1 the right.</summary>
    public void SetMouseButton(int button, bool down)
    {
        if (!_mouse || button is < 0 or >= MouseButtons)
        {
            return;
        }

        int bit = 1 << button;
        bool wasDown = (_mouseButtons & bit) != 0;
        if (down == wasDown)
        {
            return;
        }

        _mouseButtons ^= bit;
        if (down)
        {
            _presses[button]++;
            _lastPress[button] = MousePosition;
        }
        else
        {
            _releases[button]++;
            _lastRelease[button] = MousePosition;
        }
    }

    private (int X, int Y) MousePosition => ((int)_mouseX, (int)_mouseY);

    /// <summary>The driver's state after a reset: the pointer hidden, in the middle of a 640 by 200 screen.</summary>
    private void ResetMouse()
    {
        _mouseMinX = 0;
        _mouseMaxX = 639;
        _mouseMinY = 0;
        _mouseMaxY = 199;
        _mouseX = 320;
        _mouseY = 100;
        _mickeysPer8X = 8;
        _mickeysPer8Y = 16;
        _mickeyCountX = _mickeyCountY = 0;
        _mouseHidden = 1;
        Array.Clear(_presses);
        Array.Clear(_releases);
    }

    /// <summary>
    /// INT 33h, the mouse driver, as Microsoft's defined it and DOSBox's <c>mouse.cpp</c> answers
    /// it: reset, the pointer's position and buttons, presses and releases counted since last
    /// asked, the area the pointer is kept in, and the motion in mickeys.
    /// </summary>
    /// <remarks>
    /// The driver keeps the pointer in its own coordinates, which in the 320 by 200 graphics
    /// modes are 640 by 200: a program halves the column. It draws no pointer of its own here;
    /// the games draw theirs.
    /// </remarks>
    private void MouseService()
    {
        int function = (ushort)Cpu.Eax;
        if (!_mouse)
        {
            Trace($"33/{function:X4} no mouse");
            if (function == 0)
            {
                SetAx(0);
            }

            return;
        }

        var (x, y) = MousePosition;
        switch (function)
        {
            case 0x00:
            case 0x21:
                // reset: present, two buttons
                ResetMouse();
                SetAx(0xFFFF);
                SetBx(MouseButtons);
                break;
            case 0x01:
                _mouseHidden = Math.Max(0, _mouseHidden - 1);
                break;
            case 0x02:
                _mouseHidden++;
                break;
            case 0x03:
                SetBx(_mouseButtons);
                SetCx(x);
                SetDx(y);
                break;
            case 0x04:
                _mouseX = Math.Clamp((short)Cpu.Ecx, _mouseMinX, _mouseMaxX);
                _mouseY = Math.Clamp((short)Cpu.Edx, _mouseMinY, _mouseMaxY);
                break;
            case 0x05:
            case 0x06:
            {
                // presses (5) or releases (6) of a button since last asked, and where the last was
                int button = Math.Clamp((int)(ushort)Cpu.Ebx, 0, MouseButtons - 1);
                var counts = function == 0x05 ? _presses : _releases;
                var (atX, atY) = function == 0x05 ? _lastPress[button] : _lastRelease[button];
                SetAx(_mouseButtons);
                SetBx(counts[button]);
                SetCx(atX);
                SetDx(atY);
                counts[button] = 0;
                break;
            }

            case 0x07:
                (_mouseMinX, _mouseMaxX) = Range((short)Cpu.Ecx, (short)Cpu.Edx);
                _mouseX = Math.Clamp(_mouseX, _mouseMinX, _mouseMaxX);
                break;
            case 0x08:
                (_mouseMinY, _mouseMaxY) = Range((short)Cpu.Ecx, (short)Cpu.Edx);
                _mouseY = Math.Clamp(_mouseY, _mouseMinY, _mouseMaxY);
                break;
            case 0x0B:
                // the motion since last asked, in mickeys
                SetCx(_mickeyCountX);
                SetDx(_mickeyCountY);
                _mickeyCountX = _mickeyCountY = 0;
                break;
            case 0x0F:
                _mickeysPer8X = Math.Max(1, (int)(ushort)Cpu.Ecx);
                _mickeysPer8Y = Math.Max(1, (int)(ushort)Cpu.Edx);
                break;
            case 0x24:
                // the driver's version (8.05) and kind (a PS/2 mouse, on its own IRQ)
                SetBx(0x0805);
                SetCx(0x0400);
                break;
            default:
                Trace($"33/{function:X4} is not done");
                break;
        }

        static (int Min, int Max) Range(int a, int b) => a <= b ? (a, b) : (b, a);
    }
}
