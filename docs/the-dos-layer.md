# The DOS layer

A DOS game doesn't run on bare hardware. It runs on top of DOS and the BIOS, which load it,
give it memory, open its files and tell it the time. An emulator has two ways to provide them:
boot a real DOS on the emulated hardware, or answer those services itself. This note explains
why Confederation answers them itself, how it does that, and what *Wing Commander* turned out
to ask for on its way to its first screen.

The code is `src/Confederation.Core/Machine*.cs`, `ExpandedMemory.cs`, `Dos/` and `Devices/`.

## High-level emulation

Booting real DOS means emulating a floppy or hard disk controller, shipping (or asking the user
for) a copy of DOS, and running its startup files. It's faithful, and it's how a general emulator
like PCem or 86Box works.

The alternative is **high-level emulation** (HLE): when the program calls a DOS service, the
emulator does what that service would do, directly, in its own code. "Open `GAMEDAT\COCKPIT.VGA`"
becomes a `FileStream` on the host. DOSBox works this way, which is why it needs no DOS of its
own, and Confederation does the same.

HLE is far less work, needs nothing from the user but the game, and makes every service call
visible: each one can be logged, which turns out to be the most useful debugging tool in the
project (see below). The cost is that only the services implemented exist, and a program that
pokes at DOS's internals finds nothing there. Games rarely do.

## Answering an interrupt in C#

Programs call DOS with `INT 21h`, the video BIOS with `INT 10h`, and so on. The processor looks
up the interrupt's vector, a far pointer in the table at address 0, and jumps there. Something must
be at that address.

Confederation puts a four-byte stub in the BIOS area for each service:

```
0F FF 21    the escape: call C# service 21h
CF          IRET
```

`0F FF` isn't an instruction on any x86, so no program contains it by accident. The emulated 386
treats it as an escape: it calls the C# handler set on `Intel386.HostCall` with the byte after it.
The handler reads the registers, does the work, writes the results to the registers, and returns.
The stub's `IRET` then returns to the program as a real DOS would.

Two details matter:

- **The vectors are real.** A program can read the INT 21h vector, install its own handler and
  chain to the old one. *Wing Commander* does this for several interrupts. Because the stub is
  ordinary memory at an ordinary address, chaining works without the emulator knowing.
- **Results in flags go on the stack.** DOS reports errors in the carry flag. But between the `INT`
  and the `IRET` the flags are the ones the `INT` pushed, and `IRET` restores them. So a handler
  that fails writes carry into that pushed copy (`Machine.SetReturnCarry`). Forget this and every
  error is silently lost.

The BIOS's timer interrupt shows that a stub can also mix C# and x86. It counts the tick in C#,
then runs a real `INT 1Ch` for programs that hook the timer through it:

```
0F FF 08    count the tick, end the interrupt
CD 1C       INT 1Ch
CF          IRET
```

A service that has to wait (`INT 16h` with no key pressed) moves the return address back over the
`INT`. When the stub returns, the program calls the service again, with timer interrupts able to
arrive in between. That's how it waits without blocking the emulator.

## Loading a program

DOS's loader is simple enough to describe in full:

1. **The environment block.** The environment variables, then the program's own full path,
   `C:\WC.EXE`. Programs find their own folder from that path.
2. **Memory.** The program gets the largest free block, up to the maximum its header asks for.
   Usually that means all free memory: a well-behaved program gives back what it doesn't need
   with `INT 21h AH=4Ah`, and *Wing Commander* does, three times, right at the start.
3. **The program segment prefix (PSP).** 256 bytes before the program, holding its command line,
   its file handle table, a pointer to its environment and the segment where its memory ends.
4. **The image.** For an MZ executable the header gives the image's size and a table of
   *relocations*: places in the image that hold a segment number and need the load segment added.
   This is what lets the same `.EXE` run wherever DOS loads it.
5. **Registers.** CS:IP and SS:SP from the header, plus the load segment; DS and ES point at the
   PSP.

### Where the program goes

Real DOS loads programs just past its own resident code, drivers and buffers, typically somewhere
between 32 KB and 80 KB into memory. DOSBox, with almost nothing resident, loads them much lower,
around 6 KB. GOG starts both games with `LOADFIX`, a DOSBox command that reserves memory so the
program loads higher. *Wing Commander* uses `loadfix -1` and *Wing Commander II* `loadfix -32`, so
both evidently misbehave when loaded as low as DOSBox puts them.

Confederation starts DOS's memory arena at 40 KB (segment `0A00h`), roughly where a carefully
configured DOS 5 machine would leave programs, with 600 KB still free. The games load where they
were tested, and no `LOADFIX` is needed.

### The memory arena

