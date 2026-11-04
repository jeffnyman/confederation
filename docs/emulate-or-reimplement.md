# Emulate or reimplement?

This is the first decision in any project that wants to play an old game on a modern machine, and
it shapes everything after it. This note works through that decision for the *Wing Commander*
series and explains how **Confederation** came to be a focused DOS PC emulator, not a
reimplementation of Origin's engine.

The reasoning applies well beyond *Wing Commander*, so it is laid out as a general method first,
then applied.

## The two approaches

There are two broad ways to make an old game run on new hardware.

**Emulate the machine.** Rebuild the original computer in software: its CPU, memory, video, sound
and input. The game's original executable then runs unchanged, believing it is on the hardware it
was written for. DOSBox, VICE and MAME work this way.

**Reimplement the engine.** Treat the game as *data* (graphics, scripts, sounds, levels) plus an
*engine* (the program that interprets that data), and write a new engine that reads the original
data. The original executable is never run. ScummVM works this way, as do source ports and
projects like OpenTTD or the Sierra and SCUMM interpreters.

Neither approach is better in general. They have opposite strengths:

| | Emulate the machine | Reimplement the engine |
|---|---|---|
| What you have to understand | The hardware, which is documented and the same for every game | The game's engine, which is often undocumented and specific to one developer |
| Fidelity | Exact by construction: the original code runs | Only as good as your understanding of the original |
| Effort per extra game on the same machine | Small: the machine is already built | Large, unless the games share an engine |
| Modernizing (resolution, widescreen, mods) | Hard: you only see pixels and port writes | Natural: you own every part |
| Performance needed | Must run the original CPU's instructions | Runs native code |
| Legal and ethical footing | Needs only the user's own game files | Needs only the user's own game files |

### The question that usually decides it

In practice the decision often comes down to one question: **is there already a reference
implementation you can study and port?**

That's the pattern in the sibling projects in this workspace:

- **Retrograde** emulates the Commodore 64, porting logic from VICE.
- **Daventry** reimplements Sierra's AGI and SCI interpreters, porting from ScummVM's `engines/agi`
  and `engines/sci`.
- **Maniacal** reimplements SCUMM for *Maniac Mansion*, porting from ScummVM's `engines/scumm`.

Each one had a mature open-source reference to port from. Daventry and Maniacal could reimplement the engines
because someone else had already done the reverse engineering. Without that, a reimplementation
starts with disassembling the original executable and recovering its design by hand. That is
months or years of work before anything plays.

## Applying it to *Wing Commander*

**There is no ScummVM-style engine for *Wing Commander*.** The community has documented many of the
data formats (the `.VGA` image archives, the `.WLD` campaign files, the `.PAK`/`.TRE` containers in
later games), and there are editors and viewers. But no one has published a complete,
playable reimplementation of the engine of *Wing Commander* or *Wing Commander II*. So the
reimplementation route would start from reverse engineering `WC.EXE`.

**There is a mature reference for the machine.** DOSBox, and its modern descendants DOSBox Staging
and DOSBox-X, emulate everything these games need, under the GPL. That is the same position
Retrograde was in with VICE.

So Confederation follows Retrograde's model: **emulate the DOS PC, but only as much of it as these
games use**, porting from DOSBox where that helps, without wrapping or linking it.

Reimplementation isn't ruled out for good. If the emulator is built well, it becomes the best tool
for understanding the engine: you can trace what the game does, watch memory, and log every file it reads.
Later notes may well document parts of the engine that way.

## Is C# fast enough?

This is the first worry for any emulator in a managed language, so it is worth checking with
numbers.

DOSBox measures emulated CPU speed in *cycles*, roughly emulated instructions per millisecond. The
GOG releases in `_wingc` ship with these settings:

| Game | GOG `cycles` | Roughly equivalent to |
|---|---|---|
| *Wing Commander* | `4000` | A fast 286 or slow 386 |
| *Wing Commander II* | `8000` | A 386 |
| *Wing Commander III* | `max` | As fast as the host can go; the game wants a 486 |

4,000 to 8,000 instructions per millisecond is 4 to 8 million instructions per second. A
straightforward *interpreted* x86 core in C# (decode, switch on the opcode, execute) running on .NET 10
can do several times that without heroic optimization. The first two games are well within reach.

*Wing Commander III* asks for more, roughly 486-class speed, and is where performance work (careful
flag evaluation, decode caching, avoiding allocation in the hot loop) would start to matter. That
is a later concern.

One more point in C#'s favor: a fixed, low speed is a *feature* here. The first *Wing Commander*
is notorious for running too fast on faster machines. A player built only for these games can lock
each one to the right speed, which a general emulator leaves to the user to tune.

## How this compares to the C64

It is tempting to assume a PC emulator is harder than a C64 emulator because the PC is the bigger
machine. For these particular games, the reverse is partly true.

