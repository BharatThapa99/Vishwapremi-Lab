using System.Drawing.Imaging;

namespace Vishwapremi;

internal sealed class ScreenMonitorForm : Form
{
    private readonly LabStore store;
    private readonly FlowLayoutPanel cardsPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(16), BackColor = Desktop.Paper };
    private readonly Label statusLabel = Desktop.Label("Streaming active · Updates every 2–3s", 10.5f);
    private readonly TextBox searchBox = new() { Width = 180, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Search PC name…" };
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 2000 };
    private readonly Dictionary<string, (PictureBox Picture, Label Status, Label Info, Panel Card)> cardMap = new();
    private bool paused = false;

    private static Button MiniButton(string text, EventHandler? onClick, int width)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Desktop.Ink,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Desktop.Border;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 243, 234);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(218, 232, 215);
        if (onClick != null) b.Click += onClick;
        return b;
    }

    public ScreenMonitorForm(LabStore store)
    {
        this.store = store;
        Text = "Vishwapremi · Classroom Live Screen Monitor";
        Size = new Size(1180, 800);
        MinimumSize = new Size(840, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Desktop.Paper;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Desktop.Green, Padding = new Padding(18, 10, 18, 10) };
        var title = new Label { Text = "CLASSROOM LIVE SCREEN MONITOR", ForeColor = Color.White, Font = new Font("Segoe UI", 13.5f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 8) };
        statusLabel.ForeColor = Color.FromArgb(208, 225, 211);
        statusLabel.Location = new Point(20, 36);
        header.Controls.Add(title);
        header.Controls.Add(statusLabel);

        var topTools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 480, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 10, 0), BackColor = Color.Transparent };
        var pauseBtn = Desktop.Button("Pause", null!);
        pauseBtn.Click += (_, _) =>
        {
            paused = !paused;
            pauseBtn.Text = paused ? "Resume" : "Pause";
            statusLabel.Text = paused ? "Paused" : "Streaming active · Updates every 2–3s";
        };
        var refreshBtn = Desktop.Button("Refresh Now", (_, _) => RefreshScreens());
        searchBox.TextChanged += (_, _) => FilterCards();

        topTools.Controls.Add(refreshBtn);
        topTools.Controls.Add(pauseBtn);
        topTools.Controls.Add(searchBox);
        header.Controls.Add(topTools);

        Controls.Add(cardsPanel);
        Controls.Add(header);

        Shown += (_, _) =>
        {
            ScreenStore.IsMonitoringActive = true;
            BuildCards();
            refreshTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            refreshTimer.Stop();
            ScreenStore.IsMonitoringActive = false;
            ScreenStore.FullViewDeviceId = "";
            foreach (var card in cardMap.Values) card.Picture.Image?.Dispose();
            cardMap.Clear();
        };

        refreshTimer.Tick += (_, _) =>
        {
            if (!paused) RefreshScreens();
        };
    }

    private void FilterCards()
    {
        var term = searchBox.Text.Trim().ToLowerInvariant();
        foreach (var kvp in cardMap)
        {
            var match = term.Length == 0 || kvp.Value.Info.Text.ToLowerInvariant().Contains(term) || kvp.Value.Status.Text.ToLowerInvariant().Contains(term);
            kvp.Value.Card.Visible = match;
        }
    }

    private void BuildCards()
    {
        cardsPanel.Controls.Clear();
        foreach (var card in cardMap.Values) card.Picture.Image?.Dispose();
        cardMap.Clear();

        var snapshot = store.Snapshot();
        foreach (var d in snapshot.Devices.OrderBy(x => x.Name))
        {
            var card = new Panel { Size = new Size(350, 270), BackColor = Color.White, Margin = new Padding(10), BorderStyle = BorderStyle.FixedSingle };

            var cardHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = d.Online ? Desktop.Green : Color.FromArgb(120, 130, 124), Padding = new Padding(10, 6, 10, 4) };
            var cardTitle = new Label { Text = d.Name, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Left };
            var cardUser = new Label { Text = string.IsNullOrEmpty(d.User) ? (d.Online ? "Online" : "Offline") : d.User, ForeColor = Color.FromArgb(228, 240, 230), Font = new Font("Segoe UI", 9), AutoSize = true, Dock = DockStyle.Right };
            cardHeader.Controls.Add(cardTitle);
            cardHeader.Controls.Add(cardUser);

            var pic = new PictureBox
            {
                Size = new Size(332, 188),
                Location = new Point(8, 38),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(236, 240, 234),
                Cursor = Cursors.Hand
            };
            pic.DoubleClick += (_, _) => OpenFullView(d);

            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(246, 248, 245), Padding = new Padding(6, 4, 6, 4) };
            var viewBtn = MiniButton("View", (_, _) => OpenFullView(d), 56);
            viewBtn.Location = new Point(4, 5);

            var lockBtn = MiniButton("Lock", (_, _) =>
            {
                if (MessageBox.Show($"Lock Windows session on {d.Name}?", "Lock Computer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    store.Queue([d.Id], "lock", "");
                }
            }, 56);
            lockBtn.Location = new Point(64, 5);

            var msgBtn = MiniButton("Notice", (_, _) =>
            {
                using var f = Desktop.Dialog($"Send notice to {d.Name}", 320);
                var p = Desktop.Stack(); f.Controls.Add(p);
                p.Controls.Add(Desktop.Label($"Message for {d.Name}", 14, true));
                var input = new TextBox { Width = 430, Multiline = true, Height = 90, MaxLength = 500 };
                p.Controls.Add(input);
                p.Controls.Add(Desktop.Button("Send Notice", (_, _) =>
                {
                    try { Files.ValidateAction("message", input.Text.Trim()); f.DialogResult = DialogResult.OK; }
                    catch (Exception ex) { Desktop.Error(ex); }
                }, true));
                if (f.ShowDialog(this) == DialogResult.OK) store.Queue([d.Id], "message", input.Text.Trim());
            }, 64);
            msgBtn.Location = new Point(124, 5);

            var unblockBtn = MiniButton("Unblock", (_, _) =>
            {
                store.Queue([d.Id], "unblock-internet", "");
                MessageBox.Show($"Unblock Internet command sent to {d.Name}.", "Unblock Internet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, 68);
            unblockBtn.Location = new Point(192, 5);

            var powerBtn = MiniButton("Power", null, 56);
            powerBtn.Location = new Point(264, 5);
            var pMenu = new ContextMenuStrip();
            pMenu.Items.Add("Restart (5s)", null, (_, _) => { if (MessageBox.Show($"Restart {d.Name} in 5s?", "Restart", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([d.Id], "restart", "5"); });
            pMenu.Items.Add("Shut Down (15s)", null, (_, _) => { if (MessageBox.Show($"Shut down {d.Name} with 15s warning?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([d.Id], "shutdown", "15"); });
            pMenu.Items.Add("Shut Down (0s)", null, (_, _) => { if (MessageBox.Show($"Shut down {d.Name} immediately?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([d.Id], "shutdown", "0"); });
            pMenu.Items.Add("Log Off User", null, (_, _) => { if (MessageBox.Show($"Log off student session on {d.Name}?", "Log Off", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([d.Id], "logoff", ""); });
            pMenu.Items.Add("Sleep", null, (_, _) => { if (MessageBox.Show($"Put {d.Name} into sleep mode?", "Sleep", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([d.Id], "sleep", ""); });
            if (!string.IsNullOrEmpty(d.Mac)) pMenu.Items.Add("Wake-on-LAN", null, (_, _) => { NetworkSetup.SendWakeOnLan(d.Mac); MessageBox.Show($"Sent Wake-on-LAN packet to {d.Name}.", "Wake-on-LAN"); });
            pMenu.Items.Add("Cancel Shutdown", null, (_, _) => { store.Queue([d.Id], "abort-shutdown", ""); MessageBox.Show($"Cancelled shutdown on {d.Name}.", "Abort Shutdown"); });
            powerBtn.Click += (_, _) => pMenu.Show(powerBtn, new Point(0, powerBtn.Height));

            bottomBar.Controls.Add(viewBtn);
            bottomBar.Controls.Add(lockBtn);
            bottomBar.Controls.Add(msgBtn);
            bottomBar.Controls.Add(unblockBtn);
            bottomBar.Controls.Add(powerBtn);

            card.Controls.Add(pic);
            card.Controls.Add(cardHeader);
            card.Controls.Add(bottomBar);

            cardsPanel.Controls.Add(card);
            cardMap[d.Id] = (pic, cardUser, cardTitle, card);
        }
        FilterCards();
        RefreshScreens();
    }

    private void RefreshScreens()
    {
        var snapshot = store.Snapshot();
        int onlineCount = snapshot.Devices.Count(d => d.Online);
        if (!paused) statusLabel.Text = $"{onlineCount} of {snapshot.Devices.Count} online · Streaming active · Double-click any screen for Full View";

        foreach (var d in snapshot.Devices)
        {
            if (!cardMap.TryGetValue(d.Id, out var entry)) continue;
            entry.Info.Text = d.Name;
            entry.Status.Text = string.IsNullOrEmpty(d.User) ? (d.Online ? "Online" : "Offline") : d.User;
            var header = entry.Card.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Top);
            if (header != null) header.BackColor = d.Online ? Desktop.Green : Color.Gray;

            var screenBytes = ScreenStore.Get(d.Id);
            if (screenBytes != null && screenBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(screenBytes);
                    var newImg = Image.FromStream(ms);
                    var oldImg = entry.Picture.Image;
                    entry.Picture.Image = newImg;
                    oldImg?.Dispose();
                }
                catch { }
            }
            else if (!d.Online)
            {
                var oldImg = entry.Picture.Image;
                entry.Picture.Image = null;
                oldImg?.Dispose();
            }
        }
    }

    private void OpenFullView(Device device)
    {
        using var fullView = new FullScreenViewerForm(device, store);
        fullView.ShowDialog(this);
        ScreenStore.FullViewDeviceId = "";
        RefreshScreens();
    }
}

internal sealed class FullScreenViewerForm : Form
{
    private readonly Device device;
    private readonly LabStore store;
    private readonly PictureBox pictureBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly Label infoLabel = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(14, 12) };
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 1500 };

    public FullScreenViewerForm(Device device, LabStore store)
    {
        this.device = device;
        this.store = store;
        Text = $"{device.Name} — Live High-Resolution Screen View";
        Size = new Size(1280, 800);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(20, 20, 20);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Desktop.Green, Padding = new Padding(14, 10, 14, 10) };
        infoLabel.Text = $"LIVE: {device.Name}  ({(string.IsNullOrEmpty(device.User) ? "Student" : device.User)})";
        infoLabel.Location = new Point(16, 16);
        topBar.Controls.Add(infoLabel);

        var tools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 880, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 9, 8, 0), BackColor = Color.Transparent };
        var closeBtn = Desktop.Button("Back to Grid", (_, _) => Close(), true);
        var reverseBtn = Desktop.Button("Reverse Share", (_, _) =>
        {
            using var f = new ReverseShareForm(device, store);
            f.ShowDialog(this);
        }, true);
        var controlBtn = Desktop.Button("Remote Control", (_, _) =>
        {
            using var f = new ReverseShareForm(device, store, startRemoteControl: true);
            f.ShowDialog(this);
        }, true);
        var saveBtn = Desktop.Button("Save Screenshot", (_, _) => SaveScreenshot());
        var powerBtn = Desktop.Button("Power", null!);
        var fsPowerMenu = new ContextMenuStrip();
        fsPowerMenu.Items.Add("Restart (5s)", null, (_, _) => { if (MessageBox.Show($"Restart {device.Name} in 5s?", "Restart", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "restart", "5"); });
        fsPowerMenu.Items.Add("Shut Down (15s)", null, (_, _) => { if (MessageBox.Show($"Shut down {device.Name} with 15s countdown?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "shutdown", "15"); });
        fsPowerMenu.Items.Add("Shut Down Immediately (0s)", null, (_, _) => { if (MessageBox.Show($"Shut down {device.Name} immediately?", "Shut Down", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "shutdown", "0"); });
        fsPowerMenu.Items.Add("Log Off User", null, (_, _) => { if (MessageBox.Show($"Log off student session on {device.Name}?", "Log Off", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "logoff", ""); });
        fsPowerMenu.Items.Add("Sleep", null, (_, _) => { if (MessageBox.Show($"Put {device.Name} into sleep mode?", "Sleep", MessageBoxButtons.OKCancel) == DialogResult.OK) store.Queue([device.Id], "sleep", ""); });
        fsPowerMenu.Items.Add("Cancel Shutdown", null, (_, _) => { store.Queue([device.Id], "abort-shutdown", ""); MessageBox.Show($"Cancelled shutdown on {device.Name}.", "Abort Shutdown"); });
        powerBtn.Click += (_, _) => fsPowerMenu.Show(powerBtn, new Point(0, powerBtn.Height));
        var unblockBtn = Desktop.Button("Unblock Net", (_, _) =>
        {
            store.Queue([device.Id], "unblock-internet", "");
            MessageBox.Show($"Unblocked internet on {device.Name}.", "Unblock Internet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        var blockBtn = Desktop.Button("Block Net", (_, _) =>
        {
            if (MessageBox.Show($"Block all internet on {device.Name}?", "Block Internet", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                store.Queue([device.Id], "block-internet", "");
            }
        });
        var lockBtn = Desktop.Button("Lock Windows", (_, _) =>
        {
            if (MessageBox.Show($"Lock Windows session on {device.Name}?", "Lock Computer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                store.Queue([device.Id], "lock", "");
            }
        });
        var msgBtn = Desktop.Button("Send Notice", (_, _) =>
        {
            using var f = Desktop.Dialog($"Send notice to {device.Name}", 320);
            var p = Desktop.Stack(); f.Controls.Add(p);
            p.Controls.Add(Desktop.Label($"Message for {device.Name}", 14, true));
            var input = new TextBox { Width = 430, Multiline = true, Height = 90, MaxLength = 500 };
            p.Controls.Add(input);
            p.Controls.Add(Desktop.Button("Send Notice", (_, _) =>
            {
                try { Files.ValidateAction("message", input.Text.Trim()); f.DialogResult = DialogResult.OK; }
                catch (Exception ex) { Desktop.Error(ex); }
            }, true));
            if (f.ShowDialog(this) == DialogResult.OK) store.Queue([device.Id], "message", input.Text.Trim());
        });

        tools.Controls.Add(closeBtn);
        tools.Controls.Add(reverseBtn);
        tools.Controls.Add(controlBtn);
        tools.Controls.Add(saveBtn);
        tools.Controls.Add(powerBtn);
        tools.Controls.Add(unblockBtn);
        tools.Controls.Add(blockBtn);
        tools.Controls.Add(lockBtn);
        tools.Controls.Add(msgBtn);
        topBar.Controls.Add(tools);

        Controls.Add(pictureBox);
        Controls.Add(topBar);

        Shown += (_, _) =>
        {
            ScreenStore.FullViewDeviceId = device.Id;
            UpdateScreen();
            updateTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            updateTimer.Stop();
            ScreenStore.FullViewDeviceId = "";
            pictureBox.Image?.Dispose();
        };

        updateTimer.Tick += (_, _) => UpdateScreen();
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
                infoLabel.Text = $"LIVE: {device.Name}  ({device.User})  ·  Updated {time}";
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
            Title = $"Save screenshot of {device.Name}",
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
