# Cheats

Both games came with cheats Origin left in for testing, turned on by words on the command line.
`--cheats` gives the games those words, so a mission can be played through, or skipped through,
without being shot down. The cheats are the games' own: the player only gives the words, and
offers keys beside Alt for the games' keys, which are awkward to reach in a fight.

The code is the player's: `Games.CheatWords`, `Games.Arguments` and `Games.CheatKeys`, the
`StandInKeys` that swap the keys, and the `Cheats` setting in a profile.

## The words

The words are `Origin -k`, after the program's name, as in `WC2 Origin -k` at a DOS prompt. The
fans' guides agree on them, and on what they do: the ship can't be destroyed, and in flight
Alt+Del destroys the ship targeted and Alt+Ins the enemy's wing. The guides disagree on some of
the keys. The games' code, below, settles which.

## Checking that the games take them

The words themselves can be checked without flying. The machine is deterministic: the same
program, run twice for the same time, ends with the same memory, byte for byte. Run once with the
words and once without, the two differ only where the words made a difference. Most of that is
the command line itself, copied about as the program starts. Left over are two bytes in the
game's data, the same pair in all five programs:

| Programs | `Origin` sets | `-k` clears |
|---|---|---|
| `WC.EXE`, `SM2.EXE` | DS:00B6 | DS:00BA |
| `WC2.EXE`, `SO1.EXE`, `SO2.EXE` | DS:00BC | DS:00C0 |

What the tests found (**observed**, in GOG's releases):

- `Origin` alone sets its flag, and `-k` alone does nothing. `-k` counts only after `Origin`.
- The case matters: `origin` and `ORIGIN` do nothing. The programs hold the word `Origin` once, by
  itself, right before their debugging messages, and compare against it exactly.
- The flag `-k` clears starts at 1 and goes to 0, which fits "can be destroyed: no". That reading
  is a guess (**to confirm**).

## The keys, in the games' code

With the flags found, the code that reads them can be found too: an instruction such as
`cmp word ptr [00BC], 0` is the bytes `83 3E BC 00 00`, and the programs aren't compressed, so a
search of the file finds every one. Disassembled, one of them in each game guards a switch on the
key just pressed. **Observed** in `WC.EXE` and `WC2.EXE`:

- **The keys are tested by scan code**, the key's place on the keyboard, and only as a new press:
  a key held and repeating counts once.
- **Alt is the game's own idea of it.** Each game reads the keyboard itself, from port 60h, and
  keeps its own record of the keys held. Left Alt (38h) and right Alt (E0 38h) both count.
- **The extended keys lose their E0.** The games' keyboard handlers set the E0 prefix aside, so
  the Del above the arrows and the Del on the number pad are the same key to them, 53h. Ins is
  52h either way.
- **Wing Commander:** Alt+Del destroys the target. With nothing targeted, it falls back on
  another ship the game keeps track of; with neither, nothing happens. There's no Alt+Ins: Ins
  is the roll, Alt or not.
- **Wing Commander II:** Alt+Del destroys the target, and does nothing without one. Alt+Ins goes
  through the ships in flight, but for the first, and destroys each of one kind (kind 0Ch) that
  isn't on the player's side. The guides' "the enemy fighters" fits; what kind 0Ch is, is **to
  confirm**.
- **More keys:** the same switches take Alt+M, Alt+J and Alt+Z in Wing Commander, and Alt+M,
  Alt+I, Alt+O and Alt+Z in Wing Commander II. What they do is **to find out**.

**Played** in Confederation: Alt+Del destroys the target in Wing Commander. Without a target, or
without the cheats, Del is only the roll, which looks like the cheat failing.

## Keys beside Alt

Alt+Del takes a hand off the controls in a fight. So with the cheats on, the player gives keys
beside Alt that stand in for the games' own:

| Press | The game sees | Does |
|---|---|---|
| Alt+Q | Alt+Del | destroys the target |
| Alt+W | Alt+Ins | destroys the enemy fighters (Wing Commander II only) |

The game never sees the Q or the W: while Alt is held, the player hands it the extended Del or
Ins instead, for as long as the key is held, and lets go of that when the key is let go, even
after Alt. Without Alt, or without the cheats, Q and W are Q and W. In Wing Commander, which has
no Alt+Ins, Alt+W stays Alt+W. The games' own keys work as well.

## Using it

```
confederation wc2 --cheats
confederation wc2 --cheats --remember     # on for Wing Commander II and its Special Operations
confederation wc2 --no-cheats --remember  # and off again
```

Like the other options, `--cheats` is for one run unless remembered, and a profile keeps it for a
game and its mission disks together. The menu says "Cheats on." for a game whose profile has them,
and the player says which keys do what when the game starts. When the program's own arguments are
given too (`--args`), the cheats' words go first, where the games look for them.

In flight: target an enemy (T), then Alt+Q.
