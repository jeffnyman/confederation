# Confederation

**Confederation** is a C# DOS PC emulator built to play the *Wing Commander* games, not to be a
whole PC. You pick a game and it plays, at the speed it was meant to run, with modern controls and
the sound it was meant to have.

Under the hood it emulates as much of a DOS PC as these games use: an x86 CPU, VGA, the timer and
interrupt hardware, FM and MT-32 music, and Sound Blaster audio. DOS itself is answered directly
in C# instead of being booted.

**Confederation** is not a DOSBox fork. Its logic is ported from DOSBox where that helps, but
DOSBox is never wrapped or linked.

## Status

The CPU is done: a 386 in real mode that matches a real chip across 1.76 million captured test
instructions. DOS, the BIOS, expanded memory, the timer, the VGA, the keyboard and the joystick
port are answered well enough that *Wing Commander* plays in a window, by keyboard or gamepad,
with its music on an emulated AdLib or, given Roland's ROMs, an MT-32. *Wing Commander II* speaks
through an emulated Sound Blaster, and flies its first mission. *Wing Commander III* runs in
protected mode from its CD image, plays its opening movie with its sound, and goes about its
carrier by mouse. [ROADMAP.md](ROADMAP.md) lays out the order of work.

```
dotnet run --project src/Confederation.Player -c Release -- "<folder of Wing Commander>"
```

After that once, the game has a short name: `wc1`, `sm1` and `sm2` for *Wing Commander* and its
*Secret Missions*, `wc2`, `so1` and `so2` for *Wing Commander II* and its *Special Operations*,
`wc3` for *Wing Commander III*.
With no game named, a menu lists them, and a game's folder dropped on its window sets it up.
`--remember` keeps a run's options as the game's own, and `--list` shows what's set up.

The game's folder is only read; saves go to a folder of their own. A click in the window gives
the game the mouse (Ctrl+F10 takes it back), F11 is full screen, F12 full speed, Pause pauses,
Ctrl+F5 saves the whole machine's state and Ctrl+F9 loads it back (`--resume` starts from it),
`--cheats` turns on the games' own cheats, and `--help` lists the rest. `--music roland --mt32-roms <folder>` plays the
music on an emulated Roland MT-32 instead; its ROMs aren't included.

| Game | Tier |
|---|---|
| *Wing Commander*, with *The Secret Missions* 1 and 2 | First |
| *Wing Commander II*, with *Special Operations* 1 and 2 | First |
| *Wing Commander III: Heart of the Tiger* | First stretch |
| *Wing Commander IV*, *Wing Commander Prophecy* | Longer tail |

## Notes

The [docs](docs/README.md) record how the project was approached and built, as a reference for
anyone taking on a retro game the same way.
