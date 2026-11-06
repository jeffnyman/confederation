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
opening movie, whose first frame it draws. There it stays. The likely cause is the Sound
Blaster 16's 16-bit sound, which HMI's driver started and the machine doesn't have yet. *To
confirm.*

Still to come, probably:

- the Sound Blaster 16's 16-bit sound, and the second DMA controller it uses;
- the floating-point unit (*to confirm*: it hasn't come up yet);
- VESA, for its 640 by 480 mode, which it manages without;
- a much faster processor (step 11);
- the player knowing the game, which so far is run by a test harness.
