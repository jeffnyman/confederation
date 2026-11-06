# The player

With the player, *Wing Commander* runs in a window and can be played: the intro, the campaign
menu, the Tiger's Claw. (Silently, when this was written; [the sound](the-sound.md) came next.) This note covers what the player
does, how it keeps the game at the right speed, how the keyboard and a gamepad reach the game, and
how it was checked without a person at the keys.

The code is `src/Confederation.Player`, plus `ScanCodes.cs` and `Devices/GamePort.cs` in the core.

## A thin wrapper

The player is built like Retrograde's, and much of it was carried over: SDL3 for the window and
input, through the `ppy.SDL3-CS` bindings, and the same command line style. Everything that decides
what the game sees stays in the core:

- **The scan codes** are in the core (`ScanCodes`), not the player. They're a fact about PC
  keyboards, not about SDL, and in the core they're tested with the rest of the machine.
- **The game port** is in the core too. The player only says where the stick is.
- **The player owns** the window, the clock, which program to run, and where saves go.

```
confederation "F:\Games\Wing Commander"
confederation "F:\Games\Wing Commander" SM2.EXE
confederation "F:\Games\Wing Commander II\WC2.EXE" --cycles 8000
```

Given a folder, the player looks for the programs it knows, main games first, and runs the one it
finds. Saves go to a folder of the user's own (`%APPDATA%\Confederation\saves\<game folder>` on
Windows), never into the game's folder; see [the DOS layer](the-dos-layer.md#drive-c).

## Speed

Each game runs at the speed GOG chose for it in DOSBox: 4,000 cycles for *Wing Commander*, 8,000
for *Wing Commander II*. A DOSBox cycle is one instruction, counted per millisecond, so these are
4 and 8 million instructions a second, which is what the machine's `InstructionsPerSecond` counts.
A repeated string instruction counts once for each time it repeats, as in DOSBox
([the x86 CPU](the-x86-cpu.md)). `--cycles` overrides it.

