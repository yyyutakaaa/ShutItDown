using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using ShutItDown.Core.Monitoring;

namespace ShutItDown.Tray;

/// <summary>Reads CPU, memory, network, input idle time, open windows and the screen through Windows APIs.</summary>
public sealed class WindowsPcMonitor : IPcMonitor, IDisposable
{
    private static readonly TimeSpan SampleEvery = TimeSpan.FromSeconds(2);

    private readonly Timer _sampler;
    private readonly object _lock = new();
    private readonly int _ownProcessId = Environment.ProcessId;

    private ulong _lastIdle, _lastKernel, _lastUser;
    private long _lastReceived, _lastSent;
    private long _lastSampleTicks;
    private double _cpuPercent, _downPerSecond, _upPerSecond;

    public WindowsPcMonitor()
    {
        Sample(); // baseline, so the first real sample has something to compare with
        _sampler = new Timer(_ => Sample(), null, SampleEvery, SampleEvery);
    }

    public PcSnapshot GetSnapshot()
    {
        double cpu, down, up;
        lock (_lock) (cpu, down, up) = (_cpuPercent, _downPerSecond, _upPerSecond);

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref memory);

        return new PcSnapshot(cpu, memory.MemoryLoad, down, up, UserIdle(), TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    public IReadOnlyList<OpenApp> GetOpenApps()
    {
        var apps = new List<OpenApp>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, GwOwner) != IntPtr.Zero || IsCloaked(hwnd))
                return true;
            if ((GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() & WsExToolWindow) != 0)
                return true;

            var length = GetWindowTextLength(hwnd);
            if (length == 0) return true;
            var text = new StringBuilder(length + 1);
            GetWindowText(hwnd, text, text.Capacity);

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == _ownProcessId) return true;

            string name;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
            catch (ArgumentException)
            {
                return true;
            }

            // Windows' own shell windows are not apps people think of.
            if (name is "explorer" && text.ToString() == "Program Manager") return true;
            if (name is "TextInputHost" or "ShellExperienceHost" or "SearchHost" or "StartMenuExperienceHost") return true;

            apps.Add(new OpenApp(name, text.ToString()));
            return true;
        }, IntPtr.Zero);
        return apps;
    }

    public bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    public byte[]? CaptureScreenJpeg(int maxWidth)
    {
        // Capture in real pixels on every monitor, whatever the scaling is.
        var previous = SetThreadDpiAwarenessContext(DpiAwarenessPerMonitorV2);
        try
        {
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            using var full = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(full))
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);

            var scale = Math.Min(1.0, (double)maxWidth / bounds.Width);
            using var small = new Bitmap((int)(bounds.Width * scale), (int)(bounds.Height * scale), PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(full, 0, 0, small.Width, small.Height);
            }

            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
            using var stream = new MemoryStream();
            small.Save(stream, encoder, parameters);
            return stream.ToArray();
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException)
        {
            return null; // locked screen or secure desktop: CopyFromScreen throws Win32Exception (an ExternalException)
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    public void Dispose() => _sampler.Dispose();

    private void Sample()
    {
        try
        {
            GetSystemTimes(out var idle, out var kernel, out var user);
            long received = 0, sent = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                var stats = nic.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }

            var now = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                if (_lastSampleTicks != 0)
                {
                    var seconds = (now - _lastSampleTicks) / (double)Stopwatch.Frequency;
                    var total = (kernel - _lastKernel) + (user - _lastUser); // kernel time includes idle time
                    _cpuPercent = total == 0 ? 0 : Math.Clamp(100.0 * (1 - (double)(idle - _lastIdle) / total), 0, 100);
                    _downPerSecond = Math.Max(0, (received - _lastReceived) / seconds);
                    _upPerSecond = Math.Max(0, (sent - _lastSent) / seconds);
                }
                (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);
                (_lastReceived, _lastSent, _lastSampleTicks) = (received, sent, now);
            }
        }
        catch (NetworkInformationException)
        {
            // Try again on the next tick.
        }
    }

    private static TimeSpan UserIdle()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var idleMs = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    // --- Win32 -----------------------------------------------------------

    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80;
    private const int DwmwaCloaked = 14;
    private static readonly IntPtr DpiAwarenessPerMonitorV2 = new(-4);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size; public uint Time; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
