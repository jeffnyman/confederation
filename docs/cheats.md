# Cheats

Both games came with cheats Origin left in for testing, turned on by words on the command line.
`--cheats` gives the games those words, so a mission can be played through, or skipped through,
without being shot down. Nothing in the machine changes: the cheats are the games' own.

The code is the player's: `Games.CheatWords` and `Games.Arguments`, and the `Cheats` setting in a
profile.

## The words

The words are `Origin -k`, after the program's name, as in `WC2 Origin -k` at a DOS prompt. The
fans' guides agree on them, and on what they do:

- **The ship can't be destroyed.** Its shields don't run out.
- **Alt+Del** destroys the ship targeted.
- **Alt+Ins** destroys the enemy's wing. For *Wing Commander II*, one guide says every non-capital
  ship on the radar.

The guides disagree on some of the keys, and one player found Alt+Del didn't work in *Wing
Commander II* under GOG's DOSBox. **The keys are to confirm** in Confederation: playing a mission
to a fight takes a person at the keys, which a test hasn't managed yet.

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

## Using it

```
confederation wc2 --cheats
confederation wc2 --cheats --remember     # on for Wing Commander II and its Special Operations
confederation wc2 --no-cheats --remember  # and off again
```

Like the other options, `--cheats` is for one run unless remembered, and a profile keeps it for a
game and its mission disks together. The menu says "Cheats on." for a game whose profile has them.
When the program's own arguments are given too (`--args`), the cheats' words go first, where the
games look for them.
