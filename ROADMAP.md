# Roadmap

The order runs from the most testable part to the least, and each step gets a game closer to
playing. The reasoning behind the plan is in [docs/emulate-or-reimplement.md](docs/emulate-or-reimplement.md).

## *Wing Commander* and *Wing Commander II*

1. **x86 CPU.** *Done.* A 386 in real mode, every instruction but the coprocessor's and
   protected mode's, checked against all 1.76 million cases of the SingleStepTests 80386
   real-mode suite ([docs/the-x86-cpu.md](docs/the-x86-cpu.md)).
2. **The DOS layer.** An MZ loader with relocations, and DOS, BIOS and EMS services answered in C#
   (`INT 21h`, `INT 10h`, `INT 16h`, `INT 1Ah`, `INT 67h`) against the game's own folder. A trace of
   every call the games make, which documents what they need. Milestone: `WC.EXE` runs until it
   asks for a video mode.
3. **The machine.** The 8253 timer, the 8259 interrupt controller, the keyboard controller and VGA
   mode 13h with its palette, plus Mode X if the games use it. Milestone: the *Wing Commander*
   intro, rendered to images in the tests.
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
