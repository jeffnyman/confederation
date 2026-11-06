# The x86 CPU

The processor is the largest single part of an emulator, and the part every other part depends on.
This note covers which processor Confederation emulates and why, how the code is organized, and
how it was tested against a real chip. It also covers what that testing showed: in about a dozen
places, the 386 doesn't do what Intel's manuals say it does.

The code is in `src/Confederation.Core/Processors`. The tests are in
`tests/Confederation.Core.Tests/SingleStepTests.cs`.

## Which processor?

"x86" covers twenty years of chips, and they don't all behave the same. They differ in
instructions, in undefined behaviors, and in edge cases like what a word access at offset `FFFFh`
does. So the first question is which chip to be.

Confederation emulates **the Intel 80386, in real mode**. The reasons:

- **It's what the games met.** The games ran on the 286s, 386s and 486s of their day, and most
  players had a 386 or 486. DOSBox, which GOG ships, behaves like a 386/486. Where chips differ,
  this is the behavior the games were played on.
- **Borland's compilers could emit 386 code.** Real-mode programs on a 386 can still use 32-bit
  registers and the 386's new instructions through prefix bytes. An 8086-only core might trip over
  them.
- **One core for every game.** *Wing Commander III* needs the 386's protected mode. Starting with
  a 386 means that work extends this core instead of replacing it.
- **There's a test suite captured from a real one.** See below.

The 8086's quirks that later chips changed (`PUSH SP` pushing the decremented value, words wrapping
at the end of a segment, `POP CS` existing) are not emulated, since the games never saw them.

## Instruction-stepped, not cycle-stepped

Retrograde's 6502 runs one bus cycle at a time, because C64 games depend on exact timing. This
processor runs **one whole instruction per step**, prefixes and all, including every repetition of
a `REP MOVSB`.

DOS games of this era ran on machines whose speeds varied tenfold, so they couldn't depend on
instruction timing. They time themselves with the 8253 timer chip instead. Instruction stepping
is far simpler to write and to read, and much faster to run. Speed will be governed by counting
instructions against the timer, which comes with the rest of the machine.

## Real mode in brief

In real mode a **segment register** holds a 16-bit number, and the segment starts at that number
times 16. An address is written `segment:offset`, so `B800:0010` is physical address
`B8000 + 10 = B8010`. There are six segment registers: CS for code, SS for the stack, DS for data,
ES as the destination of string instructions, and FS and GS (new on the 386) as spares.

Each segment has a **limit** of 64 KB. Here the 386 differs from the 8086: an access that runs past
the limit, like a word at offset `FFFFh`, is a fault, where the 8086 silently wrapped around. A
fault in the stack segment is a *stack fault* (interrupt 12) and any other is a *general
protection fault* (interrupt 13).

Two **prefix bytes** reach the 386's 32-bit world from real mode. `66h` makes one instruction use
32-bit operands (EAX instead of AX), and `67h` makes it use 32-bit addressing, which has a richer
form: any register as a base, plus a second register scaled by 1, 2, 4 or 8.

## Protected mode in brief

*Wing Commander III* runs in **protected mode**, the 286's invention that the 386 made 32-bit.
Setting bit 0 of control register CR0 (PE, "protection enable") switches to it, and the segment
registers change meaning: instead of a paragraph number, each holds a **selector**, an index into
a table of eight-byte **descriptors** in memory. A descriptor gives the segment's base anywhere in
4 GB, its limit (counted in bytes, or in 4 KB pages when its granularity bit is set, so a single
segment can span all of memory), and its kind: code or data, readable or writable, and present or
not. There are two tables, the global one (GDT) and a local one (LDT); bit 2 of a selector picks
which, and its low two bits are its *requested privilege level*.

The processor reads a descriptor only when a segment register is loaded, and keeps it in a hidden
**descriptor cache** beside the register. Every access afterwards uses the cache. Real mode has the
same caches (loading a segment register there just sets the base to 16 times the selector), which
is why a program can set a 4 GB limit in protected mode, go back to real mode, and keep it: the
trick known as "unreal mode". The tests do exactly that.

Three more things come with it:

- **32-bit code.** A code segment's D bit makes its instructions default to 32-bit operands and
  addresses, and the `66h` and `67h` prefixes then switch one instruction back to 16. A stack
  segment's B bit makes the stack 32-bit, addressed by ESP instead of SP.
