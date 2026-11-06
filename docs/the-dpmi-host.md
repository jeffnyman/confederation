# The DPMI host

*Wing Commander III* and W.C.A.T.'s launcher are 32-bit programs. They run in the 386's protected
mode ([the x86 CPU](the-x86-cpu.md#protected-mode-in-brief)), but they start as DOS programs, in
real mode, and they still use DOS for files and the BIOS for the screen and keyboard. What gets
them from one to the other, and back again for every DOS call, is a **DOS extender**. This note
explains the ways an extender can get into protected mode, why Confederation offers just one of
them, how it's built, and what W.C.A.T.'s launcher turned out to need on its way to its first
screen.

The code is in `src/Confederation.Core/Dpmi/`, with what the processor offers it at the end of
`Processors/Intel386.Protected.cs`.

## Three ways into protected mode

A DOS extender is a small loader at the front of the program. It switches to protected mode, sets
up the program's segments, and catches its DOS calls to pass them down to real mode. By the mid
1990s there were three ways to do the switch:

- **Raw**: the extender does everything itself, from opening the A20 gate to building the
  descriptor tables. It works only when nothing else owns protected mode: no memory manager such
  as EMM386, no Windows.
- **VCPI**, the Virtual Control Program Interface: a memory manager like EMM386 already runs DOS
  in virtual-8086 mode, and VCPI lets it hand the processor over to the extender, page tables and
  all. The extender then runs the machine.
- **DPMI**, the DOS Protected Mode Interface: a *host* (Windows' DOS box, OS/2, CWSDPMI, a
  memory manager) stays in charge, and the program, its *client*, asks it for everything through
  `INT 31h`: descriptors, memory, calls down to real mode, interrupt vectors.

Extenders try them in turn. Observed in the log, PMODE/W 1.33 (W.C.A.T.'s launcher) checks for
XMS (`INT 2Fh` 4300h), then for a DPMI host (`INT 2Fh` 1687h), then reads 85 bytes of its own
file (its settings, by the look of it) and asks for VCPI first (`INT 67h` DE00h), using DPMI when
VCPI isn't there. *Wing
Commander III*'s loader, from its code (to confirm when it runs), asks for DPMI first and falls
back to VCPI with its own `JEMM.OVL`.

Confederation offers **DPMI only**, with a host of its own written in C#. It's the same decision
the DOS layer made ([high-level emulation](the-dos-layer.md#high-level-emulation)): the program
asks for a service, and C# answers it. VCPI would hand the whole machine over, page tables and
virtual-8086 mode included, which the processor would then have to emulate in full. DPMI keeps the
host in charge, needs neither, and every request it answers can be logged. An EMM that doesn't
offer VCPI says so with error 84h, "function not supported", and Confederation's does.

## What a DPMI program sees

**Finding the host.** `INT 2Fh` function 1687h answers with AX 0 and: BX bit 0 set (32-bit
programs welcome), CL 3 (a 386), DX 005Ah (version 0.90), SI the paragraphs of conventional memory
the host wants for itself, and ES:DI the address to call to switch.

**The switch.** The program gives the host its memory in ES and calls that address far, with AX 1
for a 32-bit program. It comes back at the next instruction in protected mode, at privilege level
3, with selectors for the code, data and stack segments it had (64 KB each, over the same memory),
ES a selector for its PSP, and the environment's segment in the PSP replaced by a selector.

**`INT 31h`.** The services, by function group:

| Functions | What they're for |
|---|---|
| 0000h–000Dh | Descriptors in the local table: allocate, free, set base, limit and access rights, copy in and out, and one for a real-mode segment |
| 0100h–0102h | Conventional memory from DOS, with selectors for it |
| 0200h–0205h | Real-mode vectors, exception handlers, and protected-mode vectors |
| 0300h–0306h | Calls down to real mode (an interrupt, a far call, an IRET-style call), callbacks up from it, and the raw switch |
| 0400h | The version |
| 0500h–0503h | Extended memory: how much is free, allocate, free, resize |
| 0600h–0604h, 0702h–0703h, 0800h–0801h | Locking and paging hints and physical addresses, all trivial without paging |
| 0900h–0902h | The interrupt flag the program will return with |

**Interrupts.** A program's `INT 21h` in protected mode goes to its own handler if it set one
(0205h), and otherwise to the host's, which *reflects* it: switches to real mode, runs the
real-mode vector with the program's registers, and comes back with the results. DOS extenders
hook `INT 21h` themselves, copy pointers' data between the two modes, and call DOS with 0300h.

## How the host is built

### Level 3, with I/O privilege 3

The program runs at privilege level 3, with the I/O privilege level in the flags set to 3 too. Its
`CLI`, `STI`, `IN` and `OUT` work as in real mode, which games expect, while loading the
descriptor tables or `HLT`, level 0's instructions, fault to the host. Level 0 is used for one
thing only: exceptions arrive there, on a stack of the host's own (from the task state segment),
so a fault on a broken stack can still be handled.

### Switching modes without running code

A real DPMI host switches modes with code: it builds a stack frame, loads CR0, jumps, reloads the
segments. Confederation's host does it in C#, directly on the processor. `Intel386.SaveContext`
copies out every register, the flags, CR0, the privilege level, the interrupt table register and
all six segment registers' descriptor caches; `RestoreContext` puts them back, mode and all.

So a call down to real mode is: save the processor's state, set up real mode (registers from the
program's structure, a stack, a return address pointing at a host stub), and let it run. When the
real-mode code returns to the stub, the stub calls into C#, which copies the results out and
restores the saved state. Each switch in progress is an entry in a list, newest last, so switches
nest: a DOS call made from a handler that was called up from real mode while the program was
waiting in a DOS call, and so on.

The nesting decides the stacks. Real-mode code runs on the host's own stack, in the memory the
program gave it on entry, below wherever an earlier switch left off. Calls up to the program run on
a "locked" stack of the host's, likewise below any earlier use of it.

One thing a switch has to change that's easy to forget: **the interrupt table register**. The
386 has one IDTR for both modes, and real mode uses it too: its interrupt vectors are wherever
IDTR points, 0 after reset. The first version of the host left it pointing at the protected-mode
table, so the BIOS's `INT 1Ch`, in real mode, read two bytes of a gate as a vector and jumped into
nothing. Each switch now sets IDTR, and the saved state carries it.

### Where everything is

The machine now has 16 MB of memory. Past the first megabyte it's reachable once the A20 gate is
open, which the host does on entry.

| Where | What |
|---|---|
| F000:0600 and on | The host's real-mode stubs: the entry, the return from real-mode calls, the raw switch, a stub for each vector, and the callbacks |
| 110000h | The host's own area, just past the high memory area: the global descriptor table, the interrupt table, the task state segment, the protected-mode stubs, the level 0 stack and the locked stack |
| 120000h | The local descriptor table, 8,192 descriptors, where the program's selectors are |
| 130000h–FFFFFFh | Extended memory, for function 0501h |

Without paging, a linear address is a physical one, and blocks are handed out first fit, a page at
a time. The protected-mode stubs are `0F FF` and a service number, the same escape the BIOS and DOS
use ([answering an interrupt in C#](the-dos-layer.md#answering-an-interrupt-in-c)).

### Three paths for interrupts

**A program's `INT n`, n 20h or above**, goes through its gate to the program's handler, or to the
host's default handler for n. The host writes the program's handler straight into the gate when it
sets one, so the processor does the rest.

**A program's `INT n` below 20h** is a different matter: those 32 vectors are the processor's
exceptions, and their gates belong to the host's exception handlers, at level 0. A program at
level 3 isn't allowed through a level 0 gate, so its `INT 10h` raises a general protection fault,
whose error code names the vector (10h × 8 + 2, the 2 meaning "from the interrupt table"). The host
recognises that, and makes the interrupt happen as if the gate had let it through: the program's
flags and return address on its stack, and on into the handler for 10h.

**A hardware interrupt** can't use the table at all. The PC's interrupt controller delivers IRQ 0
to 7 as vectors 8 to 0Fh, which in protected mode are the double fault, the general protection
fault and the rest. So in protected mode the machine gives the interrupt to the host, which calls
the program's handler for it from its own copy of the vectors, or its default handler, which
reflects it down to the BIOS. A real host reprograms the interrupt controller instead; the effect
is the same.

And an interrupt that comes **while the processor is in real mode**, during a DOS call, still
belongs to the program if it hooked the vector in protected mode. When the program sets a handler
for one of the hardware interrupts (or 1Ch, 23h or 24h), the host puts a stub in the real-mode
vector. The stub switches up and calls the program's handler; when the handler hands the interrupt
on to the host's default, which reflects it down, the stub, reached again, passes it on to the
vector it replaced instead of bringing it back up, so the two don't chase each other.

### Exceptions

A program can set handlers for exceptions (0203h). The host calls one on the locked stack with
DPMI's exception frame: a far return to the host, the error code, and where the fault happened
(EIP, CS, the flags, ESP, SS). The handler can change any of those, and returns far to the host,
which carries on from wherever the frame says. Without a handler, the machine stops with a
message naming the fault and where it happened.

`HLT` is level 0's, so a program waiting for an interrupt that way faults; the host treats the
fault as the wait it was meant to be.

## What the launcher needed

With the host in place, the launcher was run, and the log showed each next thing it needed:

- **VCPI's answer.** PMODE/W asked for VCPI before DPMI, and the machine stopped at the unknown
  EMS function. Answering 84h sent it on to DPMI.
- **The A20 gate, before the tables.** The host built its tables just above 1 MB, but with the
  gate still closed the writes wrapped round into the first megabyte, and loading the local table
  failed. The gate opens first now.
- **Function 0300h's interrupt.** The interrupt number in BL was read after the program's
  registers had been loaded from its structure, by which time BL was the file handle: `INT 05h`
  instead of `INT 21h`.
- **IDTR in real mode**, as above.
- **Watcom's runtime**: DOS's double-byte character table (`INT 21h` 6300h, answered with an empty
  one), the code page (6601h, 437), and the keyboard's repeat rate (`INT 16h` 03h, which the
  host's keyboard does its own way).

After those, the launcher reaches its screen: "Welcome to WING COMMANDER A.T. Performing initial
setup... Scanning game files...". It then reports `..\GAMEWC1` missing, which is right for now:
Confederation mounted the launcher's own folder as drive C:, and W.C.A.T. keeps the game in the
folder beside it. Mounting its folders as it lays them out, and running the batch files it writes,
came next ([W.C.A.T.](wcat.md)).

## Checking it

`DpmiTests.cs` runs small DPMI programs on the machine, each started from real mode through the
same prologue: shrink its memory, find the host, give it its memory, enter as a 32-bit program.
Each checks one thing and exits with 42, or 255 at the first thing that didn't hold:

- the switch, level 3, the version, and `INT 10h` from protected mode reaching the BIOS;
- extended memory through descriptors: a block, a descriptor based at it, and a flat 4 GB one
  that sees the same bytes;
- `INT 21h` called in real mode through 0300h;
- a timer handler counting ticks while the program waits with `HLT` in protected mode, and while
  a real-mode procedure it called waits: those ticks come up from real mode;
- an exception handler given a general protection fault, with the selector as its error code,
  that resumes the program past the faulting instruction;
- a real-mode callback, called far from real mode, returning the registers it set;
- a state saved while the host was down in real mode, with the protected-mode state waiting,
  loaded into a fresh machine that finishes the program.

The programs were written in assembly and assembled with Keystone; the bytes in the tests carry
their instructions as comments. To check the tests can fail, the host was broken on purpose twice:
with interrupts never coming up from real mode, the timer test failed; with exception handlers
ignored, the exception test failed with the machine's message about the fault.

## What isn't there yet

- **One program at a time.** A second one entering while the first is in protected mode is
  refused. The host lets go when the program ends, ready for the next.
- **DPMI 0.9 only.** DPMI 1.0's additions (0401h's capabilities, shared memory, the coprocessor
  functions) answer "unsupported", as do the debug watchpoints (0B00h) and vendor extensions
  (0A00h).
- **No virtual memory.** Linear addresses are physical; 0800h maps a physical address to itself.
- **Hardware interrupts in protected mode run on the program's own stack**, as under a host at
  level 0, rather than on a locked stack as some hosts do. To confirm whether any program cares.
- **16-bit programs** are provided for (their frames and returns are 16-bit) but untested.
- **States saved before this change won't load**: the machine's layout changed with the host, and
  Confederation says so rather than misreading them.
