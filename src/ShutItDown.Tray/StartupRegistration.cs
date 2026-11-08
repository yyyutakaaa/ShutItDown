using System.IO;
using Microsoft.Win32;

namespace ShutItDown.Tray;

/// <summary>Starts the app at Windows sign-in through the current user's Run key (no admin rights needed).</summary>
public static class StartupRegistration
{
    public const string MinimizedArgument = "--minimized";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShutdownServer";

    // Versions before 3.0 put a shortcut in the Startup folder instead.
    private static string LegacyShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Shutdown Server.lnk");

    private static string Command => $"\"{Environment.ProcessPath}\" {MinimizedArgument}";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Replaces the Startup-folder shortcut of versions before 3.0 with the Run key.
    /// Skipped in Debug builds, so running from Visual Studio never takes over the installed app's autostart.
    /// </summary>
    public static void MigrateLegacyShortcut()
    {
#if !DEBUG
        try
        {
            if (File.Exists(LegacyShortcutPath))
            {
                File.Delete(LegacyShortcutPath);
                SetEnabled(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth bothering the user about; the checkbox shows the real state.
        }
#endif
    }
}