- **Privilege.** There are four levels, 0 the most privileged. The level of the code running (CPL)
  is the code segment selector's low two bits. Data and stacks have levels too, and loading a
  segment register checks them. Some instructions (loading the tables, writing CR0, `HLT`) are
  level 0's alone, and `CLI`, `STI`, `IN` and `OUT` need a level at least as privileged as the I/O
  privilege level in the flags. A far return or `IRET` can go to a less privileged level, taking
  that level's stack from the stack it returns through; the data segment registers that still hold
  more privileged segments are then made null, so the outer code can't keep using them.
- **Interrupts through gates.** The interrupt descriptor table (IDT) holds a gate for each vector:
  the handler's selector and offset, whether it's an *interrupt gate* (which turns interrupts off)
  or a *trap gate* (which doesn't), and a level a program's `INT` must have to use it. A handler
  more privileged than the code it interrupts runs on its own level's stack, which comes from the
  **task state segment** (TSS) that `LTR` names; the old SS:ESP goes on the new stack first. Faults
  push an **error code** too, usually the selector at fault, so a handler can tell what went wrong.

What isn't emulated yet, each stopping the machine with a message if a program asks for it:
paging, virtual-8086 mode, task switches, and call gates. The [DPMI host](the-dpmi-host.md) the
games use doesn't need them.

## How the code is organized

The class is `Intel386`, split across files by concern:

| File | What it holds |
|---|---|
| `ICpuBus.cs` | What the CPU sees of the machine: memory by physical address, and I/O ports |
| `Intel386.cs` | The registers, `Step()`, and interrupt and fault entry |
| `Intel386.Protected.cs` | Protected mode: descriptors, segment loads and their checks, privilege, far transfers, interrupts through gates, and the system instructions |
| `Intel386.Memory.cs` | Instruction fetch, ModR/M and SIB decoding, segment-checked memory access, the stack |
| `Intel386.Execute.cs` | Prefixes and the one-byte opcodes |
| `Intel386.Extended.cs` | The two-byte (`0Fh`) opcodes, mostly the 386's additions |
| `Intel386.Strings.cs` | The string instructions and their `REP` prefixes |
| `Intel386.Alu.cs` | Arithmetic, logic, shifts, multiply, divide, BCD, and the flags they set |

Some design choices worth knowing:

- **Operand sizes are numbers.** Most instructions come in 8-, 16- and 32-bit forms. The helpers
  take the size in bytes (1, 2 or 4) and mask accordingly. So there's one `Add`, not three.
- **Flags are computed eagerly.** Each arithmetic instruction computes all its flags at once.
  Many emulators compute flags lazily (keep the operands and work out a flag only when something
  reads it) because most flags are never read. That's faster, but harder to read and to get right.
  At the few million instructions a second these games need, eager is fast enough. Lazy flags stay
  an option if profiling ever calls for them.
- **Faults are C# exceptions.** A fault can be found deep inside a memory access in the middle of
  an instruction. Throwing an exception unwinds straight back to `Step()`, which restores IP and SP
  and enters the handler. That keeps every memory access free of error checks. Exceptions are slow
  in .NET, but real-mode programs almost never fault, so the cost doesn't matter.
- **Faults are restartable.** The handler gets the address of the faulting instruction, prefixes
  included, and the stack as it was. A `REP` instruction that faults partway keeps the progress
  it made in CX, SI and DI, as on the chip.
- **A `REP` takes time for each repetition, and gives way to interrupts.** The machine's clock
  counts instructions, and at first a whole `REP MOVSD` counted as one, however many thousand
  times it repeated. DOSBox charges a cycle for each repetition (its core's `string.h`), and GOG's
  speeds are set by DOSBox's cycles, so the processor now counts its repetitions
  (`Repetitions`) and the machine adds them to its time. It also stops a long one after 1,024
  repetitions (`RepeatLimit`), with the instruction pointer left on the instruction and CX
  counting what's left, so the next step carries on with it, and an interrupt can come in
  between, as on the chip. The single-step tests run the processor without a limit, a whole
  `REP` to a step, as the chip they were captured from reports it.

Hardware interrupts arrive between instructions through `Interrupt(vector)`, which the machine
calls when its interrupt controller has one and the interrupt flag is set (see
[the DOS layer](the-dos-layer.md#the-timer-early)). What isn't there yet: protected mode's paging,
virtual-8086 mode and task switches, the floating-point unit (a program that probes for one finds
none), the trap flag's single-stepping, and the one-instruction delay after `STI` before
interrupts are taken.

## Testing against a real chip

### The SingleStepTests suite

[SingleStepTests](https://github.com/SingleStepTests/80386) is a set of CPU tests captured from real
hardware: a 386EX processor wired to a microcontroller that sets up a random machine state, runs
one instruction, and records everything. For each opcode, and each combination with the `66h` and
`67h` prefixes, there are 100 to 2,500 tests. In all that's **941 files and 1,758,700 tests** for
real mode.

Each test gives:

- the instruction's bytes
- every register and the relevant memory before
- the registers and memory that changed after
- the exception, if the instruction raised one
- every bus cycle in between, which this processor doesn't check since it isn't cycle-accurate

The tests come in a binary format called **MOO**, a series of chunks each with a four-letter type,
a length and a payload, gzip-compressed. Its
[specification](https://github.com/dbalsom/moo/blob/main/doc/moo_format_v1.md) is short, and
`MooFile.cs` reads it in about 200 lines. Reading the binary format directly beats converting to
JSON: the whole suite loads and runs in about ten seconds.

### How the tests are run

Each test's instruction is followed by a `HLT`. When the instruction faults, the handler in the
interrupt table is also a `HLT`. So the harness sets up the state, steps until the processor
halts, and compares every register and every byte of memory the test mentions.

Three details matter:

1. **Undefined flags are masked.** Where Intel calls a flag "undefined" after an instruction, the
   tests don't hold the emulator to the chip's value. The masks come from two places: some files
   carry their own, and the suite's `80386.csv` table lists the undefined flags for every opcode.
   An early run that missed the table showed hundreds of false failures on instructions like `BT`,
   whose overflow flag is undefined.
2. **The pushed flags are masked too.** When an instruction faults, the flags go on the stack, so
   the same mask applies to those two bytes of memory.
3. **I/O reads return `FFh`.** Nothing is attached to the test rig's I/O ports, and an empty ISA
   bus reads as all ones. The exceptions are ports `22h` and `23h`, which are registers inside the
   386EX itself, not the 386 the games ran on.

The test data is 576 MB and stays out of the repository. The tests look for it in the folder named
by `CONFEDERATION_TESTDATA`, or in `_testdata` or `_wingc/_testdata` in a folder above the test
binary. Without it they pass without checking, so CI stays green.

### Chasing one case

The `Trace_one_case` test runs a single case named in an environment variable and prints the
registers after each step:

```
CONFEDERATION_CASE=0FAB:19 dotnet test -c Release --filter Trace_one_case
```

That's how most of the bugs below were found. For behavior that needed a rule worked out, small
Python scripts read the same files, pulled out every case of one instruction, and checked candidate
formulas against all of them.

### Protected mode's own tests

The suite is real mode's alone, so protected mode has hand-written tests instead
(`ProtectedModeTests.cs`). Each is a small program, assembled by hand into bytes with a comment for
every instruction, that builds its descriptor tables in memory, runs, and leaves its results at
5000h for the test to check. Between them they:

- switch from real mode to 32-bit protected mode and back, keeping a 4 GB limit in real mode;
- raise faults and check the error code and return address each handler receives: a selector
  outside the table, a write to a read-only segment, a segment not present, an offset below an
  expand-down segment's limit, a stack at another privilege, a null stack and a null data segment;
- take an interrupt gate and a trap gate, and check the interrupt flag inside and after;
- drop to privilege 3 with a far return, find DS made null, come back through a privilege-3 gate
  onto the task state segment's stack, and fault on `CLI`;
- run the system instructions (`LAR`, `LSL`, `VERR`, `VERW`, `ARPL`, `SGDT`, `SIDT`, `SMSW`) and
  16-bit protected-mode code.

They check the rules as Intel's manuals and DOSBox give them (**to confirm** against a real chip,
should protected-mode captures ever join the suite). The real-mode suite is the guard on the other
side: all 1.76 million cases still pass with protected mode in place.

## What the hardware taught

The first run passed 597 of the 941 files. Getting the rest to pass meant fixing ordinary bugs and,
more interestingly, learning what the 386 actually does where the documentation is wrong or
silent. Each of these is now in the code, commented where it happens.

**Bugs in my own reading of the documentation:**

- **An interrupt reads its vector before pushing.** Entering a real-mode interrupt pushes FLAGS, CS
  and IP and loads CS:IP from the interrupt table at address 0. I had the pushes first. In one test
  the stack sat on top of the interrupt table, so the pushes overwrote the very entry being read.
  The chip reads the entry first.
- **`LOCK` is checked against the instruction.** Only read-modify-write instructions with a memory
  destination may be locked. Anything else is an invalid opcode.

**Where the 386 differs from the manuals:**

| Instruction | The manual | The 386 |
|---|---|---|
| `AAA`, `AAS` | The 386 manual: AL ± 6 and AH ± 1 | AX ± 106h, so a carry out of AL moves AH by two |
| `SHL`, `SHR` on a byte, count 16 or 24 | Carry undefined | Carry as if the count were 8 |
| `SHR`, count above 1 | Overflow undefined | Overflow clear |
| `RCR`, any count | Overflow defined for a count of 1 only | Overflow is the XOR of the result's top two bits |
| 16-bit `SHLD`/`SHRD`, count above 16 | Result and flags undefined | A 32-bit shift with the source repeated (dest:src:src left, src:src:dest right) |
| SIB byte, no index but a scale | The scale must be 0 | The scale is applied to the base register |
| `POPAD` | ESP's slot is skipped | ESP's upper half is loaded from it |
| `PUSHAD` near the segment's end | Faults | Writes from the bottom up, so the registers below the fault are already written |
| 32-bit `POP` into a segment register | — | Reads only the selector's word, though SP moves by 4 |

The lesson generalizes. **The manual isn't the chip.** Documentation describes the intended
behavior and calls the rest undefined. The chip always does *something*, and software written and
tested on real machines may depend on it. A test suite captured from hardware is how an emulator
finds out what that something is.

## Known differences

24 cases are skipped deliberately, listed in `KnownDifferences` in the tests:

- **The prefetch queue (4 cases).** The 386 reads up to 16 bytes ahead of the instruction it's
  running. In these tests a `REP STOSW` overwrites the `HLT` after it, but the chip had already
  fetched the `HLT` and runs it. Confederation has no prefetch queue, so it runs the new bytes. Only
  self-modifying code can tell the difference, mostly copy protection that uses it to detect
  debuggers and emulators. DOSBox doesn't model the queue in its normal cores either.
- **IDIV overflows the 386 misses (9 cases).** Dividing to a negative quotient too big for AL
  should be a divide error. In a few cases the 386's division microcode fails to notice and returns
  AL = 80h. No simple rule predicts which. Confederation always raises the error, as the manual
  says.
- **`AAM 0`'s flags (10 cases).** `AAM` with a base of 0 is a divide error. The chip leaves the
  flags from partial work in its microcode, with no rule I could find.

None of these matter to a program that isn't written to probe them.

## Performance

*Wing Commander III* is the game that needs speed. GOG runs it at `cycles=max`, and the player
gives it 30 million instructions a second. So that's what was measured, from three saved states
([save states](save-states.md)): its opening movie, the simulator in flight, and the cockpit in
the carrier's launch bay. Each runs headless for ten seconds of the game's time, as fast as the
host goes, sound and all. Speed is counted in steps, an instruction or a repetition of a string
instruction, which is what the machine's time counts.

Observed on the development machine:

| | Movie | Simulator | Launch bay |
|---|---|---|---|
| Before | 35.5 | 35.7 | 33.8 |
| The devices seen to between runs of instructions | 45.7 | 51.1 | 49.1 |
| Plain memory reached directly | 54.3 | 56.3 | 50.1 |
| The processor's own loop | 64.1 | 66.6 | 62.4 |

In millions of steps a second. At 30 million, the game now runs at a little over twice real time,
where it was about 1.15 times.

The profiles (`dotnet-trace`) said where the time went:

- **The devices, after every instruction.** The machine's loop updated the timer, checked the
  sound card and the keyboard, and asked the interrupt controller whether anything was pending,
  after each instruction: more than half the time, the controller's question alone 14%. But
  between the program's uses of a port and its calls to the BIOS, DOS or the DPMI host, nothing
  outside the processor changes. So the machine runs instructions until the next timer or sound
  event is due, and stops early only when the program touches a port or calls a service, the
  processor halts, or interrupts are on with one waiting. The interrupt controller keeps its
  answer up to date as its registers change, rather than working it out when asked.
- **Memory a byte at a time.** Every access, instruction fetches included, went through the bus,
  an interface call per byte. Now the machine gives the processor its memory and a map of which
  4 KB pages are plain memory: not video memory, not the EMS page frame, not the BIOS's ROM for
  writing, and nothing past 1 MB while the A20 gate is closed, where addresses wrap. There the
  processor reads and writes the array itself, a whole word at once. Elsewhere, and in the
  single-step tests' own bus, which has no map, it goes through the bus as before.
- **Entering the processor for each instruction.** `Step` set up the instruction, and caught a
  fault, once per instruction, called from the machine's loop. Now `RunFor` runs instructions in
  a loop of its own, under one `try`, until it has taken the steps the machine asked for, or one
  of the reasons to stop above. A port or a service tells it to stop by setting `Stopping`.

What's left is the interpreter itself: decoding each instruction from its bytes every time it
runs, and the work of each one. A cache of decoded instructions would be the next step, and a
bigger one.
