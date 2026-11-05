using System.Diagnostics;
using Confederation.Core;
using SDL;
using static SDL.SDL3;

namespace Confederation.Player.Sdl;

internal sealed class PlayerOptions
{
    public string Title { get; init; } = "Confederation";

    /// <summary>Integer scale for the window's first size; it can be resized afterwards.</summary>
    public int Scale { get; init; } = 3;

    public bool Fullscreen { get; init; }

    public bool SquarePixels { get; init; }

    /// <summary>Whether gamepads are the joystick on the game port; when false the port is empty.</summary>
    public bool Joystick { get; init; } = true;
}

/// <summary>
/// Plays the machine in a window, a VGA frame at a time: a frame's worth of the machine's time
/// (1/70th of a second in the 256-color modes), then the picture, then a wait for the next
/// frame's moment on the host's clock. Keyboard and gamepads are read between frames.
/// </summary>
/// <remarks>
/// The machine's own sense of time comes from the instructions it runs, so the game always sees
/// the same pace; the player's job is only to hand it its instructions at the rate of real time.
/// If the host falls behind (the window was dragged, the machine was busy), the player carries on
/// from now rather than racing to catch up.
/// </remarks>
internal sealed unsafe class SdlPlayer : IDisposable
{
    // the frame rate of the VGA's 256-color modes, which the picture is shown at
    private const double FrameRate = 70.086;

    // the stick's centre is a little loose on most gamepads: inside this, it's centred
    private const float DeadZone = 0.08f;

    // at full speed, frames run between pictures for this long, in seconds of real time
    private const double WarpSlice = 1 / 60.0;

    private readonly Machine _machine;
    private readonly PlayerOptions _options;
    private readonly SdlWindow _window;

    // the PC keys held down now, by scan code and whether extended, to let go when focus is lost
    private readonly HashSet<(byte Code, bool Extended)> _held = [];
    private readonly Dictionary<SDL_JoystickID, nint> _gamepads = [];
    private readonly float[] _stick = new float[2];
    private readonly int[] _dpad = new int[2];
    private bool _quit;
    private bool _paused;
    private bool _warp;
    private long _frames;
    private long _instructions;

