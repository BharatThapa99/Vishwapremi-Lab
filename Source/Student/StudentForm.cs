using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Vishwapremi;

internal sealed class StudentConfig
{
    public Pairing Pairing {get;set;}=new();
    public string Token {get;set;}="";
}

internal sealed class StudentForm : Form
{
    [DllImport("user32.dll",SetLastError=true)] private static extern bool LockWorkStation();
    private readonly Label statusBadge = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(12, 6, 12, 6) };
    private readonly Label state = new() { AutoSize = true, Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = Desktop.Ink, Text = "Not Paired" };
    private readonly Label detail = Desktop.Label("Ask your teacher for this computer's pairing file.", 9.5f);
    private readonly Label elevationLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Information, Text = "Vishwapremi Student", Visible = true };
    private readonly string configPath = Path.Combine(Desktop.Folder("Student"), "connection.bin"), journalPath = Path.Combine(Desktop.Folder("Student"), "actions.json");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 4000 };
    private readonly Dictionary<string, Ack> completed;
    private StudentConfig? config;
    private HttpClient? client;
    private bool busy, exiting;
    private int connectionFailures;
    private Form? notice;
    private readonly Button pairButton;
    private readonly bool elevated;
    private readonly string macAddress = NetworkSetup.GetMacAddress();

    private void SetStatus(string badgeText, Color badgeBg, Color badgeFg, string detailText, Color detailColor)
    {
        statusBadge.Text = badgeText;
        statusBadge.BackColor = badgeBg;
        statusBadge.ForeColor = badgeFg;
        detail.Text = detailText;
        detail.ForeColor = detailColor;
    }

    public StudentForm(bool startup)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        elevated = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        if (elevated) { try { NetworkSetup.AllowInternet(); NetworkSetup.ClearHostsBlock(); } catch { } }

        Text = "Vishwapremi · Student Terminal";
        ClientSize = new Size(640, 580);
        MinimumSize = new Size(620, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Desktop.Paper;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Desktop.Green, ColumnCount = 2, RowCount = 1, Padding = new Padding(22, 14, 22, 14) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        var schoolInfo = new Panel { Dock = DockStyle.Fill };
        var schoolTitle = new Label { Text = "SHREE VISHWAPREMI SECONDARY SCHOOL", Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
        var schoolSub = new Label { Text = "Arun 5, Yaku, Bhojpur  •  Student Classroom Terminal", Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.FromArgb(208, 225, 211), Font = new Font("Segoe UI", 9.5f) };
        schoolInfo.Controls.Add(schoolTitle);
        schoolInfo.Controls.Add(schoolSub);
        header.Controls.Add(schoolInfo, 0, 0);

        var headerRight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        headerRight.Controls.Add(statusBadge);
        header.Controls.Add(headerRight, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Card 1: Terminal Identity & Status
        var card1Wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 7) };
        var card1 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(18, 12, 18, 12) };
        var card1Layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        card1.Controls.Add(card1Layout);
        card1Wrapper.Controls.Add(card1);
        root.Controls.Add(card1Wrapper, 0, 1);

        card1Layout.Controls.Add(new Label { Text = "DEVICE & CLASSROOM STATUS", ForeColor = Desktop.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8) });

        var stateRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        stateRow.Controls.Add(new Label { Text = "Assigned PC: ", ForeColor = Desktop.Ink, Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true });
        stateRow.Controls.Add(state);
        card1Layout.Controls.Add(stateRow);

        var infoLabel = new Label { Text = $"Computer: {Environment.MachineName}    •    Windows User: {Environment.UserName}", ForeColor = Desktop.Ink, Font = new Font("Segoe UI", 9.5f), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        card1Layout.Controls.Add(infoLabel);

        elevationLabel.Text = elevated ? "Administrator Privileges Active (Internet Control Enabled)" : "Standard User Session (Click 'Enable Admin Control' below)";
        elevationLabel.ForeColor = elevated ? Desktop.OnlineGreen : Desktop.Amber;
        elevationLabel.AutoSize = true;
        elevationLabel.Margin = new Padding(0, 0, 0, 6);
        card1Layout.Controls.Add(elevationLabel);

        detail.MaximumSize = new Size(540, 0);
        detail.Margin = new Padding(0, 0, 0, 2);
        card1Layout.Controls.Add(detail);

        // Card 2: Student Actions & Controls
        var card2Wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 7, 20, 10) };
        var card2 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(18, 12, 18, 12) };
        var card2Layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        card2.Controls.Add(card2Layout);
        card2Wrapper.Controls.Add(card2);
        root.Controls.Add(card2Wrapper, 0, 2);

        card2Layout.Controls.Add(new Label { Text = "STUDENT TOOLS & CONTROLS", ForeColor = Desktop.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8) });

        var actionsFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(560, 0) };
        actionsFlow.Controls.Add(Desktop.Button("Open Received Files", (_, _) =>
        {
            var destFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Vishwapremi Files");
            Directory.CreateDirectory(destFolder);
            Desktop.Open(destFolder);
        }));

        actionsFlow.Controls.Add(Desktop.Button("Restore / Unblock Internet", (_, _) =>
        {
            if (elevated)
            {
                try { NetworkSetup.AllowInternet(); NetworkSetup.ClearHostsBlock(); MessageBox.Show("Internet access and website filters have been restored.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { Desktop.Error(ex); }
            }
            else MessageBox.Show("Administrator privileges are required to modify firewall rules. Click 'Enable administrator control' first.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }));

        pairButton = Desktop.Button("Import pairing file…", async (_, _) => await Pair(), true);
        actionsFlow.Controls.Add(pairButton);

        if (!elevated)
        {
            var elevateBtn = Desktop.Button("Enable Admin Control…", (_, _) =>
            {
                try
                {
                    var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
                    info.ArgumentList.Add("--register-task");
                    using var p = Process.Start(info);
                    p!.WaitForExit();
                    if (p.ExitCode == 0)
                    {
                        MessageBox.Show("Administrator privileges enabled.\n\nVishwapremi Student will now restart with administrator privileges so internet control is active immediately.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        exiting = true;
                        var restart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
                        Close();
                        Process.Start(restart);
                    }
                    else
                    {
                        MessageBox.Show("Administrator setup was cancelled or failed. Please approve the Windows administrator request.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex) { Desktop.Error(ex); }
            });
            actionsFlow.Controls.Add(elevateBtn);
        }

        actionsFlow.Controls.Add(Desktop.Button("Hide to Background", (_, _) => Hide()));
        card2Layout.Controls.Add(actionsFlow);

        // Footer
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24, 6, 24, 6) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var footerHint = Desktop.Label("Runs in background during class · Access from system tray", 8.5f);
        footerHint.ForeColor = Desktop.Muted;
        footerHint.Dock = DockStyle.Fill;
        footer.Controls.Add(footerHint, 0, 0);
        var credit = Desktop.Label("Designed by Bharat Thapa", 9.5f, true);
        credit.Dock = DockStyle.Fill;
        credit.TextAlign = ContentAlignment.MiddleRight;
        footer.Controls.Add(credit, 1, 0);
        root.Controls.Add(footer, 0, 3);

        completed = Files.Read<Dictionary<string, Ack>>(journalPath) ?? [];
        if (File.Exists(configPath))
        {
            config = JsonSerializer.Deserialize<StudentConfig>(Vault.Read(configPath)) ?? throw new InvalidDataException("The saved pairing could not be read.");
            client = CreateClient(config.Pairing);
            state.Text = config.Pairing.Name;
            SetStatus("⏳ CONNECTING", Color.FromArgb(180, 120, 30), Color.White, "Connecting to the teacher computer…", Desktop.Ink);
            pairButton.Text = "Replace pairing…";
        }
        else
        {
            SetStatus("○ NOT PAIRED", Color.FromArgb(100, 116, 106), Color.White, "Ask your teacher for this computer's pairing file.", Desktop.Muted);
        }

        if (config != null && !elevated)
            detail.Text = "Connected but not elevated. Internet control requires administrator privileges.";

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open connection status", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        menu.Items.Add("Open Received Files", null, (_, _) =>
        {
            var destFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Vishwapremi Files");
            Directory.CreateDirectory(destFolder);
            Desktop.Open(destFolder);
        });
        menu.Items.Add("Unblock Internet (Emergency)", null, (_, _) =>
        {
            if (elevated) { try { NetworkSetup.AllowInternet(); NetworkSetup.ClearHostsBlock(); MessageBox.Show("Internet access has been restored.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { Desktop.Error(ex); } }
            else MessageBox.Show("Administrator privileges are required.", "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        });
        menu.Items.Add("Teacher Disconnect...", null, (_, _) =>
        {
            using var prompt = Desktop.Dialog("Teacher Authorization", 240);
            var p = Desktop.Stack(); prompt.Controls.Add(p);
            p.Controls.Add(Desktop.Label("Teacher Authorization", 14, true));
            p.Controls.Add(Desktop.Label("Enter teacher password to close student client:"));
            var input = new TextBox { Width = 430, UseSystemPasswordChar = true }; p.Controls.Add(input);
            var error = Desktop.Label(""); error.ForeColor = Color.Maroon; p.Controls.Add(error);
            var btn = Desktop.Button("Authorize & Exit", (_, _) =>
            {
                var pass = input.Text.Trim();
                if (pass == "vishwapremi" || pass == "VishwapremiLab" || pass == "teacher123" || pass == "ArunYaku5" || (config != null && pass == config.Pairing.Name))
                {
                    exiting = true;
                    prompt.DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    error.Text = "Incorrect teacher password.";
                }
            }, true);
            p.Controls.Add(btn);
            prompt.AcceptButton = btn;
            prompt.ShowDialog(this);
        });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        timer.Tick += async (_, _) => await Poll();
        timer.Start();
        Shown += async (_, _) => { if (startup && config != null) Hide(); await Poll(); };
        FormClosing += (_, e) => { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2000, "Vishwapremi Lab", "The student connection is still running beside the Windows clock.", ToolTipIcon.Info); } };
        FormClosed += (_, _) => { StopInputLoop(); if (elevated) { try { NetworkSetup.AllowInternet(); NetworkSetup.ClearHostsBlock(); } catch { } } timer.Stop(); client?.Dispose(); tray.Dispose(); notice?.Close(); };
    }
    private static HttpClient CreateClient(Pairing pairing, TimeSpan? timeout = null)
    {
        if(!Uri.TryCreate(pairing.Server,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.AbsolutePath!="/"||pairing.Certificate.Length!=64)throw new ArgumentException("This is not a valid school pairing file.");
        var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false};
        handler.ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert!=null && DateTime.UtcNow>=cert.NotBefore.ToUniversalTime() && DateTime.UtcNow<=cert.NotAfter.ToUniversalTime() && CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.RawData),Convert.FromHexString(pairing.Certificate));
        return new HttpClient(handler){BaseAddress=uri,Timeout=timeout ?? TimeSpan.FromSeconds(10)};
    }
    private async Task Pair()
    {
        if(busy)return;
        using var open=new OpenFileDialog{Title="Choose this computer's pairing file",Filter="Vishwapremi pairing file (*.vplab)|*.vplab"};
        if(open.ShowDialog(this)!=DialogResult.OK)return;
        busy=true;pairButton.Enabled=false;
        HttpClient? newClient=null;
        try
        {
            if(new FileInfo(open.FileName).Length>32768)throw new ArgumentException("The pairing file is too large.");
            var pairing=Files.Read<Pairing>(open.FileName)??throw new ArgumentException("The pairing file is empty.");
            if(pairing.Expires<DateTimeOffset.UtcNow)throw new ArgumentException("This pairing file has expired. Ask the teacher to remove this entry and create a new pairing file.");
            if(MessageBox.Show($"Pair this Windows account as {pairing.Name}?\n\nSchool: {pairing.School}\n\nThe app will automatically find the teacher on the local network. Use only a file provided by your school.","Connect to your school",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            detail.Text="Looking for the teacher on the local network…";
            var discovered=await NetworkSetup.DiscoverTeacher(pairing);
            if(discovered!=null)pairing.Server=discovered;
            newClient=CreateClient(pairing);
            using var response=await newClient.PostAsJsonAsync("/enroll",new EnrollmentRequest(pairing.Id,pairing.EnrollmentToken));
            if(response.StatusCode==HttpStatusCode.Unauthorized)throw new ArgumentException("This pairing file was already used, expired, or revoked. Ask the teacher to remove the entry and create a new pairing file.");
            response.EnsureSuccessStatusCode();
            var enrollment=await response.Content.ReadFromJsonAsync<EnrollmentResponse>()??throw new InvalidDataException("No pairing response.");
            pairing.EnrollmentToken="";
            var replacement=new StudentConfig{Pairing=pairing,Token=enrollment.Token};
            Vault.Save(configPath,JsonSerializer.SerializeToUtf8Bytes(replacement));
            client?.Dispose();client=newClient;newClient=null;config=replacement;completed.Clear();Files.Save(journalPath,completed);
            state.Text=pairing.Name;SetStatus("⏳ CONNECTING",Color.FromArgb(180,120,30),Color.White,"Paired successfully. Connecting to the teacher…",Desktop.Ink);pairButton.Text="Replace pairing…";
            MessageBox.Show("This computer is paired. You can remove its pairing file from the USB drive.\n\nThe app will start automatically when this Windows account signs in if that installer option was enabled.","Ready for class");
        }
        catch(HttpRequestException e){Desktop.Error(new Exception($"The student could not make a secure connection to {newClient?.BaseAddress ?? new Uri("https://teacher-not-found/")}.\n\nOn the teacher PC, install the updated Teacher app, open it, and click Allow lab connections. Approve the administrator request. Then remove this pending computer in Teacher, create a fresh pairing file, and import it again.\n\nIf both PCs use the same Wi-Fi and this continues, turn off AP/client isolation in the router.\n\nTechnical detail: {e.InnerException?.Message ?? e.Message}"));}
        catch(TaskCanceledException){Desktop.Error(new Exception($"The teacher at {newClient?.BaseAddress} did not answer within 8 seconds.\n\nThis version searches automatically, so do not try the three IP addresses. Update both apps, click Allow lab connections in Teacher, create one fresh pairing file, and import it again. If it still fails, the router may have AP/client isolation enabled."));}
        catch(Exception e){Desktop.Error(e);}
        finally {newClient?.Dispose();busy=false;pairButton.Enabled=true;}
        await Poll();
    }
    private async Task Poll()
    {
        if(busy||config==null||client==null||exiting)return;busy=true;
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/poll");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",config.Token);request.Headers.Add("X-Device",config.Pairing.Id);
            request.Content=JsonContent.Create(new PollRequest(Environment.UserName,Environment.MachineName,completed.Values.TakeLast(100).ToList(),macAddress));
            using var response=await client.SendAsync(request);
            if(response.StatusCode==HttpStatusCode.Unauthorized){SetStatus("✕ REVOKED",Desktop.Danger,Color.White,"Pairing was revoked. Ask the teacher to pair this computer again.",Desktop.Danger);return;}
            response.EnsureSuccessStatusCode();var result=await response.Content.ReadFromJsonAsync<PollResponse>()??throw new InvalidDataException();
            connectionFailures=0;
            if(result.RemoteControl)
            {
                SetStatus("● REMOTE ASSISTANCE", Color.FromArgb(24, 76, 56), Color.FromArgb(220, 245, 225), "Teacher is providing live remote assistance.", Desktop.OnlineGreen);
                tray.Text = "Vishwapremi Student · Remote Assistance Active";
                EnsureInputLoop();
            }
            else
            {
                StopInputLoop();
                if(result.ScreenMode == 3)
                {
                    SetStatus("● REVERSE SHARING", Color.FromArgb(24, 76, 56), Color.FromArgb(220, 245, 225), "Your screen is sharing live to teacher monitor / smartboard.", Desktop.OnlineGreen);
                    tray.Text = "Vishwapremi Student · Presenting Screen to Teacher";
                }
                else
                {
                    SetStatus("● CONNECTED",Color.FromArgb(38,98,72),Color.FromArgb(220,245,225),"Connected to teacher · "+DateTime.Now.ToShortTimeString(),Desktop.OnlineGreen);
                    tray.Text="Vishwapremi Student · Connected";
                }
            }
            timer.Interval = result.RemoteControl ? 800 : (result.ScreenMode == 3 ? 1000 : (result.ScreenMode == 2 ? 1500 : (result.ScreenMode == 1 ? 3000 : 4000)));
            if(result.ScreenMode > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bytes = CaptureScreen(result.ScreenMode);
                        if (bytes != null && client != null && config != null)
                        {
                            using var content = new ByteArrayContent(bytes);
                            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                            using var screenReq = new HttpRequestMessage(HttpMethod.Post, "/screen") { Content = content };
                            screenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                            screenReq.Headers.Add("X-Device", config.Pairing.Id);
                            await client.SendAsync(screenReq);
                        }
                    }
                    catch { }
                });
            }
            foreach(var command in result.Commands.Take(30))
            {
                if(completed.ContainsKey(command.Id))continue;
                var ack=new Ack(command.Id,"Failed","Interrupted before completion");completed[command.Id]=ack;Files.Save(journalPath,completed);
                try
                {
                    if(command.Expired)throw new InvalidOperationException("Command expired before delivery");
                    if(command.Device!=config.Pairing.Id)throw new InvalidOperationException("Wrong computer identity");
                    Files.ValidateAction(command.Action,command.Value);
                    await Execute(command);ack=new Ack(command.Id,"Executed",command.Action switch {"message"=>"Notice displayed","website"=>"Browser launch requested","lock"=>"Windows lock requested","block-internet"=>"Internet blocked","allow-internet" or "unblock-internet"=>"Internet restored","blacklist"=>"Sites blacklisted","whitelist"=>"Whitelist applied","clear-filter"=>"Filters cleared","file"=>"File saved to Desktop","update-app"=>"Update verified and installing","shutdown"=>"Shutdown initiated","restart"=>"Restart initiated","abort-shutdown"=>"Shutdown cancelled","logoff"=>"User logged off","sleep"=>"Entering sleep mode",_=>"Action completed"});
                }
                catch(Exception e){ack=new Ack(command.Id,"Failed",e.Message[..Math.Min(e.Message.Length,180)]);}
                completed[command.Id]=ack;
                while(completed.Count>300)completed.Remove(completed.Keys.First());
                Files.Save(journalPath,completed);
            }
        }
        catch(Exception)
        {
            connectionFailures++;
            SetStatus("⏳ SEARCHING",Color.FromArgb(180,120,30),Color.White,"Waiting for teacher. Checking local network automatically…",Desktop.Amber);tray.Text="Vishwapremi Student · Waiting";
            if(connectionFailures>=2)await RelocateTeacher();
            if(connectionFailures>=150&&elevated){try{NetworkSetup.AllowInternet();NetworkSetup.ClearHostsBlock();}catch{}}
        }
        finally {busy=false;}
    }
    private async Task RelocateTeacher()
    {
        if(config==null)return;
        var discovered=await NetworkSetup.DiscoverTeacher(config.Pairing,timeout:TimeSpan.FromSeconds(1.5));
        if(discovered==null || string.Equals(discovered,config.Pairing.Server,StringComparison.OrdinalIgnoreCase))return;
        config.Pairing.Server=discovered;
        Vault.Save(configPath,JsonSerializer.SerializeToUtf8Bytes(config));
        client?.Dispose();client=CreateClient(config.Pairing);connectionFailures=0;
        SetStatus("⏳ CONNECTING",Color.FromArgb(180,120,30),Color.White,"Teacher found at a new local address. Reconnecting…",Desktop.Ink);
    }
    private async Task Execute(Command command)
    {
        switch(command.Action)
        {
            case "website":Desktop.Open(command.Value);break;
            case "lock":if(!LockWorkStation())throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());break;
            case "message":
                notice?.Close();notice=Desktop.Dialog("A notice from your teacher",360);notice.TopMost=true;
                var panel=Desktop.Stack();notice.Controls.Add(panel);panel.Controls.Add(Desktop.Label("SHREE VISHWAPREMI",15,true));
                var message=Desktop.Label(command.Value,12);message.MaximumSize=new Size(440,0);panel.Controls.Add(message);panel.Controls.Add(Desktop.Button("Understood",(_,_)=>notice?.Close(),true));notice.Show();break;
            case "block-internet":
                if(!elevated)throw new InvalidOperationException("Student app is not running with administrator privileges. Internet control requires elevation.");
                var teacherIp=new Uri(config!.Pairing.Server).Host;
                if(NetworkSetup.BlockInternet(teacherIp)!=0)throw new InvalidOperationException("Failed to apply firewall rules.");
                break;
            case "allow-internet":
            case "unblock-internet":
                if(!elevated)throw new InvalidOperationException("Student app is not running with administrator privileges.");
                if(NetworkSetup.AllowInternet()!=0)throw new InvalidOperationException("Failed to remove firewall rules.");
                break;
            case "blacklist":
                if(!elevated)throw new InvalidOperationException("Student app is not running with administrator privileges.");
                NetworkSetup.AllowInternet(); // ensure internet is on first
                NetworkSetup.ApplyHostsBlock(Files.ParseDomainList(command.Value));
                break;
            case "whitelist":
                if(!elevated)throw new InvalidOperationException("Student app is not running with administrator privileges.");
                var whitelistTeacherIp=new Uri(config!.Pairing.Server).Host;
                NetworkSetup.ClearHostsBlock();
                if(NetworkSetup.ApplyWhitelist(whitelistTeacherIp,Files.ParseDomainList(command.Value))!=0)throw new InvalidOperationException("Failed to apply whitelist rules.");
                break;
            case "clear-filter":
                if(!elevated)throw new InvalidOperationException("Student app is not running with administrator privileges.");
                NetworkSetup.AllowInternet();
                NetworkSetup.ClearHostsBlock();
                break;
            case "file":
                var fParts=command.Value.Split('|');
                if(fParts.Length<4)throw new InvalidOperationException("Invalid file transfer payload.");
                var fileId=fParts[0];var safeName=Path.GetFileName(fParts[1]);var autoOpen=fParts[3]=="1";
                var destFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),"Vishwapremi Files");
                Directory.CreateDirectory(destFolder);
                var destPath=Path.Combine(destFolder,safeName);
                await DownloadFileAsync(fileId,destPath);
                if(autoOpen)try{Desktop.Open(destPath);}catch{}
                tray.ShowBalloonTip(4000,"Vishwapremi Lab",$"Received file: {safeName}\nSaved to Desktop in 'Vishwapremi Files'",ToolTipIcon.Info);
                break;
            case "update-app":
                var uParts=command.Value.Split('|');
                if(uParts.Length<3)throw new InvalidOperationException("Invalid update payload.");
                var uId=uParts[0];var uName=Path.GetFileName(uParts[1]);var expectedHash=uParts[2];
                var tempDir=Path.Combine(Path.GetTempPath(),"VishwapremiUpdate");
                Directory.CreateDirectory(tempDir);
                var localInstaller=Path.Combine(tempDir,uName);
                await DownloadFileAsync(uId,localInstaller);
                string actualHash;
                using(var fs=File.OpenRead(localInstaller))
                    actualHash=Convert.ToHexString(await SHA256.HashDataAsync(fs));
                if(!actualHash.Equals(expectedHash,StringComparison.OrdinalIgnoreCase))
                {
                    try{File.Delete(localInstaller);}catch{}
                    throw new InvalidDataException("Downloaded installer failed integrity check.");
                }
                await SendAckImmediate(command.Id,"Executed","Update verified and installing");
                var runnerBatch=Path.Combine(tempDir,"run_update.bat");
                var currentExe=Environment.ProcessPath??"";
                var script=$"@echo off\r\ntimeout /t 3 /nobreak >nul\r\nstart /wait \"\" \"{localInstaller}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS\r\ntimeout /t 2 /nobreak >nul\r\nstart \"\" \"{currentExe}\" --startup\r\n";
                await File.WriteAllTextAsync(runnerBatch,script);
                Process.Start(new ProcessStartInfo(runnerBatch){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden,CreateNoWindow=true});
                exiting=true;
                BeginInvoke(Close);
                break;
            case "shutdown":
                var shutDelay = int.TryParse(command.Value, out var sd) ? sd : 5;
                if (shutDelay > 0) tray.ShowBalloonTip(4000, "Vishwapremi Lab", $"Teacher scheduled computer shutdown in {shutDelay} seconds.", ToolTipIcon.Warning);
                await SendAckImmediate(command.Id, "Executed", shutDelay > 0 ? $"Shutting down in {shutDelay}s" : "Shutting down now");
                Process.Start(new ProcessStartInfo("shutdown.exe", $"/s /t {shutDelay} /f /c \"Teacher initiated shutdown\"") { CreateNoWindow = true, UseShellExecute = false });
                break;
            case "restart":
                var restDelay = int.TryParse(command.Value, out var rd) ? rd : 5;
                if (restDelay > 0) tray.ShowBalloonTip(4000, "Vishwapremi Lab", $"Teacher scheduled computer restart in {restDelay} seconds.", ToolTipIcon.Warning);
                await SendAckImmediate(command.Id, "Executed", restDelay > 0 ? $"Restarting in {restDelay}s" : "Restarting now");
                Process.Start(new ProcessStartInfo("shutdown.exe", $"/r /t {restDelay} /f /c \"Teacher initiated restart\"") { CreateNoWindow = true, UseShellExecute = false });
                break;
            case "abort-shutdown":
                Process.Start(new ProcessStartInfo("shutdown.exe", "/a") { CreateNoWindow = true, UseShellExecute = false });
                tray.ShowBalloonTip(3000, "Vishwapremi Lab", "Computer shutdown cancelled.", ToolTipIcon.Info);
                break;
            case "logoff":
                await SendAckImmediate(command.Id, "Executed", "User session logged off");
                Process.Start(new ProcessStartInfo("shutdown.exe", "/l") { CreateNoWindow = true, UseShellExecute = false });
                break;
            case "sleep":
                await SendAckImmediate(command.Id, "Executed", "Computer entering sleep mode");
                Application.SetSuspendState(PowerState.Suspend, force: true, disableWakeEvent: false);
                break;
        }
    }
    private async Task SendAckImmediate(string commandId, string status, string detail)
    {
        try
        {
            completed[commandId] = new Ack(commandId, status, detail);
            Files.Save(journalPath, completed);
            if (client != null && config != null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/poll");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                request.Headers.Add("X-Device", config.Pairing.Id);
                request.Content = JsonContent.Create(new PollRequest(Environment.UserName, Environment.MachineName, completed.Values.TakeLast(100).ToList(), macAddress));
                using var resp = await client.SendAsync(request);
            }
        }
        catch { }
    }
    private async Task DownloadFileAsync(string fileId, string destinationPath)
    {
        using var dlClient = CreateClient(config!.Pairing, timeout: TimeSpan.FromMinutes(15));
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/file/{fileId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        req.Headers.Add("X-Device", config.Pairing.Id);
        using var resp = await dlClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(fs);
    }
    private static byte[]? CaptureScreen(int mode)
    {
        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            }
            int targetWidth;
            long quality;
            if (mode == 3) // Reverse Sharing: High Quality Presentation Mode
            {
                targetWidth = Math.Min(1920, bounds.Width);
                quality = 85L;
            }
            else if (mode == 2) // Full view
            {
                targetWidth = Math.Min(1280, bounds.Width);
                quality = 65L;
            }
            else // Grid thumbnail
            {
                targetWidth = 360;
                quality = 45L;
            }
            int targetHeight = Math.Max(1, (int)((double)bounds.Height / bounds.Width * targetWidth));
            using var resized = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = mode == 3 ? System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic : System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(bmp, 0, 0, targetWidth, targetHeight);
            }
            using var ms = new MemoryStream();
            var encoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
            if (encoder == null)
            {
                resized.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            else
            {
                using var p = new System.Drawing.Imaging.EncoderParameters(1);
                p.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                resized.Save(ms, encoder, p);
            }
            return ms.ToArray();
        }
        catch { return null; }
    }

    private CancellationTokenSource? inputLoopCts;
    private void EnsureInputLoop()
    {
        if (inputLoopCts != null && !inputLoopCts.IsCancellationRequested) return;
        inputLoopCts = new CancellationTokenSource();
        var token = inputLoopCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && client != null && config != null)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "/input");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                    req.Headers.Add("X-Device", config.Pairing.Id);
                    using var resp = await client.SendAsync(req, token);
                    if (resp.IsSuccessStatusCode)
                    {
                        var events = await resp.Content.ReadFromJsonAsync<List<InputEvent>>(cancellationToken: token);
                        if (events != null && events.Count > 0)
                        {
                            InputSimulator.ExecuteEvents(events);
                        }
                    }
                    else
                    {
                        await Task.Delay(300, token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(300, token); }
            }
        }, token);
    }
    private void StopInputLoop()
    {
        if (inputLoopCts != null)
        {
            inputLoopCts.Cancel();
            inputLoopCts.Dispose();
            inputLoopCts = null;
        }
    }
}
