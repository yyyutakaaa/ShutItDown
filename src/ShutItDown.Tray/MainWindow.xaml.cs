using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Networking;
using ShutItDown.Core.Pairing;
using ShutItDown.Core.Security;

namespace ShutItDown.Tray;

/// <summary>Settings window. Closing it only hides it; the app keeps running in the tray.</summary>
public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _copyReset = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private string? _qrUrl;
    private string? _phoneUrl; // cached so the 1-second QR check doesn't scan network adapters
    private bool _hintShown;

    public MainWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;

        // The server can finish starting on a background thread, so always hop back to the UI thread.
        _controller.StateChanged += (_, _) => Dispatcher.BeginInvoke(RefreshView);
        _clock.Tick += (_, _) =>
        {
            RefreshScheduled();
            RefreshWatch();
            if (_controller.IsRunning) RefreshQr();
        };
        _copyReset.Tick += (_, _) => ResetCopyButton();
        IsVisibleChanged += (_, _) =>
        {
            _clock.IsEnabled = IsVisible;
            if (IsVisible) RefreshView();
        };
        SourceInitialized += (_, _) => UseDarkTitleBar();
        RefreshView();
    }

    /// <summary>Set by the tray before exiting, so closing really closes instead of hiding.</summary>
    public bool AllowClose { get; set; }

    private Brush Res(string key) => (Brush)FindResource(key);

    private void RefreshView()
    {
        var running = _controller.IsRunning;
        ServerSwitch.IsChecked = running;
        StatusDot.Fill = Res(running ? "SuccessBrush" : "MutedBrush");
        StatusText.Text = running ? $"online · port {_controller.Port}" : "offline";
        OnlinePanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        OfflinePanel.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        UpdateServerHint();

        if (running)
        {
            var address = _controller.Address;
            _phoneUrl = $"http://{address.Host}:{_controller.Port}/";
            UrlText.Text = $"{address.Host}:{_controller.Port}";
            PairSubtitle.Text = address.ViaTailscale
                ? "Scan once with your phone's camera. Works at home and away, through Tailscale."
                : "Scan once with your phone's camera. Works while your phone is on this Wi-Fi.";
            RefreshQr();
        }

        RefreshDevices();

        var hasPin = _controller.HasPin;
        PinSubtitle.Text = hasPin
            ? "On. Phones that aren't paired can use the PIN."
            : "Off. Only paired phones can shut down this PC.";
        PinButton.Content = PinEditor.Visibility == Visibility.Visible ? "Close" : hasPin ? "Change" : "Set PIN";
        RemovePinButton.Visibility = hasPin ? Visibility.Visible : Visibility.Collapsed;

        StartupSwitch.IsChecked = StartupRegistration.IsEnabled;

        // Advanced
        if (!PortBox.IsKeyboardFocusWithin)
            PortBox.Text = _controller.Port.ToString();
        UpdateApplyButton();
        SelectOption(GraceOptions, _controller.GraceSeconds);
        SelectOption(AttemptOptions, _controller.MaxPinAttempts);
        NotifySwitch.IsChecked = _controller.NotifyOnRequest;
        TailscaleSwitch.IsChecked = _controller.PreferTailscale;
        ScreenPreviewSwitch.IsChecked = _controller.AllowScreenPreview;
        RefreshWatch();
        TailscaleSubtitle.Text = NetworkInfo.GetTailscaleAddress() is { } tailscale
            ? $"Found ({tailscale}). Paired phones with Tailscale can reach this PC from anywhere."
            : "Tailscale isn't running on this PC. Phones can only reach it on the same Wi-Fi.";

        RefreshScheduled();
    }

    // The pairing code changes after every use and every 10 minutes, so the QR code follows it.
    private void RefreshQr()
    {
        if (_phoneUrl is null) return;
        var url = $"{_phoneUrl}#pair={_controller.Pairing.Current}";
        if (url == _qrUrl) return;
        QrImage.Source = QrCode.Create(url);
        _qrUrl = url;
    }

    private void RefreshDevices()
    {
        DeviceList.Children.Clear();
        var devices = _controller.Devices.Devices;
        if (devices.Count == 0)
        {
            DeviceList.Children.Add(new TextBlock
            {
                Text = "No phones yet. Scan the code above with your phone.",
                Style = (Style)FindResource("Subtitle"),
                Margin = new Thickness(18, 14, 18, 14),
            });
            return;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (i > 0)
                DeviceList.Children.Add(new Border { Style = (Style)FindResource("Divider") });

            var row = new Grid { Style = (Style)FindResource("Row") };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = device.Name, Style = (Style)FindResource("Title") });
            text.Children.Add(new TextBlock { Text = DescribeDevice(device), Style = (Style)FindResource("Subtitle") });
            row.Children.Add(text);

            var remove = new Button { Content = "Remove", Style = (Style)FindResource("GhostButton"), VerticalAlignment = VerticalAlignment.Center };
            remove.Click += (_, _) => RemoveDevice(device);
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);

            DeviceList.Children.Add(row);
        }
    }

    private static string DescribeDevice(PairedDevice device)
    {
        var paired = $"paired {device.PairedAt.ToLocalTime():d MMM}";
        if (device.LastUsedAt is not { } used) return paired;

        var ago = DateTimeOffset.UtcNow - used;
        var usedText = ago.TotalMinutes < 1 ? "just now"
            : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
            : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago"
            : $"{used.ToLocalTime():d MMM}";
        return $"{paired} · last used {usedText}";
    }

    private void RemoveDevice(PairedDevice device)
    {
        if (!Confirm($"Remove “{device.Name}”?\n\nThat phone can't shut down this PC anymore until you pair it again."))
            return;
        Run(() => _controller.RemoveDevice(device.Id));
    }

    private static void SelectOption(Panel options, int value)
    {
        foreach (var option in options.Children.OfType<ToggleButton>())
            option.IsChecked = Equals(option.Tag, value.ToString());
    }

    private void RefreshWatch()
    {
        if (_controller.Watcher.Current is { } watch)
        {
            WatchTitle.Text = $"Shuts down {watch.Describe()}";
            WatchSubtitle.Text = watch.Kind == WatchKind.AppExit
                ? $"{watch.ProcessName} is still open"
                : watch.QuietSince is { } since
                    ? $"quiet for {DateTimeOffset.UtcNow - since:m\\:ss}"
                    : "still busy";
            WatchBanner.Visibility = Visibility.Visible;
        }
        else
        {
            WatchBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelWatch_Click(object sender, RoutedEventArgs e) => Run(_controller.Watcher.Cancel);

    private void ScreenPreviewSwitch_Click(object sender, RoutedEventArgs e) =>
        Run(() => _controller.SetAllowScreenPreview(ScreenPreviewSwitch.IsChecked == true));

    private void RefreshScheduled()
    {
        if (_controller.Power.ScheduledShutdown is { } at)
        {
            var left = at - DateTimeOffset.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            ScheduledTitle.Text = $"Shutting down at {at.ToLocalTime():HH:mm}";
            ScheduledSubtitle.Text = left.TotalHours >= 1 ? $"{left:h\\:mm\\:ss} left" : $"{left:mm\\:ss} left";
            ScheduledBanner.Visibility = Visibility.Visible;
        }
        else
        {
            ScheduledBanner.Visibility = Visibility.Collapsed;
        }
    }

    // --- "Turn on here" hint ----------------------------------------------

    private void UpdateServerHint()
    {
        var show = !_controller.IsRunning && !_controller.ServerHintDismissed && HomePage.Visibility == Visibility.Visible;
        if (show == _hintShown) return;
        _hintShown = show;

        // Make room under the bubble so it never covers the "Server is off" card content.
        OfflinePanel.Margin = show ? new Thickness(0, 56, 0, 18) : new Thickness(0, 18, 0, 18);

        if (show)
        {
            ServerHint.Visibility = Visibility.Visible;
            ServerHint.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(250)));

            // Slide in from above, then nudge toward the switch now and then.
            var enter = new DoubleAnimation(-8, 0, Ms(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            enter.Completed += (_, _) =>
            {
                if (!_hintShown) return;
                var nudge = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
                nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                nudge.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), new SineEase()));
                nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), new SineEase()));
                nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400))));
                HintOffset.BeginAnimation(TranslateTransform.YProperty, nudge);
            };
            HintOffset.BeginAnimation(TranslateTransform.YProperty, enter);
        }
        else
        {
            var fade = new DoubleAnimation(0, Ms(150));
            fade.Completed += (_, _) =>
            {
                if (_hintShown) return;
                ServerHint.Visibility = Visibility.Collapsed;
                HintOffset.BeginAnimation(TranslateTransform.YProperty, null);
            };
            ServerHint.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void DismissHint_Click(object sender, RoutedEventArgs e) => Run(_controller.DismissServerHint);

    private static Duration Ms(int ms) => new(TimeSpan.FromMilliseconds(ms));

    // --- Pages ------------------------------------------------------------

    private void AdvancedButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(AdvancedPage.Visibility == Visibility.Visible ? HomePage : AdvancedPage);

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowPage(HomePage);

    private void ShowPage(FrameworkElement page)
    {
        var other = page == HomePage ? AdvancedPage : HomePage;
        other.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        AdvancedButton.Foreground = Res(page == AdvancedPage ? "TextBrush" : "MutedBrush");

        var slide = new TranslateTransform(page == AdvancedPage ? 16 : -16, 0);
        page.RenderTransform = slide;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Ms(160)) { EasingFunction = ease });
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(160)));

        Keyboard.ClearFocus();
        RefreshView();
    }

    // --- Server -----------------------------------------------------------

    private async void ServerSwitch_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            if (_controller.IsRunning)
                await _controller.StopServerAsync();
            else
                await _controller.StartServerAsync();
        });
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_controller.PairingUrl);
        CopyLabel.Text = "Copied";
        _copyReset.Stop();
        _copyReset.Start();
    }

    private void ResetCopyButton()
    {
        _copyReset.Stop();
        CopyLabel.Text = "Copy pairing link";
    }

    private void CancelShutdown_Click(object sender, RoutedEventArgs e) => Run(_controller.CancelShutdown);

    // --- PIN --------------------------------------------------------------

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        var open = PinEditor.Visibility != Visibility.Visible;
        PinEditor.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        PinBox.Clear();
        RefreshView();
        if (open)
            PinBox.Focus();
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        PinPlaceholder.Visibility = PinBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SavePin();
        if (e.Key == Key.Escape) ClosePinEditor();
    }

    private void SavePin_Click(object sender, RoutedEventArgs e) => SavePin();

    private void SavePin()
    {
        if (!PinHasher.IsValidFormat(PinBox.Password))
        {
            ShowMessage($"The PIN must be {PinHasher.MinLength} to {PinHasher.MaxLength} digits.", MessageBoxImage.Warning);
            PinBox.Focus();
            return;
        }

        Run(() => _controller.SetPin(PinBox.Password));
        ClosePinEditor();
    }

    private void RemovePin_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm("Without a PIN, anyone on your network who opens the link can shut down this PC.\n\nRemove the PIN?"))
            return;

        Run(_controller.RemovePin);
        ClosePinEditor();
    }

    private void ClosePinEditor()
    {
        PinBox.Clear();
        PinEditor.Visibility = Visibility.Collapsed;
        RefreshView();
    }

    private void StartupSwitch_Click(object sender, RoutedEventArgs e) =>
        Run(() => StartupRegistration.SetEnabled(StartupSwitch.IsChecked == true));

    // --- Advanced ---------------------------------------------------------

    private void PortBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateApplyButton();

    private void PortBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyPort_Click(sender, e);
    }

    private void UpdateApplyButton()
    {
        if (ApplyPortButton is null) return; // TextChanged fires while the XAML is still loading
        ApplyPortButton.Visibility = PortBox.Text != _controller.Port.ToString()
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void ApplyPort_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1024 or > 65535)
        {
            ShowMessage("Pick a port between 1024 and 65535.", MessageBoxImage.Warning);
            return;
        }

        await RunAsync(() => _controller.ChangePortAsync(port));
        Keyboard.ClearFocus();
        RefreshView();
    }

    private void GraceOption_Click(object sender, RoutedEventArgs e) =>
        Run(() => _controller.SetGraceSeconds(TagValue(sender)));

    private void AttemptOption_Click(object sender, RoutedEventArgs e) =>
        Run(() => _controller.SetMaxPinAttempts(TagValue(sender)));

    private static int TagValue(object sender) => int.Parse((string)((FrameworkElement)sender).Tag);

    private void TailscaleSwitch_Click(object sender, RoutedEventArgs e) =>
        Run(() => _controller.SetPreferTailscale(TailscaleSwitch.IsChecked == true));

    private void NotifySwitch_Click(object sender, RoutedEventArgs e) =>
        Run(() => _controller.SetNotifyOnRequest(NotifySwitch.IsChecked == true));

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm("This unpairs all phones, removes your PIN and puts every setting back to its default.\n\nReset settings?"))
            return;

        await RunAsync(_controller.ResetSettingsAsync);
    }

    private void DigitsOnly(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    // --- Helpers ----------------------------------------------------------

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, MessageBoxImage.Error);
        }
        RefreshView();
    }

    private async Task RunAsync(Func<Task> action)
    {
        Cursor = Cursors.Wait;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, MessageBoxImage.Error);
        }
        finally
        {
            Cursor = null;
            RefreshView();
        }
    }

    private bool Confirm(string text) =>
        MessageBox.Show(this, text, Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    private void ShowMessage(string text, MessageBoxImage icon) =>
        MessageBox.Show(this, text, Title, MessageBoxButton.OK, icon);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    // Windows 11: dark title bar in the same color as the window, so it looks like one surface.
    private void UseDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var on = 1;
        DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref on, sizeof(int));

        var bg = (Color)FindResource("BgColor");
        var colorRef = bg.R | (bg.G << 8) | (bg.B << 16);
        DwmSetWindowAttribute(hwnd, DwmCaptionColor, ref colorRef, sizeof(int));
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
