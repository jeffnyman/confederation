# The CD

*Wing Commander III* came on four CDs and reads from them as it plays: its movies, its missions,
its sound drivers, its own program. GOG ships the CDs as one image, `DATA.DAT`, which its DOSBox
setup mounts as drive D:. This note covers what's in a CD image, how Confederation makes one a
DOS drive, and the driver DOS reached CDs through, MSCDEX.

## ISO 9660

A CD-ROM's file system is ISO 9660 (1988), and an image is its sectors in order, 2,048 bytes
each. GOG's `DATA.DAT` is 909,643 sectors, 1.8 GB (observed):

- **Sectors 0–15** are left for the system, and are zeros here.
- **Sector 16 on** are volume descriptors, each beginning with its type and `CD001`. Type 1, the
  primary one, holds the volume's name (`WC3_CD1`), its size in sectors, and the root
  directory's record. Type 255 ends the set.
- **A directory** is sectors of records, one per entry. Each record gives its length, the sector
  where the entry's data starts and its length in bytes, its date, its flags (02h for a
  directory), and its name. A record never crosses a sector, so the rest of a sector too short
  for the next record is zeros. The first two records of every directory are itself and its
  parent, named by the bytes 0 and 1.
- **Numbers are written twice**, little-endian and then big-endian, for the machines of both
  kinds. Confederation reads the first.
- **A file's name ends in its version**, `;1`, which DOS never shows. A name without an extension
  ends in a dot (`README.;1`).

A file is a single run of sectors, so reading one is a seek to its first sector and a read.

GOG's image isn't any of the four CDs: it's all of them in one volume, every file in the root
(observed). `CD1MOVIE.TRE` to `CD4MOVIE.TRE` (about 400 MB each) and `CD1MISS.TRE` to
`CD4MISS.TRE` sit beside the files every disc carried, such as `WC3.EXE`, `GLOBALS.IFF`, the
shared `.TRE` archives, the HMI sound drivers and the video drivers. So the game never has to
ask for another disc.

`IsoImage` reads the primary descriptor and directories as they're asked for, and leaves out
the Joliet and Rock Ridge extensions for long names, which came after these games.

## Drive D:

Until now DOS here had one drive, C:, and the current drive was always C:. With a CD in, D: is a
second drive. It's read-only, has its own current directory, and is reached the same way C: is:

- **A path is resolved against its drive.** `D:CD1MOVIE.TRE`, `D:\WC3.EXE`, or a bare name when
  D: is the current drive, all reach the image. The current drive is real state now, which
  functions 0Eh and 19h set and read.
- **A file on the CD is a window on the image.** Opening one opens the image file itself, and
  the open file remembers where its sectors start and how long it is. Reads stop at its end,
  and seeks are relative to its start. Since the stream is an ordinary host file, a save state
  keeps it as it keeps any open file, by name and position. It's reopened read-only. *To
  confirm*: no test saves a state with a CD file open yet.
- **Nothing on it can be changed.** Opening for writing, creating, deleting, renaming, making
  directories and setting attributes all answer "access denied" (5). Its files have the
  read-only attribute.
- **The drive calls know it.** The free space call says it's full. The IOCTL calls say it isn't
  removable to DOS, but remote, as MSCDEX's drives were, like a network drive. The file
  information call gives a CD file's drive as D:.
- **Programs run from it.** `D:\WC3.EXE` is read from the image and loaded as any program is.
  Naming a program with its drive's letter makes that drive the current one, as typing `d:`
  before it does in GOG's setup.

## MSCDEX

On a real PC, a CD was two drivers. The hardware's own (`CDROM.SYS`, say) knew the drive, and
Microsoft's CD-ROM Extensions, `MSCDEX.EXE`, made it a DOS drive. MSCDEX also answers programs
on `INT 2Fh` with AH=15h. A game asks it whether there's a CD drive at all, and which letter. A
game on several CDs needs that to find its disc wherever it is, rather than assume D:.

Confederation answers the functions *Wing Commander III* asked for, and absolute reads:

| AX | What | The answer |
|---|---|---|
| 1500h | Installed? How many CD drives, and the first | BX = 1, CX = 3 (D:) |
| 150Bh | Is drive CX a CD drive? | AX nonzero for D:, BX = ADADh |
| 150Ch | Version | BX = 0215h, 2.21 |
| 150Dh | The CD drives' letters | one byte, 3, at ES:BX |
| 1508h | Read sectors, absolute | from the image |

Without a CD, 1500h answers zero drives, as when MSCDEX isn't loaded. Anything else on AH=15h
stops the machine, saying so, so the next thing a game needs shows up.

## What Wing Commander III did with it

Run as GOG runs it, with its folder as C:, `DATA.DAT` as D: and `D:\WC3.EXE`, observed at 20,000
and 40,000 cycles:

1. It loaded from the CD. Origin's loader then reopened its own program, `D:\WC3.EXE`, through
   the environment's path, to read the rest of it.
2. It found `C:\WC3` already there, and read `WC3.CON` and `WC3.CFG` from it. It copied the CD's
   `WC3.CON` over the one in `C:\WC3`, which goes to the save folder.
3. It printed "Loading Wing Commander III ......." and opened `GLOBALS.IFF` from the current
   directory, the CD's root. It looked at the CD's `MCGA.DLL` and `VESA480.DLL` video drivers.
   It found no VESA BIOS ("No VESA Driver -- Cannot use SVGA") and carried on in 320 by 200.
4. It wrote and deleted `C:\TESTE.TST`, to test that C: can be written, and changed to C:.
5. It tried to open each archive on C: first and then, not finding it, asked MSCDEX where the CD
   was (1500h, 150Dh) and opened it there: `D:CD1MOVIE.TRE`, `D:MOVIES.TRE`, `D:CD1MISS.TRE`,
   `D:MISSIONS.TRE` and `D:GAMEFLOW.TRE`. `OBJECTS.TRE` it found on C:, where GOG put a copy.
   That's the installer's choice of what to copy to the hard disk.
6. It made a swap file of 20,000,000 bytes, `C:\SWAPFILE.$$$`, for its virtual memory. Like
   everything it writes, it lands in the save folder.
7. It loaded HMI's sound drivers from the CD (`HMIDRV.386`, `HMIMDRV.386`) and started the Sound
   Blaster 16.
8. It began its opening movie: it read `CD1MOVIE.TRE` in 64 KB pieces and drew the first frame,
   a planet in space.

It then stays on that frame. It reads no more, checks the keyboard now and then, and otherwise
runs in protected mode, at 40,000 cycles as at 20,000. Just before, HMI's driver started 16-bit
sound on the Sound Blaster 16: DSP command B4h and the second DMA controller's ports (C4h–D8h,
for channel 5), none of which the machine has yet. A movie is usually timed by its sound, so
the likely cause is that the 16-bit sound never plays, and its interrupt never comes. *To
confirm.* Reporting an older Sound Blaster didn't change anything, because the driver goes by
the card chosen in the game's setup, not by the card's version.

## Checking it

`CdTests.cs` builds an ISO image of a few sectors and runs a program from it. The program checks
that its drive is D:, reads a file from the CD to the console, fails to create one there, and
finds MSCDEX with one drive, D:. Another test runs the same program from C: with no CD, so the
checks fail and there's no drive D: to load from. With GOG's image present, a third runs *Wing
Commander III* for five seconds and looks for its message, `GLOBALS.IFF` and `CD1MOVIE.TRE` on
the CD, and MSCDEX's answer.
