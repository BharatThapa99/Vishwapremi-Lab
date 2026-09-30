using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vishwapremi;

internal sealed class Controller : IAsyncDisposable
{
    private WebApplication? app;
    private UdpClient? discovery;
    private CancellationTokenSource? discoveryCancellation;
    private Task? discoveryTask;
    private readonly X509Certificate2 certificate;
    private readonly int controllerPort,discoveryPort;
    public string Fingerprint=>Convert.ToHexString(SHA256.HashData(certificate.RawData));
    public Controller(int controllerPort=NetworkSetup.ControllerPort,int discoveryPort=NetworkSetup.DiscoveryPort)
    {
        this.controllerPort=controllerPort;this.discoveryPort=discoveryPort;
        var path=Path.Combine(Desktop.Folder("Teacher"),"identity.bin");
        if(File.Exists(path))certificate=X509CertificateLoader.LoadPkcs12(Vault.Read(path),null,X509KeyStorageFlags.UserKeySet);
        else
        {
            using var rsa=RSA.Create(3072);
            var request=new CertificateRequest("CN=Vishwapremi Lab Controller",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new Oid("1.3.6.1.5.5.7.3.1")},false));
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(5));
            var bytes=cert.Export(X509ContentType.Pfx);Vault.Save(path,bytes);
            certificate=X509CertificateLoader.LoadPkcs12(bytes,null,X509KeyStorageFlags.UserKeySet);
        }
    }
    public async Task Start(LabStore store)
    {
        var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions {Args=[]});
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options=>
        {
            options.Limits.MaxRequestBodySize=1048576;options.Limits.MaxConcurrentConnections=64;
            options.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);
            options.Limits.KeepAliveTimeout=TimeSpan.FromMinutes(10);
            options.Limits.MinResponseDataRate=null;
            options.Limits.MinRequestBodyDataRate=null;
            options.ListenAnyIP(controllerPort,listen=>listen.UseHttps(certificate));
        });
        app=builder.Build();
        app.MapPost("/enroll",(EnrollmentRequest request)=>
        {
            if(string.IsNullOrEmpty(request.Id)||string.IsNullOrEmpty(request.Token))return Results.Unauthorized();
            var token=store.Enroll(request.Id,request.Token);
            return token==null?Results.Unauthorized():Results.Ok(new EnrollmentResponse(token));
        });
        app.MapPost("/poll",(HttpContext context,PollRequest request)=>
        {
            var id=context.Request.Headers["X-Device"].ToString();
            var auth=context.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.Ordinal))return Results.Unauthorized();
            var reply=store.Poll(id,auth[7..],request);
            if(reply==null)return Results.Unauthorized();
            var screenMode=ScreenStore.ReverseShareDeviceId == id ? 3 : (ScreenStore.IsMonitoringActive ? (ScreenStore.FullViewDeviceId == id ? 2 : 1) : 0);
            var remoteControl=ScreenStore.RemoteControlDeviceId == id;
            return Results.Ok(new PollResponse(reply.Commands, screenMode, remoteControl));
        });
        app.MapPost("/screen",async (HttpContext context)=>
        {
            var id=context.Request.Headers["X-Device"].ToString();
            var auth=context.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.Ordinal))return Results.Unauthorized();
            if(!store.CheckDeviceToken(id,auth[7..]))return Results.Unauthorized();
            using var ms=new MemoryStream();
            await context.Request.Body.CopyToAsync(ms);
            var bytes=ms.ToArray();
            if(bytes.Length>0 && bytes.Length<=4194304)ScreenStore.Update(id,bytes);
            return Results.Ok();
        });
        app.MapPost("/input",async (HttpContext context)=>
        {
            var id=context.Request.Headers["X-Device"].ToString();
            var auth=context.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.Ordinal))return Results.Unauthorized();
            if(!store.CheckDeviceToken(id,auth[7..]))return Results.Unauthorized();
            var events=await InputStore.WaitForInputAsync(id,TimeSpan.FromSeconds(2));
            return Results.Ok(events);
        });
        app.MapGet("/file/{id}",(HttpContext context,string id)=>
        {
            var devId=context.Request.Headers["X-Device"].ToString();
            var auth=context.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.Ordinal))return Results.Unauthorized();
            if(!store.CheckDeviceToken(devId,auth[7..]))return Results.Unauthorized();
            if(!FileStore.TryGet(id,out var file)||!File.Exists(file.FilePath))return Results.NotFound();
            var stream=new FileStream(file.FilePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,81920,useAsync:true);
            return Results.Stream(stream,"application/octet-stream",file.Name,enableRangeProcessing:true);
        });
        await app.StartAsync();
        discoveryCancellation=new CancellationTokenSource();
        discovery=new UdpClient(new IPEndPoint(IPAddress.Any,discoveryPort));
        discovery.EnableBroadcast=true;
        discoveryTask=DiscoveryLoop(discoveryCancellation.Token);
    }
    private async Task DiscoveryLoop(CancellationToken cancellation)
    {
        try
        {
            while(!cancellation.IsCancellationRequested)
            {
                UdpReceiveResult received;
                try { received=await discovery!.ReceiveAsync(cancellation); }
                catch(SocketException) { continue; }

                var text=Encoding.ASCII.GetString(received.Buffer);
                var parts=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length!=2 || parts[0]!="VPLAB_DISCOVER_V1" || parts[1].Length!=32 || !parts[1].All(Uri.IsHexDigit))continue;
                var response=Encoding.ASCII.GetBytes($"VPLAB_TEACHER_V1 {parts[1]} {Fingerprint} {controllerPort}");
                try { await discovery.SendAsync(response,received.RemoteEndPoint,cancellation); }
                catch(SocketException) { }
            }
        }
        catch(OperationCanceledException){}
        catch(ObjectDisposedException){}
    }
    public async ValueTask DisposeAsync()
    {
        if(discoveryCancellation!=null)discoveryCancellation.Cancel();
        discovery?.Dispose();
        if(discoveryTask!=null)try{await discoveryTask;}catch(Exception){}
        discoveryCancellation?.Dispose();
        if(app!=null){await app.StopAsync();await app.DisposeAsync();}
        certificate.Dispose();
        ScreenStore.Clear();
        FileStore.Clear();
    }
}

