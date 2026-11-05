# The sound

*Wing Commander* now plays its score, through an emulated AdLib. This note covers the FM
synthesizer the AdLib carried, how the machine finds out when to make each sample, and how the
player keeps the sound and the picture together. The Roland MT-32 and the Sound Blaster's
digitized sound come next, and will be added here.

The code is `src/Confederation.Core/Sound/` and `Machine.Sound.cs`, and `Sdl/SdlAudio.cs` in the player.

## What the games ask for

Before writing any sound code, it's worth seeing what the games use, from their files and from
the service log:

- **GOG's DOSBox settings** give both games a Sound Blaster 16 at port 220h (IRQ 5, DMA 1), its
  OPL3 FM chip (`oplmode=auto`), an MPU-401 MIDI interface, and sound at 44,100 Hz.
- **Each game's config file** names its devices: *Wing Commander*'s `WINGCMDR.CFG` holds
  `v a904 z`, and *Wing Commander II*'s `wc2.cfg` holds `v a904 c25 d1`. The meaning of the letters
  isn't documented; `c25 d1` matches a Sound Blaster at 2**2**0h, IRQ **5**, DMA **1**, which is a
  guess to confirm.
- **The log**: *Wing Commander* writes the AdLib's ports 388h and 389h and nothing else that
  makes sound. *Wing Commander II* writes the AdLib too, and also resets a Sound Blaster's DSP at
  226h and reads 22Ah and 22Eh, which is how a program finds the card's digitized sound.

So the FM chip comes first, as it plays *Wing Commander*'s music and effects, and both games
use it.

## FM synthesis and the OPL

Yamaha's **OPL2** (YM3812), on the AdLib card of 1987, makes sound by *frequency modulation*: each
of its nine voices has two *operators*, oscillators with their own envelope, and one modulates the
frequency of the other. A few dozen register settings make an instrument, which is why one cheap
chip could play a whole score. The **OPL3** (YMF262), on the Sound Blaster Pro 2 and 16, doubles the
voices and adds stereo and four-operator voices, but behaves exactly like an OPL2 until a program
sets its "new" bit (register 105h). So the machine emulates an OPL3, as GOG's DOSBox does, and both
kinds of program are served.

### The synthesis: ported, twice removed

The synthesis is **DOSBox's DBOPL**, ported to C# for the author's earlier projects, Daventry and
GUE, and carried over unchanged (`DbOplChip.cs`). There's no reason to write an FM synthesizer
from scratch: it's 1,500 lines of careful tables and envelope arithmetic, and DBOPL is DOSBox's
default, so it's what most people playing these games today hear.

Two other cores exist, and are worth knowing about: **Nuked OPL3** (a transistor-level
reconstruction, the most exact, and slower) and **MAME's** `fmopl` (which ScummVM also carries).
DBOPL is the middle ground between them in exactness and speed.

### The card: the part an emulator can't skip

Ported game players like ScummVM can use DBOPL alone: they drive the chip themselves and never ask
it anything. An emulator runs the game's own sound driver, and that driver *asks*. So around the
synthesis there's a card (`Opl.cs`), written from DOSBox's `adlib.cpp`:

- **The address and data ports.** A write to 388h picks a register, a write to 389h sets it. 38Ah
  picks a register in the OPL3's second bank. The Sound Blaster has the same chip at 220h–223h,
  and at 228h and 229h for programs that think it's an AdLib.
- **The timers.** The OPL has two: timer 1 counts in 80-microsecond steps, timer 2 in 320. When one
  runs out, it sets its bit in the status byte read from port 388h, and the top bit with it.
- **The status.** This is how a program **finds an AdLib**: stop and clear the timers, check the
  status is clear, start timer 1 one step from the top, wait at least 80 microseconds, and check
  that the status now shows it ran out. Without timers there's no AdLib, as far as the game can tell.
  The test (`SoundTests.cs`) runs exactly the sequence AdLib's documentation gave.

The timers are kept as DOSBox keeps them: nothing counts, but each remembers when it will run out,
and a read compares that with the machine's time.

## When to make each sample

A sound chip makes samples continuously, 44,100 a second here, while the processor runs. The
emulator has to decide when to work them out. There are three common answers:

1. **Every few instructions**, interleaved with the processor. Exact, and slow.
2. **Once a frame**, all at once. Simple, but every register write in the frame takes effect at
   the frame's start, and notes land up to 14 ms early. Rhythms come out uneven.
3. **Just in time**: run the processor, and whenever a program is about to change the sound
   (a write to a sound port), first make the samples up to that moment, with the chip as it was.
   Then apply the write. At the end of each run, make the rest.

Confederation does the third (`Machine.UpdateSound`). Because the machine's time follows the
instructions run, "now" is exact: each write lands on the sample it would on the real machine, and
nothing is computed that isn't needed. Every sound device adds its samples into one stereo mix, so
the MT-32 and the Sound Blaster will join the same way.

The machine keeps the last second of sound for the player to read, and drops what's older. A test
or a headless run that never reads the sound doesn't fill memory.

## Keeping picture and sound together

The player can be timed by two clocks: the host's, or the sound device's. They drift apart, by a
fraction of a percent, which over minutes is audible as stutter (the queue runs dry) or growing lag
(it fills). So, as in Retrograde, **the sound device sets the pace** when there's sound: the player
runs a frame, queues its sound, and waits until the device has played the queue down to about
60 ms before running the next frame. The picture then follows the sound exactly. Without a sound
device, or with `--no-sound`, the host's clock sets the pace as before. At full speed (F12) the
sound is dropped.

## Checking it

- **The card, on its own** (`SoundTests.cs`): AdLib's detection sequence finds the chip; timer 2
  runs out after its count of 320-microsecond steps; an unprogrammed chip is silent; and one voice
  set to a plain sine and keyed on sounds, the same on both sides in OPL2 mode.
- **The game** (`GameStartTests.cs`): the intro test now records its sound to `wc-intro.wav`
  beside the screenshots, and checks there are 36 seconds of it, and that it's loud enough to be
  music.

The recording's loudness, two seconds at a time, follows the intro: silence for the first six
seconds, a quiet stretch with very few zero crossings (a low drone, or the driver setting up its
voices; *to confirm by listening*), then the score building through the Origin FX logo to its
loudest in the space battle, around 30 seconds in.

## Still to come

- **The Roland MT-32**, through an MPU-401 at 330h, from Daventry's port of the Munt emulator. It
  needs the MT-32's ROMs, which the user supplies.
- **The Sound Blaster's DSP and DMA**, for digitized sound: *Wing Commander II*'s speech and
  effects. That needs the 8237 DMA controller, and the DSP's IRQ.
- **The PC speaker**, the timer's channel 2 through port 61h, for whatever still beeps.
