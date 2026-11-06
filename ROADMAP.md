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
   playable by keyboard, stick and, added after step 6, mouse (`INT 33h`)
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
7. **Per-game profiles.** *Done*: each series' speed, controls and sound device kept in one
   settings file, the games known by short names (`wc1`, `sm2`, `so1`...), and a menu in a window
   when none is named ([docs/profiles-and-the-menu.md](docs/profiles-and-the-menu.md)).
8. **Save states.** *Done*: the whole machine saved and restored, found by walking its objects:
   the processor, memory, every device, DOS's open files, and the MT-32's synthesizer down to its
   notes in flight. Ctrl+F5 saves, Ctrl+F9 loads, `--resume` starts from one
   ([docs/save-states.md](docs/save-states.md)).

## *Wing Commander III*, the first stretch

9. **Protected mode.** The 386's 32-bit protected mode, and the DPMI services the DOS extenders
   expect: Origin's own loader for *Wing Commander III*, and PMODE/W for W.C.A.T.'s launcher. Both
   ask for DPMI first, so the machine provides a DPMI host of its own, in C#, rather than VCPI.
   - **9a. The CPU.** *Done.* Descriptors, segment loads and their checks, 32-bit code and stacks,
     privilege levels, far transfers, interrupts and exceptions through gates, the task state
     segment's stacks, and the system instructions; checked by hand-assembled programs
     ([docs/the-x86-cpu.md](docs/the-x86-cpu.md#protected-mode-in-brief)). Not yet: paging,
     virtual-8086 mode, task switches, call gates.
   - **9b. The DPMI host.** *Done.* INT 2Fh/1687h, the switch into protected mode, INT 31h's
     services (descriptors, memory, calls down to real mode and callbacks up, the raw switch,
     vectors and exception handlers), interrupts reflected down to DOS and the BIOS and brought
     back up, and 16 MB of memory; checked by small DPMI programs
     ([docs/the-dpmi-host.md](docs/the-dpmi-host.md)).
   - **9c. W.C.A.T.'s launcher** to its screen: the first real protected-mode program. *Done*:
     PMODE/W runs on the host and the launcher draws its screen and scans for the game. Still to
     do: W.C.A.T.'s folders mounted as it lays them out (the game beside the launcher), and the
     batch files it writes to start the game.
   - **9d. *Wing Commander III*** as far as it gets before it needs the CD.
10. **CD-ROM.** The game's ISO image, with MSCDEX-style file access.
11. **Speed and video.** The CPU fast enough for a 486-class game, and its full-motion video.

## Later

*Wing Commander IV* (DOS) follows from *Wing Commander III*. *Wing Commander Prophecy* is a Windows
95 game and needs a different approach, so it's left open for now.
