using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vishwapremi;

internal static class Desktop
{
    internal static readonly Color Green = Color.FromArgb(24, 76, 56);
    internal static readonly Color GreenHover = Color.FromArgb(34, 102, 75);
    internal static readonly Color Paper = Color.FromArgb(246, 248, 245);
    internal static readonly Color Ink = Color.FromArgb(28, 44, 35);
    internal static readonly Color Muted = Color.FromArgb(100, 116, 106);
    internal static readonly Color Border = Color.FromArgb(212, 222, 209);
    internal static readonly Color BorderLight = Color.FromArgb(232, 238, 230);
    internal static readonly Color OnlineGreen = Color.FromArgb(22, 138, 78);
    internal static readonly Color OfflineGray = Color.FromArgb(128, 138, 132);
    internal static readonly Color Amber = Color.FromArgb(196, 120, 20);
    internal static readonly Color Danger = Color.FromArgb(184, 46, 46);

    internal static string DataRoot => Environment.GetEnvironmentVariable("VISHWAPREMI_TEST_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VishwapremiLab");
    internal static string Folder(string name) {var path=Path.Combine(DataRoot,name);Directory.CreateDirectory(path);return path;}

    internal static Button Button(string title, EventHandler handler, bool primary = false)
    {
        var b = new Button
        {
            Text = title,
            AutoSize = true,
            MinimumSize = new Size(110, 36),
            Padding = new Padding(12, 5, 12, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Green : Color.White,
            ForeColor = primary ? Color.White : Ink,
            Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 3, 8, 3)
        };
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = primary ? GreenHover : Border;
        b.FlatAppearance.MouseOverBackColor = primary ? GreenHover : Color.FromArgb(238, 244, 236);
        b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(18, 56, 42) : Color.FromArgb(224, 234, 221);
        if (handler != null) b.Click += handler;
        return b;
    }

    internal static Label Label(string text, float size = 10, bool bold = false) => new() { Text = text, AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 6, 0, 6) };

    internal static Form Dialog(string title, int height = 330)
    {
        return new Form { Text = title, ClientSize = new Size(500, height), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Paper, Font = new Font("Segoe UI", 10), Padding = new Padding(24), AutoScaleMode = AutoScaleMode.Dpi };
    }

    internal static FlowLayoutPanel Stack() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    internal static void Error(Exception e) => MessageBox.Show(e.Message, "Vishwapremi Lab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    internal static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    internal static string? AskPassword(IWin32Window? owner, bool create, LabStore store)
    {
        using var f=Dialog(create?"Welcome · Set a teacher password":"Teacher sign-in",create?410:280);
        var stack=Stack();f.Controls.Add(stack);
        stack.Controls.Add(Label(create?"Set up your teacher workspace":"Welcome back",19,true));
        stack.Controls.Add(Label(create?"Use at least 12 characters. Share only with authorized staff.":"Enter the teacher password to open the lab."));
        var password=new TextBox {Width=440,UseSystemPasswordChar=true};stack.Controls.Add(password);
        TextBox? repeat=null;
        if(create) {stack.Controls.Add(Label("Repeat password"));repeat=new TextBox {Width=440,UseSystemPasswordChar=true};stack.Controls.Add(repeat);}
        var error=Label("");error.ForeColor=Color.Maroon;stack.Controls.Add(error);
        var submit=Button(create?"Create workspace":"Sign in",async (_,_)=>
        {
            try
            {
                if(create) {if(password.Text!=repeat!.Text)throw new ArgumentException("Passwords do not match.");store.SetPassword(password.Text);}
                else if(!store.CheckPassword(password.Text)) {await Task.Delay(1000);error.Text="Incorrect password. Please try again.";return;}
                f.DialogResult=DialogResult.OK;
            }
            catch(Exception e){error.Text=e.Message;}
        },true);
        stack.Controls.Add(submit);f.AcceptButton=submit;
#if TEACHER
        if(create)
        {
            var restoreBtn = Button("📥 Restore from backup file (.vpbak)…", (_, _) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select Vishwapremi Lab Profile Backup (.vpbak)",
                    Filter = "Vishwapremi Lab Profile Backup (*.vpbak)|*.vpbak"
                };
                if (ofd.ShowDialog(f) == DialogResult.OK)
                {
                    using var passDlg = Dialog("Enter Backup Password", 230);
                    var pStack = Stack(); passDlg.Controls.Add(pStack);
                    pStack.Controls.Add(Label("Enter Backup Password", 14, true));
                    pStack.Controls.Add(Label("Enter the teacher password for this backup:"));
                    var passBox = new TextBox { Width = 430, UseSystemPasswordChar = true };
                    pStack.Controls.Add(passBox);
                    var btnOk = Button("Restore Profile", (_, _) => passDlg.DialogResult = DialogResult.OK, true);
                    pStack.Controls.Add(btnOk); passDlg.AcceptButton = btnOk;
                    if (passDlg.ShowDialog(f) == DialogResult.OK)
                    {
                        try
                        {
                            var res = ProfileMigration.Import(ofd.FileName, passBox.Text);
                            MessageBox.Show($"Profile restored successfully!\nRestored {res.DeviceCount} computer(s).\n\nThe teacher workspace will now restart.", "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Application.Restart();
                            Environment.Exit(0);
                        }
                        catch (Exception ex)
                        {
                            Desktop.Error(ex);
                        }
                    }
                }
            });
            restoreBtn.Margin = new Padding(0, 10, 0, 0);
            stack.Controls.Add(restoreBtn);
        }
#endif
        return f.ShowDialog(owner)==DialogResult.OK?password.Text:null;
    }
}