*Wing Commander III* is the exception. GOG runs it at `cycles=max`, as fast as the host goes, and
the machine here manages about 65 million steps a second in it (observed, headless, on the
development machine; [the x86 CPU](the-x86-cpu.md#performance)). So its default is 30,000, which
leaves room for the window and the sound.
Its movies keep time by their sound, so they play in step whatever the speed (observed at 20,000,
30,000 and 40,000). Its space flight runs at about 24 frames a second in empty space at both
30,000 and 40,000, so there the game sets its own pace; in the carrier's launch bay it's about 21
(observed, headless). How it fares in a dogfight is to confirm by playing; making the processor
faster is [roadmap step 11](../ROADMAP.md).

Why not "as fast as possible"? These games time some things by the timer and others by how fast
the processor runs. *Wing Commander* came out in 1990, for the 286s and early 386s of the day, and
on a much faster machine its space flight is notoriously too quick. The speed GOG tested is the
safe default; profiles (roadmap step 7) will let it be tuned per game.

### Holding real time

The machine's own time comes from the instructions it runs ([the DOS layer](the-dos-layer.md#the-timer-early)),
so the game always sees the same pace, however fast the host is. The player's only job is to hand
the machine its instructions at the rate of real time:

1. Run one VGA frame's worth of instructions: 1/70.086 of a second's, since mode 13h shows
   70.086 frames a second.
2. Draw the picture.
3. Wait for the next frame's moment on the host's clock.

The instructions are counted from the start (frame *n* ends at *n* × speed / 70.086), so the
fractions of an instruction a frame leaves over add up instead of being lost: at 4 million a second,
a frame is about 57,072.7 instructions, and dropping the fraction each time would leave the game
slightly slow, an error that grows for as long as it runs. If the host falls well behind, because the window was dragged or the system was
busy, the player carries on from now rather than racing through the missed frames.
Once the machine had sound, the sound device's clock took over from the host's in step 3; see
[the sound](the-sound.md#keeping-picture-and-sound-together).

The emulator runs *Wing Commander* at three to five times real speed on a desktop machine, so
there's room to spare. F12 lets it run flat out, to get past things quickly.

## The picture

The window draws the machine's 32-bit picture into an SDL texture, and SDL's renderer scales it
onto the window, on the graphics card where there is one, with the nearest pixel so it stays
sharp. Two details are particular to the PC:

- **The shape.** Mode 13h is 320 by 200, but monitors were 4:3, so each pixel was 1.2 times taller
  than wide. The player shows the picture at 4:3 whatever its size in pixels. Shift+F11 shows
  square pixels instead, as most screenshots of these games do (the test screenshots included),
  which is why they look flatter than people remember the games.
- **The size changes** with the mode: text, then 320 by 200. The texture follows the size the
  machine reports.

At first the player scaled the picture in software onto the window's own surface, as Retrograde
does. A C64's picture is small, and so was the window, but full screen on a large monitor is
millions of pixels seventy times a second. **Observed:** on a high-resolution screen, a frame
took 19 ms to draw, in a frame of 14.3 ms. The sound is queued a frame at a time, so it ran dry between frames
and crackled, in full screen only. Through the renderer the same frame takes a quarter of a
millisecond.

## The icon

Each game's window has the game's own icon, in its title bar and the taskbar. The icons are built
into the player (`src/Confederation.Player/Icons`, one for each program it knows by name; *The
Secret Missions* share *Wing Commander*'s, as they share its program), and `GameIcons` reads them.

An `.ico` file is a directory of pictures of one icon at several sizes, each a PNG or a Windows
bitmap without its file header. A bitmap's height counts twice: its colors are followed by a mask
of a bit a pixel, set where the icon is see-through, and both run from the bottom row up, each row
padded to four bytes. These icons have bitmaps of 24 bits a pixel at 32 and 48 pixels, which take
their shape from the mask, and a PNG at 256, which a window doesn't need. The window takes the
largest picture as its icon and the others as its smaller sizes, and the system picks among them.

The player has an icon of its own too (`Icons/confederation.ico`): the program's, in Explorer,
the menu's, and the window's for a program it doesn't know. It's the W from *Wing Commander*'s
logo, with its speed lines. The whole logo is two lines of text, twice as wide as it's tall, and
at 32 pixels it was a smudge; the W alone still reads at 16. The logo's black background was made
see-through, fading with the brightness so the edges don't fringe, and the icon was saved at seven
sizes from 16 to 256 as 32-bit bitmaps with their own alpha. A writer can leave the mask out of
those, as this one did, since the alpha gives the shape; the reader takes them either way.

## The keyboard

SDL reports each key by its USB HID *usage*, a number for the key's place on the keyboard. The PC
expects *scan codes*, also numbers for places, from a different table
([the VGA and the keyboard](the-vga-and-keyboard.md#the-keyboard)). `ScanCodes.TryFromUsb` maps one
to the other place by place, so a game sees its keys where its manual puts them, whatever the
host's keyboard layout. Keys the PC of 1990 didn't have, such as the Windows keys, aren't passed on.

A few details matter more than they look:

- **Held keys repeat.** A real PC keyboard sends a key's make code again and again while it's held,
  and the player does the same with the host's key repeat. Games that track keys up and down
  ignore the repeats; text entry through the BIOS relies on them.
- **Losing focus lets go of every key.** After Alt+Tab, the Alt key comes up in another window, and
  the game would never hear of it. So when the window loses focus, the player sends a release for
  every key it pressed.
- **The player keeps three keys.** F11 (full screen), F12 (full speed) and Pause. F11 and F12
  arrived on keyboards after these games' controls were designed, and Pause is the one key a game
  can't read anyway: on a real PC, the BIOS handles it. With Ctrl, it keeps three more: Ctrl+F10
  frees the mouse, and Ctrl+F5 and Ctrl+F9 save and load the machine's state
  ([save states](save-states.md)). The game sees the Ctrl, but not the function key.
- **With the cheats on, two keys stand in for others.** Alt+Q goes to the game as Alt+Del, and in
  *Wing Commander II* Alt+W as Alt+Ins: the cheats' keys, moved beside Alt
  ([cheats](cheats.md)). The game sees the Alt, and the extended Del or Ins in place of the letter.

## The joystick

The PC's game port is unusual, and worth understanding before mapping a gamepad onto it. There is
no analog-to-digital converter. Each axis of the stick is a variable resistor, charging a
capacitor through a timer chip. A program writes anything to port 201h, which starts the four
timers, then counts in a loop how long each axis's bit stays at 1. The further the stick is pushed,
the higher the resistance and the longer the count.

That count depends on the processor's speed, which is why every game of the time asked the player
to calibrate the stick: centre it, then push it to the corners. It also explains a setting in GOG's
configuration for *Wing Commander II*, `timed=false`: DOSBox then ends the timers after a number of
reads instead of a time, so the count no longer depends on the emulated speed.

Confederation's port (`GamePort.cs`) uses DOSBox's timings: 24.2 microseconds plus 11 nanoseconds
an ohm, over 0 to 120 kilohms, so about 0.68 ms centred. Because the machine's time follows the
instructions, the counts come out the same every run, like a real stick on a real machine.

The player puts the gamepad on the port:

- **The left stick is the joystick, in proportion.** These games were flown with an analog stick.
  A small dead zone keeps a gamepad's loose centre from drifting.
- **The d-pad pushes the stick all the way**, over the left stick.
- **The four face buttons are the port's four buttons**: the first stick's two, then the second's
  two, as four-button sticks of the time had them.

The port is empty until a gamepad is plugged in, and reads `FFh` like an empty port. Games look for
a joystick when they start, so the gamepad should be plugged in before. `--no-joystick` leaves the
port empty regardless.

*To confirm by playing:* that *Wing Commander* uses the stick in flight once it finds one, and how
its calibration feels with a gamepad.

## The mouse

A PC's mouse isn't a device programs read directly. It plugs into a serial or PS/2 port, and a
**driver** (Microsoft's `MOUSE.COM`, or a compatible one) reads it and answers `INT 33h`. Every
DOS program of the time talks to the driver, never to the mouse. So that's what the machine
provides (`Machine.Mouse.cs`, after the interface Microsoft defined and DOSBox's `mouse.cpp`
answers):

- **Reset** (function 0) says whether there's a mouse, and how many buttons. Without one, it
  answers "no mouse", and the games steer their pointers by keyboard or stick, as before.
- **Where the pointer is, and the buttons** (function 3). *Wing Commander* and *Wing Commander
  II* poll this every frame. It's the only thing they read: they also set the pointer's place
  (function 4) as they start, and that's all. They install no event handler and draw their own
  pointer.
- **Presses and releases counted** since last asked (5 and 6), **the area the pointer is kept in**
  (7 and 8), **the motion** in mickeys (0Bh) and **the ratio** of mickeys to pixels (0Fh), for
  programs that want them.
- **An event handler** of the program's own (0Ch, and 14h, which swaps one for another), which the
  driver calls when the mouse moves or a button goes down or up. *Wing Commander III* reads the
  mouse only this way: it never asks where the pointer is.

### The event handler

A program gives the driver a far address and a mask of the events it wants: motion (bit 0), and
each button's press and release (bits 1 and 2 for the left, 3 and 4 for the right). From then on
the driver calls it, as an interrupt handler would, with AX holding the events since the last
call, BX the buttons held, CX and DX the pointer, and SI and DI the motion in mickeys. The
handler returns with a `RETF`.

On a real PC the driver calls it from the mouse's own interrupt. A PS/2 mouse is IRQ 12, and
DOSBox's driver works the same way. So the machine has the AT's **second interrupt controller**
now, for IRQ 8 to 15, as vectors 70h to 77h. It's wired to the first one's IRQ 2, which passes
on its interrupts between IRQ 1 and IRQ 3 in priority. When an event the program wants happens,
the mouse raises IRQ 12. The driver's handler for it, in the BIOS segment, saves every register,
asks C# for the event, calls the program's handler with it, and ends the interrupt on both
controllers. Before, the second controller was only a mask that nothing used, and a program's
handler was never called.

*Wing Commander III* installs its handler in real mode, at an address in conventional memory. It
gets there while the game runs in protected mode: the DPMI host reflects IRQ 12 down to real
mode, as it does the timer's ([the DPMI host](the-dpmi-host.md)). Without the handler, the game's
pointer stayed in the screen's corner and nothing on its screens could be clicked. With it, the
pointer follows the mouse, the places to go are named as it passes over them ("Go to FLIGHT
CONTROL."), and a click goes there (observed).

### The driver's coordinates

Two details of the driver are worth knowing. Its coordinates aren't the screen's: in the 320 by
200 graphics modes the driver counts **640 across** and 200 down, and a program halves the column.
And the mouse moves in **mickeys**, its own steps of about a two hundredth of an inch, which the
driver turns into its pixels at 8 mickeys to 8 pixels across and 16 to 8 down, so the pointer
moves half as fast down as across, as on the real driver.

The player passes the host's mouse through **as motion, not as a place**. The desktop's pointer
can't be the game's, because the game draws its own and moves it at its own pace. So a click in the window **captures** the mouse, as DOSBox does: the
desktop's pointer disappears, and its motion and its left and right buttons are the PC mouse's.
That first click isn't passed on. **Ctrl+F10** gives the mouse back, as does leaving the window.
`--no-mouse` leaves the PC without one.

**The motion is scaled to the picture.** At first one host pixel of motion was one mickey. But
the game's screen is 320 by 200, and the window is several times that, and a full screen more
again: a small move of the hand swept the pointer across the game's screen, and the bigger the
window, the worse. Now the motion is spread over the picture as it's shown: by the driver's
defaults, 640 mickeys take the pointer across and 400 down, so that's what moving across the
picture gives, however big it is. The pointer then moves under the hand as the desktop's would
over the same picture. For that, SDL is asked to apply the system's pointer speed to the captured
mouse (`SDL_HINT_MOUSE_RELATIVE_SYSTEM_SCALE`); otherwise the motion comes in the mouse's own
counts, which depend on its resolution. **Observed:** in a window at scale 3, the picture is 960
points across, and a point of motion is two thirds of a mickey.

That's the driver's own area. A game can set another (functions 7 and 8), and *Wing Commander
III* sets **1280 by 480**, twice as wide as its screen's 640 and more than twice as tall as its
200 (observed: its pointer is at a quarter of the driver's column, and 200/480 of its row). By
the default scale, a sweep across the picture took its pointer only halfway across. So the
motion is spread over the area the game set, not the default one: the machine says how many
mickeys take the pointer across it (`MickeysAcross`), 640 by 400 by default, and the player
divides that by the picture's size.

Games that read the motion itself, as a stick, rather than the pointer, have their own feel for
it, and so do players. `--mouse-speed <percent>` scales it further: 50 for half as fast, 200 for
twice. It's kept in a profile like the other options.

## Checking it without a player

A window is hard to test, but the parts that matter aren't in the window:

- **The scan code table and the game port** have unit tests (`InputTests.cs`): letters, the AT's
  extended keys, the keypad, and each axis timing out at its position.
- **The mouse driver**, with small programs (`DosTests.cs`): no driver without a mouse, two
  buttons with one, the pointer following the motion at the driver's ratio and held in its area,
  presses counted, and the motion in mickeys.
- **The game, by mouse** (`GameStartTests.cs`): at *Wing Commander II*'s title menu, the mouse
  moves the pointer onto "Start New Game" (`wc2-mouse-on-button.png`), and a click starts a new
  game.
- **The game, by keyboard** (`GameStartTests.cs`): *Wing Commander* runs from launch; Space cuts the
  intro short, Enter ends it at the campaign menu, and Enter again starts the Vega campaign. The
  test waits for the game to open `RECROOM.VGA`, the Tiger's Claw's rec room, and saves a
  screenshot of it.

Getting there showed how the game reads its keys: it installs its own IRQ 1 handler (see
[the DOS layer](the-dos-layer.md#what-wing-commander-asks-for)) and reads port 60h itself. An
arrow held for a second with a single make code moved its pointer only a little, so the host's key
repeats probably matter in the rec room; that's to confirm by playing.

The player itself was checked by running it: `--headless --seconds 25` runs the game with no
window and saves the picture, and a windowed run of 5 game seconds took 5.4 seconds of real time,
start-up included.

## What isn't there yet

- **Sound** came next: [the sound](the-sound.md).
- **Saved states** and per-game controls, with the profiles.
