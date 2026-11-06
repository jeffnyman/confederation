# Profiles and the menu

The games can now be chosen in one place, and each is played the way it was last set up. That
means short names on the command line (`confederation wc2`, `confederation so1`), a menu in a
window when no game is named, and one settings file that remembers where each game is and how
it's played. Save states, the other half of this step on the roadmap, are a step of their own.

The code is the player's: `Games.cs`, `Settings.cs`, `Program.cs` and `Sdl/SdlMenu.cs`.

## A series, not a program

The first question was what a profile belongs to. Each GOG folder holds a main game and its
mission disks:

| Folder | Programs | Short names |
|---|---|---|
| `Wing Commander` | `WC.EXE`, `SM2.EXE` | `wc1`, `sm1`, `sm2` |
| `Wing Commander II` | `WC2.EXE`, `SO1.EXE`, `SO2.EXE` | `wc2`, `so1`, `so2` |

The programs in a folder share more than the folder. They read the same configuration file, the
one the game's installer wrote (`WINGCMDR.CFG` or `WC2.CFG`, where the music device is chosen).
They share their saves, as on a real disk, because a mission disk carries on with the main game's
pilots. And GOG runs them at the same speed. So a profile belongs to the **series**: a main game
and its mission disks together. Setting *Wing Commander II* to the Roland sets *Special
Operations* to it too, as running its installer would have.

*The Secret Missions* (the first) has no program of its own. Once installed, it's a choice in
*Wing Commander*'s campaign menu, so `sm1` starts `WC.EXE`, and the player says where to find it.

## The settings file

Everything is in one file, `confederation.json` in the user's application data, beside the saves
and the MT-32's ROMs (`%APPDATA%\Confederation` on Windows). It's never in a game's folder, which
the player only reads. It's plain JSON, meant to be read and edited by hand as well as by the
player:

```json
{
  "mt32Roms": "D:\\Roland\\roms",
  "folders": {
    "wc1": "D:\\Games\\Wing Commander",
    "wc2": "D:\\Games\\Wing Commander II"
  },
  "profiles": {
    "wc2": {
      "music": "roland",
      "scale": 4
    }
  }
}
```

A profile keeps only what differs from the defaults: speed (`cycles`), `music`, `sound`,
`joystick`, `mouse`, `mouseSpeed`, `scale`, `fullscreen`, `squarePixels` and `cheats` ([cheats](cheats.md)). Anything not in it is the default,
so a new default reaches every game that hasn't chosen otherwise.

**What's remembered by itself, and what isn't.** A game's folder is remembered the first time it's
played by its folder, so after `confederation "D:\Games\Wing Commander II"` once, `confederation
wc2` finds it. The MT-32's ROM folder is remembered once given. Options aren't: `--scale 4` is for
one run, and `--remember` keeps the run's options in the series' profile, added to what's there.
`--forget` goes back to the defaults. Options always win over the profile for the run they're
given in, so a profile never stops an experiment. To undo a remembered "no", the options come in
pairs: `--sound` and `--no-sound`, `--joystick`, `--mouse`, `--windowed`, `--no-square-pixels`.

The music device is the exception, as it was before. The player sets it by writing the game's own
configuration file into the save folder ([the sound](the-sound.md#which-letter-is-the-roland)), and
it stays there, profile or not, until it's set again.

`confederation --list` shows all of this: the settings file, the ROMs, each series' folder and
profile, and its games.

## The menu

With no game named, the player opens a window listing every game it knows. The ones whose
folders are known are bright and can be played: Up and Down and Enter, or a click. The rest are
dim. **Dropping a game's folder on the window** sets up its series: the player recognizes the
folder by its main program and remembers it, as the command line would. Esc or closing the window
quits. The game then opens in a window of its own, as from the command line.

The text is SDL's own built-in 8 by 8 font (`SDL_RenderDebugText`), drawn at twice its size by a
software renderer straight into the window's picture. That's enough for a list, and needs no font
file or text library: the menu is a convenience, not a front end.

## Checking it

The player's tests (`tests/Confederation.Player.Tests`) need no game. A temporary folder holds a
stand-in `WC.EXE`, a few bytes that end at once, which is enough to be recognized and run:

- **Settings** start empty when there's no file, are written and read back with their names
  matched without regard to case, and say which file is wrong when one can't be read.
- **Profiles** take only what another sets, and describe themselves for `--list`.
- **The short names**, and a folder recognized by its main program.
- **The command line, run for real**: a short name isn't playable until its folder has been
  played once; then it is, and `sm1` with it, but not `sm2` with no `SM2.EXE`. Options are kept
  only with `--remember`, added to, and cleared by `--forget`, which keeps the folder.
- **The menu** is drawn once with SDL's "dummy" video driver, which keeps a window's picture in
  memory with nothing on screen, and saved as `menu.bmp`. The test checks it has the colors of the
  background, the bar and the text.
