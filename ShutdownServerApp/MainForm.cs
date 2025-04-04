using System;
using System.Drawing;
using System.Net.Http;
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

        public MainForm()
        {
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            Text = "Shutdown Server";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            webServer = new WebServer();
            this.Opacity = 0;
            InitializeComponents();
            this.Shown += MainForm_Shown;
            this.Load += async (s, e) =>
            {
                await LoadLogoImageAsync();
                await LoadCopyIconAsync();
                await LoadStatusIconsAsync();
            };
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
            pinTextBox2 = new TextBox()
            {
                Location = new Point(250, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox3 = new TextBox()
            {
                Location = new Point(290, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
            pinTextBox4 = new TextBox()
            {
                Location = new Point(330, 280),
                Size = new Size(30, 30),
                MaxLength = 1,
                PasswordChar = '*',
                Visible = false,
            };
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
            trayIcon = new NotifyIcon()
            {
                Text = "Shutdown Server",
                Icon = SystemIcons.Application,
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
                MessageBox.Show("Voer exact 4 cijfers in.");
            }
        }

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

        private async Task LoadLogoImageAsync()
        {
            string imageUrl = "https://img.icons8.com/fluency/64/000000/shutdown.png";
            try
            {
                using HttpClient client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(imageUrl);
                using var ms = new System.IO.MemoryStream(bytes);
                logoPictureBox.Image = Image.FromStream(ms);
            }
            catch
            {
                logoPictureBox.Image = SystemIcons.Application.ToBitmap();
            }
        }

        private async Task LoadCopyIconAsync()
        {
            string copyIconUrl = "https://img.icons8.com/material-rounded/24/000000/copy.png";
            try
            {
                using HttpClient client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(copyIconUrl);
                using var ms = new System.IO.MemoryStream(bytes);
                copyPictureBox.Image = Image.FromStream(ms);
            }
            catch
            {
                copyPictureBox.Image = SystemIcons.Application.ToBitmap();
            }
        }

        private async Task LoadStatusIconsAsync()
        {
            string runningIconUrl = "https://img.icons8.com/color/48/000000/ok.png";
            string stoppedIconUrl = "https://img.icons8.com/color/48/000000/cancel.png";
            try
            {
                using HttpClient client = new HttpClient();
                var runningBytes = await client.GetByteArrayAsync(runningIconUrl);
                using (var ms = new System.IO.MemoryStream(runningBytes))
                {
                    statusRunningIcon = Image.FromStream(ms);
                }
                var stoppedBytes = await client.GetByteArrayAsync(stoppedIconUrl);
                using (var ms = new System.IO.MemoryStream(stoppedBytes))
                {
                    statusStoppedIcon = Image.FromStream(ms);
                }
            }
            catch
            {
                statusRunningIcon = SystemIcons.Application.ToBitmap();
                statusStoppedIcon = SystemIcons.Error.ToBitmap();
            }
        }

        private async Task ToggleServerAsync()
        {
            if (!_serverRunning)
            {
                await webServer.StartAsync();
                _serverRunning = true;
                UpdateUI();
            }
            else
            {
                await webServer.StopAsync();
                _serverRunning = false;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (_serverRunning)
            {
                statusLabel.Text = "Status: Server is running";
                statusPictureBox.Image = statusRunningIcon;
                toggleButton.Text = "Stop Server";
                linkLabel.Text = $"http://{webServer.LocalIPAddress}:5050/shutdown";
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
    }
}
