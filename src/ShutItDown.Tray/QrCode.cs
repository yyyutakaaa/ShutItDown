using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace ShutItDown.Tray;

public static class QrCode
{
    public static BitmapImage Create(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 10, drawQuietZones: false);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
