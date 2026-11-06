# W.C.A.T.

W.C.A.T., the Wing Commander AllTinker Overhaul, is a fan mod for *Wing Commander* and *Wing
Commander II*. It fixes bugs, adds options, and limits the game's speed itself, by the display's
vertical blank. It is played through a launcher of its own, a 32-bit protected-mode program, and
it starts the game through batch files. This note covers how Confederation runs it. That takes
three things the original games never needed: the DPMI host ([the DPMI host](the-dpmi-host.md)),
a drive laid out as W.C.A.T. lays it out, and DOS's batch files.

## How it's laid out

W.C.A.T. comes bundled with DOSBox Staging. Its DOSBox setup mounts the `files` folder as drive
C: and runs `WCAT.BAT`:

```
files\
  WCAT.BAT        the launcher, then whatever it was told to start
  WC.BAT          straight into Wing Commander, once the launcher is set up
  SM2.BAT         straight into The Secret Missions 2
  LAUNCHER\       LAUNCHER.EXE and LAUNCHER.DAT, its fixes and its program
  GAMEWC1\        empty: the game's data goes here
```

Its readme asks for the game's `GAMEDAT` to be copied into `GAMEWC1` (`GAMEWC2` for the second
game). On its first run the launcher scans those files and installs its fixes over them. It
writes changed `BRIEFING`, `CAMP`, `MIDGAME` and other files, and builds its own version of the
game's program, `WC-AT.EXE`, into `LAUNCHER`. All of that is observed in the log.

## The game's data, not copied

Confederation never writes to a game's folder: writes go to a save folder laid over it (see
[the DOS layer](the-dos-layer.md#drive-c)). Copying `GAMEDAT` would only make a second copy to
keep, so the player **mounts** the series' own folder there instead. The game's `GAMEDAT` (or
the folder itself, if the data is there) appears as `C:\GAMEWC1`, read-only like the game's
folder, under the save folder. When the launcher installs its fixes, the changed files land in
the save folder. Only those go there, a few megabytes, and the GOG install is left as it was.

Lookups go through the save folder first, then the mounted folders, then the game's folder. A
copy in `GAMEWC1`, as the readme asks for, is used as it is, and then nothing is mounted. So
W.C.A.T. set up for DOSBox plays the same here.

On the command line, W.C.A.T.'s folder is enough: `confederation "<W.C.A.T.'s folder>"` finds
`files`, and `WC.BAT` after it runs that batch file instead. The series has to be set up already,
so the player knows where its data is.

## Batch files

Here is `WC.BAT`. The others have the same shape:

```bat
@echo off
CD LAUNCHER
IF EXIST LAUNCHER.CFG GOTO :CONFIG
GOTO :NOCONFIG
:CONFIG
CALL LAUNCHER.EXE LAUNCH WC1
GOTO :LAUNCH
:NOCONFIG
echo No configuration present! Running launcher...
CALL LAUNCHER.EXE
:LAUNCH
CD ..
IF EXIST LAUNCHER\ACTION.BAT GOTO :ACTION
GOTO :NOACTION
:ACTION
CALL LAUNCHER\ACTION.BAT
DEL LAUNCHER\ACTION.BAT
...
```

The launcher doesn't start the game itself. It writes what's chosen into `ACTION.BAT` and
exits, and the batch file that ran it calls that. Choosing the Vega campaign wrote one line
(observed): `@CALL LAUNCHER\WC-AT.EXE v a904 z`. The words after the program are the same ones
the original installer writes into `WINGCMDR.CFG` ([the sound](the-sound.md)).

### A shell in C#

On a real PC, batch files are COMMAND.COM's work. It's a program like any other, which runs a
line, loads the program the line names as its child (`INT 21h` 4Bh), and takes the next line when
the child ends. Confederation doesn't run a COMMAND.COM. It does what DOSBox does, and runs batch
files in the host. `CommandShell` reads the lines and runs the commands it knows. When a line
names a program, it loads it as DOS loads any program. When that program ends, the machine's run
loop sees it, before running another instruction, and asks the shell for the next line.

So between two programs no code runs in the machine, and nothing is left in its memory. The next
program is loaded over the last one's stack and registers, into an arena started afresh. That
matches what a program finds under real DOS, except for COMMAND.COM's few kilobytes, which
Confederation's DOS has never had.

Rules the shell takes from COMMAND.COM:

- **A batch file is read when it starts, not before.** The launcher writes `ACTION.BAT` while
  `WC.BAT` is running, and `WC.BAT` must see the new file. COMMAND.COM goes further and reads a
  batch file a line at a time, from where it left off. The difference only shows when a batch
  file changes itself, which W.C.A.T.'s don't.
- **`CALL`** runs a batch file inside the current one, and the current one carries on afterwards.
  Without `CALL`, the new batch file takes the current one's place. For a program, `CALL` makes no
  difference.
- **A program's name** is tried as given, then with `.COM`, `.EXE` and `.BAT`, in the current
  directory and then in the root, which is the `PATH` programs are given.
- **A command it doesn't know**, that names no program, gets "Bad command or file name" and the
  batch file carries on. A `GOTO` to a missing label gets "Label not found" and ends the batch
  file.
- **When a program ends, DOS closes the files it left open.** W.C.A.T.'s launcher closes
  `ACTION.BAT` itself, but a program that didn't would leave its last writes unflushed for the
  batch file to read.

The commands are `ECHO` (with `ECHO.` for a blank line), `CD`, `IF [NOT] EXIST`, `IF [NOT]
ERRORLEVEL`, `IF [NOT] a==b`, `GOTO`, `CALL`, `DEL`, `CLS`, `REM`, `EXIT`, `LOADFIX` (which runs
the program as it is: everything loads above 64 KB here anyway), and `%0`–`%9`. There is no
redirection, `FOR`, `SET`, `SHIFT` or `PAUSE`. The batch files' output goes to the log, like
everything a program writes to the console, since text modes aren't drawn yet.

## What it did

Run headless with the launcher's Enter pressed by a test harness (observed, at W.C.A.T.'s 22,000
cycles):

