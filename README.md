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
instructions. DOS, the BIOS, expanded memory and the timer are answered well enough that
*Wing Commander* plays its intro, silently, rendered to screenshots in the tests. There's no
window to play in yet. [ROADMAP.md](ROADMAP.md) lays out the order of work.

| Game | Tier |
|---|---|
| *Wing Commander*, with *The Secret Missions* 1 and 2 | First |
| *Wing Commander II*, with *Special Operations* 1 and 2 | First |
| *Wing Commander III: Heart of the Tiger* | First stretch |
| *Wing Commander IV*, *Wing Commander Prophecy* | Longer tail |

## Notes

The [docs](docs/README.md) record how the project was approached and built, as a reference for
anyone taking on a retro game the same way.
