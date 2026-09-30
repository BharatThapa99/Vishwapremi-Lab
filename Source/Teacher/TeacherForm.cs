using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

namespace Vishwapremi;

internal sealed class TeacherForm : Form
{
    private readonly LabStore store;
    private readonly Controller controller=new();
    private readonly DataGridView grid=new(),history=new();
    private readonly Label summary=Desktop.Label("Starting your lab…",10.5f,true),status=Desktop.Label(""),network=Desktop.Label("");
    private readonly TextBox searchBox=new(){Width=220,Font=new Font("Segoe UI",9.5f),PlaceholderText="🔍 Search computers…"};
    private readonly System.Windows.Forms.Timer timer=new(){Interval=3000};
    private readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="Vishwapremi Teacher",Visible=true};
    private bool exiting,ready,locked;
    public TeacherForm(LabStore store)
    {
        this.store=store;Text="Vishwapremi · Teacher workspace";Size=new Size(1180,830);MinimumSize=new Size(960,700);StartPosition=FormStartPosition.CenterScreen;BackColor=Desktop.Paper;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(24)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,96));root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Percent,58));root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));root.RowStyles.Add(new RowStyle(SizeType.Percent,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
        
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,BackColor=Desktop.Green,ColumnCount=2,RowCount=1,Padding=new Padding(20,12,20,12)};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        var schoolInfo=new Panel{Dock=DockStyle.Fill};
        var school=new Label {Text="SHREE VISHWAPREMI SECONDARY SCHOOL",Dock=DockStyle.Top,AutoSize=true,ForeColor=Color.White,Font=new Font("Segoe UI",17,FontStyle.Bold)};
        var address=new Label {Text="Arun 5, Yaku, Bhojpur  •  Computer Lab Management Workspace",Dock=DockStyle.Bottom,Height=22,ForeColor=Color.FromArgb(208,225,211),Font=new Font("Segoe UI",9.5f)};
        schoolInfo.Controls.Add(school);schoolInfo.Controls.Add(address);header.Controls.Add(schoolInfo,0,0);
        var headerRight=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,6,0,0)};
        var statusBadge=new Label{Text="● CONTROLLER ACTIVE",ForeColor=Color.FromArgb(220,245,225),BackColor=Color.FromArgb(38,98,72),Font=new Font("Segoe UI",9,FontStyle.Bold),Padding=new Padding(12,6,12,6),AutoSize=true};
        headerRight.Controls.Add(statusBadge);header.Controls.Add(headerRight,1,0);root.Controls.Add(header,0,0);

        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,4,0,0)};
        tools.Controls.Add(Desktop.Button("🖥️ Live Screens",(_,_)=>OpenScreenMonitor(),true));tools.Controls.Add(Desktop.Button("🔄 Update Student Apps",(_,_)=>UpdateStudentApps()));tools.Controls.Add(Desktop.Button("+ Add computer",(_,_)=>AddComputer()));tools.Controls.Add(Desktop.Button("📦 Export Profile",(_,_)=>ExportProfile()));tools.Controls.Add(Desktop.Button("📥 Import Profile",(_,_)=>ImportProfile()));tools.Controls.Add(Desktop.Button("Allow lab connections",(_,_)=>Firewall()));tools.Controls.Add(Desktop.Button("Remove selected",(_,_)=>Remove()));tools.Controls.Add(Desktop.Button("Lock teacher app",(_,_)=>LockApp()));tools.Controls.Add(Desktop.Button("Help",(_,_)=>Help()));root.Controls.Add(tools,0,1);

        StyleGrid(grid);grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Select",HeaderText="Select",Width=60});grid.Columns.Add("Name","Computer");grid.Columns.Add("State","Connection");grid.Columns.Add("User","Windows user");grid.Columns.Add("Host","Device name");grid.Columns.Add("Last","Last seen");
        foreach(DataGridViewColumn c in grid.Columns)if(c.Name!="Select")c.ReadOnly=true;
        grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        grid.CellFormatting+=(_,e)=>{
            if(e.ColumnIndex==2 && e.Value is string s){
                if(s=="Connected"){e.Value="● Connected";e.CellStyle.ForeColor=Desktop.OnlineGreen;e.CellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);}
                else if(s=="Offline"){e.Value="○ Offline";e.CellStyle.ForeColor=Desktop.OfflineGray;}
                else if(s=="Awaiting pairing"){e.Value="⏳ Pairing";e.CellStyle.ForeColor=Desktop.Amber;}
            }
        };
        grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0 && e.RowIndex<grid.Rows.Count)ReverseShareRow(e.RowIndex);};
        root.Controls.Add(grid,0,2);

        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,6,0,0)};
        actions.Controls.Add(Desktop.Button("☑️ Select online",(_,_)=>{foreach(DataGridViewRow r in grid.Rows)r.Cells[0].Value=r.Cells[2].Value as string=="Connected";}));
        actions.Controls.Add(Desktop.Button("◻️ Clear",(_,_)=>{foreach(DataGridViewRow r in grid.Rows)r.Cells[0].Value=false;}));
        actions.Controls.Add(Desktop.Button("📺 Reverse Share",(_,_)=>ReverseShareSelected(),true));
        actions.Controls.Add(Desktop.Button("🎮 Remote Control",(_,_)=>RemoteControlSelected(),true));
        actions.Controls.Add(Desktop.Button("📁 Share File",(_,_)=>ShareFile(),true));actions.Controls.Add(Desktop.Button("🌐 Open Website",(_,_)=>Send("website"),true));actions.Controls.Add(Desktop.Button("💬 Send Notice",(_,_)=>Send("message")));
        actions.Controls.Add(new Label{Text="|",ForeColor=Desktop.Border,AutoSize=true,Padding=new Padding(2,8,2,0),Font=new Font("Segoe UI",11)});
        actions.Controls.Add(Desktop.Button("🔒 Lock PCs",(_,_)=>Send("lock")));actions.Controls.Add(Desktop.Button("⚡ Power",(_,_)=>PowerDialog()));
        actions.Controls.Add(new Label{Text="|",ForeColor=Desktop.Border,AutoSize=true,Padding=new Padding(2,8,2,0),Font=new Font("Segoe UI",11)});
        actions.Controls.Add(Desktop.Button("🚫 Block Internet",(_,_)=>Send("block-internet")));actions.Controls.Add(Desktop.Button("🌐 Unblock Internet",(_,_)=>Send("unblock-internet")));
        actions.Controls.Add(Desktop.Button("⚙️ Filter Websites",(_,_)=>{var ids=Selected();if(ids.Length==0){MessageBox.Show("Select one or more connected computers first.","Classroom tools");return;}using var f=Desktop.Dialog("Website filtering",460);var p=Desktop.Stack();f.Controls.Add(p);p.Controls.Add(Desktop.Label("Website filtering",19,true));p.Controls.Add(Desktop.Label("Enter domain names, one per line (e.g. youtube.com)"));var input=new TextBox{Width=430,Height=140,Multiline=true,MaxLength=4000};p.Controls.Add(input);var btns=new FlowLayoutPanel{Width=450,Height=40};var action="";btns.Controls.Add(Desktop.Button("Blacklist these sites",(_,_)=>{try{Files.ValidateAction("blacklist",input.Text.Trim());action="blacklist";f.DialogResult=DialogResult.OK;}catch(Exception e){Desktop.Error(e);}}));btns.Controls.Add(Desktop.Button("Whitelist only these",(_,_)=>{try{Files.ValidateAction("whitelist",input.Text.Trim());action="whitelist";f.DialogResult=DialogResult.OK;}catch(Exception e){Desktop.Error(e);}}));p.Controls.Add(btns);if(f.ShowDialog(this)==DialogResult.OK){try{var count=store.Queue(ids,action,input.Text.Trim());status.Text=$"Sent to {count} online computer(s). Check the result below.";RefreshRoom();}catch(Exception e){Desktop.Error(e);}}}));
        actions.Controls.Add(Desktop.Button("Clear Filters",(_,_)=>Send("clear-filter")));root.Controls.Add(actions,0,3);

        var stats=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
        summary.Dock=DockStyle.Fill;summary.TextAlign=ContentAlignment.MiddleLeft;stats.Controls.Add(summary,0,0);
        var searchPanel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,4,0,0)};
        searchBox.TextChanged+=(_,_)=>RefreshRoom();searchPanel.Controls.Add(searchBox);stats.Controls.Add(searchPanel,1,0);root.Controls.Add(stats,0,4);

        StyleGrid(history);history.ReadOnly=true;history.Columns.Add("Time","Time");history.Columns.Add("PC","Computer");history.Columns.Add("Action","Recent action");history.Columns.Add("Result","Result");history.Columns.Add("Detail","Details");
        history.CellFormatting+=(_,e)=>{
            if(e.ColumnIndex==3 && e.Value is string r){
                if(r=="Executed"){e.Value="✓ Executed";e.CellStyle.ForeColor=Desktop.OnlineGreen;e.CellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);}
                else if(r=="Failed"){e.Value="✕ Failed";e.CellStyle.ForeColor=Desktop.Danger;e.CellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);}
                else if(r=="Waiting"){e.Value="⏳ Waiting";e.CellStyle.ForeColor=Desktop.Amber;}
                else if(r=="Expired"){e.Value="— Expired";e.CellStyle.ForeColor=Desktop.OfflineGray;}
            }
        };root.Controls.Add(history,0,5);

        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));status.Dock=DockStyle.Fill;footer.Controls.Add(status);var credit=Desktop.Label("Designed by Bharat Thapa",10,true);credit.Dock=DockStyle.Fill;credit.TextAlign=ContentAlignment.MiddleRight;footer.Controls.Add(credit);root.Controls.Add(footer,0,6);
        var menu=new ContextMenuStrip();menu.Items.Add("Open teacher workspace",null,(_,_)=>Restore());menu.Items.Add("Exit controller",null,(_,_)=>Exit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Restore();
        timer.Tick+=(_,_)=>RefreshRoom();
        Shown+=async (_,_)=>{try {await controller.Start(store);ready=true;status.Text="Lab controller running · encrypted local connections";RefreshRoom();timer.Start();}catch(Exception e){Desktop.Error(new Exception("The controller could not listen on port 8766. Close other copies or check whether this port is in use.\n"+e.Message));status.Text="Controller is not running.";}};
        FormClosing+=(_,e)=>{if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"Vishwapremi Lab","The lab controller is still running. Open it beside the Windows clock.",ToolTipIcon.Info);}};
        FormClosed+=async (_,_)=>{timer.Stop();tray.Dispose();await controller.DisposeAsync();};
    }
    private static void StyleGrid(DataGridView g)
    {
        g.Dock=DockStyle.Fill;g.BackgroundColor=Color.White;g.BorderStyle=BorderStyle.None;g.AllowUserToAddRows=false;g.AllowUserToDeleteRows=false;g.RowHeadersVisible=false;g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;g.SelectionMode=DataGridViewSelectionMode.FullRowSelect;g.MultiSelect=false;g.EnableHeadersVisualStyles=false;
        g.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(232,238,228);g.ColumnHeadersDefaultCellStyle.ForeColor=Desktop.Ink;g.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);g.ColumnHeadersHeight=38;g.RowTemplate.Height=38;
        g.DefaultCellStyle.Font=new Font("Segoe UI",9.5f);g.DefaultCellStyle.SelectionBackColor=Color.FromArgb(218,232,212);g.DefaultCellStyle.SelectionForeColor=Desktop.Ink;g.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;g.GridColor=Color.FromArgb(234,239,232);
    }
    private string[] Selected()=>grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Cells[0].Value is true).Select(r=>(string)r.Tag!).ToArray();
    private void RefreshRoom()
    {
        var selected=Selected().ToHashSet();var snapshot=store.Snapshot();grid.Rows.Clear();
        var filter=searchBox.Text.Trim().ToLowerInvariant();
        foreach(var d in snapshot.Devices.OrderBy(x=>x.Name))
        {
            if(filter.Length>0 && !d.Name.ToLowerInvariant().Contains(filter) && !d.User.ToLowerInvariant().Contains(filter) && !d.Computer.ToLowerInvariant().Contains(filter)) continue;
            var row=grid.Rows[grid.Rows.Add(selected.Contains(d.Id),d.Name,d.Online?"Connected":d.TokenHash.Length==0?"Awaiting pairing":"Offline",d.User,d.Computer,d.Seen==default?"—":d.Seen.LocalDateTime.ToShortTimeString())];row.Tag=d.Id;
            if(!d.Online)row.DefaultCellStyle.ForeColor=Desktop.OfflineGray;
        }
        summary.Text=$"{snapshot.Devices.Count(d=>d.Online)} connected   /   {snapshot.Devices.Count} enrolled       •       Classroom Activity";
        history.Rows.Clear();foreach(var c in snapshot.Commands.OrderByDescending(c=>c.Created).Take(30))history.Rows.Add(c.Created.LocalDateTime.ToShortTimeString(),snapshot.Devices.Find(d=>d.Id==c.Device)?.Name??"Removed",c.Action switch {"website"=>"Open website","message"=>"Classroom notice","file"=>"Send file","update-app"=>"Update app","block-internet"=>"Block internet","allow-internet" or "unblock-internet"=>"Unblock internet","blacklist"=>"Blacklist sites","whitelist"=>"Whitelist sites","clear-filter"=>"Clear filters","shutdown"=>"Shut down PC","restart"=>"Restart PC","logoff"=>"Log off user","sleep"=>"Sleep PC","abort-shutdown"=>"Abort shutdown",_=>"Lock Windows"},c.Status=="Waiting"&&c.Expired?"Expired":c.Status,c.Detail);
    }
    private void AddComputer()
    {
        if(!ready){MessageBox.Show("Wait for the controller to start.");return;}
        using var f=Desktop.Dialog("Add a student computer",400);var panel=Desktop.Stack();f.Controls.Add(panel);panel.Controls.Add(Desktop.Label("Connect a new computer",19,true));panel.Controls.Add(Desktop.Label("1. Name the PC.  2. Save its pairing file to a USB drive."));
        panel.Controls.Add(Desktop.Label("Computer name"));var name=new TextBox{Width=430,Text="LAB-"+(store.Snapshot().Devices.Count+1).ToString("00")};panel.Controls.Add(name);
        panel.Controls.Add(Desktop.Label("Active school network"));var address=new ComboBox{Width=430,DropDownStyle=ComboBoxStyle.DropDownList};
        foreach(var item in NetworkSetup.ActiveIPv4())address.Items.Add(new NetworkChoice(item.Address.ToString(),item.Adapter));
        if(address.Items.Count>0)address.SelectedIndex=0;panel.Controls.Add(address);
        panel.Controls.Add(Desktop.Label("The student app will find this teacher automatically. The selected address is a fallback. The file works once, within 24 hours."));
        panel.Controls.Add(Desktop.Button("Save pairing file…",(_,_)=>
        {
            try
            {
                if(address.SelectedItem is not NetworkChoice choice)throw new ArgumentException("No active school network was found. Connect the teacher computer to the same router as the student PCs, then reopen this window.");
                var host=choice.Address;
                using var save=new SaveFileDialog{Title="Save pairing file to your USB drive",Filter="Vishwapremi pairing file (*.vplab)|*.vplab",FileName=string.Concat(name.Text.Trim().Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c))+".vplab"};
                if(save.ShowDialog(f)!=DialogResult.OK)return;
                var pairing=store.Add(name.Text.Trim(),"https://"+host+":8766",controller.Fingerprint);
                try {Files.Save(save.FileName,pairing);}catch{store.Remove(pairing.Id);throw;}
                f.DialogResult=DialogResult.OK;RefreshRoom();status.Text="Pairing file saved. Import it on "+pairing.Name+".";
            }
            catch(Exception e){Desktop.Error(e);}
        },true));f.ShowDialog(this);
    }
    private void Send(string action)
    {
        var ids=Selected();if(ids.Length==0){MessageBox.Show("Select one or more connected computers first.","Classroom tools");return;}
        var value="";
        if(action is "lock" or "block-internet" or "allow-internet" or "unblock-internet" or "clear-filter")
        {
            if(action=="lock" && MessageBox.Show("Lock the selected Windows sessions? Students will need to sign back into Windows. Save work first.","Lock computers",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            if(action=="block-internet" && MessageBox.Show("Block all internet access on selected computers? Only the teacher connection will remain active.","Block Internet",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
        }
        else
        {
            using var f=Desktop.Dialog(action=="website"?"Open a lesson website":"Send a classroom notice",340);var p=Desktop.Stack();f.Controls.Add(p);p.Controls.Add(Desktop.Label(action=="website"?"Where should students go?":"A note for the classroom",18,true));
            p.Controls.Add(Desktop.Label(action=="website"?"Enter the full website address, beginning with https://":"Up to 500 characters. Appears as a notice on each selected PC."));
            var input=new TextBox{Width=430,Multiline=action=="message",Height=action=="message"?100:30,MaxLength=action=="message"?500:2048,Text=action=="website"?"https://":""};p.Controls.Add(input);
            p.Controls.Add(Desktop.Button("Send to selected computers",(_,_)=>{try{Files.ValidateAction(action,input.Text.Trim());f.DialogResult=DialogResult.OK;}catch(Exception e){Desktop.Error(e);}},true));
            if(f.ShowDialog(this)!=DialogResult.OK)return;value=input.Text.Trim();
        }
        try {var count=store.Queue(ids,action,value);status.Text=$"Sent to {count} online computer(s). Check the result below.";RefreshRoom();}catch(Exception e){Desktop.Error(e);}
    }
    private void Remove()
    {
        var ids=Selected();if(ids.Length==0)return;
        if(MessageBox.Show("Remove the selected computers and revoke their connections? They must be paired again to reconnect.","Remove computers",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
        foreach(var id in ids)store.Remove(id);RefreshRoom();
    }
    private void Firewall()
    {
        if(MessageBox.Show("Allow student computers on this local network to find and connect to the teacher?\n\nThis adds two restricted Windows Firewall rules: encrypted controller traffic on TCP 8766 and automatic discovery on UDP 8765. Both accept local-subnet devices only, whether Windows calls the network Public or Private.\n\nWindows will ask an administrator to approve it once.","Allow lab connections",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
        try
        {
            var info=new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
            info.ArgumentList.Add("--configure-firewall");
            using var process=Process.Start(info);
            process!.WaitForExit();
            MessageBox.Show(process.ExitCode==0?"Lab connections are allowed from this local subnet on Public and Private networks. Pair the student computer with a newly created file.":"The firewall rules were not added. Ask a Windows administrator to allow the teacher app on TCP 8766 and UDP 8765 for the local subnet.","Lab connection setup");
        }
        catch(Exception e){Desktop.Error(e);}
    }
    private void OpenScreenMonitor()
    {
        if(!ready){MessageBox.Show("Wait for the controller to start.");return;}
        using var f=new ScreenMonitorForm(store);
        f.ShowDialog(this);
    }
    private void ShareFile()
    {
        var ids=Selected();if(ids.Length==0){MessageBox.Show("Select one or more connected computers first.","Classroom tools");return;}
        using var open=new OpenFileDialog{Title="Select file to send to student computers",Filter="All files (*.*)|*.*"};
        if(open.ShowDialog(this)!=DialogResult.OK)return;
        var autoOpen=MessageBox.Show($"Send '{Path.GetFileName(open.FileName)}' to {ids.Length} computer(s)?\n\nClick YES to automatically open the file on student computers after saving to their Desktop.\nClick NO to save it to their Desktop without opening it.","Share File",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        if(autoOpen==DialogResult.Cancel)return;
        try
        {
            var fileId=Guid.NewGuid().ToString("N");
            var fileName=Path.GetFileName(open.FileName);
            var fileInfo=new FileInfo(open.FileName);
            string hash;
            using(var fs=File.OpenRead(open.FileName))hash=Convert.ToHexString(SHA256.HashData(fs));
            FileStore.Register(fileId,open.FileName,fileName,hash);
            var payload=$"{fileId}|{fileName}|{fileInfo.Length}|{(autoOpen==DialogResult.Yes?"1":"0")}";
            var count=store.Queue(ids,"file",payload);
            status.Text=$"Sent file to {count} online computer(s). Check the result below.";
            RefreshRoom();
        }
        catch(Exception ex){Desktop.Error(ex);}
    }
    private void UpdateStudentApps()
    {
        if(!ready){MessageBox.Show("Wait for the controller to start.");return;}
        var snapshot=store.Snapshot();
        var onlineIds=snapshot.Devices.Where(d=>d.Online).Select(d=>d.Id).ToArray();
        if(onlineIds.Length==0){MessageBox.Show("No student computers are currently connected and online.","Update Student Apps");return;}
        string installerPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Vishwapremi-Student-Setup.exe");
        if(!File.Exists(installerPath))installerPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Release","Vishwapremi-Student-Setup.exe");
        if(!File.Exists(installerPath))installerPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Source","installers","Vishwapremi-Student-Setup.exe");
        if(!File.Exists(installerPath))
        {
            using var open=new OpenFileDialog{Title="Select Vishwapremi-Student-Setup.exe installer to distribute",Filter="Installer executable (*.exe)|*.exe"};
            if(open.ShowDialog(this)!=DialogResult.OK)return;
            installerPath=open.FileName;
        }
        if(MessageBox.Show($"Push update '{Path.GetFileName(installerPath)}' to {onlineIds.Length} connected computer(s)?\n\nStudents will download the installer, update silently in the background, and reconnect automatically.","Remote Auto-Update",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
        try
        {
            var fileId="update-"+Guid.NewGuid().ToString("N");
            var fileName=Path.GetFileName(installerPath);
            string hash;
            using(var fs=File.OpenRead(installerPath))hash=Convert.ToHexString(SHA256.HashData(fs));
            FileStore.Register(fileId,installerPath,fileName,hash);
            var payload=$"{fileId}|{fileName}|{hash}";
            var count=store.Queue(onlineIds,"update-app",payload);
            status.Text=$"Pushed update to {count} online computer(s). They will update and reconnect in ~15 seconds.";
            RefreshRoom();
        }
        catch(Exception ex){Desktop.Error(ex);}
    }
    private void PowerDialog()
    {
        var ids=Selected();
        if(ids.Length==0){MessageBox.Show("Select one or more computers first.\n\nTip: You can select online computers to shut down/restart, or offline computers to send Wake-on-LAN.","Power Controls",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var snapshot=store.Snapshot();
        var selectedDevices=snapshot.Devices.Where(d=>ids.Contains(d.Id)).ToList();
        var onlineCount=selectedDevices.Count(d=>d.Online);
        var offlineCount=selectedDevices.Count-onlineCount;

        using var f=Desktop.Dialog("Power Management",490);var p=Desktop.Stack();f.Controls.Add(p);
        p.Controls.Add(Desktop.Label("⚡ Classroom Power Controls",18,true));
        p.Controls.Add(Desktop.Label($"Selected: {selectedDevices.Count} computer(s)  ({onlineCount} online, {offlineCount} offline)"));

        var buttons=new FlowLayoutPanel{Width=450,Height=260,FlowDirection=FlowDirection.TopDown,WrapContents=false};

        var btnShut=Desktop.Button("🛑 Shut Down Selected Computers…",(_,_)=>
        {
            if(onlineCount==0){MessageBox.Show("No online computers selected to shut down.","Power");return;}
            var choice=MessageBox.Show($"Shut down {onlineCount} online computer(s)?\n\nClick YES to warn students with a 15-second countdown.\nClick NO to shut down immediately (0s).\nClick CANCEL to abort.","Shut Down Computers",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning);
            if(choice==DialogResult.Cancel)return;
            var delay=choice==DialogResult.Yes?"15":"0";
            var count=store.Queue(ids,"shutdown",delay);
            status.Text=$"Shutdown scheduled on {count} computer(s).";RefreshRoom();f.DialogResult=DialogResult.OK;
        });
        btnShut.Width=430;btnShut.Height=36;buttons.Controls.Add(btnShut);

        var btnRestart=Desktop.Button("🔄 Restart Selected Computers…",(_,_)=>
        {
            if(onlineCount==0){MessageBox.Show("No online computers selected to restart.","Power");return;}
            if(MessageBox.Show($"Restart {onlineCount} online computer(s)?\n\nComputers will restart in 5 seconds.","Restart Computers",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            var count=store.Queue(ids,"restart","5");
            status.Text=$"Restart scheduled on {count} computer(s).";RefreshRoom();f.DialogResult=DialogResult.OK;
        });
        btnRestart.Width=430;btnRestart.Height=36;buttons.Controls.Add(btnRestart);

        var btnLogoff=Desktop.Button("👤 Log Off Current Users…",(_,_)=>
        {
            if(onlineCount==0){MessageBox.Show("No online computers selected to log off.","Power");return;}
            if(MessageBox.Show($"Sign out Windows user sessions on {onlineCount} online computer(s)?","Log Off Users",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            var count=store.Queue(ids,"logoff","");
            status.Text=$"Logoff command sent to {count} computer(s).";RefreshRoom();f.DialogResult=DialogResult.OK;
        });
        btnLogoff.Width=430;btnLogoff.Height=36;buttons.Controls.Add(btnLogoff);

        var btnSleep=Desktop.Button("💤 Sleep / Suspend Computers…",(_,_)=>
        {
            if(onlineCount==0){MessageBox.Show("No online computers selected to put to sleep.","Power");return;}
            if(MessageBox.Show($"Put {onlineCount} computer(s) into sleep / standby mode?","Sleep Computers",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            var count=store.Queue(ids,"sleep","");
            status.Text=$"Sleep command sent to {count} computer(s).";RefreshRoom();f.DialogResult=DialogResult.OK;
        });
        btnSleep.Width=430;btnSleep.Height=36;buttons.Controls.Add(btnSleep);

        var btnWake=Desktop.Button("⚡ Wake-on-LAN (Turn On Sleeping/Off PCs)",(_,_)=>
        {
            var sent=0;var noMac=0;
            foreach(var d in selectedDevices)
            {
                if(!string.IsNullOrEmpty(d.Mac)&&NetworkSetup.SendWakeOnLan(d.Mac))sent++;
                else noMac++;
            }
            if(sent>0)
                MessageBox.Show($"Sent Wake-on-LAN magic packet to {sent} computer(s)." + (noMac>0 ? $"\n\n({noMac} computer(s) have no recorded MAC address yet. Connect them once to save their MAC.)" : ""), "Wake-on-LAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Could not send Wake-on-LAN.\nNone of the selected computers have a recorded MAC address yet.\nOnce a student computer connects to the teacher, its MAC address is remembered automatically.","Wake-on-LAN",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            f.DialogResult=DialogResult.OK;
        });
        btnWake.Width=430;btnWake.Height=36;buttons.Controls.Add(btnWake);

        var btnAbort=Desktop.Button("❌ Cancel / Abort Pending Shutdown",(_,_)=>
        {
            var count=store.Queue(ids,"abort-shutdown","");
            status.Text=$"Abort shutdown sent to {count} computer(s).";RefreshRoom();f.DialogResult=DialogResult.OK;
        });
        btnAbort.Width=430;btnAbort.Height=36;buttons.Controls.Add(btnAbort);

        p.Controls.Add(buttons);
        f.ShowDialog(this);
    }
    private void ReverseShareSelected()
    {
        var ids=Selected();
        if(ids.Length==0){MessageBox.Show("Select a connected student computer to reverse share on the teacher monitor or smartboard.","Reverse Share",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        if(ids.Length>1){MessageBox.Show("Please select a single student computer to project on the monitor.","Reverse Share",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        StartReverseShare(ids[0]);
    }
    private void ReverseShareRow(int rowIndex)
    {
        if(rowIndex<0 || rowIndex>=grid.Rows.Count)return;
        var name=grid.Rows[rowIndex].Cells[1].Value as string;
        var snap=store.Snapshot();
        var dev=snap.Devices.FirstOrDefault(d=>d.Name==name);
        if(dev!=null)StartReverseShare(dev.Id);
    }
    private void StartReverseShare(string deviceId)
    {
        var snap=store.Snapshot();
        var dev=snap.Devices.FirstOrDefault(d=>d.Id==deviceId);
        if(dev==null)return;
        if(!dev.Online){MessageBox.Show($"{dev.Name} is currently offline. Turn on or connect the computer to reverse share.","Reverse Share",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        using var f=new ReverseShareForm(dev,store);
        f.ShowDialog(this);
    }
    private void RemoteControlSelected()
    {
        var ids=Selected();
        if(ids.Length==0){MessageBox.Show("Select a connected student computer to control remotely.","Remote Control",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        if(ids.Length>1){MessageBox.Show("Please select a single student computer to control.","Remote Control",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        StartRemoteControl(ids[0]);
    }
    private void StartRemoteControl(string deviceId)
    {
        var snap=store.Snapshot();
        var dev=snap.Devices.FirstOrDefault(d=>d.Id==deviceId);
        if(dev==null)return;
        if(!dev.Online){MessageBox.Show($"{dev.Name} is currently offline. Turn on or connect the computer to control.","Remote Control",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        using var f=new ReverseShareForm(dev,store,startRemoteControl:true);
        f.ShowDialog(this);
    }
    private void ExportProfile()
    {
        var password=Desktop.AskPassword(this,false,store);
        if(password==null)return;

        using var sfd=new SaveFileDialog
        {
            Title="Export Lab Profile Backup (for other laptops/smartboards)",
            Filter="Vishwapremi Lab Profile Backup (*.vpbak)|*.vpbak",
            FileName=$"VishwapremiLab-Profile-{DateTime.Now:yyyyMMdd}.vpbak"
        };
        if(sfd.ShowDialog(this)==DialogResult.OK)
        {
            try
            {
                ProfileMigration.Export(sfd.FileName,password,store);
                MessageBox.Show($"Lab profile exported successfully to:\n{sfd.FileName}\n\nThis single file contains your teacher security certificate and all {store.Snapshot().Devices.Count} computer pairings.\n\nTo use on another teacher laptop or smartboard:\n1. Copy this file to a USB drive.\n2. Open Vishwapremi Teacher on the other computer.\n3. Click '📥 Import Profile' and enter your teacher password. All student computers will connect automatically with zero USB hassle!","Export Complete",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            catch(Exception ex)
            {
                Desktop.Error(ex);
            }
        }
    }
    private void ImportProfile()
    {
        if(MessageBox.Show("Importing a lab profile will replace the current computers and certificate on this computer with the ones from the backup file.\n\nAll existing student computers from that backup will then connect to this laptop automatically.\n\nDo you want to continue?","Import Lab Profile",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)
            return;

        using var ofd=new OpenFileDialog
        {
            Title="Select Vishwapremi Lab Profile Backup (.vpbak)",
            Filter="Vishwapremi Lab Profile Backup (*.vpbak)|*.vpbak"
        };
        if(ofd.ShowDialog(this)==DialogResult.OK)
        {
            using var passDlg=Desktop.Dialog("Enter Backup Password",230);
            var pStack=Desktop.Stack();passDlg.Controls.Add(pStack);
            pStack.Controls.Add(Desktop.Label("Enter Backup Password",14,true));
            pStack.Controls.Add(Desktop.Label("Enter the teacher password used to encrypt this backup:"));
            var passBox=new TextBox{Width=430,UseSystemPasswordChar=true};
            pStack.Controls.Add(passBox);
            var btnOk=Desktop.Button("Restore Profile",(_,_)=>passDlg.DialogResult=DialogResult.OK,true);
            pStack.Controls.Add(btnOk);passDlg.AcceptButton=btnOk;
            if(passDlg.ShowDialog(this)==DialogResult.OK)
            {
                try
                {
                    var res=ProfileMigration.Import(ofd.FileName,passBox.Text);
                    MessageBox.Show($"Lab profile successfully imported!\n\nRestored: {res.DeviceCount} student computer(s)\n\nVishwapremi Teacher will now restart to activate the restored lab identity. Student computers will begin connecting automatically.","Profile Restored",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    Application.Restart();
                    Environment.Exit(0);
                }
                catch(Exception ex)
                {
                    Desktop.Error(ex);
                }
            }
        }
    }
    private void Help()=>MessageBox.Show("REMOTE ASSISTANCE (REMOTE CONTROL)\nClick '🎮 Remote Control' (or toggle it inside Reverse Share) to take direct mouse and keyboard control of a student's computer. Click and type directly on the student's screen to help them fix errors or guide them through software.\n\nLAB PROFILE MIGRATION & BACKUP\nClick '📦 Export Profile' to save your security certificate and all student computer pairings into a single encrypted file (.vpbak) onto a USB drive. On any other teacher laptop or smartboard, click '📥 Import Profile' to restore it. All student computers will immediately connect to the new laptop without needing any new pairing files or USB visits!\n\nREVERSE SHARING (SMARTBOARD / MONITOR PROJECTION)\nClick '📺 Reverse Share' (or double-click any student computer in the list) to project that student's screen in high-definition 1080p onto the teacher monitor or smartboard. Includes Freeze Frame (Space), Fullscreen (F11), and quick PC switching.\n\nPOWER MANAGEMENT & WAKE-ON-LAN\nClick '⚡ Power' to shut down, restart, log off, or suspend selected computers. Use Wake-on-LAN to remotely turn on sleeping or powered-off computers without walking around the lab.\n\nFILE SHARING & AUTO-UPDATE\nClick 'Share File' to send any assignment, PDF or document directly to student Desktops with optional auto-open.\nClick 'Update Student Apps' to push a new version of the Student installer to all connected PCs silently over the network with no USB drive needed.\n\nLIVE SCREEN MONITOR\nClick 'Live Screens' to view live thumbnails of all student computers. Double-click any screen to open a high-resolution live view, lock the computer, or save a screenshot.\n\nINTERNET CONTROL\nBlock Internet cuts all internet access on selected computers. Unblock Internet restores it. Filter Websites lets you blacklist or whitelist specific sites. Clear Filters removes all filtering.\n\nGET STARTED\n1. Allow lab connections once and approve the administrator request.\n2. Add each computer and save its pairing file to a USB drive.\n3. Install the Student app in the student's usual Windows account and import its file. Student finds the correct teacher address automatically.\n4. Select connected computers to start a lesson.\n\nDesigned by Bharat Thapa", "Using your lab",MessageBoxButtons.OK,MessageBoxIcon.Information);
    private void LockApp(){locked=true;Hide();Restore();}
    private void Restore(){if(locked){if(Desktop.AskPassword(null,false,store)==null)return;locked=false;}Show();WindowState=FormWindowState.Normal;Activate();}
    private void Exit(){if(MessageBox.Show("Stop the teacher controller? Student computers will disconnect until you open it again.","Exit controller",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;exiting=true;Close();}
    private sealed record NetworkChoice(string Address,string Adapter)
    {
        public override string ToString()=>$"{Address}  —  {Adapter}";
    }
}
