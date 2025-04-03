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
        private Label statusLabel;
        private Button toggleButton;
        private Label linkLabel;
        private NotifyIcon trayIcon;

        private WebServer webServer;
        private bool _serverRunning = false;

        // Timers voor animaties
        private Timer fadeInTimer;
        private Timer buttonAnimationTimer;
        private double fadeInStep = 0.05;
        private double buttonAnimProgress = 0.0;
        private const double buttonAnimStep = 0.1; // sneller dan fade in

        // Kleuren voor de knopanimatie
        private readonly Color buttonStartColor = Color.SlateBlue;
        private readonly Color buttonHighlightColor = Color.MediumSlateBlue;

        public MainForm()
        {
            // Donkere achtergrond en witte tekst
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            Text = "Shutdown Server - Ultimate Edition";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            webServer = new WebServer();

            // Start met volledig transparant en fade in
            this.Opacity = 0;
            InitializeComponents();
            this.Shown += MainForm_Shown;
            this.Load += async (s, e) => await LoadLogoImageAsync();
        }

        private void InitializeComponents()
        {
            // Header panel met gradient achtergrond
            headerPanel = new GradientPanel()
            {
                Dock = DockStyle.Top,
                Height = 80,
                // Optioneel: overschrijf de standaard gradientkleuren
                ColorTop = Color.FromArgb(30, 30, 30),
                ColorBottom = Color.FromArgb(60, 60, 60),
            };

            headerLabel = new Label()
            {
                Text = "🚀 Ultimate Shutdown Server",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
            };
            headerPanel.Controls.Add(headerLabel);
            Controls.Add(headerPanel);

            // Logo PictureBox; we laden de afbeelding online later in LoadLogoImageAsync()
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

            // Status label
            statusLabel = new Label()
            {
                Text = "Status: 🔴 Server is stopped",
                Font = new Font("Segoe UI", 12),
                AutoSize = true,
                Location = new Point(100, 140),
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
                // Start de knopanimatie
                StartButtonAnimation();
                await ToggleServerAsync();
            };
            Controls.Add(toggleButton);

            // Link label
            linkLabel = new Label()
            {
                Text = "N/A",
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(100, 230),
                ForeColor = Color.White,
            };
            Controls.Add(linkLabel);

            // NotifyIcon voor minimaliseren naar tray
            trayIcon = new NotifyIcon()
            {
                Text = "Ultimate Shutdown Server",
                Icon = SystemIcons.Application,
                Visible = false,
            };

            // Contextmenu voor tray icoon
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

            // Minimaliseer naar tray
            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    trayIcon.Visible = true;
                }
            };
        }

        // Fade-in animatie bij form load
        private void MainForm_Shown(object sender, EventArgs e)
        {
            fadeInTimer = new Timer();
            fadeInTimer.Interval = 30; // 30 ms
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
            // Haal een shutdown icoon op van Icons8 (PNG)
            string imageUrl = "https://img.icons8.com/fluency/64/000000/shutdown.png";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(imageUrl);
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        var image = Image.FromStream(ms);
                        logoPictureBox.Image = image;
                    }
                }
            }
            catch
            {
                // Fallback naar een standaard afbeelding als er iets misgaat
                logoPictureBox.Image = SystemIcons.Application.ToBitmap();
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
                statusLabel.Text = "Status: 🟢 Server is running";
                toggleButton.Text = "Stop Server";
                linkLabel.Text = $"http://{webServer.LocalIPAddress}:5050/shutdown";
            }
            else
            {
                statusLabel.Text = "Status: 🔴 Server is stopped";
                toggleButton.Text = "Start Server";
                linkLabel.Text = "N/A";
            }
        }

        // Eenvoudige knopanimatie: pulserende kleur
        private void StartButtonAnimation()
        {
            buttonAnimProgress = 0;
            if (buttonAnimationTimer == null)
            {
                buttonAnimationTimer = new Timer();
                buttonAnimationTimer.Interval = 30;
                buttonAnimationTimer.Tick += ButtonAnimationTimer_Tick;
            }
            buttonAnimationTimer.Start();
        }

        private void ButtonAnimationTimer_Tick(object sender, EventArgs e)
        {
            // Gebruik een sinusfunctie om een soepele animatie te maken (0 tot π)
            buttonAnimProgress += buttonAnimStep;
            if (buttonAnimProgress >= Math.PI)
            {
                buttonAnimationTimer.Stop();
                toggleButton.BackColor = buttonStartColor;
                return;
            }

            double factor = (1 - Math.Cos(buttonAnimProgress)) / 2; // 0->1->0 verloop
            toggleButton.BackColor = InterpolateColor(
                buttonStartColor,
                buttonHighlightColor,
                factor
            );
        }

        // Hulpfunctie voor kleurinterpolatie
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
