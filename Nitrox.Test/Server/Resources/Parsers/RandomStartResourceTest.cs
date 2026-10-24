namespace Nitrox.Server.Subnautica.Models.Resources.Parsers;

[TestClass]
public class RandomStartResourceTest
{
    [TestMethod]
    public void PixelProvider_ReturnsRgbChannelsFromBgraPixels()
    {
        RandomStartResource.PixelProvider provider = new([0x33, 0x22, 0x11, 0xFF], 1, 1);

        provider.GetRed(0, 0).Should().Be((byte)0x11);
        provider.GetGreen(0, 0).Should().Be((byte)0x22);
        provider.GetBlue(0, 0).Should().Be((byte)0x33);
    }

    [TestMethod]
    public void PixelProvider_IgnoresTrailingBytes()
    {
        RandomStartResource.PixelProvider provider = new([0x33, 0x22, 0x11, 0xFF, 0xEE], 1, 1);

        provider.GetRed(0, 0).Should().Be((byte)0x11);
        provider.GetGreen(0, 0).Should().Be((byte)0x22);
        provider.GetBlue(0, 0).Should().Be((byte)0x33);
    }

    [TestMethod]
    public void PixelProvider_FlipsRowsVertically()
    {
        byte[] textureData =
        [
            0x30, 0x20, 0x10, 0xFF,
            0x60, 0x50, 0x40, 0xFF
        ];
        RandomStartResource.PixelProvider provider = new(textureData, 1, 2);

        provider.GetRed(0, 0).Should().Be((byte)0x40);
        provider.GetGreen(0, 0).Should().Be((byte)0x50);
        provider.GetBlue(0, 0).Should().Be((byte)0x60);
        provider.GetRed(0, 1).Should().Be((byte)0x10);
        provider.GetGreen(0, 1).Should().Be((byte)0x20);
        provider.GetBlue(0, 1).Should().Be((byte)0x30);
    }

    [TestMethod]
    public void PixelProvider_ClampsCoordinatesToTextureBounds()
    {
        byte[] textureData =
        [
            0x03, 0x02, 0x01, 0xFF,
            0x06, 0x05, 0x04, 0xFF,
            0x09, 0x08, 0x07, 0xFF,
            0x0C, 0x0B, 0x0A, 0xFF
        ];
        RandomStartResource.PixelProvider provider = new(textureData, 2, 2);

        provider.GetRed(-1, -1).Should().Be((byte)0x07);
        provider.GetRed(2, -1).Should().Be((byte)0x0A);
        provider.GetRed(-1, 2).Should().Be((byte)0x01);
        provider.GetRed(2, 2).Should().Be((byte)0x04);
    }
}
