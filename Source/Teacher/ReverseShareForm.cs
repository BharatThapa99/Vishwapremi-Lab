using System.Drawing.Imaging;

namespace Vishwapremi;

internal sealed class ReverseShareForm : Form
{
    private Device device;
    private readonly LabStore store;
    private readonly PictureBox pictureBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(16, 20, 18) };
    private readonly Label liveBadge = new() { Text = "● LIVE PROJECTION", ForeColor = Color.White, BackColor = Color.FromArgb(200, 38, 38), Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(8, 4, 8, 4), AutoSize = true, Location = new Point(14, 12) };
    private readonly Label freezeBadge = new() { Text = "❄️ FREEZE ACTIVE", ForeColor = Color.Black, BackColor = Color.FromArgb(100, 220, 240), Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(8, 4, 8, 4), AutoSize = true, Visible = false, Location = new Point(14, 12) };
    private readonly Label controlBadge = new() { Text = "🎮 CONTROL ACTIVE", ForeColor = Color.Black, BackColor = Color.FromArgb(130, 240, 130), Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(8, 4, 8, 4), AutoSize = true, Visible = false, Location = new Point(14, 12) };
    private readonly Label titleLabel = new() { ForeColor = Color.White, Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), AutoSize = true, Location = new Point(175, 10) };
    private readonly Label infoLabel = new() { ForeColor = Color.FromArgb(208, 225, 211), Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(175, 32) };
    private readonly Button freezeBtn;
    private readonly Button fullscreenBtn;
    private readonly Button remoteBtn;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 800 };
    private bool isFullscreen = false;
    private bool isFrozen = false;
    private bool isRemoteControl = false;
    private DateTime lastMoveSent = DateTime.MinValue;
    private Rectangle normalBounds;
    private FormBorderStyle normalBorderStyle;
    private FormWindowState normalWindowState;

    public ReverseShareForm(Device initialDevice, LabStore store, bool startRemoteControl = false)
    {
        this.device = initialDevice;
        this.store = store;
        Text = $"{device.Name} — Reverse Sharing & Remote Assistance";
        Size = new Size(1300, 820);
        MinimumSize = new Size(840, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 20, 18);
        Font = new Font("Segoe UI", 10);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Desktop.Green, Padding = new Padding(12, 8, 12, 8) };
        topBar.Controls.Add(liveBadge);
        topBar.Controls.Add(freezeBadge);
        topBar.Controls.Add(controlBadge);
        topBar.Controls.Add(titleLabel);
        topBar.Controls.Add(infoLabel);

        var tools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 840, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 8, 0), BackColor = Color.Transparent };

        var stopBtn = Desktop.Button("⏹ Stop", (_, _) => Close(), true);
        stopBtn.BackColor = Color.FromArgb(180, 40, 40);
        stopBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 50, 50);
        stopBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 45, 45);

        fullscreenBtn = Desktop.Button("🔲 Fullscreen (F11)", (_, _) => ToggleFullscreen());
        remoteBtn = Desktop.Button("🎮 Remote Control", (_, _) => ToggleRemoteControl());
        freezeBtn = Desktop.Button("⏸ Freeze", (_, _) => ToggleFreeze());

        var switchBtn = Desktop.Button("🔄 Switch PC ▾", null!);
        switchBtn.Click += (_, _) => ShowSwitchMenu(switchBtn);

        var saveBtn = Desktop.Button("📸 Save", (_, _) => SaveScreenshot());

        var lockBtn = Desktop.Button("🔒 Lock", (_, _) =>
        {
            if (MessageBox.Show($"Lock Windows session on {device.Name}?", "Lock Computer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                store.Queue([device.Id], "lock", "");
            }
        });

        var powerBtn = Desktop.Button("⚡ Power ▾", null!);
        var powerMenu = new ContextMenuStrip();
        powerMenu.Items.Add("🔄 Restart (5s)", null, (_, _) => { if (MessageBox.Show($"Restart {device.Name} in 5s?", "Restart", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "restart", "5"); });
        powerMenu.Items.Add("🛑 Shut Down (15s)", null, (_, _) => { if (MessageBox.Show($"Shut down {device.Name} with 15s warning?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "shutdown", "15"); });
        powerMenu.Items.Add("🛑 Shut Down (0s)", null, (_, _) => { if (MessageBox.Show($"Shut down {device.Name} immediately?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "shutdown", "0"); });
        powerMenu.Items.Add("👤 Log Off User", null, (_, _) => { if (MessageBox.Show($"Log off student session on {device.Name}?", "Log Off", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "logoff", ""); });
        powerMenu.Items.Add("💤 Sleep", null, (_, _) => { if (MessageBox.Show($"Put {device.Name} into sleep mode?", "Sleep", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "sleep", ""); });
        powerMenu.Items.Add("❌ Cancel Shutdown", null, (_, _) => { store.Queue([device.Id], "abort-shutdown", ""); MessageBox.Show($"Cancelled shutdown on {device.Name}.", "Abort Shutdown"); });
        powerBtn.Click += (_, _) => powerMenu.Show(powerBtn, new Point(0, powerBtn.Height));

        tools.Controls.Add(stopBtn);
        tools.Controls.Add(fullscreenBtn);
        tools.Controls.Add(remoteBtn);
        tools.Controls.Add(freezeBtn);
        tools.Controls.Add(switchBtn);
        tools.Controls.Add(saveBtn);
        tools.Controls.Add(lockBtn);
        tools.Controls.Add(powerBtn);
        topBar.Controls.Add(tools);

        pictureBox.DoubleClick += (_, _) =>
        {
            if (!isRemoteControl) ToggleFullscreen();
        };

        pictureBox.MouseMove += (_, e) =>
        {
            if (!isRemoteControl) return;
            var (nx, ny, inside) = GetNormalizedCoords(e.Location);
            if (!inside) return;
            if ((DateTime.UtcNow - lastMoveSent).TotalMilliseconds >= 35)
            {
                lastMoveSent = DateTime.UtcNow;
                InputStore.Enqueue(device.Id, new InputEvent { Type = "move", X = nx, Y = ny });
            }
        };

        pictureBox.MouseDown += (_, e) =>
        {
            if (!isRemoteControl) return;
            var (nx, ny, inside) = GetNormalizedCoords(e.Location);
            if (!inside) return;
            var btn = e.Button == MouseButtons.Right ? "right" : (e.Button == MouseButtons.Middle ? "middle" : "left");
            InputStore.Enqueue(device.Id, new InputEvent { Type = "down", Button = btn, X = nx, Y = ny });
        };

        pictureBox.MouseUp += (_, e) =>
        {
            if (!isRemoteControl) return;
            var (nx, ny, inside) = GetNormalizedCoords(e.Location);
            if (!inside) return;
            var btn = e.Button == MouseButtons.Right ? "right" : (e.Button == MouseButtons.Middle ? "middle" : "left");
            InputStore.Enqueue(device.Id, new InputEvent { Type = "up", Button = btn, X = nx, Y = ny });
        };

        pictureBox.MouseWheel += (_, e) =>
        {
            if (!isRemoteControl) return;
            var (nx, ny, inside) = GetNormalizedCoords(e.Location);
            if (!inside) return;
            InputStore.Enqueue(device.Id, new InputEvent { Type = "wheel", X = nx, Y = ny, Data = e.Delta });
        };

        pictureBox.Paint += (_, e) =>
        {
            if (pictureBox.Image == null)
            {
                using var font = new Font("Segoe UI", 14, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(160, 180, 170));
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString($"Connecting to {device.Name}…\nWaiting for high-resolution stream from student computer.", font, brush, pictureBox.ClientRectangle, sf);
            }
        };

        Controls.Add(pictureBox);
        Controls.Add(topBar);

        UpdateHeaderInfo();

        Shown += (_, _) =>
        {
            normalBounds = Bounds;
            normalBorderStyle = FormBorderStyle;
            normalWindowState = WindowState;
            ScreenStore.ReverseShareDeviceId = device.Id;
            if (startRemoteControl) ToggleRemoteControl();
            UpdateScreen();
            updateTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            updateTimer.Stop();
            if (ScreenStore.ReverseShareDeviceId == device.Id) ScreenStore.ReverseShareDeviceId = "";
            if (ScreenStore.RemoteControlDeviceId == device.Id) ScreenStore.RemoteControlDeviceId = "";
            InputStore.Clear(device.Id);
            pictureBox.Image?.Dispose();
        };

        updateTimer.Tick += (_, _) =>
        {
            if (!isFrozen) UpdateScreen();
        };
    }

    private void UpdateHeaderInfo()
    {
        titleLabel.Text = $"{device.Name}  •  {(string.IsNullOrEmpty(device.User) ? "Student Session" : device.User)}";
        if (isRemoteControl)
        {
            infoLabel.Text = $"🎮 REMOTE CONTROL ACTIVE  •  Click and type to control {device.Name}  •  Press Esc or click Control to exit";
        }
        else
        {
            infoLabel.Text = "High Definition (1080p · Quality 85%) · Double-click screen or press F11 for Fullscreen";
        }
    }

    private void ToggleRemoteControl()
    {
        isRemoteControl = !isRemoteControl;
        if (isRemoteControl)
        {
            if (isFrozen) ToggleFreeze();
            ScreenStore.RemoteControlDeviceId = device.Id;
            remoteBtn.Text = "🎮 Control: ON";
            remoteBtn.BackColor = Color.FromArgb(34, 139, 34);
            remoteBtn.ForeColor = Color.White;
            controlBadge.Visible = true;
            liveBadge.Visible = false;
            freezeBadge.Visible = false;
            pictureBox.Cursor = Cursors.Default;
        }
        else
        {
            if (ScreenStore.RemoteControlDeviceId == device.Id) ScreenStore.RemoteControlDeviceId = "";
            InputStore.Clear(device.Id);
            remoteBtn.Text = "🎮 Remote Control";
            remoteBtn.BackColor = Color.White;
            remoteBtn.ForeColor = Desktop.Ink;
            controlBadge.Visible = false;
            liveBadge.Visible = !isFrozen;
            freezeBadge.Visible = isFrozen;
            pictureBox.Cursor = Cursors.Default;
        }
        UpdateHeaderInfo();
    }

    private (double NormX, double NormY, bool Inside) GetNormalizedCoords(Point mousePt)
    {
        if (pictureBox.Image == null) return (0, 0, false);
        var rect = GetZoomedImageRect(pictureBox.ClientSize, pictureBox.Image.Size);
        if (!rect.Contains(mousePt) || rect.Width <= 0 || rect.Height <= 0) return (0, 0, false);
        double nx = Math.Clamp((double)(mousePt.X - rect.X) / rect.Width, 0.0, 1.0);
        double ny = Math.Clamp((double)(mousePt.Y - rect.Y) / rect.Height, 0.0, 1.0);
        return (nx, ny, true);
    }

    private static Rectangle GetZoomedImageRect(Size boxSize, Size imgSize)
    {
        if (boxSize.Width <= 0 || boxSize.Height <= 0 || imgSize.Width <= 0 || imgSize.Height <= 0)
            return Rectangle.Empty;

        float boxAspect = (float)boxSize.Width / boxSize.Height;
        float imgAspect = (float)imgSize.Width / imgSize.Height;

        if (boxAspect > imgAspect)
        {
            int drawH = boxSize.Height;
            int drawW = (int)(imgAspect * drawH);
            int drawX = (boxSize.Width - drawW) / 2;
            return new Rectangle(drawX, 0, drawW, drawH);
        }
        else
        {
            int drawW = boxSize.Width;
            int drawH = (int)(drawW / imgAspect);
            int drawY = (boxSize.Height - drawH) / 2;
            return new Rectangle(0, drawY, drawW, drawH);
        }
    }

    private void SwitchDevice(Device newDevice)
    {
        if (isRemoteControl && ScreenStore.RemoteControlDeviceId == device.Id)
        {
            ScreenStore.RemoteControlDeviceId = newDevice.Id;
        }
        InputStore.Clear(device.Id);
        device = newDevice;
        ScreenStore.ReverseShareDeviceId = device.Id;
        Text = $"{device.Name} — Reverse Sharing & Remote Assistance";
        UpdateHeaderInfo();
        var old = pictureBox.Image;
        pictureBox.Image = null;
        old?.Dispose();
        pictureBox.Invalidate();
        UpdateScreen();
    }

    private void ShowSwitchMenu(Control anchor)
    {
        var menu = new ContextMenuStrip();
        var snap = store.Snapshot();
        var onlineDevices = snap.Devices.Where(d => d.Online).OrderBy(d => d.Name).ToList();

        if (onlineDevices.Count == 0)
        {
            menu.Items.Add("No other online computers").Enabled = false;
        }
        else
        {
            foreach (var d in onlineDevices)
            {
                var itemText = d.Id == device.Id ? $"✓ {d.Name} ({d.User}) [Current]" : $"{d.Name} ({d.User})";
                var item = menu.Items.Add(itemText, null, (_, _) => SwitchDevice(d));
                if (d.Id == device.Id) item.Font = new Font(item.Font, FontStyle.Bold);
            }
        }
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private void ToggleFullscreen()
    {
        if (!isFullscreen)
        {
            normalBounds = Bounds;
            normalBorderStyle = FormBorderStyle;
            normalWindowState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            isFullscreen = true;
            fullscreenBtn.Text = "🗗 Windowed (F11)";
        }
        else
        {
            FormBorderStyle = normalBorderStyle;
            WindowState = normalWindowState;
            Bounds = normalBounds;
            isFullscreen = false;
            fullscreenBtn.Text = "🔲 Fullscreen (F11)";
        }
    }

    private void ToggleFreeze()
    {
        if (isRemoteControl) ToggleRemoteControl();
        isFrozen = !isFrozen;
        freezeBtn.Text = isFrozen ? "▶️ Resume" : "⏸ Freeze";
        freezeBtn.BackColor = isFrozen ? Color.FromArgb(100, 220, 240) : Color.White;
        freezeBtn.ForeColor = isFrozen ? Color.Black : Desktop.Ink;
        freezeBadge.Visible = isFrozen;
        liveBadge.Visible = !isFrozen;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            if (isRemoteControl)
            {
                ToggleRemoteControl();
                return true;
            }
            if (isFullscreen)
            {
                ToggleFullscreen();
                return true;
            }
            Close();
            return true;
        }
        if (!isRemoteControl && keyData == Keys.Space)
        {
            ToggleFreeze();
            return true;
        }
        if (isRemoteControl)
        {
            var key = keyData & Keys.KeyCode;
            if (key is Keys.Enter or Keys.Back or Keys.Tab or Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown)
            {
                InputStore.Enqueue(device.Id, new InputEvent { Type = "keydown", Data = (int)key });
                InputStore.Enqueue(device.Id, new InputEvent { Type = "keyup", Data = (int)key });
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (isRemoteControl && !char.IsControl(e.KeyChar))
        {
            InputStore.Enqueue(device.Id, new InputEvent { Type = "char", Text = e.KeyChar.ToString() });
            e.Handled = true;
        }
    }

    private void UpdateScreen()
    {
        var bytes = ScreenStore.Get(device.Id);
        if (bytes != null && bytes.Length > 0)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                var newImg = Image.FromStream(ms);
                var oldImg = pictureBox.Image;
                pictureBox.Image = newImg;
                oldImg?.Dispose();
                var time = ScreenStore.GetUpdated(device.Id)?.ToLocalTime().ToLongTimeString() ?? "";
                if (!isRemoteControl)
                {
                    infoLabel.Text = $"High Definition ({newImg.Width}×{newImg.Height} · Quality 85%)  •  Live update: {time}";
                }
            }
            catch { }
        }
    }

    private void SaveScreenshot()
    {
        if (pictureBox.Image == null)
        {
            MessageBox.Show("No screen image to save.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var save = new SaveFileDialog
        {
            Title = $"Save high-definition screenshot of {device.Name}",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
            FileName = $"{device.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (save.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var format = save.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png;
                pictureBox.Image.Save(save.FileName, format);
                MessageBox.Show($"Screenshot saved successfully:\n{save.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Desktop.Error(ex); }
        }
    }
}
