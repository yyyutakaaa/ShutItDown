using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShutdownServerApp
{
    public class MainForm : Form
    {
        private GradientPanel headerPanel;
        private Label headerLabel;
        private PictureBox logoPictureBox;
        private Label titleLabel;
        private PictureBox statusPictureBox;
        private Label statusLabel;
        private Button toggleButton;
        private Label linkLabel;
        private PictureBox copyPictureBox;
        private NotifyIcon trayIcon;
        private Button togglePinButton;
        private TextBox pinTextBox1;
        private TextBox pinTextBox2;
        private TextBox pinTextBox3;
        private TextBox pinTextBox4;
        private Button confirmPinButton;
        private WebServer webServer;
        private bool _serverRunning = false;
        private Image statusRunningIcon;
        private Image statusStoppedIcon;
        private ToolTip copyToolTip = new ToolTip();
        private Timer fadeInTimer;
        private Timer buttonAnimationTimer;
        private double fadeInStep = 0.05;
        private double buttonAnimProgress = 0.0;
        private const double buttonAnimStep = 0.1;
        private readonly Color buttonStartColor = Color.SlateBlue;
        private readonly Color buttonHighlightColor = Color.MediumSlateBlue;
        private bool _isToggling = false;
        private CheckBox startupCheckBox;
        private bool _suppressStartupPrompt = false;
        private Image defaultLogoIcon;
        private readonly Image defaultCopyIcon = SystemIcons.Information.ToBitmap();
        private string ShutdownUrl => $"http://{webServer.LocalIPAddress}:5050/shutdown";
        private string StartupShortcutPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "Shutdown Server.lnk"
            );
        private readonly string logoImagePath =
            Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");

        public MainForm()
        {
            var associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? (Icon)SystemIcons.Application.Clone();
            Icon = (Icon)associatedIcon.Clone();
            defaultLogoIcon = LoadHighResolutionLogo() ?? associatedIcon.ToBitmap();
            associatedIcon.Dispose();
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            Text = "Shutdown Server";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            webServer = new WebServer();
            statusRunningIcon = SystemIcons.Information.ToBitmap();
            statusStoppedIcon = SystemIcons.Error.ToBitmap();
            this.Opacity = 0;
            InitializeComponents();
            UpdateUI();
            this.Shown += MainForm_Shown;
        }

        private Image LoadHighResolutionLogo()
        {
            try
            {
                if (!File.Exists(logoImagePath)) return null;
                using var temp = Image.FromFile(logoImagePath);
                return new Bitmap(temp);
            }
            catch
            {
                return null;
            }
        }

        private void InitializeComponents()
        {
            headerPanel = new GradientPanel()
            {
                Dock = DockStyle.Top,
                Height = 80,
                ColorTop = Color.FromArgb(30, 30, 30),
                ColorBottom = Color.FromArgb(60, 60, 60),
            };
            headerLabel = new Label()
            {
                Text = "🚀 Shutdown Server",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
            };
            headerPanel.Controls.Add(headerLabel);
            Controls.Add(headerPanel);
            logoPictureBox = new PictureBox()
            {
                Size = new Size(64, 64),
                Location = new Point(20, 100),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
            };
            logoPictureBox.Image = defaultLogoIcon;
            Controls.Add(logoPictureBox);
            titleLabel = new Label()
            {
                Text = "Shutdown Server",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(100, 100),
                ForeColor = Color.White,
            };
            Controls.Add(titleLabel);
            statusPictureBox = new PictureBox()
            {
                Size = new Size(24, 24),
                Location = new Point(100, 140),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
            };
            Controls.Add(statusPictureBox);
            statusLabel = new Label()
            {
                Text = "Status: Server is stopped",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Location = new Point(statusPictureBox.Right + 5, 140),
                ForeColor = Color.White,
            };
            Controls.Add(statusLabel);
            toggleButton = new Button()
            {
                Text = "Start Server",
                Location = new Point(100, 180),
                Size = new Size(140, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = buttonStartColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
            };
            toggleButton.FlatAppearance.BorderSize = 0;
            toggleButton.Click += async (s, e) =>
            {
                StartButtonAnimation();
                await ToggleServerAsync();
            };
            Controls.Add(toggleButton);
            linkLabel = new Label()
            {
                Text = "N/A",
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(100, 230),
                ForeColor = Color.White,
            };
            Controls.Add(linkLabel);
            copyPictureBox = new PictureBox()
            {
                Size = new Size(24, 24),
                Location = new Point(linkLabel.Right + 5, linkLabel.Top),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Cursor = Cursors.Hand,
                Visible = false,
                BackColor = Color.Transparent,
            };
            copyPictureBox.Click += CopyLinkToClipboard;
            copyPictureBox.Image = defaultCopyIcon;
            Controls.Add(copyPictureBox);
            togglePinButton = new Button()
            {
                Text = "Set Pin",
                Location = new Point(100, 280),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Gray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
            };
            togglePinButton.Click += TogglePinButton_Click;
            Controls.Add(togglePinButton);
            pinTextBox1 = new TextBox()
            {
                Location = new Point(210, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox1.KeyPress += PinTextBox_KeyPress;
            pinTextBox1.TextChanged += PinTextBox_TextChanged;
            pinTextBox2 = new TextBox()
            {
                Location = new Point(250, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox2.KeyPress += PinTextBox_KeyPress;
            pinTextBox2.TextChanged += PinTextBox_TextChanged;
            pinTextBox3 = new TextBox()
            {
                Location = new Point(290, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox3.KeyPress += PinTextBox_KeyPress;
            pinTextBox3.TextChanged += PinTextBox_TextChanged;
            pinTextBox4 = new TextBox()
            {
                Location = new Point(330, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox4.KeyPress += PinTextBox_KeyPress;
            pinTextBox4.TextChanged += PinTextBox_TextChanged;
            Controls.Add(pinTextBox1);
            Controls.Add(pinTextBox2);
            Controls.Add(pinTextBox3);
            Controls.Add(pinTextBox4);
            confirmPinButton = new Button()
            {
                Text = "Confirm",
                Location = new Point(370, 280),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.DarkGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Visible = false,
            };
            confirmPinButton.Click += ConfirmPinButton_Click;
            Controls.Add(confirmPinButton);
            startupCheckBox = new CheckBox()
            {
                Text = "Launch Shutdown Server when Windows starts",
                Location = new Point(100, 330),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
            };
            startupCheckBox.CheckedChanged += StartupCheckBox_CheckedChanged;
            Controls.Add(startupCheckBox);
            trayIcon = new NotifyIcon()
            {
                Text = "Shutdown Server",
                Icon = this.Icon != null
                    ? (Icon)this.Icon.Clone()
                    : (Icon)SystemIcons.Application.Clone(),
                Visible = false,
            };
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (s, e) => ShowForm());
            trayMenu.Items.Add(
                "Exit",
                null,
                (s, e) =>
                {
                    trayIcon.Visible = false;
                    Application.Exit();
                }
            );
            trayIcon.ContextMenuStrip = trayMenu;
            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    trayIcon.Visible = true;
                }
            };
            SyncStartupCheckBox();
        }

        private void TogglePinButton_Click(object sender, EventArgs e)
        {
            bool visible = !pinTextBox1.Visible;
            pinTextBox1.Visible = visible;
            pinTextBox2.Visible = visible;
            pinTextBox3.Visible = visible;
            pinTextBox4.Visible = visible;
            confirmPinButton.Visible = visible;
        }

        private void ConfirmPinButton_Click(object sender, EventArgs e)
        {
            string newPin =
                pinTextBox1.Text + pinTextBox2.Text + pinTextBox3.Text + pinTextBox4.Text;
            if (newPin.Length == 4)
            {
                webServer.PinCode = newPin;
                pinTextBox1.Text = "";
                pinTextBox2.Text = "";
                pinTextBox3.Text = "";
                pinTextBox4.Text = "";
                pinTextBox1.Visible = false;
                pinTextBox2.Visible = false;
                pinTextBox3.Visible = false;
                pinTextBox4.Visible = false;
                confirmPinButton.Visible = false;
            }
            else
            {
                MessageBox.Show("Enter exactly 4 digits.");
                pinTextBox1.Focus();
            }
        }

        private void StartupCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressStartupPrompt) return;
            bool enableStartup = startupCheckBox.Checked;
            if (enableStartup)
            {
                HandleEnableStartupRequest();
            }
            else
            {
                HandleDisableStartupRequest();
            }
        }

        private void SyncStartupCheckBox()
        {
            SetStartupCheckboxSilently(StartupShortcutExists());
        }

        private void SetStartupCheckboxSilently(bool isChecked)
        {
            _suppressStartupPrompt = true;
            startupCheckBox.Checked = isChecked;
            _suppressStartupPrompt = false;
        }

        private void HandleEnableStartupRequest()
        {
            var result = MessageBox.Show(
                "Enabling this option will launch Shutdown Server automatically when Windows starts. Click OK to save this change or Cancel to keep the current behavior.",
                "Launch On Startup",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information
            );
            if (result != DialogResult.OK)
            {
                SetStartupCheckboxSilently(false);
                return;
            }

            try
            {
                CreateStartupShortcut();
                MessageBox.Show(
                    "A shortcut was stored inside the Windows startup folder (shell:startup). Shutdown Server will now launch together with Windows.",
                    "Startup Enabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not create the startup shortcut: " + ex.Message,
                    "Startup Change Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                SetStartupCheckboxSilently(false);
            }
        }

        private void HandleDisableStartupRequest()
        {
            var result = MessageBox.Show(
                "This will stop Shutdown Server from launching when Windows starts. Are you sure?",
                "Disable Startup Launch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );
            if (result != DialogResult.Yes)
            {
                SetStartupCheckboxSilently(true);
                return;
            }

            try
            {
                RemoveStartupShortcut();
                MessageBox.Show(
                    "The startup shortcut was removed. Shutdown Server will no longer start automatically with Windows.",
                    "Startup Disabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not remove the startup shortcut: " + ex.Message,
                    "Startup Change Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                SetStartupCheckboxSilently(true);
            }
        }

        private void CreateStartupShortcut()
        {
            var startupDirectory = Path.GetDirectoryName(StartupShortcutPath);
            if (!string.IsNullOrEmpty(startupDirectory))
            {
                Directory.CreateDirectory(startupDirectory);
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("WScript.Shell COM automation is not available.");
            }

            dynamic shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Failed to create WScript.Shell.");
            dynamic shortcut = shell.CreateShortcut(StartupShortcutPath);
            shortcut.Description = "Shutdown Server";
            shortcut.TargetPath = Application.ExecutablePath;
            var exeDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            shortcut.WorkingDirectory = !string.IsNullOrEmpty(exeDirectory)
                ? exeDirectory
                : Environment.CurrentDirectory;
            shortcut.Save();
        }

        private void RemoveStartupShortcut()
        {
            if (File.Exists(StartupShortcutPath))
            {
                File.Delete(StartupShortcutPath);
            }
        }

        private bool StartupShortcutExists() => File.Exists(StartupShortcutPath);

        private void MainForm_Shown(object sender, EventArgs e)
        {
            fadeInTimer = new Timer { Interval = 30 };
            fadeInTimer.Tick += (s, evt) =>
            {
                if (this.Opacity < 1.0)
                    this.Opacity += fadeInStep;
                else
                    fadeInTimer.Stop();
            };
            fadeInTimer.Start();
        }

        private void PinTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private void PinTextBox_TextChanged(object sender, EventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.Text.Length == 1)
            {
                MoveToNextPinBox(tb);
            }
        }

        private void MoveToNextPinBox(TextBox current)
        {
            if (current == pinTextBox1)
            {
                pinTextBox2.Focus();
            }
            else if (current == pinTextBox2)
            {
                pinTextBox3.Focus();
            }
            else if (current == pinTextBox3)
            {
                pinTextBox4.Focus();
            }
            else if (current == pinTextBox4)
            {
                confirmPinButton.Focus();
            }
        }

        private async Task ToggleServerAsync()
        {
            if (_isToggling) return;
            _isToggling = true;
            toggleButton.Enabled = false;
            try
            {
                if (!_serverRunning)
                {
                    try
                    {
                        await webServer.StartAsync();
                        _serverRunning = true;
                    }
                    catch (Exception ex)
                    {
                        _serverRunning = false;
                        MessageBox.Show(
                            "Kon de server niet starten: " + ex.Message,
                            "Start mislukt",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
                else
                {
                    await webServer.StopAsync();
                    _serverRunning = false;
                }
                UpdateUI();
            }
            finally
            {
                toggleButton.Enabled = true;
                _isToggling = false;
            }
        }

        private void UpdateUI()
        {
            if (_serverRunning)
            {
                statusLabel.Text = "Status: Server is running";
                statusPictureBox.Image = statusRunningIcon;
                toggleButton.Text = "Stop Server";
                linkLabel.Text = ShutdownUrl;
                linkLabel.Tag = $"LAN: http://{webServer.LocalIPAddress}:5050/shutdown";
                linkLabel.Refresh();
                copyPictureBox.Location = new Point(linkLabel.Right + 5, linkLabel.Top);
                copyPictureBox.Visible = true;
            }
            else
            {
                statusLabel.Text = "Status: Server is stopped";
                statusPictureBox.Image = statusStoppedIcon;
                toggleButton.Text = "Start Server";
                linkLabel.Text = "N/A";
                copyPictureBox.Visible = false;
            }
        }

        private void CopyLinkToClipboard(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(linkLabel.Text) && linkLabel.Text != "N/A")
            {
                Clipboard.SetText(linkLabel.Text);
                copyToolTip.Show("Copied!", copyPictureBox, 0, -20, 1500);
            }
        }

        private void StartButtonAnimation()
        {
            buttonAnimProgress = 0;
            if (buttonAnimationTimer == null)
            {
                buttonAnimationTimer = new Timer { Interval = 30 };
                buttonAnimationTimer.Tick += ButtonAnimationTimer_Tick;
            }
            buttonAnimationTimer.Start();
        }

        private void ButtonAnimationTimer_Tick(object sender, EventArgs e)
        {
            buttonAnimProgress += buttonAnimStep;
            if (buttonAnimProgress >= Math.PI)
            {
                buttonAnimationTimer.Stop();
                toggleButton.BackColor = buttonStartColor;
                return;
            }
            double factor = (1 - Math.Cos(buttonAnimProgress)) / 2;
            toggleButton.BackColor = InterpolateColor(
                buttonStartColor,
                buttonHighlightColor,
                factor
            );
        }

        private Color InterpolateColor(Color start, Color end, double factor)
        {
            int r = (int)(start.R + (end.R - start.R) * factor);
            int g = (int)(start.G + (end.G - start.G) * factor);
            int b = (int)(start.B + (end.B - start.B) * factor);
            return Color.FromArgb(r, g, b);
        }

        private void ShowForm()
        {
            Show();
            WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            trayIcon.Visible = false;
            fadeInTimer?.Stop();
            buttonAnimationTimer?.Stop();
            try
            {
                webServer.StopAsync().GetAwaiter().GetResult();
            }
            catch { }
            trayIcon?.Dispose();
            fadeInTimer?.Dispose();
            buttonAnimationTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
