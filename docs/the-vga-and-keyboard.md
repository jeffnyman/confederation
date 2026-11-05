# The VGA and the keyboard

With the VGA emulated, *Wing Commander* plays its intro: the conductor before the Earth, the
Origin FX logo with its fireworks, "In the distant future, mankind is locked in a deadly war...",
and the space battle. This note covers the two devices that step needed, the VGA and the keyboard
controller, and how the result is checked.

The code is `src/Confederation.Core/Devices/Vga.cs`, `Devices/KeyboardController.cs`,
`Machine.Keyboard.cs` and `Screenshot.cs`.

## The VGA

IBM's VGA (1987) is a frame buffer with a great deal of machinery around it. Most of that
machinery exists for the 16-color planar modes and text. Games of this era mostly used one mode:
**mode 13h, 320 by 200 with 256 colors**, which hides nearly all of it.

### Memory: planes, chained and unchained

The VGA has 256 KB of memory, arranged as **four planes** of 64 KB. The processor sees only a
64 KB window at `A0000h`, so how its addresses reach the planes depends on the *sequencer's memory
mode* register:

- **Chained ("chain 4"), mode 13h.** The low two bits of the address pick the plane. To the
  program, video memory is one linear array: the pixel at (x, y) is the byte at `y * 320 + x`.
  This is why mode 13h was so popular. It's the simplest graphics programming the PC ever had.
- **Unchained ("Mode X").** An address names the same byte in all four planes at once. The
  *map mask* register picks which planes a write reaches, and the display shows four neighbouring
  pixels from the four planes at each address. Programs used this for the extra memory: four times
  the 64,000 bytes a screen needs, so there's room to draw the next frame while the current one
  shows, and to flip between them.

Confederation stores the planes interleaved, the four planes' bytes for each address side by side,
as DOSBox does. A chained write is then simply a write to that linear array, and an unchained one
writes up to four neighbouring bytes.

*Wing Commander* stays chained, as far as its intro shows. Mode X support is there for the games
that may not.

### The graphics controller

Unchained writes pass through the *graphics controller*. Each write can be rotated, combined with
the *latches* by AND, OR or XOR, and masked bit by bit. The latches are four bytes, one from each
plane, loaded by every read. In *write mode 1*, a write stores the latches unchanged, so one read
and one write copy four pixels. All four write modes and both read modes are emulated. They cost
little, and the tests check the latch copy.

### The palette

In 256-color modes each pixel is an index into the **DAC**, a table of 256 colors, each 6 bits of
red, green and blue. Programs set it through three ports: an index to port 3C8h, then red, green
and blue to port 3C9h, which moves to the next color by itself. They can also use the video BIOS's
`INT 10h AH=10h`. *Wing Commander* uses both: the BIOS for its first palette, the ports from then
on, for its fades.

To draw, each 6-bit value is widened to 8 bits by repeating its top bits (`v << 2 | v >> 4`), so
63 becomes 255 and full brightness stays full.

### The size of the picture

The picture's size isn't stored anywhere as a number. It comes from the *CRT controller*'s timing
registers:

- The *horizontal display end*, in units of 4 pixels in these modes. 79 means 80 units, so 320
  pixels.
- The *vertical display end*, in scan lines, split across two registers. It's 399 for 400 lines.
- The *maximum scan line*: how many times each line is drawn. Mode 13h draws each line twice, so
  400 scan lines make 200 rows of pixels.

Reading the size from the registers means a program that reprograms them, as Mode X variants of
320×240 do, gets the size it asked for.

### The retrace

Bit 3 of port 3DAh is set during the **vertical retrace**, when the electron beam returns from the
bottom of the screen to the top. That happens 70 times a second in mode 13h, about every 14 ms.
Programs change the palette or flip pages while it's set, so the change doesn't show as a tear
across the picture. They wait for it in a tight loop:

```
wait:   in   al, dx      ; DX = 3DAh
        test al, 8
        jz   wait
```

The bit comes from the emulated time. A frame is 449 scan lines, 400 of them visible, and the
retrace lasts two lines, from line 412. Because time in the machine follows the instructions run,
a loop like that one ends after exactly the right number of instructions.

### What isn't there