internal static class ScreenStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Data, DateTime Updated)> screens = new();
    public static volatile bool IsMonitoringActive = false;
    public static volatile string FullViewDeviceId = "";
    public static volatile string ReverseShareDeviceId = "";
    public static volatile string RemoteControlDeviceId = "";
    public static void Update(string id, byte[] data) => screens[id] = (data, DateTime.UtcNow);
    public static byte[]? Get(string id) => screens.TryGetValue(id, out var item) && item.Updated > DateTime.UtcNow.AddSeconds(-20) ? item.Data : null;
    public static DateTime? GetUpdated(string id) => screens.TryGetValue(id, out var item) ? item.Updated : null;
    public static void Clear() { RemoteControlDeviceId = ""; ReverseShareDeviceId = ""; FullViewDeviceId = ""; IsMonitoringActive = false; screens.Clear(); }
}

internal static class InputStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentQueue<InputEvent>> queues = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<bool>> waiters = new();

    public static void Enqueue(string deviceId, InputEvent ev)
    {
        var q = queues.GetOrAdd(deviceId, _ => new System.Collections.Concurrent.ConcurrentQueue<InputEvent>());
        q.Enqueue(ev);
        if (waiters.TryRemove(deviceId, out var tcs))
        {
            tcs.TrySetResult(true);
        }
    }

    public static async Task<List<InputEvent>> WaitForInputAsync(string deviceId, TimeSpan timeout)
    {
        var q = queues.GetOrAdd(deviceId, _ => new System.Collections.Concurrent.ConcurrentQueue<InputEvent>());
        if (!q.IsEmpty)
        {
            return DequeueAll(q);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiters[deviceId] = tcs;

        using var cts = new CancellationTokenSource(timeout);
        using (cts.Token.Register(() => {
            if (waiters.TryGetValue(deviceId, out var w) && w == tcs)
                waiters.TryRemove(deviceId, out _);
            tcs.TrySetResult(false);
        }))
        {
            await tcs.Task;
        }

        return DequeueAll(q);
    }

    private static List<InputEvent> DequeueAll(System.Collections.Concurrent.ConcurrentQueue<InputEvent> q)
    {
        var list = new List<InputEvent>();
        while (q.TryDequeue(out var ev) && list.Count < 50)
        {
            list.Add(ev);
        }
        return list;
    }

    public static void Clear(string deviceId)
    {
        if (queues.TryRemove(deviceId, out var q))
        {
            while (q.TryDequeue(out _)) { }
        }
        if (waiters.TryRemove(deviceId, out var tcs))
        {
            tcs.TrySetResult(false);
        }
    }
}

internal static class FileStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string FilePath, string Name, string Hash)> files = new();
    public static void Register(string id, string filePath, string name, string hash) => files[id] = (filePath, name, hash);
    public static bool TryGet(string id, out (string FilePath, string Name, string Hash) file) => files.TryGetValue(id, out file);
    public static void Remove(string id) => files.TryRemove(id, out _);
    public static void Clear() => files.Clear();
}
