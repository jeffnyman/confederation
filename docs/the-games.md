# The games

This note records what each *Wing Commander* release is, what is on hand in `_wingc`, and what can
be learned about each game's needs *before* writing any emulator code, just by looking at the
files. That last part is a habit worth building: the files usually tell you a lot about what the
machine has to provide.

Facts marked **(observed)** come from inspecting the files in `_wingc`. Facts marked **(to
confirm)** are from general knowledge of the games and should be checked by tracing them once the
emulator runs.

## What is on hand

| Folder | What it is | How GOG launches it |
|---|---|---|
| `_wingc/Wing Commander` | *Wing Commander* (1990) with *The Secret Missions* 1 and 2. GOG release. | DOSBox, `mount C ".."`, then a menu choosing `WC.EXE` or the mission disks |
| `_wingc/Wing Commander II` | *Wing Commander II: Vengeance of the Kilrathi* (1991) with *Special Operations* 1 and 2. GOG release. | DOSBox, `loadfix -32 wc2.exe` (or `so1.exe`, `so2.exe`) |
| `_wingc/Wing Commander III - Heart of the Tiger` | *Wing Commander III* (1994). GOG release. | DOSBox, `imgmount D "..\DATA.DAT" -t iso`, then `wc3.exe` from `D:` |
| `_wingc/WC-Kilrathi-Saga` | *The Kilrathi Saga* (1996), the Windows 95 re-release of the first three games, as five ISOs | Out of scope |
| `_wingc/W.C.A.T. ...` | *W.C.A.T.*, a fan overhaul mod for WC1 and WC2, packaged with DOSBox Staging | A reference, and a possible later target |

## *Wing Commander* and *Wing Commander II*

### The executables (observed)

Both `WC.EXE` and `WC2.EXE` are plain DOS **MZ executables**. They are not compressed with
EXEPACK, LZEXE or PKLITE (none of their signatures or error strings appear), and they don't use a
DOS extender. They run in real mode.

Both carry a Borland copyright string (`Copyright 1990 Borland Intl.` and `Copyright 1991 Borland
Intl.`), and both MZ headers have the bytes `FB 30` at offset `0x1C`, the mark Borland's TLINK
linker leaves. So the games were written in Borland C/C++, and the code will look like
Borland-compiled code. That matters for which instructions the CPU core sees first.

Reading the MZ header fields:

| | `WC.EXE` | `WC2.EXE` |
|---|---|---|
| File size | 304,976 bytes | 363,696 bytes |
| Load image (header pages and last-page bytes) | 134,080 bytes | 168,416 bytes |
| Header size | 4,608 bytes (`0x120` paragraphs) | 6,656 bytes (`0x1A0` paragraphs) |
| Relocation entries | 1,134 | 1,597 |
| Data appended after the load image | 170,896 bytes | 195,280 bytes |

**More than half of each file sits after the part DOS loads.** That's the signature of
*overlays*: code that is not kept in memory all the time but read from the executable on demand.
Borland's overlay manager (VROOMM) worked this way. It could also cache overlays in EMS, which fits
the next observation.

**Confirmed for *Wing Commander*** ([the DOS layer](the-dos-layer.md)): at startup the game opens
`WC.EXE`, seeks to offset 134,080, exactly the end of the load image, and installs a handler for
INT 3Fh, the vector Borland's overlay manager uses. File-level DOS emulation handles this with no
special support.

### Expanded memory (observed)

Both executables contain the strings `Expanded Memory Detected.` and `Expanded Memory fully used.`
So both games look for EMS. GOG's DOSBox configs turn on EMS and XMS for all three games.
Confederation will need to provide EMS through the `INT 67h` interface.

**Confirmed for *Wing Commander***: it calls EMS functions 40h to 44h and 46h, allocates every
free page (4 MB here), which is what "Expanded Memory fully used" reports, and maps pages
constantly while loading graphics. It doesn't ask for XMS. *Wing Commander II* is still to be
traced.

### The `loadfix` question (observed, unexplained)

GOG launches *Wing Commander* and *The Secret Missions* with `loadfix -1`, and *Wing Commander II*
and its *Special Operations* with `loadfix -32`. That DOSBox
command takes 32 KB of low memory before starting the program, so the game loads higher in memory
than it otherwise would. The usual reason is EXEPACK's "Packed file is corrupt" bug for programs
loaded below 64 KB, but `WC2.EXE` is not EXEPACK-compressed. So the reason here is still open.
**(to confirm)** what goes wrong without it. A focused emulator can simply load the game at the
segment that works and record why. Confederation loads programs at about 40 KB, where a real DOS
machine would, which is higher than either `LOADFIX` puts them (see
[the DOS layer](the-dos-layer.md#where-the-program-goes)).

### Speed (observed)

GOG sets `cycles=4000` for *Wing Commander* and `cycles=8000` for *Wing Commander II*. The first
game is known for running too fast on faster machines. Confederation should fix each game's speed
in its profile, not leave it to the user.

### Data files (observed)

`GAMEDAT` holds the data, and the names already hint at the engine's structure:

- `*.VGA`, `*.V00`–`*.V08`: image archives for VGA (cockpits, ships, planets, briefing and
  cutscene art)
- `*.PAL`: palettes (`GAME.PAL`, `BRIEF.PAL`, `CONVERT.PAL`)
- `*.WLD`: campaign and save data (`CRUSADE.WLD`, `SAVEGAME.WLD`)
- `CAMP.*`, `MODULE.*`, `CAMPAIGN.S*`: campaign and mission data
- `COMMUNIC.*`: in-flight communications
- `MUSIC.MID`, `FONTS.FNT`

None of these need to be understood to *play* the games under emulation. They become interesting
later, for tooling or for documenting the engine.

### Sound and video (to confirm)

- Video: VGA mode 13h (320×200, 256 colors). Whether either game also uses unchained
  "Mode X" for page flipping needs tracing.
- Music: AdLib (OPL2 FM), Roland MT-32/LAPC-I, and Sound Blaster as an FM device. The MT-32
  versions are the ones people remember.
- *Wing Commander II* adds digitized speech through the Sound Blaster (the *Speech Accessory
  Pack* content), so it needs the DSP and DMA.
- Copy protection: *Wing Commander* asks a question answered from the manual. GOG's release may
  have it removed. To be checked.

## *Wing Commander III*

### What is on hand (observed)

The game's files are inside `DATA.DAT`, a 1.86 GB ISO 9660 CD image that GOG mounts as drive `D:`.
The executable, `wc3.exe`, is inside the image, not in the folder. The folder itself holds only
`OBJECTS.TRE`, the config files and DOSBox.

### What it needs (to confirm)

- A 32-bit protected-mode program running under a DOS extender. Which extender should be read
  from the executable once it is pulled out of the ISO.
- A CD-ROM: at minimum file access to the ISO through MSCDEX-style HLE, possibly streaming at CD
  speeds for video.
- Full-motion video and much more CPU: GOG sets `cycles=max` and 30 MB of memory.

## *Wing Commander IV* and *Prophecy*

Not on hand yet. *Wing Commander IV* (1996) had a DOS version and is a natural extension of the
*Wing Commander III* work. *Wing Commander Prophecy* (1997) is a Windows 95 game built for 3D
accelerators. It isn't a DOS program, so it falls outside this emulator's model and would need its
own approach.
