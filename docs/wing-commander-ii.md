# *Wing Commander II*

*Wing Commander II: Vengeance of the Kilrathi* (1991) now plays from its title menu, through
the introduction with its speech, to signing on a pilot and flying the first mission. Its
*Special Operations* 1 and 2 start too. Getting there took one real fix to the machine, found by
following a key press through the game's own code, and that search is most of this note. It's a
good example of how a focused emulator finds what it's missing: the game does something
reasonable, nothing happens, and the question is *why*.

## The run, step by step

This is the path the game test (`Wing_Commander_II_signs_on_a_pilot_and_flies`) takes, with the
emulated time it takes:

1. **The title** (about 60 s, after the Origin logo and the opening credits). The menu has a
   pointer, which the arrow keys move and Enter clicks. It starts just *below* the "Start New
   Game" button, so Enter alone does nothing; a tap of Up puts it on the button.
2. **The introduction.** Speech from the *Speech Accessory Pack*, which GOG's copy includes:
   `speech.s00`, `s01` and `s02`, in 4,000-byte blocks through the Sound Blaster
   ([the sound](the-sound.md#what-wing-commander-ii-does)). Esc skips each scene.
3. **The personnel database**: "[T]ransfer an existing personnel file" or "[C]reate a new
   personnel file". It takes no keys for its first five or six seconds (a key pressed earlier is
   dropped), then C creates a pilot.
4. **Names.** "Enter first name", "Enter last name", "Enter callsign". The game writes
   `SAVEGAME.WC2`, into the save folder like every other write.
5. **The quarters** on the *Concordia*, with "Fly mission" under the pointer. Enter goes to the
   stardate, the conversations and the briefing; Esc skips each.
6. **The cockpit.** The game loads the player's ship (`pcship.v00`), the cockpit displays
   (`vdu.v00`), its enemies and missiles, and `tape.tmp` for the instant replay, and the range
   to Nav 1 counts down.

Nothing on the way leaves a port unanswered or a DSP command undone.

## The keys that never arrived

Step 4 didn't work at first. The prompt appeared, and typing did nothing. This is how the cause
was found, in the order it went; each step is something the log or a probe of memory showed.

**DOS was answering, wrongly.** The log showed the game calling DOS function 07h (read a key)
over and over. DOS's console input had been a stub that never returned a key, since *Wing
Commander* never asks for one. It now reads the BIOS's buffer as DOS does
([the DOS layer](the-dos-layer.md)): functions 01h, 06h, 07h, 08h, 0Bh and 0Ch, with a key that
has no character (an arrow) coming as a zero and then its scan code. That was a real gap, but
typing still did nothing: the BIOS buffer stayed empty.

**The game had taken the keyboard over.** WC2 installs its own IRQ 1 handler, as *Wing
Commander* does. Reading its code in memory (a probe dumped the bytes at the vector's address and
they were decoded by hand), the handler begins `cmp word cs:[1D4h],0`. When that word is zero it
passes the key on to the BIOS. When it's not, it handles the key itself: it reads port 60h,
acknowledges through port 61h, and calls a routine that records each key as down (40h) or
released (80h) in a 256-byte table. Its main loop walks that table and posts events (key down,
repeat, up) to a queue its screens read. The word was 1, so no key reached the BIOS.

**The prompt reads through DOS anyway.** The code calling DOS turned out to be Borland C's
`kbhit()` and `getch()`, inside a `waitkey()` (`while (kbhit()) getch(); return getch();`), called
for each character by the name prompt. So the game *expects* keys in the BIOS buffer, while its
handler keeps them out. On a real machine that would hang, so the machine had to be different
somehow.

**The flag never changed.** Sampling the word through the whole run showed it set at start-up
and never cleared. Searching memory for writes to it found two: one sets it to 1, one clears it.
Neither ran anywhere near the prompt.

**The other half of the handler's segment.** Dumping the code around the handler turned up a
second entry point, beginning `cmp ah,4Fh`: an **INT 15h handler for the keyboard intercept**,
doing the same key bookkeeping and then passing the key on with carry set. And in the game's
start-up code, just before it decides which way to watch the keyboard, a call to `INT 15h`
function C0h. The log had one line about it: `15/C0 not supported`.

That was the cause. WC2 asks the BIOS for its **system configuration**. If the table says the
BIOS calls the intercept from its keyboard handler (feature byte bit 4), the game hooks `INT 15h`
and watches keys through it, and the BIOS goes on buffering them for DOS. That's what happens on
AT-class machines from about 1986 on, whose BIOSes have both (and presumably under DOSBox, *to
confirm*). If the BIOS doesn't answer, the game assumes an older machine and takes
IRQ 1 over entirely, and its prompts are left with nothing to read.

### The fix

Two things in the BIOS (`Machine.Bios.cs`):

- **`INT 15h` function C0h** returns a pointer to a configuration table in ROM: model FCh (an
  AT), and the first feature byte 70h: a second interrupt controller, a real-time clock, and the
  keyboard intercept.
- **The BIOS's keyboard handler** is now real code in ROM, as an AT's is: read port 60h, call
  `INT 15h` function 4Fh with the scan code and carry set, and only if it comes back with carry
  still set, buffer the key (the C# part). Then end the interrupt. `INT 15h` function 4Fh itself,
  unhooked, passes every key through.

With that, WC2's log shows `set vector 15h` where it used to show `set vector 09h`, and the name
prompts take what's typed. The DOS tests check each piece on its own: the configuration table's
feature byte, a program on the intercept keeping one key from the BIOS while the next gets
through, and DOS reading keys and arrows.

### The lesson

The bug wasn't in the code that misbehaved. The keyboard handler, DOS and the prompt were all
doing what they should, given what the game had decided at start-up from one BIOS call the
machine didn't answer. A game probing its machine and quietly taking a fallback path is common.
So **every "not supported" line in the log is worth reading**, even when nothing seems wrong at
the time.

### Two things that looked like bugs and weren't

- **Keys ignored at the database.** A press in its first few seconds is dropped, and for a while
  it looked as if the screen needed a key *held* for half a second. Logging the events the game
  posted showed that a long hold only worked because the key was still down when the screen
  became ready. Waiting six seconds, a quick tap works.
- **"maverich".** A conversation seemed to call the pilot "maverich". The save file says
  "maverick": the game's small font fooled the eye.

## Expanded memory (observed)

WC2 asks for EMS at start-up (functions 40h, 46h, 41h and 42h), then allocates 256 pages, the
whole 4 MB on offer (function 43h, BX = 0100h), which is its "Expanded Memory fully used". From
then on it maps pages constantly (function 44h), and saves and restores the page mapping about 110
times a second (functions 47h and 48h, 3,326 of each in its first 30 seconds). That's the pattern
of code that runs on an interrupt, borrows the page frame, and must leave it as it found it:
probably the music or the sound effects (*to confirm*). It needs nothing the EMS emulation
didn't already do.

## `loadfix` (still open)

GOG starts WC2 and the *Special Operations* with `loadfix -32`, which in DOSBox loads the program
32 KB higher than it would otherwise go ([the games](the-games.md#the-loadfix-question-observed-unexplained)).
Confederation loads programs where a real DOS machine would, about 40 KB up, and WC2 plays from the
title to the cockpit there with nothing going wrong. So whatever `loadfix` is for (the usual
reason, EXEPACK's bug below 64 KB, doesn't apply to `WC2.EXE`), it isn't needed here. Why GOG
added it is still unexplained.

## *Special Operations* 1 and 2

`SO1.EXE` and `SO2.EXE` share WC2's folder, its data and its settings. Each prints "Loading WC2 -
SPECIAL OPERATIONS 1 (or 2). Please wait...", finds the expanded memory, enables speech and
reaches the same title menu, with nothing left unanswered. The player starts them with
`confederation "<folder>" SO1.EXE`. They haven't yet been played past their menus.

## What's next

- **Playing a mission through**: flying, firing, the sound effects, and landing, which nothing
  has checked yet.
- **The mouse.** The menus and the quarters are made for a pointer, and the arrow keys only
  stand in for it; an `INT 33h` mouse driver will make them natural.
- **Typing at the right moment.** The player passes keys through as they're pressed, so a person
  will simply type when the prompt appears. A test script has to wait for it.