    public SdlPlayer(Machine machine, PlayerOptions options)
    {
        _machine = machine;
        _options = options;
        SdlException.ThrowIfFalse(
            SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_EVENTS | SDL_InitFlags.SDL_INIT_GAMEPAD),
            "SDL_Init");
        _window = new SdlWindow(options.Title, options.Scale, options.Fullscreen, options.SquarePixels);
    }

    /// <summary>
    /// Plays until the window is closed or the program ends, or for the emulated seconds given.
    /// Returns whether the program is still running.
    /// </summary>
    public bool Run(double? seconds = null)
    {
        long frameTicks = (long)(Stopwatch.Frequency / FrameRate);
        long next = Stopwatch.GetTimestamp();
        bool running = true;

        // gamepads already plugged in arrive as events: take them before the game looks for a joystick
        Pump();

        while (running && !_quit && (seconds is null || _machine.Time.TotalSeconds < seconds))
        {
            Pump();
            if (_paused)
            {
                _window.Redraw();
                SDL_Delay(16);
                next = Stopwatch.GetTimestamp();
                continue;
            }

            if (_warp)
            {
                // as many frames as fit in a slice of real time, then one picture
                long until = Stopwatch.GetTimestamp() + (long)(WarpSlice * Stopwatch.Frequency);
                do
                {
                    running = RunFrame();
                }
                while (running && Stopwatch.GetTimestamp() < until);

                Present();
                next = Stopwatch.GetTimestamp();
                continue;
            }

            running = RunFrame();
            Present();

            next += frameTicks;
            long wait = next - Stopwatch.GetTimestamp();
            if (wait > 0)
            {
                SDL_DelayPrecise((ulong)(wait * 1_000_000_000L / Stopwatch.Frequency));
            }
            else if (-wait > frameTicks * 5)
            {
                next = Stopwatch.GetTimestamp();
            }
        }

        return running;
    }

    public void Dispose()
    {
        foreach (var gamepad in _gamepads.Values)
        {
            SDL_CloseGamepad((SDL_Gamepad*)gamepad);
        }

        _window.Dispose();
        SDL_Quit();
    }

    /// <summary>
    /// One frame of the machine's time. The instructions are counted from the start, so the
    /// fractions of an instruction a frame leaves over add up rather than being lost.
    /// </summary>
    private bool RunFrame()
    {
        _frames++;
        long due = (long)(_frames * (double)_machine.InstructionsPerSecond / FrameRate);
        bool running = _machine.Run(due - _instructions);
        _instructions = due;
        return running;
    }

    private void Present() => _window.Present(_machine.DisplayWidth, _machine.DisplayHeight, _machine.Render);

    private void Pump()
    {
        SDL_Event sdlEvent;
        while (SDL_PollEvent(&sdlEvent))
        {
            switch ((SDL_EventType)sdlEvent.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                    _quit = true;
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                case SDL_EventType.SDL_EVENT_WINDOW_EXPOSED:
                    _window.Redraw();
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                    // the keys up go to another window: let go of every key held, or one (Alt,
                    // after Alt+Tab) stays down in the game
                    foreach (var (code, extended) in _held)
                    {
                        _machine.ReleaseKey(code, extended);
                    }

                    _held.Clear();
                    break;

                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                case SDL_EventType.SDL_EVENT_KEY_UP:
                    Key(sdlEvent.key);
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                    var gamepad = SDL_OpenGamepad(sdlEvent.gdevice.which);
                    if (gamepad is not null)
                    {
                        _gamepads[sdlEvent.gdevice.which] = (nint)gamepad;
                    }

                    UpdateJoystick();
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                    if (_gamepads.Remove(sdlEvent.gdevice.which, out var removed))
                    {
                        SDL_CloseGamepad((SDL_Gamepad*)removed);
                    }

                    UpdateJoystick();
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                    GamepadButton((SDL_GamepadButton)sdlEvent.gbutton.button, sdlEvent.gbutton.down);
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                    GamepadAxis((SDL_GamepadAxis)sdlEvent.gaxis.axis, sdlEvent.gaxis.value);
                    break;
            }
        }
    }

    private void Key(SDL_KeyboardEvent key)
    {
        // the player's own keys, which the game never sees: F11 and F12 came after these games
        // were written, and Pause is the one key a game can't read
        switch (key.scancode)
        {
            case SDL_Scancode.SDL_SCANCODE_F11:
                if (key.down && !key.repeat)
                {
                    if ((key.mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0)
                    {
                        _window.ToggleSquarePixels();
                    }
                    else
                    {
                        _window.ToggleFullscreen();
                    }
                }

                return;
            case SDL_Scancode.SDL_SCANCODE_F12:
                if (key.down && !key.repeat)
                {
                    _warp = !_warp;
                    ShowStatus();
                }

                return;
            case SDL_Scancode.SDL_SCANCODE_PAUSE:
                if (key.down && !key.repeat)
                {
                    _paused = !_paused;
                    ShowStatus();
                }

                return;
        }

        if (!ScanCodes.TryFromUsb((int)key.scancode, out byte code, out bool extended))
        {
            return;
        }

        if (key.down)
        {
            // a key held down repeats its make code, as the PC's keyboard does by itself
            _held.Add((code, extended));
            _machine.PressKey(code, extended);
        }
        else if (_held.Remove((code, extended)))
        {
            _machine.ReleaseKey(code, extended);
        }
    }

    private void ShowStatus()
        => _window.ShowStatus(_paused ? "paused" : _warp ? "full speed" : null);

    /// <summary>The game port has a joystick while a gamepad is plugged in, unless the joystick is turned off.</summary>
    private void UpdateJoystick()
    {
        _machine.Joysticks = _options.Joystick && _gamepads.Count > 0 ? 1 : 0;
        _stick[0] = _stick[1] = 0;
        _dpad[0] = _dpad[1] = 0;
        for (int button = 0; button < 4; button++)
        {
            _machine.SetJoystickButton(button, false);
        }

        UpdateAxes();
    }

    /// <summary>
    /// The face buttons are the joystick's four (A's two, then B's two, as four-button sticks had
    /// them); the d-pad pushes the stick all the way, over the left stick.
    /// </summary>
    private void GamepadButton(SDL_GamepadButton button, bool down)
    {
        int index = button switch
        {
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => 0,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => 1,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => 2,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => 3,
            _ => -1,
        };

        if (index >= 0)
        {
            _machine.SetJoystickButton(index, down);
            return;
        }

        (int axis, int direction) = button switch
        {
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => (0, -1),
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => (0, 1),
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => (1, -1),
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => (1, 1),
            _ => (-1, 0),
        };

        if (axis >= 0)
        {
            if (down)
            {
                _dpad[axis] = direction;
            }
            else if (_dpad[axis] == direction)
            {
                _dpad[axis] = 0;
            }

            UpdateAxes();
        }
    }

    /// <summary>The left stick is the joystick, in proportion: these games were flown with an analog stick.</summary>
    private void GamepadAxis(SDL_GamepadAxis axis, short value)
    {
        int index = axis switch
        {
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => 0,
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => 1,
            _ => -1,
        };

        if (index >= 0)
        {
            float position = Math.Max(-1f, value / 32767f);
            _stick[index] = Math.Abs(position) < DeadZone ? 0 : position;
            UpdateAxes();
        }
    }

    private void UpdateAxes()
    {
        for (int axis = 0; axis < 2; axis++)
        {
            _machine.SetJoystickAxis(axis, _dpad[axis] != 0 ? _dpad[axis] : _stick[axis]);
        }
    }
}
