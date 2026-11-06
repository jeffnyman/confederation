namespace Confederation.Core.Tests;

/// <summary>
/// The DOS layer, with small programs assembled by hand: each is an MZ executable written to a
/// temporary drive C:, loaded and run as a game would be.
/// </summary>
public sealed class DosTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("confederation-game").FullName;
    private readonly string _saves = Directory.CreateTempSubdirectory("confederation-saves").FullName;
    private readonly List<string> _log = [];

    public void Dispose()
    {
        Directory.Delete(_game, recursive: true);
        Directory.Delete(_saves, recursive: true);
    }

    [Fact]
    public void A_program_prints_and_exits_with_its_code()
    {
        // push cs; pop ds; mov dx, message; mov ah, 9; int 21h; mov ax, 4C07h; int 21h
        var machine = Start(
        [
            0x0E, 0x1F, 0xBA, 0x0E, 0x00, 0xB4, 0x09, 0xCD, 0x21, 0xB8, 0x07, 0x4C, 0xCD, 0x21,
            .. "Hello, Confederation\r\n$"u8,
        ]);

        Assert.Contains("console: Hello, Confederation", _log);
        Assert.Equal(7, machine.ExitCode);
    }

    [Fact]
    public void A_program_shrinks_its_block_and_allocates_another()
    {
        // a program gets all free memory, so it must give some back before it can allocate:
        //   mov ah, 4Ah; mov bx, 100h; int 21h; jc fail
        //   mov ah, 48h; mov bx, 1000h; int 21h; jc fail
        //   mov ax, 4C00h; int 21h
        //   fail: mov ax, 4C01h; int 21h
        var machine = Start(
        [
            0xB4, 0x4A, 0xBB, 0x00, 0x01, 0xCD, 0x21, 0x72, 0x0E,
            0xB4, 0x48, 0xBB, 0x00, 0x10, 0xCD, 0x21, 0x72, 0x05,
            0xB8, 0x00, 0x4C, 0xCD, 0x21,
            0xB8, 0x01, 0x4C, 0xCD, 0x21,
        ]);

        Assert.Equal(0, machine.ExitCode);
    }

    [Fact]
    public void Writing_a_file_leaves_the_game_folder_untouched()
    {
        File.WriteAllText(Path.Combine(_game, "SAVE.DAT"), "original");

        //   push cs; pop ds
        //   mov ax, 3D01h; mov dx, name; int 21h     open SAVE.DAT for writing
        //   mov bx, ax; mov ah, 40h; mov cx, 3; mov dx, data; int 21h
        //   mov ah, 3Eh; int 21h; mov ax, 4C00h; int 21h
        var machine = Start(
        [
            0x0E, 0x1F,
            0xB8, 0x01, 0x3D, 0xBA, 0x1F, 0x00, 0xCD, 0x21,
            0x89, 0xC3, 0xB4, 0x40, 0xB9, 0x03, 0x00, 0xBA, 0x28, 0x00, 0xCD, 0x21,
            0xB4, 0x3E, 0xCD, 0x21, 0xB8, 0x00, 0x4C, 0xCD, 0x21,
            .. "SAVE.DAT\0"u8,
            .. "new"u8,
        ]);

        Assert.Equal(0, machine.ExitCode);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_game, "SAVE.DAT")));
        Assert.Equal("newginal", File.ReadAllText(Path.Combine(_saves, "SAVE.DAT")));
    }

    [Fact]
    public void The_expanded_memory_manager_is_found_by_its_name()
    {
        //   mov ax, 3567h; int 21h            ES:BX = the INT 67h vector
        //   mov di, 0Ah; mov si, name; push cs; pop ds; mov cx, 8; repe cmpsb
        //   jne fail; mov ax, 4C00h; int 21h; fail: mov ax, 4C01h; int 21h
        var machine = Start(
        [
            0xB8, 0x67, 0x35, 0xCD, 0x21,
            0xBF, 0x0A, 0x00, 0xBE, 0x1E, 0x00, 0x0E, 0x1F, 0xB9, 0x08, 0x00, 0xF3, 0xA6,
            0x75, 0x05, 0xB8, 0x00, 0x4C, 0xCD, 0x21,
            0xB8, 0x01, 0x4C, 0xCD, 0x21,
            .. "EMMXXXX0"u8,
        ]);

        Assert.Equal(0, machine.ExitCode);
    }

    [Fact]
    public void A_key_pressed_reaches_the_program_through_the_bios()
    {
        // mov ah, 0; int 16h; mov ah, 4Ch; int 21h: exits with the character of the key
        var machine = Start([0xB4, 0x00, 0xCD, 0x16, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            // shift down, A down and up, shift up: the keyboard controller's IRQ 1, the BIOS's
            // handler and its buffer turn it into a capital A for INT 16h
            m.PressKey(0x2A);
            m.PressKey(0x1E);
            m.ReleaseKey(0x1E);
            m.ReleaseKey(0x2A);
        });

        Assert.Equal('A', machine.ExitCode);
    }

    [Fact]
    public void Dos_reads_a_key_through_the_bios_and_an_arrow_as_two()
    {
        // mov ah, 8; int 21h; mov ah, 4Ch; int 21h: the key's character is the exit code
        var machine = Start([0xB4, 0x08, 0xCD, 0x21, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            m.PressKey(0x30);
            m.ReleaseKey(0x30);
        });
        Assert.Equal('b', machine.ExitCode);

        // the Up arrow of the AT's own block comes as a zero, then its scan code: mov ah, 7;
        // int 21h; mov bl, al; mov ah, 7; int 21h; or al, bl; mov ah, 4Ch; int 21h
        machine = Start([0xB4, 0x07, 0xCD, 0x21, 0x88, 0xC3, 0xB4, 0x07, 0xCD, 0x21, 0x08, 0xD8, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            m.PressKey(0x48, extended: true);
            m.ReleaseKey(0x48, extended: true);
        });
        Assert.Equal(0x48, machine.ExitCode);
    }

    [Theory]
    [InlineData(false, 0x00)]
    [InlineData(true, 0xFF)]
    public void Dos_says_whether_a_key_is_waiting(bool press, int answer)
    {
        // mov ah, 0Bh; int 21h; mov ah, 4Ch; int 21h
        var machine = Start([0xB4, 0x0B, 0xCD, 0x21, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            if (press)
            {
                m.PressKey(0x1E);
                m.ReleaseKey(0x1E);
            }
        });

        Assert.Equal(answer, machine.ExitCode);
    }

    [Fact]
    public void The_bios_reports_the_keyboard_intercept_in_its_configuration()
    {
        // mov ah, 0C0h; int 15h; jc fail; mov al, es:[bx+5]; mov ah, 4Ch; int 21h;
        // fail: mov ax, 4CFFh; int 21h. The first feature byte is the exit code.
        var machine = Start([0xB4, 0xC0, 0xCD, 0x15, 0x72, 0x08, 0x26, 0x8A, 0x47, 0x05, 0xB4, 0x4C, 0xCD, 0x21, 0xB8, 0xFF, 0x4C, 0xCD, 0x21]);
        Assert.Equal(0x70, machine.ExitCode);
        Assert.NotEqual(0, machine.ExitCode & 0x10); // INT 09h calls INT 15h function 4Fh
    }

    [Fact]
    public void A_program_on_the_keyboard_intercept_can_keep_a_key_from_the_bios()
    {
        // hook INT 15h with a handler that keeps A (scan code 1Eh) and passes the rest on, then
        // read a key with INT 16h: A and then B are pressed, and B is the key the BIOS has
        //
        //   push cs; pop ds; mov dx, handler; mov ax, 2515h; int 21h
        //   mov ah, 0; int 16h; mov ah, 4Ch; int 21h
        // handler:
        //   cmp ah, 4Fh; jne pass; cmp al, 1Eh; jne pass; clc; retf 2
        // pass:
        //   stc; retf 2
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), Exe(
        [
            0x0E, 0x1F, 0xBA, 0x12, 0x00, 0xB8, 0x15, 0x25, 0xCD, 0x21,
            0xB4, 0x00, 0xCD, 0x16, 0xB4, 0x4C, 0xCD, 0x21,
            0x80, 0xFC, 0x4F, 0x75, 0x08, 0x3C, 0x1E, 0x75, 0x04, 0xF8, 0xCA, 0x02, 0x00,
            0xF9, 0xCA, 0x02, 0x00,
        ]));
        var machine = new Machine(_game, _saves) { Log = _log.Add };
        machine.Load("TEST.EXE");
        machine.Run(10_000);
        Assert.False(machine.Exited);

        machine.PressKey(0x1E);
        machine.ReleaseKey(0x1E);
        machine.Run(10_000);
        Assert.False(machine.Exited);

        machine.PressKey(0x30);
        machine.ReleaseKey(0x30);
        machine.Run(10_000);
        Assert.True(machine.Exited, string.Join('\n', _log));
        Assert.Equal('b', machine.ExitCode);
    }

    [Theory]
    [InlineData(false, 0x00)]
    [InlineData(true, 0x02)]
    public void The_mouse_driver_is_there_only_with_a_mouse(bool mouse, int buttons)
    {
        // xor bx, bx; xor ax, ax; int 33h; mov al, bl; mov ah, 4Ch; int 21h: the buttons it
        // reports are the exit code, none without a mouse
        var machine = Start([0x31, 0xDB, 0x31, 0xC0, 0xCD, 0x33, 0x88, 0xD8, 0xB4, 0x4C, 0xCD, 0x21], m => m.Mouse = mouse);
        Assert.Equal(buttons, machine.ExitCode);
    }

    [Theory]
    [InlineData(0xC8, 50)]  // mov al, cl: the column, in the driver's 640
    [InlineData(0xD0, 30)]  // mov al, dl: the row, moving half as fast as the column
    public void The_mouse_driver_follows_the_mouse(byte register, int expected)
    {
        // mov ax, 3; int 33h; mov al, cl (or dl); mov ah, 4Ch; int 21h
        var machine = Start([0xB8, 0x03, 0x00, 0xCD, 0x33, 0x88, register, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            m.Mouse = true;
            m.MoveMouse(-5000, -5000); // into the top left corner, which holds it
            m.MoveMouse(50, 60);
        });

        Assert.Equal(expected, machine.ExitCode);
    }

    [Fact]
    public void The_mouse_driver_counts_presses_and_motion()
    {
        // mov ax, 5; xor bx, bx; int 33h; mov al, bl; mov ah, 4Ch; int 21h: presses of the left
        // button since last asked
        var machine = Start([0xB8, 0x05, 0x00, 0x31, 0xDB, 0xCD, 0x33, 0x88, 0xD8, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            m.Mouse = true;
            for (int i = 0; i < 3; i++)
            {
                m.SetMouseButton(0, true);
                m.SetMouseButton(0, false);
            }
        });
        Assert.Equal(3, machine.ExitCode);

        // mov ax, 0Bh; int 33h; mov al, cl; mov ah, 4Ch; int 21h: the motion in mickeys, which
        // the driver's ratio doesn't touch
        machine = Start([0xB8, 0x0B, 0x00, 0xCD, 0x33, 0x88, 0xC8, 0xB4, 0x4C, 0xCD, 0x21], m =>
        {
            m.Mouse = true;
            m.MoveMouse(7, -3);
            m.MoveMouse(5, 0);
        });
        Assert.Equal(12, machine.ExitCode);
    }

    [Fact]
    public void Waiting_for_a_key_works_through_a_far_call_with_interrupts_off()
    {
        // as Wing Commander's own INT 21h handler passes a call on to DOS's:
        //   cli; mov ah, 7; pushf; call far F000:0184 (DOS's stub); mov ah, 4Ch; int 21h
        // DOS waits for the key with interrupts on, and returns where the far call does
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), Exe(
        [
            0xFA, 0xB4, 0x07, 0x9C, 0x9A, 0x84, 0x01, 0x00, 0xF0, 0xB4, 0x4C, 0xCD, 0x21,
        ]));
        var machine = new Machine(_game, _saves) { Log = _log.Add };
        machine.Load("TEST.EXE");
        machine.Run(100_000);
        Assert.False(machine.Exited);

        machine.PressKey(0x1E); // A
        machine.Run(100_000);
        Assert.True(machine.Exited, string.Join('\n', _log));
        Assert.Equal('a', machine.ExitCode);
    }

    private Machine Start(byte[] code, Action<Machine>? before = null)
    {
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), Exe(code));
        var machine = new Machine(_game, _saves) { Log = _log.Add };
        machine.Load("TEST.EXE");
        before?.Invoke(machine);
        machine.Run(100_000);
        Assert.True(machine.Exited, string.Join('\n', _log));
        return machine;
    }

    /// <summary>
    /// An MZ executable around code that starts at its first byte: a two-paragraph header, no
    /// relocations, and a stack at the top of a 64 KB segment of its own.
    /// </summary>
    internal static byte[] Exe(byte[] code)
    {
        int total = 32 + code.Length;
        var exe = new byte[total];
        void Word(int at, int value) => BitConverter.GetBytes((ushort)value).CopyTo(exe, at);
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        Word(0x02, total % 512);
        Word(0x04, (total + 511) / 512);
        Word(0x08, 2);                          // header paragraphs
        Word(0x0A, 0x2000);                     // at least 128 KB beyond the image
        Word(0x0C, 0xFFFF);                     // at most everything
        Word(0x0E, 0x1000);                     // SS, relative to the image
        Word(0x10, 0xFFFE);                     // SP
        Word(0x18, 0x1C);                       // where relocations would be
        code.CopyTo(exe, 32);
        return exe;
    }
}
