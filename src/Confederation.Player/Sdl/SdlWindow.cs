using SDL;
using static SDL.SDL3;

namespace Confederation.Player.Sdl;

/// <summary>
/// The window: the machine's picture in an SDL surface of 32-bit pixels, scaled onto the window's
/// surface. No GPU renderer is involved, which keeps the path simple and exact. The window can be
/// resized and switched to full screen; the picture keeps its shape, centred with black around it.
/// </summary>
/// <remarks>
/// The VGA's 320 by 200 filled a 4:3 monitor, so its pixels were 1.2 times as tall as they were
/// wide; the picture is shown at 4:3 whatever its size in pixels, unless square pixels are asked
/// for. The size can change as the game changes mode, and the surface follows it.
/// </remarks>
internal sealed unsafe class SdlWindow : IDisposable
{
    private const float MonitorAspect = 4f / 3f;

    private readonly string _title;
    private SDL_Window* _window;
    private SDL_Surface* _frame;
    private int _width, _height;

    public SdlWindow(string title, int scale, bool fullscreen, bool squarePixels)
    {
        _title = title;
        SquarePixels = squarePixels;
        var flags = SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        if (fullscreen)
        {
            flags |= SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
        }

        // sized for mode 13h, the games' own
        scale = Math.Max(1, scale);
        int height = squarePixels ? 200 * scale : (int)(320 * scale / MonitorAspect);
        _window = SdlException.ThrowIfNull(SDL_CreateWindow(title, 320 * scale, height, flags), "SDL_CreateWindow");
        SdlException.ThrowIfFalse(SDL_SetWindowMinimumSize(_window, 320, 200), "SDL_SetWindowMinimumSize");
        Resize(320, 200);
    }

    public bool IsFullscreen => (SDL_GetWindowFlags(_window) & SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;

    /// <summary>Shows the picture's pixels square, rather than in the 4:3 shape a monitor gave them.</summary>
    public bool SquarePixels { get; private set; }

    /// <summary>
    /// Copies a picture into the window: <paramref name="width"/> by <paramref name="height"/>
    /// pixels of ARGB, row by row, drawn by <paramref name="render"/> straight into the surface.
    /// </summary>
    public void Present(int width, int height, RenderAction render)
    {
        if (width != _width || height != _height)
        {
            Resize(width, height);
        }

        SdlException.ThrowIfFalse(SDL_LockSurface(_frame), "SDL_LockSurface");
        try
        {
            // the surface's rows can be padded; the machine draws unpadded rows, so pad them here
            int pitch = _frame->pitch / sizeof(uint);
            var pixels = new Span<uint>((void*)_frame->pixels, pitch * height);
            if (pitch == width)
            {
                render(pixels[..(width * height)]);
            }
            else
            {
                uint[] rows = new uint[width * height];
                render(rows);
                for (int y = height - 1; y >= 0; y--)
                {
                    rows.AsSpan(y * width, width).CopyTo(pixels.Slice(y * pitch, width));
                }
            }
        }
        finally
        {
            SDL_UnlockSurface(_frame);
        }

        Redraw();
    }

    /// <summary>Paints the last picture onto the window again, scaled to fit.</summary>
    public void Redraw()
    {
        var windowSurface = SDL_GetWindowSurface(_window);
        if (windowSurface is null)
        {
            // minimized, on some systems
            return;
        }

        var target = Fit(windowSurface->w, windowSurface->h);
        if (target.x > 0 || target.y > 0)
        {
            SdlException.ThrowIfFalse(SDL_FillSurfaceRect(windowSurface, null, SDL_MapSurfaceRGB(windowSurface, 0, 0, 0)), "SDL_FillSurfaceRect");
        }

        SdlException.ThrowIfFalse(
            SDL_BlitSurfaceScaled(_frame, null, windowSurface, &target, SDL_ScaleMode.SDL_SCALEMODE_NEAREST),
            "SDL_BlitSurfaceScaled");
        SdlException.ThrowIfFalse(SDL_UpdateWindowSurface(_window), "SDL_UpdateWindowSurface");
    }

    /// <summary>Switches between a window and the full screen.</summary>
    public void ToggleFullscreen() => SdlException.ThrowIfFalse(SDL_SetWindowFullscreen(_window, !IsFullscreen), "SDL_SetWindowFullscreen");

    /// <summary>Switches between square pixels and the monitor's shape, and repaints.</summary>
    public void ToggleSquarePixels()
    {
        SquarePixels = !SquarePixels;
        Redraw();
    }

    /// <summary>Shows a short status, such as "paused", after the title in the title bar; null clears it.</summary>
    public void ShowStatus(string? status) => SDL_SetWindowTitle(_window, status is null ? _title : $"{_title}  ({status})");

    public void Dispose()
    {
        if (_frame is not null)
        {
            SDL_DestroySurface(_frame);
            _frame = null;
        }

        if (_window is not null)
        {
            SDL_DestroyWindow(_window);
            _window = null;
        }
    }

    private void Resize(int width, int height)
    {
        if (_frame is not null)
        {
            SDL_DestroySurface(_frame);
        }

        _frame = SdlException.ThrowIfNull(SDL_CreateSurface(width, height, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888), "SDL_CreateSurface");
        _width = width;
        _height = height;
    }

    /// <summary>The largest rectangle of the picture's shape that fits the window, centred.</summary>
    private SDL_Rect Fit(int windowWidth, int windowHeight)
    {
        float aspect = SquarePixels ? (float)_width / _height : MonitorAspect;
        float scale = Math.Min(windowWidth / aspect, windowHeight);
        int w = Math.Max(1, (int)(aspect * scale));
        int h = Math.Max(1, (int)scale);
        return new SDL_Rect { x = (windowWidth - w) / 2, y = (windowHeight - h) / 2, w = w, h = h };
    }
}

/// <summary>Draws a picture into the pixels given, row by row.</summary>
internal delegate void RenderAction(Span<uint> pixels);
