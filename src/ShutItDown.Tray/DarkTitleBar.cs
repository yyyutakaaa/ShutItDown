using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ShutItDown.Tray;

/// <summary>Windows 11: a dark title bar in the window's own color, so the window looks like one surface.</summary>
public static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int CaptionColor = 35;

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var on = 1;
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int));

        var bg = (Color)window.FindResource("BgColor");
        var colorRef = bg.R | (bg.G << 8) | (bg.B << 16);
        DwmSetWindowAttribute(hwnd, CaptionColor, ref colorRef, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
