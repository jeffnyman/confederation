using System.Buffers.Binary;
using System.Text;

namespace Confederation.Player.Tests;

/// <summary>
/// Wing Commander III, as GOG lays it out: a folder with no program, only the CD image
/// <c>DATA.DAT</c>, which the player knows it by and runs <c>WC3.EXE</c> from, as drive D:. The
/// image here is a few sectors, with a stand-in <c>WC3.EXE</c>.
/// </summary>
public sealed class CdGameTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("confederation-wc3").FullName;

    /// <summary>
    /// A program that ends with 0 when it's run from D:, and 1 otherwise:
    ///   mov ah, 19h; int 21h; cmp al, 3; jne fail; mov ax, 4C00h; int 21h; fail: mov ax, 4C01h; int 21h
    /// </summary>
    private static readonly byte[] OnD = ProfileTests.Exe(
        [0xB4, 0x19, 0xCD, 0x21, 0x3C, 0x03, 0x75, 0x05, 0xB8, 0x00, 0x4C, 0xCD, 0x21, 0xB8, 0x01, 0x4C, 0xCD, 0x21]);

    public CdGameTests()
    {
        Folder = Directory.CreateDirectory(Path.Combine(_root, "Wing Commander III")).FullName;
        Directory.CreateDirectory(Path.Combine(Folder, "WC3"));
    }

    private string Folder { get; }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Its_folder_is_known_by_its_CD_and_the_game_runs_from_it()
    {
        Image("WC3_CD1");
        Assert.Equal(Games.Wc3, Games.SeriesIn(Folder));

        var game = Games.Find(Folder, null);
        Assert.Equal(Games.Wc3, game.Series);
        Assert.Equal(Path.Combine(Folder, "DATA.DAT"), game.Cd);
        Assert.Equal(@"D:\WC3.EXE", game.Start);
        Assert.Equal(30000, game.Cycles);

        // the image itself stands for the game too; and the short name finds it once it's set up
        Assert.Equal(game, Games.Find(Path.Combine(Folder, "DATA.DAT"), null));
        Assert.Equal(game, Games.In(Folder, Games.Named("wc3")!));

        string settings = Path.Combine(_root, "home", "confederation.json");
        string saves = Path.Combine(_root, "saves");
        Assert.Equal(0, Program.Run([Folder, "--settings", settings, "--saves", saves, "--headless", "--seconds", "1"]));
        Assert.Equal(Folder, Settings.Load(settings).FolderOf(Games.Wc3));
        Assert.Equal(0, Program.Run(["wc3", "--settings", settings, "--saves", saves, "--headless", "--seconds", "1"]));
    }

    [Fact]
    public void Another_CD_is_not_Wing_Commander_III()
    {
        Image("SOMETHING_ELSE");
        Assert.Null(Games.SeriesIn(Folder));
        Assert.False(Games.IsIn(Folder, Games.Named("wc3")!));
        Assert.Throws<FileNotFoundException>(() => Games.In(Folder, Games.Named("wc3")!));
    }

    /// <summary>
    /// <c>DATA.DAT</c>: an ISO 9660 image with its primary volume descriptor at sector 16, the
    /// set's terminator at 17, the root directory at 18, and <c>WC3.EXE</c> at 19.
    /// </summary>
    private void Image(string label)
    {
        const int Sector = 2048;
        int sectors = 19 + ((OnD.Length + Sector - 1) / Sector);
        var image = new byte[sectors * Sector];

        static byte[] Record(int sector, int size, bool directory, byte[] name)
        {
            int length = 33 + name.Length + ((name.Length % 2 == 0) ? 1 : 0);
            var record = new byte[length];
            record[0] = (byte)length;
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(2), (uint)sector);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), (uint)size);
            record[18] = 94;
            record[19] = 11;
            record[20] = 1;
            record[25] = (byte)(directory ? 0x02 : 0x00);
            record[32] = (byte)name.Length;
            name.CopyTo(record, 33);
            return record;
        }

        var descriptor = image.AsSpan(16 * Sector, Sector);
        descriptor[0] = 1;
        "CD001"u8.CopyTo(descriptor[1..]);
        descriptor[6] = 1;
        Encoding.ASCII.GetBytes(label.PadRight(32)).CopyTo(descriptor[40..]);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor[80..], (uint)sectors);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor[128..], Sector);
        Record(18, Sector, true, [0]).CopyTo(descriptor[156..]);
        image[17 * Sector] = 255;
        "CD001"u8.CopyTo(image.AsSpan((17 * Sector) + 1));

        int at = 18 * Sector;
        foreach (var record in new[] { Record(18, Sector, true, [0]), Record(18, Sector, true, [1]), Record(19, OnD.Length, false, "WC3.EXE;1"u8.ToArray()) })
        {
            record.CopyTo(image, at);
            at += record.Length;
        }

        OnD.CopyTo(image, 19 * Sector);
        File.WriteAllBytes(Path.Combine(Folder, "DATA.DAT"), image);
    }
}
