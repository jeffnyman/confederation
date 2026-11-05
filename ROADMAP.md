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
3. **The machine.** *Done.* The VGA (mode 13h and Mode X, the palette, the retrace) and the
   keyboard controller. *Wing Commander*'s intro plays, checked by screenshots in the tests
   ([docs/the-vga-and-keyboard.md](docs/the-vga-and-keyboard.md)).
4. **The player.** *Done.* An SDL3 window, the keyboard key for key, and a gamepad on the
   analog joystick port. Each game held to its own speed in real time. *Wing Commander* is
   playable, silently, by keyboard and stick; a mouse driver (`INT 33h`) is still to come
   ([docs/the-player.md](docs/the-player.md)).
5. **Sound.** OPL2/OPL3 FM for AdLib and Sound Blaster music (*done*: *Wing Commander* plays its
   score, [docs/the-sound.md](docs/the-sound.md)); the MPU-401 feeding an MT-32, from Daventry's
   and GUE's port of Munt (*done*: both games play their Roland scores, chosen with
   `--music roland`); the Sound Blaster DSP and DMA for digital sound (*done*: *Wing Commander
   II* speaks in its introduction).
6. ***Wing Commander II.*** *Done*: it plays from the title, through its speech, to signing on a
   pilot and flying the first mission, once the BIOS answered for the keyboard intercept; its EMS
   use is traced, `loadfix` isn't needed, and *Special Operations* 1 and 2 reach their menus
   ([docs/wing-commander-ii.md](docs/wing-commander-ii.md)). Still to check: a mission played
   through.
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