DOS keeps memory as a chain of blocks, each led by a 16-byte *memory control block*: a type byte
(`M`, or `Z` for the last), the owner's PSP (0 when free), and the size in paragraphs.
Confederation keeps that chain in emulated memory, exactly as DOS does, and allocates, frees and
resizes by walking it. Programs that inspect the chain see what they'd see on real DOS.

## Drive C:

C: is the game's folder. Names are matched without regard to case, as DOS matches them, so the
game's files can be in any case on the host.

**The game's folder is never written.** Anything a game writes goes to a separate save folder.
A file opened for writing is read from the game's folder until the first actual write, when it's
copied into the save folder; from then on the copy is the file. The delay matters: *Wing
Commander* opens every data file in read/write mode, `COCKPIT.VGA` and its sound drivers included,
and writes none of them. Copying on open would have filled the save folder with game data. The
start-up test checks that the folder is still empty when the game reaches its first screen.

## Expanded memory

640 KB wasn't enough for games by 1990. Expanded memory (EMS, the LIM specification) was one way
past it: memory beyond 1 MB, reached through a 64 KB *page frame* at `E0000h`. A program allocates
16 KB *logical pages* under a handle, then *maps* up to four of them into the frame's four
*physical pages*, and reads and writes them there.

On a 386, EMM386 provided EMS with the processor's paging hardware. Confederation decodes it on the
bus instead: an access between `E0000h` and `EFFFFh` goes straight to whichever logical page is
mapped there. A program finds the EMS manager by looking for the name `EMMXXXX0` at offset 0Ah of
the segment the INT 67h vector points into, so the EMS stub lives in a segment of its own with that
name in place.

*Wing Commander* asks how much expanded memory there is, takes all of it, prints "Expanded Memory
Detected", and from then on maps four pages at a time, thousands of times, to load its graphics.
Its "Expanded Memory fully used" message, which looked alarming in the executable's strings,
simply reports that it took everything.

## The timer, early

The roadmap put the timer chip with the rest of the hardware, after DOS. *Wing Commander* moved it
forward. After loading its sound driver, the game measures a short delay by reading the 8253
timer's count. With nothing behind the timer's ports, every read returned `FFFFh`, the count
never changed, and the game waited forever.

So the machine got its 8253 timer and 8259 interrupt controller now:

- **Emulated time follows instructions.** The machine runs at a set number of instructions per
  second, 4 million by default (GOG's DOSBox setting for the game is 4,000 "cycles", about the
  same). The timer's count at any moment is worked out from the instructions run so far, so the
  game's sense of time is exact and repeatable, whatever the host's speed.
- **The timer raises IRQ 0** 18.2 times a second, or at whatever rate a program sets. The interrupt
  controller passes it to the processor when interrupts are on, and the BIOS handler counts the
  tick and calls `INT 1Ch`. *Wing Commander* replaces the timer handler four times during startup,
  each new handler chaining to the last.
- **A halted processor sleeps.** `HLT` with interrupts on waits for the next interrupt, and the
  emulated time skips ahead to it instead of being spent instruction by instruction.

## What Wing Commander asks for

The service log, each DOS, BIOS and EMS call and each I/O port nothing answers, is the record of
what a game needs. From launch to its first VGA screen, *Wing Commander* runs 11.3 million
instructions (2.8 emulated seconds) and calls:

| Service | What for |
|---|---|
| `INT 21h` 30h | The DOS version, twice |
| `INT 21h` 25h, 35h | Its own handlers for divide errors, the timer (four times), the keyboard, and INT 3Fh |
| `INT 21h` 4Ah | Shrinking its memory to what it needs, then growing it to about 600 KB |
| `INT 21h` 3Dh, 3Fh, 42h, 3Eh | Reading `WC.EXE` itself, its config, its sound drivers and its graphics |
| `INT 21h` 3Bh | Changing into `GAMEDAT` and back |
| `INT 21h` 44h 0Eh | Whether the drive goes by other letters |
| `INT 67h` 40h–44h, 46h | Finding expanded memory, taking all of it, and mapping pages |
| `INT 33h` 0 | Whether a mouse driver is installed |
| `INT 10h` 00h | Video mode 13h, the 320×200, 256-color VGA mode |
| Ports 40h–43h | The timer, read to time short delays |
| Port 201h | The joystick |
| Ports 388h–389h | The AdLib FM synthesizer, probing for it |
| Ports 3C8h, 3C9h, 3DAh | The VGA's palette and status, right after the mode is set |

Two observations confirm guesses from [the games](the-games.md):

- **The overlays are real.** The game opens its own executable, reads from offset 134,080, exactly
  where the part DOS loads ends, and installs a handler for INT 3Fh. INT 3Fh is the vector Borland's
  overlay manager uses to load overlay code on demand.
- **It wants EMS.** It doesn't ask for XMS.

The log stops where the next part of the machine begins: the VGA.
