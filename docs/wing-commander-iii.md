# Wing Commander III

*Wing Commander III: Heart of the Tiger* (1994) is a different kind of program from the first
two. It's a 32-bit protected-mode game on four CDs, with full-motion video, and it needs a 486.
This note follows it as far as it gets before it needs its CD, which is the next step.

## How GOG sets it up

GOG's folder holds the four CDs as one image, `DATA.DAT` (1.8 GB, ISO 9660), with `OBJECTS.TRE`
beside it and a `WC3` folder with two small files. Its DOSBox setup (`dosboxWC3_single.conf`)
does this:

```
mount C ".."
imgmount D "..\DATA.DAT" -t iso
d:
wc3.exe
```

So the program, `WC3.EXE`, runs from the CD as D:, and C: is GOG's folder. The installer's two
files are in `C:\WC3`:

- `WC3.CFG`: the installer's choices, 126 bytes, binary, with the path `c:\` in it.
- `WC3.CON`: which archives (`.TRE` files) each CD holds, as text:
  `CD1`, `=cd1movie.tre`, `=movies.tre`, … `NOMORECDS`.

GOG's DOSBox gives it 30 MB of memory and `cycles=max`.

## As far as it gets without the CD

Until the CD exists (step 10), the probe copies `WC3.EXE`, `WC3.CFG` and `WC3.CON` into one
folder, as C:. Observed in the log, at 20,000 cycles:

1. **Origin's loader**, in real mode, checks the DOS version. It then follows DOS's "list of
   lists" (`INT 21h` 52h, undocumented) to the table of open files, to count how many files DOS
   can have open, its `FILES=` setting. The machine answers with a small table of 40.
2. **Protected mode.** It finds the DPMI host and enters as a 32-bit program. It sets up flat
   4 GB descriptors, and reads its own 1.1 MB from `WC3.EXE` in 64 KB pieces through real-mode
   DOS into extended memory. It then grows that block to about 5.6 MB. It hooks `INT 21h`, and
   the Ctrl+Break and critical-error vectors (23h, 24h).
3. **Its folder.** It makes `C:\WC3` (`INT 21h` 39h) and looks there for `WC3.CFG`. It copies
   `WC3.CON` into it and looks for `VIDEO.DLL` and `WC3JOY.DLL` there. As everything a game
   writes, the directory and the copy go to the save folder.
4. **VESA.** It asks for the details of VESA mode 101h, 640 by 480 in 256 colors (`INT 10h`
   4F01h), its high-resolution mode. The machine has no VESA BIOS and says so, by not answering
   4Fh, and the game carries on without it.
5. **"Loading Wing Commander III .......".** It then opens `.\GLOBALS.IFF`, in the current
   directory. On GOG's setup that's the CD's root. Here there's no such file, so it says "Open
   failed on file: .\GLOBALS.IFF" and blames "a bad CD-ROM read". It exits cleanly: it closes
   its handles and frees its memory and descriptors, and the host lets it go.

## What it needed

- **The list of lists** (`INT 21h` 52h). It's DOS's own table of its tables: memory, drives,
  devices, and at offset 4 a far pointer to the first file table. The machine's is in the BIOS
  segment: a single file table, whose pointer to the next is `FFFF:FFFF` (none), with room for 40
  files. The rest of the
  list isn't filled in, and nothing has asked for it.
- **Making and removing directories** (39h, 3Ah). A directory is made in the save folder, like a
  file. Making one that's there already fails with "access denied", as DOS does. Only a directory
  a program made can be removed: the game's own folder isn't changed.

Its protected mode needed nothing new from the host. Origin's loader uses DPMI as PMODE/W does,
for descriptors, memory, calls down to real mode, and vectors.

## With the CD

With `DATA.DAT` as drive D: ([the CD](the-cd.md)), the game runs from it as GOG runs it. It
reads its archives from the CD, makes its swap file, loads its sound drivers, and starts its
opening movie. The movie is timed by its sound, 16-bit, which comes through the second DMA
controller. Without it, the movie stopped on its first frame. With it, the movie plays
([the sound](the-sound.md)): the planet, Origin's logo, the title, and the credits over a burning
world. Esc skips it a part at a time, and after four the game comes to its first screen, the
hangar, with its mouse pointer (observed).