// Windows DPAPI binds stored student credentials and teacher certificate to this Windows user.
internal static class Vault
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob {public int Size; public IntPtr Data;}
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
    private static byte[] Convert(byte[] value,bool protect)
    {
        var input=new Blob {Size=value.Length,Data=Marshal.AllocHGlobal(value.Length)};
        Marshal.Copy(value,0,input.Data,value.Length);
        try
        {
            Blob output;
            var ok=protect?CryptProtectData(ref input,"Vishwapremi Lab",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
            finally {LocalFree(output.Data);}
        }
        finally {Marshal.FreeHGlobal(input.Data);}
    }
    internal static void Save(string path,byte[] value) {var tmp=path+".new";File.WriteAllBytes(tmp,Convert(value,true));File.Move(tmp,path,true);}
    internal static byte[] Read(string path)=>Convert(File.ReadAllBytes(path),false);
}

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>Desktop.Error(e.Exception);
        try
        {
#if TEACHER
            const string role="Teacher";
            if(args.Contains("--configure-firewall"))
            {
                Environment.ExitCode=NetworkSetup.ConfigureFirewall(Environment.ProcessPath ?? "");
                return;
            }
#else
            const string role="Student";
            if(args.Contains("--register-task"))
            {
                Environment.ExitCode=NetworkSetup.RegisterElevatedTask(Environment.ProcessPath ?? "");
                return;
            }
#endif
            using var singleton=new Mutex(true,@"Local\VishwapremiLab-"+role,out var first);
            if(!first) {if(!args.Contains("--startup")) MessageBox.Show("Vishwapremi "+role+" is already running. Open it from the notification area beside the Windows clock.","Vishwapremi Lab");return;}
#if TEACHER
            var store=new LabStore(Path.Combine(Desktop.Folder("Teacher"),"school.json"));
            if(Desktop.AskPassword(null,!store.HasPassword,store)==null)return;
            Application.Run(new TeacherForm(store));
#else
            Application.Run(new StudentForm(args.Contains("--startup")));
#endif
        }
        catch(Exception e) {Desktop.Error(new Exception("The application could not start. "+e.Message+"\nYour saved configuration has not been reset.",e));}
    }
}
