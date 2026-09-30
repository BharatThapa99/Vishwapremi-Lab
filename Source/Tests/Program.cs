using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using Vishwapremi;

internal static class Tests
{
    private static int passed;
    static void Check(bool condition,string label){if(!condition)throw new Exception("FAILED: "+label);Console.WriteLine("PASS: "+label);passed++;}
    static void Throws(Action action,string label){try{action();}catch(ArgumentException){Check(true,label);return;}throw new Exception("FAILED: "+label);}
    static int FreeTcpPort(){using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();return ((IPEndPoint)listener.LocalEndpoint).Port;}
    static int FreeUdpPort(){using var socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;}
    [STAThread]
    static int Main(string[] args)
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"test-data",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("VISHWAPREMI_TEST_DATA",root);
        if(args.Contains("--preview") || args.Length==0)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var preview=new LabStore(Path.Combine(root,"preview.json"));preview.SetPassword("Preview-only-password");
            for(var i=1;i<=8;i++){var p=preview.Add("LAB-"+i.ToString("00"),"https://127.0.0.1:8766",new string('A',64));if(i<=5){var token=preview.Enroll(p.Id,p.EnrollmentToken)!;preview.Poll(p.Id,token,new PollRequest("Student","TEST-PC-"+i,[]));}}
            var identity=new Controller();var fingerprint=identity.Fingerprint;identity.DisposeAsync().AsTask().GetAwaiter().GetResult();
            var agentPair=preview.Add("QA-STUDENT","https://127.0.0.1:8766",fingerprint);var agentToken=preview.Enroll(agentPair.Id,agentPair.EnrollmentToken)!;agentPair.EnrollmentToken="";
            Vault.Save(Path.Combine(Desktop.Folder("Student"),"connection.bin"),System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new {Pairing=agentPair,Token=agentToken}));
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"preview-root.txt"),root);
            var queueTimer=new System.Windows.Forms.Timer{Interval=2000};var queued=false;
            queueTimer.Tick+=(_,_)=>{if(!queued&&preview.Snapshot().Devices.Any(d=>d.Id==agentPair.Id&&d.Online)){preview.Queue([agentPair.Id],"message","Connection test passed. This notice was sent by the C# teacher controller over an encrypted local connection.");queued=true;queueTimer.Stop();}};queueTimer.Start();
            Application.Run(new TeacherForm(preview));return 0;
        }
        try {Run(root).GetAwaiter().GetResult();Console.WriteLine($"\n{passed} checks passed.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static async Task Run(string root)
    {
        var path=Path.Combine(root,"school.json");var store=new LabStore(path);
        Check(!store.HasPassword,"fresh installation has no password");
        Throws(()=>store.SetPassword("short"),"reject short password");store.SetPassword("teacher-test-password");
        Check(store.CheckPassword("teacher-test-password")&&!store.CheckPassword("incorrect"),"password verification");
        Throws(()=>store.Add("A","http://example.com",new string('A',64)),"reject unencrypted enrollment endpoint");
        var p=store.Add("LAB-01","https://localhost:8766",new string('A',64));
        Throws(()=>store.Add("lab-01","https://localhost:8766",new string('A',64)),"reject duplicate computer names");
        Check(store.Enroll(p.Id,"wrong")==null,"reject wrong pairing token");
        var token=store.Enroll(p.Id,p.EnrollmentToken)!;Check(token.Length==64,"enrollment issues device credential");
        Check(store.Enroll(p.Id,p.EnrollmentToken)==null,"pairing file can only be used once");
        Check(store.Poll(p.Id,"wrong",new PollRequest("Student","PC",[]))==null,"reject wrong device token");
        Check(store.Queue([p.Id],"message","Hello")==0,"do not queue new actions to offline computers");
        store.Poll(p.Id,token,new PollRequest("Student","PC",[]));Check(store.Snapshot().Devices[0].Online,"authenticated heartbeat marks computer online");
        Throws(()=>store.Queue([p.Id],"shell","cmd.exe"),"reject arbitrary command execution");
        Throws(()=>store.Queue([p.Id],"website","file:///C:/Windows"),"reject local file URLs");
        Throws(()=>store.Queue([p.Id],"website","javascript:alert(1)"),"reject JavaScript URLs");
        Throws(()=>store.Queue([p.Id],"message",""),"reject empty notices");
        Throws(()=>store.Queue([p.Id],"block-internet","extra"),"reject value for block-internet");
        Throws(()=>store.Queue([p.Id],"blacklist","invalid domain with space.com"),"reject invalid domain in blacklist");
        Throws(()=>store.Queue([p.Id],"blacklist","nodotdomain"),"reject domain without dot in blacklist");
        Throws(()=>store.Queue([p.Id],"file",""),"reject empty file payload");
        Throws(()=>store.Queue([p.Id],"file","nopipe"),"reject file payload without delimiter");
        Throws(()=>store.Queue([p.Id],"update-app",""),"reject empty update payload");
        Throws(()=>store.Queue([p.Id],"shutdown","invalid"),"reject non-numeric shutdown delay");
        Throws(()=>store.Queue([p.Id],"shutdown","-5"),"reject negative shutdown delay");
        Throws(()=>store.Queue([p.Id],"logoff","extra"),"reject value for logoff");
        Throws(()=>store.Queue([p.Id],"sleep","extra"),"reject value for sleep");
        Throws(()=>store.Queue([p.Id],"abort-shutdown","extra"),"reject value for abort-shutdown");
        Check(store.Queue([p.Id,p.Id],"message","Lesson starts")==1,"deduplicate target computers");
        var reply=store.Poll(p.Id,token,new PollRequest("Student","PC",[]))!;Check(reply.Commands.Count==1&&reply.Commands[0].Value=="Lesson starts","deliver queued notice");
        var c=reply.Commands[0];
        var other=store.Add("LAB-02","https://localhost:8766",new string('A',64));var otherToken=store.Enroll(other.Id,other.EnrollmentToken)!;
        store.Poll(other.Id,otherToken,new PollRequest("Other","PC2",[new Ack(c.Id,"Executed","forged")]));
        Check(store.Snapshot().Commands[0].Status=="Waiting","one device cannot acknowledge another device's action");
        reply=store.Poll(p.Id,token,new PollRequest("Student","PC",[new Ack(c.Id,"Executed","Displayed")]))!;
        Check(reply.Commands.Count==0&&store.Snapshot().Commands[0].Status=="Executed","acknowledged commands are not resent");
        Check(store.Queue([p.Id],"block-internet","")==1,"queue block-internet action");
        Check(store.Queue([p.Id],"blacklist","youtube.com\nfacebook.com")==1,"queue blacklist action");
        Check(store.Queue([p.Id],"whitelist","wikipedia.org")==1,"queue whitelist action");
        Check(store.Queue([p.Id],"allow-internet","")==1,"queue allow-internet action");
        Check(store.Queue([p.Id],"unblock-internet","")==1,"queue unblock-internet action");
        Check(store.Queue([p.Id],"file","fid|doc.txt|10|0")==1,"queue file transfer action");
        Check(store.Queue([p.Id],"update-app","uid|setup.exe|hash")==1,"queue update action");
        Check(store.Queue([p.Id],"shutdown","15")==1,"queue shutdown action");
        Check(store.Queue([p.Id],"restart","5")==1,"queue restart action");
        Check(store.Queue([p.Id],"logoff","")==1,"queue logoff action");
        Check(store.Queue([p.Id],"sleep","")==1,"queue sleep action");
        Check(store.Queue([p.Id],"abort-shutdown","")==1,"queue abort-shutdown action");
        Check(store.Queue([p.Id],"clear-filter","")==1,"queue clear-filter action");
        var filterReply=store.Poll(p.Id,token,new PollRequest("Student","PC",[]))!;
        Check(filterReply.Commands.Count==13,"deliver all queued actions");
        Check(NetworkSetup.SendWakeOnLan("00-11-22-33-44-55"),"send Wake-on-LAN magic packet");
        Check(!NetworkSetup.SendWakeOnLan("invalid-mac"),"reject invalid MAC for WoL");
        store.Poll(p.Id,token,new PollRequest("Student","PC",[], "AA:BB:CC:DD:EE:FF"));
        Check(store.Snapshot().Devices[0].Mac=="AA:BB:CC:DD:EE:FF","store records device MAC address from poll");
        store.Poll(p.Id,token,new PollRequest("Student","PC",filterReply.Commands.Select(cmd=>new Ack(cmd.Id,"Executed","OK")).ToList()));
        var loaded=new LabStore(path);Check(loaded.CheckPassword("teacher-test-password")&&loaded.Snapshot().Commands[0].Status=="Executed","configuration and history survive restart");
        Check(!loaded.Snapshot().Devices[0].Online,"restart does not falsely report devices online");
        var state=loaded.Snapshot();state.Commands.Add(new Command{Device=p.Id,Action="lock",Created=DateTimeOffset.UtcNow.AddMinutes(-2)});Files.Save(path,state);loaded=new LabStore(path);
        Check(loaded.Poll(p.Id,token,new PollRequest("Student","PC",[]))!.Commands.Count==0,"expired commands are not delivered");
        loaded.Remove(p.Id);Check(loaded.Poll(p.Id,token,new PollRequest("Student","PC",[]))==null,"removing a device revokes access");
        var vault=Path.Combine(root,"secret.bin");Vault.Save(vault,"secret-test"u8.ToArray());Check(Vault.Read(vault).SequenceEqual("secret-test"u8.ToArray())&&!File.ReadAllBytes(vault).SequenceEqual("secret-test"u8.ToArray()),"Windows credential encryption round trip");

        var controllerPort=FreeTcpPort();var discoveryPort=FreeUdpPort();
        await using var controller=new Controller(controllerPort,discoveryPort);await controller.Start(store);
        var discoveryPair=new Pairing{Certificate=controller.Fingerprint,Server="https://192.0.2.1:8766"};
        var discovered=await NetworkSetup.DiscoverTeacher(discoveryPair,[new IPEndPoint(IPAddress.Loopback,discoveryPort)],TimeSpan.FromSeconds(2));
        Check(discovered==$"https://127.0.0.1:{controllerPort}","automatic teacher discovery finds the pinned controller and replaces a wrong address");
        discoveryPair.Certificate=new string('0',64);
        Check(await NetworkSetup.DiscoverTeacher(discoveryPair,[new IPEndPoint(IPAddress.Loopback,discoveryPort)],TimeSpan.FromMilliseconds(300))==null,"discovery rejects a teacher with the wrong secure identity");
        using var handler=new HttpClientHandler{UseProxy=false,ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert!=null&&Convert.ToHexString(SHA256.HashData(cert.RawData))==controller.Fingerprint};
        using var http=new HttpClient(handler){BaseAddress=new Uri($"https://127.0.0.1:{controllerPort}"),Timeout=TimeSpan.FromSeconds(5)};
        var networkPair=store.Add("LAB-NETWORK",$"https://127.0.0.1:{controllerPort}",controller.Fingerprint);
        using var enroll=await http.PostAsJsonAsync("/enroll",new EnrollmentRequest(networkPair.Id,networkPair.EnrollmentToken));
        Check(enroll.IsSuccessStatusCode,"TLS enrollment endpoint");var credentials=(await enroll.Content.ReadFromJsonAsync<EnrollmentResponse>())!;
        using var bad=await http.PostAsJsonAsync("/poll",new PollRequest("Student","PC",[]));Check(bad.StatusCode==HttpStatusCode.Unauthorized,"network poll requires authentication");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",credentials.Token);http.DefaultRequestHeaders.Add("X-Device",networkPair.Id);
        using var poll=await http.PostAsJsonAsync("/poll",new PollRequest("Student","PC",[]));Check(poll.IsSuccessStatusCode,"authenticated TLS heartbeat");
        store.Queue([networkPair.Id],"website","https://example.org/lesson");
        using var delivery=await http.PostAsJsonAsync("/poll",new PollRequest("Student","PC",[]));Check((await delivery.Content.ReadFromJsonAsync<PollResponse>())!.Commands.Single().Action=="website","deliver action over pinned TLS");
        ScreenStore.IsMonitoringActive = true;
        using var pollScreen = await http.PostAsJsonAsync("/poll", new PollRequest("Student", "PC", []));
        var pollScreenResp = await pollScreen.Content.ReadFromJsonAsync<PollResponse>();
        Check(pollScreenResp!.ScreenMode == 1, "poll returns screen mode 1 when monitoring active");
        var fakeJpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        using var screenContent = new ByteArrayContent(fakeJpg);
        screenContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        using var screenMsg = new HttpRequestMessage(HttpMethod.Post, "/screen") { Content = screenContent };
        using var screenResp = await http.SendAsync(screenMsg);
        Check(screenResp.IsSuccessStatusCode, "screen image upload endpoint accepted");
        Check(ScreenStore.Get(networkPair.Id) != null, "screen store holds uploaded screen bytes");
        ScreenStore.ReverseShareDeviceId = networkPair.Id;
        using var pollReverse = await http.PostAsJsonAsync("/poll", new PollRequest("Student", "PC", []));
        var pollReverseResp = await pollReverse.Content.ReadFromJsonAsync<PollResponse>();
        Check(pollReverseResp!.ScreenMode == 3, "poll returns screen mode 3 when reverse sharing active");
        ScreenStore.ReverseShareDeviceId = "";

        ScreenStore.RemoteControlDeviceId = networkPair.Id;
        using var pollRemote = await http.PostAsJsonAsync("/poll", new PollRequest("Student", "PC", []));
        var pollRemoteResp = await pollRemote.Content.ReadFromJsonAsync<PollResponse>();
        Check(pollRemoteResp!.RemoteControl, "poll returns remote control flag when active");
        ScreenStore.RemoteControlDeviceId = "";

        InputStore.Enqueue(networkPair.Id, new InputEvent { Type = "move", X = 0.5, Y = 0.5 });
        using var inputMsg = new HttpRequestMessage(HttpMethod.Post, "/input");
        using var inputResp = await http.SendAsync(inputMsg);
        Check(inputResp.IsSuccessStatusCode, "input endpoint delivers queued events");
        var receivedEvents = await inputResp.Content.ReadFromJsonAsync<List<InputEvent>>();
        Check(receivedEvents != null && receivedEvents.Count == 1 && receivedEvents[0].Type == "move", "received event matches enqueued input");

        ScreenStore.IsMonitoringActive = false;
        var testFilePath = Path.Combine(root, "test-share.txt");
        await File.WriteAllTextAsync(testFilePath, "Hello Vishwapremi Class!");
        FileStore.Register("test-file-id", testFilePath, "test-share.txt", "somehash");
        using var fileReq = new HttpRequestMessage(HttpMethod.Get, "/file/test-file-id");
        using var fileResp = await http.SendAsync(fileReq);
        Check(fileResp.IsSuccessStatusCode, "download shared file over TLS");
        var downloadedText = await fileResp.Content.ReadAsStringAsync();
        Check(downloadedText == "Hello Vishwapremi Class!", "downloaded file content matches");
        using var wrongHandler=new HttpClientHandler{UseProxy=false,ServerCertificateCustomValidationCallback=(_,_,_,_)=>false};using var wrongClient=new HttpClient(wrongHandler){BaseAddress=http.BaseAddress};
        try{await wrongClient.PostAsJsonAsync("/poll",new PollRequest("Student","PC",[]));throw new Exception("Wrong certificate accepted");}catch(HttpRequestException){Check(true,"untrusted TLS certificate rejected");}

        var backupPath = Path.Combine(root, "test-backup.vpbak");
        ProfileMigration.Export(backupPath, "StrongTeacherPassword123!", store);
        Check(File.Exists(backupPath), "export encrypted lab profile backup");

        try
        {
            ProfileMigration.Import(backupPath, "WrongPassword");
            Check(false, "import with wrong password should fail");
        }
        catch (UnauthorizedAccessException)
        {
            Check(true, "reject backup import with wrong password");
        }

        var restoreFolder = Path.Combine(root, "RestoredTeacher");
        var (restoredCount, _) = ProfileMigration.Import(backupPath, "StrongTeacherPassword123!", restoreFolder);
        Check(restoredCount > 0, "import lab profile restores devices");
        Check(File.Exists(Path.Combine(restoreFolder, "identity.bin")), "restored identity certificate file exists");
        Check(File.Exists(Path.Combine(restoreFolder, "school.json")), "restored school database file exists");
    }
}
