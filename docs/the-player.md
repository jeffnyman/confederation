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
`--cycles` overrides it.

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
- **Where the pointer is, and the buttons** (function 3). Both games poll this every frame. It's
  the only thing they read: they also set the pointer's place (function 4) as they start, and
  that's all. They install no event handler and draw their own pointer.
- **Presses and releases counted** since last asked (5 and 6), **the area the pointer is kept in**
  (7 and 8), **the motion** in mickeys (0Bh) and **the ratio** of mickeys to pixels (0Fh), for
  programs that want them.

Two details of the driver are worth knowing. Its coordinates aren't the screen's: in the 320 by
200 graphics modes the driver counts **640 across** and 200 down, and a program halves the column.
And the mouse moves in **mickeys**, its own steps of about a two hundredth of an inch, which the
driver turns into its pixels at 8 mickeys to 8 pixels across and 16 to 8 down, so the pointer
moves half as fast down as across, as on the real driver.

The player passes the host's mouse through **as motion, not as a place**: one host pixel of motion
is one mickey. The desktop's pointer can't be the game's, because the game draws its own and
moves it at its own pace. So a click in the window **captures** the mouse, as DOSBox does: the
desktop's pointer disappears, and its motion and its left and right buttons are the PC mouse's.
That first click isn't passed on. **Ctrl+F10** gives the mouse back, as does leaving the window.
`--no-mouse` leaves the PC without one.

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
