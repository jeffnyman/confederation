# Save states

A save state is the whole machine written to a file at one moment, to be put back later exactly as
it was: in the middle of a dogfight, a line of speech or a bar of music. The games have their own
saves, but only between missions. **Ctrl+F5** saves the state while playing, **Ctrl+F9** loads it
back, and `confederation wc2 --resume` starts from it.

The code is the core's `MachineState.cs` and `Machine.State.cs`, and the player's `SdlPlayer.cs`.

## Every field, not a list of them

The obvious way to save a machine is for each device to write what it keeps, and read it back in
the same order. It's also the easy way to get it wrong. Each device has to list its fields by hand,
and a field added later and forgotten in the list doesn't fail. It's simply not saved, and the
state comes back subtly wrong, perhaps only in one game, minutes after loading.

Confederation does what Retrograde, the author's C64 emulator, does: it **walks the objects**.
Starting from the machine, reflection finds every field of every object, its private fields
included, and writes it. Numbers, structures and strings are written as they are. Objects, arrays
and collections are written once each and referred to by number after that, so the wiring between
them survives: two parts of the MT-32 sharing one array of its memory still share it after a load.
Nothing a device keeps can be forgotten, because nothing is listed. The machine is about 9,000
lines of C#, and the MT-32's synthesizer, ported from Munt, is another 7,600, with many
small objects for its parts, partials and envelopes. Listing all of that by hand would be a long
job, and an easy one to get wrong.

Three kinds of thing are left out:

- **Delegates**, the wiring set up when the machine is built: the processor's call into the BIOS,
  the log, the sound card's interrupt. Loading fills in the machine's own objects rather than
  making new ones, so the delegates that point at them still do.
- **Fields marked `[NotSaved]`**, which describe how the machine was built rather than what it
  does. So far, that's the folders drive C: was made from. A state goes into a machine made with
  folders of its own.
- **Fixed objects**, which are written as a reference to the loading machine's own. These are the
  objects in the core's static fields, the lookup tables that are the same in every run, and the
  MT-32's ROMs and what's made from them. The ROMs are the user's, they're large, and the machine
  a state goes into has loaded its own. A state with an MT-32 can only go into a machine with an
  MT-32 made from the same ROMs. The player gets that right by itself when the game is played the
  same way, and says so when it isn't.

Two things outside the machine need more than a field's value:

- **An open file** is saved as its name, whether it was open for writing, and how far in it was.
  On loading, it's opened again and moved to the same place. *Wing Commander II* keeps its speech
  file open while it talks, reading a block at a time, and the test of its speech saves halfway
  through one.
- **A search in progress** (DOS's find-first and find-next) keeps the files still to come. They're
  saved by their names.

## What a state doesn't hold

**The files.** Neither the game's folder nor the save folder is copied into a state. A state is a
moment of the machine, not of the disk. So loading a state doesn't undo a game the player saved
since: that save is still on the disk, and the game will find it. This is what DOSBox does too,
and it's the right way round. The game's own saves are the ones to trust for keeping progress.

**Compatibility with another build.** A state is stamped with a fingerprint of every field's name
and type in the core. A build whose fields differ refuses the state ("saved by a different
version") rather than misreading it. The cost is that any change to a device's fields retires the
states saved before it. Save states are for the moment, and the games' saves are for keeping.

## What the tests found

The test runs the machine three ways from one state and checks that they agree: sample for
sample in the sound, and in the processor, memory and picture at the end. The original runs on
from the state; a fresh machine loads it and runs; and the original loads it back over where it
had got to, and runs again. That third way is what Ctrl+F9 does. *Wing Commander*'s intro was
saved while the MT-32 played its music, and *Wing Commander II*'s introduction while the Sound
Blaster spoke. The first runs found two problems:

- **Munt's random numbers.** The synthesizer adds a little jitter to a timer with C's `rand()`. The
  port used .NET's `Random`, whose state is hidden inside the class library, out of reach of a
  walk of the core's own fields. It's now the C standard's own example `rand()`, one number the
  synthesizer keeps. In a state saved and loaded at once nothing differs, so a test of a state
  loaded straight back wouldn't notice. It took a test running on for seconds.
- **An object filled twice.** Loading fills in the objects already there, at the same places in
  the machine. But a machine that has run on since the save has moved its objects around: the
  MT-32's pool of partials has handed them out differently. One object can now sit at two places
  that held two different objects in the state. Filled in from both, it made them one, and the
  sound differed four samples after loading. Now each of the machine's objects is filled in once
  at most; the second place gets a new object.

The fresh machine passed from the start. Only the reload, the thing a player actually does, found
the second problem, which is why the test checks all three ways.

*Wing Commander III* found a third. Its files are on the CD, and DOS keeps, for each file open
there, its directory entry from the disc, with the file's time as a `DateTime`, which a state
couldn't hold. Saving one failed while the game had a file open on the CD, which is always. It
holds them now, and a test saves a state while a program has a file on a CD open. A state of
*Wing Commander III* is about 9 MB, most of it the 16 MB of memory.

## Keys held

The keyboard in a state is the keyboard as it was when the state was saved. That's at least Ctrl,
held for Ctrl+F5. The keys held now may be different, so a key down then would stay down in the
game forever. The machine keeps a note of the keys it has been told are down, and that note is
saved with everything else. After loading, the player lets them all go, then presses again the
ones still held.

## Size and speed

Observed for *Wing Commander* with the MT-32 playing: a state is about 750 KB, compressed with
gzip at its fastest setting. It saves in 10 to 20 ms and loads in under 15 ms, less than a frame
at 70 a second. Most of it is memory: 1.1 MB of conventional and high memory, 256 KB of video
memory, and whatever expanded memory the game has allocated.

## Where states go

One state for each program, in a folder `states` beside the saves, named for the game's folder as
its save folder is: `%APPDATA%\Confederation\states\Wing Commander II\WC2.state`. A state isn't
kept in the save folder itself, because that is the game's drive C:, and the game would see it
there. `--state <file>` names another file. A state is written beside its file first and then
moved over it, so a save that fails leaves the last one whole.

## Checking it

- **The core** (`StateTests`): the two runs above, three ways each, with the games and the ROMs.
  Without the games, the tests check that a broken or cut-short file is refused, and that a state
  with an MT-32 goes only into a machine with one.
- **The player** (`ProfileTests`): `--resume` with no state says how to make one. With a state of a
  stand-in program, it runs. With a file that isn't a state, it refuses.
