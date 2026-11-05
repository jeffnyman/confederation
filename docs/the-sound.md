# The sound

Both games play their scores, on an emulated AdLib or an emulated Roland MT-32, and *Wing
Commander II* speaks through an emulated Sound Blaster. This note covers the FM synthesizer the
AdLib carried, how the machine finds out when to make each sample, how the player keeps the sound
and the picture together, the MT-32 with the MPU-401 interface in front of it, and the Sound
Blaster's digitized sound with the DMA controller that feeds it.

The code is `src/Confederation.Core/Sound/`, `Devices/DmaController.cs` and `Machine.Sound.cs`, and
`Sdl/SdlAudio.cs` in the player.

## What the games ask for

Before writing any sound code, it's worth seeing what the games use, from their files and from
the service log:

- **GOG's DOSBox settings** give both games a Sound Blaster 16 at port 220h (IRQ 5, DMA 1), its
  OPL3 FM chip (`oplmode=auto`), an MPU-401 MIDI interface, and sound at 44,100 Hz.
- **Each game's config file** names its devices: *Wing Commander*'s `WINGCMDR.CFG` holds
  `v a904 z`, and *Wing Commander II*'s `wc2.cfg` holds `v a904 c25 d1`. The format isn't
  documented. Trying letters showed that `a…` means the AdLib and `r` the Roland
  ([below](#which-letter-is-the-roland)), and that `c25` is the Sound Blaster at 2**2**0h and IRQ
  **5**: with `c35` the game looks for the card at 236h, and with `c27` it takes over IRQ 7's vector
  instead of IRQ 5's. `d1` is then most likely DMA **1** (*to confirm*).
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
the MT-32 joins the same way, and the Sound Blaster will.

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
- **The MPU-401 and the MT-32** (`MidiTests.cs`): reset and UART mode are acknowledged, and a reset
  in UART mode isn't; bytes are gathered into messages, with running status, a real-time byte in
  the middle of a message, and System Exclusive; in intelligent mode, data goes nowhere except the
  one message after a "want to send". With the ROMs at hand, the module shows a message sent to
  its display and sounds a note.
- **The DMA controller and the DSP** (`SoundBlasterTests.cs`): a channel moves its bytes and stops
  at its terminal count, or starts over in auto-initialize mode, and wraps within its 64 KB page;
  the DSP resets with AAh and answers its version; a forced interrupt comes at once and is
  acknowledged; a block plays loudly and interrupts on exactly its last sample, which
  `FramesUntilInterrupt` said beforehand; auto-initialize interrupts at each block's end until
  told to stop; a masked channel holds the DSP; direct output sets the level at once.
- **The game on the Sound Blaster**: *Wing Commander II* starts a new game from its menu and reaches
  the Emperor's scene (`wc2-emperor.png`), and eight seconds of speech are recorded to
  `wc2-speech.wav`: at least fifteen blocks, back to back, and loud.
- **The game on the Roland**: the intro runs with `r` in its config, the display reads "Origin
  Sound System!", the first ten seconds are silent while the instruments go across, and there's
  music after seventeen. It's recorded to `wc-intro-mt32.wav`. Without the ROMs (in `_mt32-roms`
  beside the project, or in `CONFEDERATION_MT32_ROMS`), these tests pass without checking.

The recording's loudness, two seconds at a time, follows the intro: silence for the first six
seconds, a quiet stretch with very few zero crossings (a low drone, or the driver setting up its
voices; *to confirm by listening*), then the score building through the Origin FX logo to its
loudest in the space battle, around 30 seconds in.

## The Roland MT-32

Roland's MT-32 (1987) was a synthesizer in a box, sold to musicians and then, in great numbers, to
PC owners, because Sierra and then Origin wrote their scores for it. It makes sound by "linear
arithmetic" (LA): short sampled attacks from a PCM ROM joined to synthesized sustains, through
filters and a reverb. A computer reached it by a MIDI cable from an interface card, Roland's
**MPU-401**, at ports 330h and 331h. The CM-32L (1989) is the same module made for computers, with
more sound effects and no buttons, and the LAPC-I is a CM-32L on a card with the MPU-401 built in.
Games of 1990 on were written with the CM-32L's sounds in mind, so it's what the machine emulates
when its ROMs are there.

### The synthesizer: Munt, ported for earlier projects

The synthesis is **Munt** (libmt32emu 2.7.0), which rebuilds the module around its own ROMs: it
doesn't run Roland's firmware, but reads the control ROM's patches, timbres and tables and the PCM
ROM's samples, and implements the LA chip and the reverb from the hardware's behaviour. It was
ported to C# for Daventry and then GUE, and is carried over unchanged (`Sound/Mt32/`). DOSBox uses
the same library, so again it's what most people playing today hear.

The ROMs are Roland's, and aren't part of the project. The player is pointed at a folder of them
(`--mt32-roms`), and the files are recognized by their SHA-1, whatever they're called; halves
dumped from separate chips are put back together. The CM-32L's are preferred, then the MT-32's,
newest first-generation firmware (1.07) first.

Munt makes its sound at the module's own 32,000 samples a second. `Mt32Module` converts that to
the machine's 44,100 by straight-line interpolation between samples, and adds it to the mix like
any other source. Messages are played the moment they arrive, which is exact for the same reason
the FM chip's register writes are: the machine makes the sound up to the moment of each write to
the MPU-401 before the write takes effect.

### The MPU-401: the "intelligent" mode, barely

The MPU-401 (1984) was a small computer of its own. In its **intelligent mode** it kept time and
played up to eight tracks for the program; in **UART mode** it passes every byte straight to the
cable. Most games use UART mode: reset the card (command FFh, acknowledged with FEh on the data
port), command 3Fh, and from then on write MIDI bytes. The status port says when a byte may be
written (bit 6 clear) and when one is waiting to be read (bit 7 clear).

*Wing Commander*'s driver does neither, quite. It stays in intelligent mode, and before each
message sends command **DFh**, "want to send a system message", then the message itself, a System
Exclusive. The card passes that one message to the cable and goes back to waiting for commands.
D0h-D7h, "want to send data", does the same for one channel message. That's the slice of the
intelligent mode written here (`Mpu401.cs`, from DOSBox's `mpu401.cpp`): the "want to send"
commands, reset and UART mode. Any other command is acknowledged and logged; none has come up.

The bytes are gathered back into whole MIDI messages before they reach the synth: a status byte
and its data, with *running status* (a message may leave out its status byte when it's the same
as the last one's), System Exclusive messages from F0h to F7h, and real-time bytes (F8h and up)
set aside wherever they fall.

### Which letter is the Roland

The games choose their music device from their config files, which their installers wrote, and
GOG's say AdLib. *Wing Commander II*'s installer names the devices in its menus (`PC speaker`,
`AdLib Music Card`, `Sound Blaster`, `Roland MT-32 or LAPC-1`), but nothing says which word each
one writes, and the games' sound drivers (`TM.DRV`, `SUPERTM.DRV`, `STRAX.DRV`) are compressed,
so their strings can't be read. So the letters were tried: each game was run for a few seconds
with a dozen configs in turn, an MT-32 plugged in, and the MPU-401's log read.

- In *Wing Commander*, `v r z` reaches the MPU-401; `m`, `l`, `s`, `c` and `b` don't. A config
  holding both `a904` and `r` takes the later one.
- In *Wing Commander II*, the same `r` works, and the game then loads `music.r00` instead of
  `music.a00`.

`--music roland` in the player writes the game's config into the save folder with `r` in place of
the AdLib's word, as running the installer again would, and `--music adlib` puts the AdLib's word
back. The game's own file isn't touched, and the choice stays with the saves.

### What the games do with it

Both drivers start the same way. They put **"Origin Sound System!"** on the module's display, then
send the game's own instruments as System Exclusive messages: *Wing Commander* sends 76 of them,
up to 256 bytes each, one about every eighth of a second. A real module needs the pause: a MIDI
cable carries about 3,000 bytes a second, and the MT-32 takes time to digest each message (early
firmware is known to overflow its buffer when sent data too fast). So the intro waits for them.
*Wing Commander*'s first notes come at about 16 seconds, where the AdLib's come at about 6. That's
the game's own pacing, not the emulator's (*to confirm against DOSBox*). *Wing Commander II* sends
118 messages and starts its notes at about 14.5 seconds.

## The Sound Blaster's digitized sound

Creative's Sound Blaster (1989) carried an AdLib's FM chip, which is how it played everyone's
music, and something the AdLib hadn't: a **DSP**, a small microcontroller with a digital-to-analog
converter, which plays recorded sound. That's *Wing Commander II*'s speech. The machine's card is
a Sound Blaster 16 (DSP version 4.05), as in GOG's settings, at 220h, IRQ 5 and DMA 1, written from
DOSBox's `sblaster.cpp` (`Sound/SoundBlaster.cs`).

### Talking to the DSP

The DSP has four ports, at the card's base plus 6, A, C and E:

- **Reset** (226h): write 1, then 0, and the DSP answers AAh. That's how a program finds the card,
  and the probe *Wing Commander II* made at start-up.
- **Commands** (22Ch): a command byte and its arguments, written once bit 7 of a read of the same
  port says the DSP is ready.
- **Answers** (22Ah), read once bit 7 of **22Eh** says one is waiting. Reading 22Eh also
  acknowledges the card's interrupt.

The commands that matter for playing are few: **40h** sets the rate as a "time constant" (256 less
a million over the rate), **D1h** turns the speaker on, and **14h** plays a block of 8-bit,
unsigned samples of a given length. **1Ch** plays blocks over and over ("auto-initialize"), which
later games use. Version (E1h), identification (E0h), a forced interrupt (F2h, which programs use
to find the card's IRQ) and a few more answer as DOSBox's do. Any other command is logged and
skipped.

### DMA: the samples come by themselves

The DSP doesn't take its samples from the processor. A program puts the sound in memory and sets up
a channel of the **8237 DMA controller** (`Devices/DmaController.cs`, from DOSBox's `dma.cpp`)
to hand it over: the buffer's address and its length less one, the direction, and then unmasks the
channel. The DSP takes a byte through the channel whenever it needs one, and the channel steps its
address and counts down, without the processor. When the count passes zero, the channel reaches
its "terminal count" and stops (or starts over, in auto-initialize mode).

The 8237 is a 1970s chip with 16-bit addresses. The top bits of a PC's address come from a separate
page register that doesn't count, so a transfer can't cross a 64 KB boundary: it wraps within its
page. That's why DOS programs of the time align their sound buffers carefully. Its 16-bit registers
are written and read a byte at a time, low then high, through a flip-flop.

### The interrupt, on time

At the end of a block the DSP raises its interrupt, and that's what keeps speech going: *Wing
Commander II*'s interrupt handler starts the next block. If the interrupt comes late, there's a gap
in the speech, as long as it's late.

The samples are made just in time, as the FM chip's are: the DSP takes bytes through DMA as the
sound is rendered, and raises its interrupt when it takes a block's last byte. But rendering only
happens at a port write or the end of a run, up to 14 ms apart, and a program waiting for the
interrupt writes to no port. So the machine also asks the card how many samples are left until its
interrupt (`FramesUntilInterrupt`), works out the instruction at which that sample falls, and
makes the sound up to then: a **sound event**, checked alongside the timer in the run loop. The
interrupt comes on the very sample it would on the card. Any write to the card or the DMA
controller works the event out again, since it may start, stop or change the block, and a halted
processor waiting for an interrupt skips ahead to the sound event as it does to the timer's.

Sound is fetched from memory as it's rendered, so a program that writes into its buffer sees its
writes played if the playing hasn't reached them yet, as on the real card. Reading the DMA
controller's address and count makes the sound first, so a program polling them sees the channel
where it would really be.

### What *Wing Commander II* does

At start-up, it resets the DSP, sets the time constant to A3h (10,752 samples a second), and turns
the speaker on. Then it takes over IRQ 5's vector (0Dh). Without the *Speech Accessory Pack*, the
game's own README says, its only speech is in the first scene of the introduction, where the
Emperor and Prince Thrakhath plan the war, and the introduction plays when a new game is started.
At the title menu the pointer starts below the "Start New Game" button. A tap of Up puts it on the
button, and Enter starts the game.

Then the game opens `speech.s00` and plays it in single-cycle blocks of 4,000 bytes, a little over
a third of a second each. Its interrupt handler starts each block the moment the last one ends,
within a tenth of a millisecond of machine time. In the log, every block is a line such as
`sound blaster: 4000 bytes at 10752 Hz`.

### Not done

16-bit sound (on the AT's second DMA controller), the ADPCM-compressed formats, recording, and the
SB16's mixer's volume controls. Nothing *Wing Commander II* has asked for so far needs them (only its
opening has been run). The commands are logged if a program uses them.

## Still to come

- **The PC speaker**, the timer's channel 2 through port 61h, for whatever still beeps.
