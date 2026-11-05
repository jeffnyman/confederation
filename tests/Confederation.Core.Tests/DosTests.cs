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

    private Machine Start(byte[] code)
    {
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), Exe(code));
        var machine = new Machine(_game, _saves) { Log = _log.Add };
        machine.Load("TEST.EXE");
        machine.Run(100_000);
        Assert.True(machine.Exited, string.Join('\n', _log));
        return machine;
    }

    /// <summary>
    /// An MZ executable around code that starts at its first byte: a two-paragraph header, no
    /// relocations, and a stack at the top of a 64 KB segment of its own.
    /// </summary>
    private static byte[] Exe(byte[] code)
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