**Easier than the C64:**

- **Timing is loose.** Retrograde had to emulate the C64 and its 1541 drive cycle by cycle,
  because the games' fast loaders and copy protection depend on exact timing. DOS games of this
  era run on PCs of wildly different speeds, so they were written to tolerate timing variation.
  An instruction-accurate CPU with approximate cycle counts is enough.
- **No disks to emulate.** Retrograde emulates the 1541 drive's own CPU and its GCR encoding.
  Confederation can skip emulating DOS itself and answer the game's DOS calls (the `INT 21h`
  services: open, read, seek, allocate memory) directly in C#, against the real files on disk.
  This is called *high-level emulation* (HLE) of the operating system, and DOSBox does the same.
- **Copy protection is in the game.** *Wing Commander* asks a question from the manual.
  There's nothing on the disk to emulate.
- **The video mode is simple.** VGA mode 13h, 320×200 with 256 colors, is a flat 64,000-byte
  framebuffer, one byte per pixel. It's much simpler than the VIC-II's character modes, sprites and
  raster tricks.

**Harder than the C64:**

- **The x86 is a large, irregular CPU.** The 6502 has 151 documented opcodes with simple
  encodings. The x86 has prefixes, ModR/M and SIB addressing bytes, segment registers, string
  instructions, and the rules of real mode versus protected mode. The CPU core is the largest
  single piece of work in the project.
- **Memory has more layers.** Conventional memory, the high memory area, EMS (expanded memory,
  bank-switched through a page frame) and XMS (extended memory). Both games look for EMS; see
  [the games](the-games.md).
- **More sound devices.** FM synthesis (the AdLib's Yamaha OPL2), Sound Blaster digital audio
  over DMA, and MIDI to a Roland MT-32.

## Assets already in the workspace

- **An MT-32 emulator in C#.** Daventry's `Daventry.Audio/Mt32` is a port of Munt, the Roland
  MT-32 emulator, and `_mt32-roms` holds the ROMs it needs. *Wing Commander*'s MT-32 soundtrack is
  one of the most celebrated of its era, so this is a significant head start. It may be worth
  extracting into a library the two projects share.
- **Retrograde's architecture.** Its split into a core library (`Retrograde.Core`) and a player
  (`Retrograde.Player`), its per-game profiles and its save states carry over directly.

## What the emulator has to include

For *Wing Commander* and *Wing Commander II*:

| Part | What it does | Notes |
|---|---|---|
| x86 CPU | Runs the game's code | Real mode, with the 286 and 386 instructions Borland's compilers emit |
| Memory | 640 KB conventional memory, plus EMS | EMS can be provided by HLE (the `INT 67h` services), not emulated hardware |
| DOS and BIOS services | File access, memory allocation, program loading, time | HLE in C#: `INT 21h`, `INT 10h`, `INT 16h`, `INT 1Ah` and friends |
| EXE loader | Loads the MZ executable and applies relocations | See [the games](the-games.md) for what the headers show |
| Timer (8253/8254 PIT) | Drives the game's clock and music tempo | Games reprogram it, so it must be accurate in rate |
| Interrupt controller (8259 PIC) | Routes timer and keyboard interrupts | |
| Keyboard controller | Delivers scancodes | Games install their own `INT 9h` handler |
| Joystick port | Analog joystick, read by timing a one-shot | Map a modern gamepad onto it |
| VGA | Mode 13h, the palette registers, possibly unchained "Mode X" | Confirm which modes each game uses by tracing |
| OPL2 / OPL3 | AdLib and Sound Blaster FM music | Port an existing OPL emulator |
| Sound Blaster DSP and DMA | Digital speech and effects | Needed for *Wing Commander II*'s speech |
| MPU-401 | MIDI out to the MT-32 | Feeds Daventry's Munt port |

## Scope that follows from this

- **First:** *Wing Commander* (with *The Secret Missions* 1 and 2) and *Wing Commander II* (with
  *Special Operations* 1 and 2). These are real-mode DOS programs and fit the emulator described
  above.
- **First stretch:** *Wing Commander III*. A 32-bit protected-mode program on CD-ROM with
  full-motion video. It needs the CPU to grow protected mode, paging and a DOS extender's
  expectations (DPMI), plus a CD-ROM layer and more speed. This is effectively a second machine
  built on the first.
- **Longer tail:** *Wing Commander IV* (DOS, CD-ROM, higher-resolution video) and *Wing Commander
  Prophecy*. Prophecy was a Windows 95 game built for 3D acceleration, so it falls outside a DOS
  emulator entirely. It would need its own approach, and that question is left open for now.
- **Out of scope:** *The Kilrathi Saga*, the Windows 95 re-release of the first three games
  (`_wingc/WC-Kilrathi-Saga`). Running it means emulating Windows, and the DOS originals are the
  better way to play those games anyway.