1. `WCAT.BAT` (or `WC.BAT` with no `LAUNCHER.CFG`) ran the launcher. It entered protected mode,
   scanned the 85 files in `GAMEWC1`, installed its fixes into the save folder, and reached its
   menu within a minute of the machine's time. A second run scans and installs again.
2. Enter on "Start VEGA CAMPAIGN" wrote `LAUNCHER.CFG`, built `WC-AT.EXE` and wrote
   `ACTION.BAT`. The launcher then left protected mode and exited.
3. `WC.BAT` carried on: `CD ..`, `IF EXIST`, `CALL LAUNCHER\ACTION.BAT`, and that loaded
   `WC-AT.EXE`. The game is a real-mode program like the original. It changed into `GAMEWC1`,
   found expanded memory ("Expanded Memory Detected"), and played its opening, the orchestra
   under the planet.

## Checking it

`BatchTests.cs` runs batch files over small programs: programs one after another with their
arguments, the exit code reaching `IF ERRORLEVEL`, `CD`, `IF EXIST` and `GOTO`, a `CALL` with an
argument, `DEL` leaving the game's folder alone, a batch file written by a program it ran, and a
folder mounted as `GAMEWC1`. `OverhaulTests.cs` in the player's tests finds W.C.A.T. as its
download lays it out, and the series' data for it.

## Not yet

- The launcher's own settings belong to it: the player's profile for the series (music, cheats)
  doesn't reach W.C.A.T., whose launcher sets the music and passes it on the command line.
- W.C.A.T. isn't in the menu, and its folder isn't remembered. It's started by its folder each
  time.
- *Wing Commander II*'s W.C.A.T. has the same layout and is found the same way, but hasn't been
  run yet.
