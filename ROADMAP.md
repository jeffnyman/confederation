# Roadmap

The order runs from the most testable part to the least, and each step gets a game closer to
playing. The reasoning behind the plan is in [docs/emulate-or-reimplement.md](docs/emulate-or-reimplement.md).

## *Wing Commander* and *Wing Commander II*

1. **x86 CPU.** *Done.* A 386 in real mode, every instruction but the coprocessor's and
   protected mode's, checked against all 1.76 million cases of the SingleStepTests 80386
   real-mode suite ([docs/the-x86-cpu.md](docs/the-x86-cpu.md)).
2. **The DOS layer.** *Done.* An MZ loader, DOS, BIOS and EMS answered in C# against the game's
   folder (never written; saves go elsewhere), and a log of every call. The 8253 timer and 8259
   interrupt controller came early, because the game times delays with the timer. `WC.EXE` runs
   until it sets VGA mode 13h ([docs/the-dos-layer.md](docs/the-dos-layer.md)).
3. **The machine.** The keyboard controller and VGA mode 13h with its palette and retrace status,
   plus Mode X if the games use it. Milestone: the *Wing Commander* intro, rendered to images in
   the tests.
4. **The player.** An SDL3 window, keyboard, and gamepads mapped onto the analog joystick port. Each
   game held to its own speed.
5. **Sound.** OPL2/OPL3 FM for AdLib and Sound Blaster music; the MPU-401 feeding an MT-32 (from
   Daventry's port of Munt); the Sound Blaster DSP and DMA for digital sound.
6. ***Wing Commander II.*** Speech, its EMS use, and the `loadfix` question
   ([docs/the-games.md](docs/the-games.md)). *Special Operations* 1 and 2.
7. **Per-game profiles.** Save states, each game's speed, controls and sound device, and the
   *Secret Missions* and *Special Operations* chosen from one place.

## *Wing Commander III*, the first stretch

8. **Protected mode.** The 386's 32-bit protected mode with paging, and the DPMI services its DOS
   extender expects.
9. **CD-ROM.** The game's ISO image, with MSCDEX-style file access.
10. **Speed and video.** The CPU fast enough for a 486-class game, and its full-motion video.

## Later

*Wing Commander IV* (DOS) follows from *Wing Commander III*. *Wing Commander Prophecy* is a Windows
95 game and needs a different approach, so it's left open for now.