- **Text modes aren't drawn.** They need the VGA's 8×16 font, which comes from IBM's ROM; DOSBox
  carries a copy. *Wing Commander* uses text only for its "Loading..." lines, which the DOS layer
  logs anyway.
- **The 16-color planar modes aren't drawn.**
- **The default palette is only partly there:** the 16 EGA colors and 16 grays, not the VGA
  BIOS's hue ramps. Games set their own palette before drawing.

## The keyboard

The PC's keyboard sends a **scan code** for each key going down (its *make code*) and the same code
with bit 7 set when it comes up (its *break code*). Keys the AT keyboard added, such as the separate
arrow block and the right Ctrl, come with an `E0h` byte first. The 8042 keyboard controller passes
each byte to port 60h and raises IRQ 1.

There are two ways a program reads keys, and the machine supports both:

1. **Through the BIOS.** The BIOS's IRQ 1 handler reads port 60h, tracks the shift keys, turns
   each key into a scan code and a character, and puts it in a 16-key ring buffer in the BIOS
   data area. `INT 16h` takes keys from that buffer, and DOS's keyboard functions read through
   it. Confederation's BIOS handler is a few bytes of real code in ROM, which reads the port and
   offers the key to the intercept (below), and C# (`Machine.Keyboard.cs`) for the rest. The
   buffer is the real one in emulated memory.
2. **Directly.** Games install their own IRQ 1 handler and read port 60h themselves, for the
   up-and-down state of every key: a flight game has to know that the stick key is *held*.
   *Wing Commander* does this. Its handler can still chain to the BIOS's.
3. **Through the keyboard intercept.** On an AT, the BIOS's IRQ 1 handler first calls `INT 15h`
   function 4Fh with each scan code. A program hooking it sees every key, up and down, and can
   keep one from the BIOS (by returning with carry clear) or let it through, so it gets the
   held-key state *and* leaves the BIOS buffering keys for DOS. A program asks whether the BIOS
   does this with `INT 15h` function C0h, which points at a table of the machine's features.
   *Wing Commander II* asks, and uses the intercept if it's there; if not, it takes IRQ 1 over
   completely, as in 2, and then its own name prompts, which read keys through DOS, never get any
   ([how that was found](wing-commander-ii.md#the-keys-that-never-arrived)).

The controller passes on one byte at a time: the next waits until the program has read port 60h,
as the real keyboard waits for the controller. The 8042 also holds the **A20 gate**, the line that
lets addresses wrap at 1 MB or reach the memory above it, so that's handled here too, along with
the faster port 92h later machines added.

## Checking it

The tests check the VGA in two ways:

- **Without the game** (`VgaTests.cs`): a pixel in mode 13h through a palette entry set by port,
  a Mode X write reaching only the plane the map mask picks, a four-pixel copy through the latches,
  and the retrace coming at its line in every frame. The keyboard is checked the same way in
  `DosTests.cs`: a program waits on `INT 16h`, and a Shift+A pressed on the machine's keyboard
  arrives as `'A'`.
- **With the game** (`GameStartTests.cs`): *Wing Commander* runs from launch, and screenshots are
  saved at 12, 25 and 36 emulated seconds: the conductor, the Origin FX logo, and the battle. Each
  must be a real picture, at least 16 colors. The screenshots land in `screenshots/` next to the
  test binary, to look at; they stay out of the repository, as the game's art isn't ours to
  publish.

Because time in the machine follows instructions, each run is the same: the same scene is on
screen at the same emulated second every time. The whole intro, 40 emulated seconds, runs in about
three seconds.

## What the intro showed

The service log, read past the mode set:

- The game loads `FONTS.FNT`, then `MUSIC.MID` dozens of times (it streams the score from disk),
  then `TITLE.VGA` for the logo and the battle.
- It installs another timer handler for its music, and on every tick that handler saves and
  restores the EMS page map (`INT 67h` functions 47h and 48h). A music driver running in an
  interrupt has to do this: the interrupted code may have any pages mapped, and the music data
  lives in EMS too.
- The intro is silent. Sound is step 5 of the [roadmap](../ROADMAP.md): the game probes the AdLib
  at ports 388h and 389h, and finds nothing yet.

Next, the machine gets a window and a player: [the player](the-player.md).
