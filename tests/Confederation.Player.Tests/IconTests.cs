namespace Confederation.Player.Tests;

/// <summary>The games' icons, built into the player, and the reader of the icon files they come in.</summary>
public sealed class IconTests
{
    [Theory]
    [InlineData("wc1")]
    [InlineData("sm1")]
    [InlineData("sm2")]
    [InlineData("wc2")]
    [InlineData("so1")]
    [InlineData("so2")]
    public void Every_game_has_its_icon(string name)
    {
        var known = Games.Named(name)!;
        var game = new Game("", known.Program, known.Title, known.Series.Cycles, known.Series);
        var images = GameIcons.For(game);

        // the bitmaps, at 32 and 48 pixels; the PNG at 256 is left
        Assert.Equal([32, 48], images.Select(image => image.Width).Order());
        foreach (var image in images)
        {
            Assert.Equal(image.Width, image.Height);
            Assert.Equal(image.Width * image.Height, image.Pixels.Length);

            // a picture, not a blank: many colors, and solid where the mask says
            Assert.True(image.Pixels.Select(pixel => pixel & 0xFFFFFF).Distinct().Count() > 16);
            Assert.Contains(image.Pixels, pixel => pixel >> 24 == 0xFF);
        }
    }

    [Fact]
    public void A_program_the_player_does_not_know_has_none()
        => Assert.Empty(GameIcons.For(new Game("", "OTHER.EXE", "OTHER", 4000, null)));

    [Fact]
    public void A_bitmap_takes_its_colors_and_its_shape_from_the_file()
    {
        // a 2 by 2 icon of 24 bits: bottom row blue and green, top row red and white, and the
        // mask clearing the top right
        byte[] dib =
        [
            .. BitConverter.GetBytes(40), .. BitConverter.GetBytes(2), .. BitConverter.GetBytes(4),
            1, 0, 24, 0, .. new byte[24],
            0xFF, 0, 0, 0, 0xFF, 0, 0, 0, // bottom: blue, green, padded to 8
            0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, // top: red, white
            0, 0, 0, 0, // mask, bottom: solid
            0x40, 0, 0, 0, // mask, top: the second pixel clear
        ];
        byte[] ico =
        [
            0, 0, 1, 0, 1, 0,
            2, 2, 0, 0, 1, 0, 24, 0, .. BitConverter.GetBytes(dib.Length), .. BitConverter.GetBytes(22),
            .. dib,
        ];

        var image = Assert.Single(GameIcons.Read(ico));
        Assert.Equal([0xFFFF0000u, 0x00FFFFFFu, 0xFF0000FFu, 0xFF00FF00u], image.Pixels);
    }
}
