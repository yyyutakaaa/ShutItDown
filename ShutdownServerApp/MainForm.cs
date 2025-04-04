using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShutdownServerApp
{
    public class MainForm : Form
    {
        private Panel headerPanel;
        private Label headerLabel;
        private RoundedPanel cardPanel;
        private Label statusLabel;
        private Button toggleServerButton;
        private Label linkLabel;
        private PictureBox copyPictureBox;
        private Button setPinButton;
        private RoundedPanel pinCardPanel;
        private TextBox pinTextBox1;
        private TextBox pinTextBox2;
        private TextBox pinTextBox3;
        private TextBox pinTextBox4;
        private Button confirmPinButton;
        private NotifyIcon trayIcon;
        private Timer fadeInTimer;
        private WebServer webServer;
        private bool serverRunning = false;
        private Color lightFormBack = Color.White;
        private Color lightText = Color.Black;
        private Color lightCardBack = Color.FromArgb(240, 240, 240);
        private Color darkFormBack = Color.FromArgb(45, 45, 48);
        private Color darkText = Color.White;
        private Color darkCardBack = Color.FromArgb(30, 30, 30);

        public MainForm()
        {
            webServer = new WebServer();
            this.Text = "Shutdown Server";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            ApplyTheme();
            InitializeComponents();
            this.Shown += MainForm_Shown;
            this.Load += async (s, e) =>
            {
                await LoadCopyIconAsync();
            };
        }

        private void ApplyTheme()
        {
            bool light = IsLightTheme();
            this.BackColor = light ? lightFormBack : darkFormBack;
            headerPanel.BackColor = light ? Color.LightSkyBlue : Color.MediumSlateBlue;
            headerLabel.ForeColor = light ? lightText : darkText;
        }

        private bool IsLightTheme()
        {
            try
            {
                using (
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
                    )
                )
                {
                    if (key != null)
                    {
                        object theme = key.GetValue("AppsUseLightTheme");
                        if (theme != null)
                        {
                            return ((int)theme) == 1;
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        private void InitializeComponents()
        {
            headerPanel = new Panel();
            headerPanel.Size = new Size(this.Width, 80);
            headerPanel.Location = new Point(0, 0);
            headerPanel.BackColor = IsLightTheme() ? Color.LightSkyBlue : Color.MediumSlateBlue;
            headerLabel = new Label();
            headerLabel.Text = "🚀 Shutdown Server";
            headerLabel.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            headerLabel.AutoSize = false;
            headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            headerLabel.Dock = DockStyle.Fill;
            headerLabel.ForeColor = IsLightTheme() ? lightText : darkText;
            headerPanel.Controls.Add(headerLabel);
            this.Controls.Add(headerPanel);
            cardPanel = new RoundedPanel();
            cardPanel.Size = new Size(500, 200);
            cardPanel.Location = new Point(
                (this.ClientSize.Width - cardPanel.Width) / 2,
                headerPanel.Bottom + 20
            );
            cardPanel.BackColor = IsLightTheme() ? lightCardBack : darkCardBack;
            statusLabel = new Label();
            statusLabel.Text = "Status: Server is stopped";
            statusLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(20, 20);
            statusLabel.ForeColor = IsLightTheme() ? lightText : darkText;
            cardPanel.Controls.Add(statusLabel);
            toggleServerButton = new Button();
            toggleServerButton.Text = "Start Server";
            toggleServerButton.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            toggleServerButton.Size = new Size(140, 40);
            toggleServerButton.Location = new Point(20, statusLabel.Bottom + 20);
            toggleServerButton.FlatStyle = FlatStyle.Flat;
            toggleServerButton.BackColor = Color.SlateBlue;
            toggleServerButton.ForeColor = Color.White;
            toggleServerButton.Click += async (s, e) =>
            {
                await ToggleServerAsync();
            };
            cardPanel.Controls.Add(toggleServerButton);
            linkLabel = new Label();
            linkLabel.Text = "N/A";
            linkLabel.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            linkLabel.AutoSize = true;
            linkLabel.Location = new Point(20, toggleServerButton.Bottom + 20);
            linkLabel.ForeColor = IsLightTheme() ? lightText : darkText;
            cardPanel.Controls.Add(linkLabel);
            copyPictureBox = new PictureBox();
            copyPictureBox.Size = new Size(24, 24);
            copyPictureBox.Location = new Point(linkLabel.Right + 10, linkLabel.Top - 3);
            copyPictureBox.Cursor = Cursors.Hand;
            copyPictureBox.Click += CopyLinkToClipboard;
            cardPanel.Controls.Add(copyPictureBox);
            this.Controls.Add(cardPanel);
            setPinButton = new Button();
            setPinButton.Text = "Set Pin";
            setPinButton.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            setPinButton.Size = new Size(100, 30);
            setPinButton.Location = new Point(
                (this.ClientSize.Width - setPinButton.Width) / 2,
                cardPanel.Bottom + 20
            );
            setPinButton.FlatStyle = FlatStyle.Flat;
            setPinButton.BackColor = Color.Gray;
            setPinButton.ForeColor = Color.White;
            setPinButton.Click += SetPinButton_Click;
            this.Controls.Add(setPinButton);
            pinCardPanel = new RoundedPanel();
            pinCardPanel.Size = new Size(300, 80);
            pinCardPanel.Location = new Point(
                (this.ClientSize.Width - pinCardPanel.Width) / 2,
                setPinButton.Bottom + 10
            );
            pinCardPanel.BackColor = IsLightTheme() ? lightCardBack : darkCardBack;
            pinCardPanel.Visible = false;
            pinTextBox1 = new TextBox();
            pinTextBox1.Size = new Size(40, 40);
            pinTextBox1.Location = new Point(20, 20);
            pinTextBox1.MaxLength = 1;
            pinTextBox1.TextAlign = HorizontalAlignment.Center;
            pinTextBox1.PasswordChar = '*';
            pinTextBox2 = new TextBox();
            pinTextBox2.Size = new Size(40, 40);
            pinTextBox2.Location = new Point(pinTextBox1.Right + 10, 20);
            pinTextBox2.MaxLength = 1;
            pinTextBox2.TextAlign = HorizontalAlignment.Center;
            pinTextBox2.PasswordChar = '*';
            pinTextBox3 = new TextBox();
            pinTextBox3.Size = new Size(40, 40);
            pinTextBox3.Location = new Point(pinTextBox2.Right + 10, 20);
            pinTextBox3.MaxLength = 1;
            pinTextBox3.TextAlign = HorizontalAlignment.Center;
            pinTextBox3.PasswordChar = '*';
            pinTextBox4 = new TextBox();
            pinTextBox4.Size = new Size(40, 40);
            pinTextBox4.Location = new Point(pinTextBox3.Right + 10, 20);
            pinTextBox4.MaxLength = 1;
            pinTextBox4.TextAlign = HorizontalAlignment.Center;
            pinTextBox4.PasswordChar = '*';
            confirmPinButton = new Button();
            confirmPinButton.Text = "Confirm";
            confirmPinButton.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            confirmPinButton.Size = new Size(80, 30);
            confirmPinButton.Location = new Point(pinTextBox4.Right + 10, 25);
            confirmPinButton.FlatStyle = FlatStyle.Flat;
            confirmPinButton.BackColor = Color.DarkGreen;
            confirmPinButton.ForeColor = Color.White;
            confirmPinButton.Click += ConfirmPinButton_Click;
            pinCardPanel.Controls.Add(pinTextBox1);
            pinCardPanel.Controls.Add(pinTextBox2);
            pinCardPanel.Controls.Add(pinTextBox3);
            pinCardPanel.Controls.Add(pinTextBox4);
            pinCardPanel.Controls.Add(confirmPinButton);
            this.Controls.Add(pinCardPanel);
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Shutdown Server";
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Visible = false;
            ContextMenuStrip trayMenu = new ContextMenuStrip();
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
            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    trayIcon.Visible = true;
                }
            };
        }

        private async Task ToggleServerAsync()
        {
            if (!serverRunning)
            {
                await webServer.StartAsync();
                serverRunning = true;
                UpdateUI();
            }
            else
            {
                await webServer.StopAsync();
                serverRunning = false;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (serverRunning)
            {
                statusLabel.Text = "Status: Server is running";
                toggleServerButton.Text = "Stop Server";
                linkLabel.Text = $"http://{webServer.LocalIPAddress}:5050/shutdown";
            }
            else
            {
                statusLabel.Text = "Status: Server is stopped";
                toggleServerButton.Text = "Start Server";
                linkLabel.Text = "N/A";
            }
        }

        private void SetPinButton_Click(object sender, EventArgs e)
        {
            pinCardPanel.Visible = !pinCardPanel.Visible;
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
                pinCardPanel.Visible = false;
            }
            else
            {
                MessageBox.Show("Enter exactly 4 digits.");
            }
        }

        private void CopyLinkToClipboard(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(linkLabel.Text) && linkLabel.Text != "N/A")
            {
                Clipboard.SetText(linkLabel.Text);
            }
        }

        private async Task LoadCopyIconAsync()
        {
            string copyIconUrl = "https://img.icons8.com/material-rounded/24/000000/copy.png";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    byte[] bytes = await client.GetByteArrayAsync(copyIconUrl);
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        copyPictureBox.Image = Image.FromStream(ms);
                    }
                }
            }
            catch
            {
                copyPictureBox.Image = SystemIcons.Application.ToBitmap();
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            fadeInTimer = new Timer();
            fadeInTimer.Interval = 30;
            fadeInTimer.Tick += (s, evt) =>
            {
                if (this.Opacity < 1.0)
                    this.Opacity += 0.05;
                else
                    fadeInTimer.Stop();
            };
            fadeInTimer.Start();
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
        }
    }
}