While a movie plays, the game calls DOS's function 0Ch with AL=0Bh about 10,000 times a second,
from protected mode through the DPMI host. 0Ch empties the keyboard's buffer and then runs the
input function in AL. 0Bh isn't one DOS runs there, so the call only empties the buffer. Each
call was logged on its way down, nearly half a million lines a minute, so the host leaves DOS's
keyboard checks (06h, 0Bh, 0Ch) out of its log, as DOS leaves them out of its own.

## Past the hangar

The game is played by mouse: its screens are pictures of the carrier, and the places to go and
the people to talk to are named as the pointer passes over them. In the hangar the pointer stayed
in the corner. The game reads the mouse only through an **event handler** it gives the driver
(`INT 33h` function 0Ch, for every event), and the driver here had none. With the handler called
from IRQ 12 and the second interrupt controller ([the player](the-player.md#the-mouse)), the
pointer moves, and (observed, by a scripted mouse):

1. at the foot of the hangar's screen it says "Go to FLIGHT CONTROL.", and a click plays the
   walk there, then shows the room;
2. in flight control, "Talk to HOBBES.", "Activate MAIN TERMINAL." and "Go to LIFT.";
3. the terminal asks for a callsign "for security clearance", from the keyboard, and then shows
   its menu: duty logs, controls, logoff.

The game's mouse area is 1280 by 480 (functions 7 and 8), so the player spreads the mouse's
motion over that rather than the driver's usual 640 by 200.

## To the first mission

From there, with nothing more needed from the machine but one fix to its clock (below), the
game goes on to its first mission (observed, by a scripted mouse, at 30,000 cycles):

1. talking to Hobbes plays the conversation, full-motion video with its subtitles, and back in
   flight control there's more to do: "Attend BRIEFING.", "Run SIMULATOR.", "Go to FLIGHT
   DECK.";
2. the lift goes to the bridge, living and flight levels, and the bridge has its own people and
   places (Maniac, gunnery control, a terminal);
3. the briefing plays, the captain's map and all, and asks for a wingman ("Pick HOBBES.");
4. back in flight control, "Fly MISSION." plays the launch, and the game comes to the cockpit in
   the carrier's launch bay, with Hobbes and the other ships named on the display. With the
   throttle up, the fighter moves, at 86 KPS on its display. In flight the mouse steers, so
   flying out of the bay is for a player.

The simulator ("Run SIMULATOR.") offers ten training missions, and the first flies: the HUD, the
radar and "FLY TO WAY AREA", at about 24 frames a second, without the cockpit's picture.

The game never needed a floating-point unit. Its coprocessor instructions are no-ops here, as on
a 386 without one, and it flies without them.

### The terminal, and time

The main terminal, which asks for a callsign, ran seven times slower than real time, with
nothing in the log. A profile showed half the host's time in string instructions: the screen
is copied with `REP MOVS`, again and again. The machine's clock counted each `REP` as one
instruction, however many thousand times it repeated, so a second of the game's time held far
more copying than it should. DOSBox counts each repetition, and now so does the machine
([the x86 CPU](the-x86-cpu.md)). The terminal then runs faster than real time.

That made *Wing Commander II*'s speech test, run at the machine's default 4 million
instructions a second, load its speech too slowly to keep up. GOG runs the game at 8,000 cycles,
as the player does, and at that speed it keeps up again, so the test now runs the game at its
own speed.

## In the player

The player knows the game as `wc3`, from GOG's folder, by its CD image
([profiles and the menu](profiles-and-the-menu.md)). It puts `DATA.DAT` in as D:, runs
`D:\WC3.EXE` at 30,000 cycles ([the player](the-player.md#speed)), and has the game's own icon.
Save states work for it since they can hold the time of a file open on the CD
([save states](save-states.md)).

Still to come, probably:

- VESA, for its 640 by 480 mode, which it manages without;
- a much faster processor (step 11);
- playing the first mission through, its landing, and what follows, untried so far.
