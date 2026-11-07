# Notes

These notes record how **Confederation** was approached, decided and built, as the work happens.
They are meant to serve two kinds of reader:

- **Someone who wants to play the *Wing Commander* games** and wonders what's going on under the
  hood.
- **Someone who wants to build something like this for another retro game**: how to decide
  between emulation and reimplementation, how to find out what a game needs from the machine, and
  how to build only that.

Each note explains its reasoning as well as its conclusion, since the reasoning is what transfers to other
games.

## Reading order

1. [Emulate or reimplement?](emulate-or-reimplement.md): the first decision, the general method
   for making it, and why Confederation emulates a focused DOS PC in C#.
2. [The games](the-games.md): what each *Wing Commander* release is, and what can be learned about
   its needs just by inspecting its files.
3. [Project layout](project-layout.md): how the repository is organized, and why: a core library,
   a player, strict builds, the included games and the tests that use them, and the downloads.
4. [The x86 CPU](the-x86-cpu.md): why a 386, how the processor is built, how it was tested
   against 1.7 million instructions captured from a real chip, and what the chip does that its
   manuals don't say.
5. [The DOS layer](the-dos-layer.md): answering DOS, the BIOS and expanded memory in C#,
   loading a program, keeping the game's folder untouched, the timer *Wing Commander* couldn't
   start without, and what the game asks for on its way to its first screen.
6. [The VGA and the keyboard](the-vga-and-keyboard.md): planes, chained and unchained memory,
   the palette and the retrace, the keyboard's scan codes and the BIOS's buffer, and *Wing
   Commander*'s intro checked by screenshots.
7. [The player](the-player.md): the window, holding the game to real time at its own speed, the
   keyboard's scan codes, the PC's odd analog joystick port and a gamepad on it, and testing a
   game by keyboard without a person at the keys.
8. [The sound](the-sound.md): what the games ask for, the OPL FM synthesizer and the card around
   it, making each sample just in time, letting the sound device set the pace, and the Roland
   MT-32 behind an MPU-401, with how the games' settings were worked out by trying them, and the
   Sound Blaster's digitized sound, the DMA controller that feeds it, and its interrupt on time.
9. [*Wing Commander II*](wing-commander-ii.md): from the title to the cockpit, and how a key that
   never arrived was followed through the game's own code to one BIOS call the machine didn't
   answer; its expanded memory, `loadfix`, and the *Special Operations*.

10. [Profiles and the menu](profiles-and-the-menu.md): why a profile belongs to a game and its
    mission disks together, one settings file that remembers where each game is and how it's
    played, short names, and a menu drawn with SDL's own debug font.
11. [Save states](save-states.md): the whole machine saved by walking its objects rather than
    listing its fields, what's left out and why, open files, and the two things the tests found
    that a state loaded straight back would never have shown.
12. [Cheats](cheats.md): the words Origin left in both games, how two runs of a deterministic
    machine, compared byte for byte, showed the games take them, and how the flags led to the
    games' own cheat keys in their code.
13. [The DPMI host](the-dpmi-host.md): the three ways a DOS extender gets into protected mode and
    why Confederation offers one, a host that switches modes by saving and restoring the
    processor rather than running code, the three paths an interrupt can take, and what W.C.A.T.'s
    launcher needed on its way to its first screen.
14. [W.C.A.T.](wcat.md): a fan overhaul played through its own launcher, the game's data mounted
    where it expects it rather than copied, and batch files run by a shell in C# between
    programs, as DOSBox runs them, rather than by COMMAND.COM.
15. [*Wing Commander III*](wing-commander-iii.md): GOG's CD image and folders, and Origin's
    loader on the DPMI host as far as the game's first file on the CD: DOS's list of lists, and
    a directory of its own.
16. [The CD](the-cd.md): ISO 9660, GOG's four CDs in one image, a second, read-only drive whose
    files are windows on the image, MSCDEX, and what *Wing Commander III* read from it on the way
    to its opening movie.

More notes will follow as the work does, and each game as it comes up.
