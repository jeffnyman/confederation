# Project layout

Confederation's repository is laid out the same way as Retrograde's, its C64 sibling. This note
explains the layout and why each part is there. These choices aren't specific to emulators and are
worth copying into any project like this.

## Two projects: a core and a player

```
src/Confederation.Core     the emulated machine: a library, with no user interface
src/Confederation.Player   the program you run: a window, sound, input, and the per-game settings
tests/Confederation.Core.Tests
```

The **core** is the PC: CPU, memory, devices, the DOS layer. It takes input as method calls and
produces output as a framebuffer and audio samples. It never opens a window, reads a gamepad or
plays a sound.

The **player** wraps the core in a real program, using SDL3 for the window, audio and input as
Retrograde does.

Splitting the two makes the core *testable without a screen*. A test can load a game, run it for a
number of emulated frames, feed it keystrokes, and compare the framebuffer against a known image,
all in a CI job with no display. Most of an emulator's bugs show up this way. The split also lets
other programs use the core: a debugger, a tracer, a tool that documents the engine.

## The core's public surface is tracked

`Confederation.Core` uses the Microsoft public API analyzer. Every public type and member is listed
in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and the build fails if the code and the
lists disagree. A change to the library's surface then shows up as a change to those files in a
diff, where it's easy to review. Every public member must also carry an XML documentation comment,
or the build fails.

## Strict builds

`Directory.Build.props` applies to every project:

- **Warnings are errors.** A new warning fails the build, so warnings never pile up.
- **Nullable reference types** are on.
- **Package versions are central**, in `Directory.Packages.props`, so all projects use the same
  versions.
- **The core is optimized even in Debug builds.** An emulator's CPU loop runs millions of times a
  second, and an unoptimized build can be too slow to reach the parts of a game you want to debug.

## Tests that need the games

The games themselves never go in the repository (`.gitignore` blocks the common file types).
Tests that need them will look for the game files in a known place and pass without checking
when they aren't there. So CI on GitHub runs everything that doesn't need copyrighted data, and a
local run with the games present runs everything.

## Continuous integration

`.github/workflows/build.yml` builds, tests and packs on Windows, Linux and macOS on every push to
`main` and every pull request, the same as Retrograde. macOS is advisory, because GitHub's hosted
Macs are sometimes unavailable.
