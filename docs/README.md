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
   a player, strict builds, and tests that run with or without the games.
4. [The x86 CPU](the-x86-cpu.md): why a 386, how the processor is built, how it was tested
   against 1.7 million instructions captured from a real chip, and what the chip does that its
   manuals don't say.
5. [The DOS layer](the-dos-layer.md): answering DOS, the BIOS and expanded memory in C#,
   loading a program, keeping the game's folder untouched, the timer *Wing Commander* couldn't
   start without, and what the game asks for on its way to its first screen.

More notes will follow as the work does: VGA, sound, and each game as it comes up.
