using System.Text.RegularExpressions;
using System.Windows;
using ShutItDown.Core.Updates;

namespace ShutItDown.Tray;

/// <summary>Shows a new version with its release notes, and downloads and starts the installer.</summary>
public partial class UpdateWindow : Window
{
    private readonly AppController _controller;
    private readonly UpdateInfo _update;
    private readonly Func<string, Task> _install;
    private CancellationTokenSource? _download;

    /// <param name="install">Starts the downloaded installer and closes the app.</param>
    public UpdateWindow(AppController controller, UpdateInfo update, Func<string, Task> install)
    {
        InitializeComponent();
        _controller = controller;
        _update = update;
        _install = install;

        VersionText.Text = $"{controller.CurrentVersion} → {update.Version}";
        NotesText.Text = CleanNotes(update.Notes);
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        ErrorText.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Downloading…";

        _download = new CancellationTokenSource();
        var progress = new Progress<double>(value =>
        {
            Progress.Value = value;
            ProgressText.Text = $"Downloading… {value:P0}";
        });

        try
        {
            var installer = await _controller.Updates.DownloadAsync(_update, progress, _download.Token);
            ProgressText.Text = "Installing. Shutdown Server starts again in a moment.";
            await _install(installer);
        }
        catch (OperationCanceledException)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            SetBusy(false);
        }
        catch (Exception ex)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = $"The update didn't work: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            SetBusy(false);
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _controller.SkipVersion(_update.Version);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _download?.Cancel();
        base.OnClosed(e);
    }

    private void SetBusy(bool busy)
    {
        UpdateButton.IsEnabled = !busy;
        SkipButton.IsEnabled = !busy;
        LaterButton.Content = busy ? "Cancel" : "Later";
    }

    /// <summary>Turns GitHub's Markdown release notes into plain, readable text.</summary>
    public static string CleanNotes(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "Bug fixes and improvements.";

        var lines = new List<string>();
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("**Full Changelog**", StringComparison.OrdinalIgnoreCase)) continue;
            line = Regex.Replace(line, @"^#+\s*", "");
            line = Regex.Replace(line, @"^[*-]\s+", "• ");
            line = Regex.Replace(line, @"\s+by @\S+ in https://\S+$", "");   // "by @user in https://github.com/.../pull/1"
            line = Regex.Replace(line, @"\*\*|__|`", "");
            line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1");     // [text](link) -> text
            if (line.Length == 0 && (lines.Count == 0 || lines[^1].Length == 0)) continue;
            lines.Add(line);
        }

        var text = string.Join("\n", lines).Trim();
        return text.Length == 0 ? "Bug fixes and improvements." : text;
    }
}
