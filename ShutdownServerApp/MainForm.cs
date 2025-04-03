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

        private WebServer webServer;
        private bool _serverRunning = false;

        // Status-icoon afbeeldingen
        private Image statusRunningIcon;
        private Image statusStoppedIcon;

        // ToolTip voor de copy-notificatie
        private ToolTip copyToolTip = new ToolTip();

        // Timers voor animaties
        private Timer fadeInTimer;
        private Timer buttonAnimationTimer;
        private double fadeInStep = 0.05;
        private double buttonAnimProgress = 0.0;
        private const double buttonAnimStep = 0.1; // snellere animatie

        // Kleuren voor de knopanimatie
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

            // Start met volledige transparantie en fade-in
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
            // Header panel met gradient achtergrond
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

            // Logo PictureBox; laadt de shutdown afbeelding online
            logoPictureBox = new PictureBox()
            {
                Size = new Size(64, 64),
                Location = new Point(20, 100),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
            };
            Controls.Add(logoPictureBox);

            // Titel label
            titleLabel = new Label()
            {
                Text = "Shutdown Server",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(100, 100),
                ForeColor = Color.White,
            };
            Controls.Add(titleLabel);

            // Status PictureBox voor het status-icoon
            statusPictureBox = new PictureBox()
            {
                Size = new Size(24, 24),
                Location = new Point(100, 140),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
            };
            Controls.Add(statusPictureBox);

            // Status label naast de status PictureBox
            statusLabel = new Label()
            {
                Text = "Status: Server is stopped",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Location = new Point(statusPictureBox.Right + 5, 140),
                ForeColor = Color.White,
            };
            Controls.Add(statusLabel);

            // Toggle-knop met initiële styling
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

            // Link label met de shutdown URL
            linkLabel = new Label()
            {
                Text = "N/A",
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(100, 230),
                ForeColor = Color.White,
            };
            Controls.Add(linkLabel);

            // Copy icoon PictureBox voor de link
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

            // NotifyIcon voor minimaliseren naar de tray
            trayIcon = new NotifyIcon()
            {
                Text = "Shutdown Server",
                Icon = SystemIcons.Application,
                Visible = false,
            };

            // Contextmenu voor het tray-icoon
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

            // Minimaliseren naar de tray
            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    trayIcon.Visible = true;
                }
            };
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
                // Laad icoon voor "running"
                var runningBytes = await client.GetByteArrayAsync(runningIconUrl);
                using (var ms = new System.IO.MemoryStream(runningBytes))
                {
                    statusRunningIcon = Image.FromStream(ms);
                }
                // Laad icoon voor "stopped"
                var stoppedBytes = await client.GetByteArrayAsync(stoppedIconUrl);
                using (var ms = new System.IO.MemoryStream(stoppedBytes))
                {
                    statusStoppedIcon = Image.FromStream(ms);
                }
            }
            catch
            {
                // Fallback naar systeemicoontjes bij fout
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
                // Toon een kleine popup boven het copy-icoon
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
