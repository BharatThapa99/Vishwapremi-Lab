using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Vishwapremi;

internal static class NetworkSetup
{
    internal const int DiscoveryPort = 8765;
    internal const int ControllerPort = 8766;

    internal static IReadOnlyList<(IPAddress Address, int PrefixLength, string Adapter)> ActiveIPv4()
    {
        var preferred = new List<(IPAddress, int, string)>();
        var fallback = new List<(IPAddress, int, string)>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            IPInterfaceProperties properties;
            try { properties = nic.GetIPProperties(); }
            catch (NetworkInformationException) { continue; }

            var hasGateway=properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

            foreach (var address in properties.UnicastAddresses)
                if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address) && address.DuplicateAddressDetectionState != DuplicateAddressDetectionState.Duplicate)
                {
                    var entry=(address.Address,address.PrefixLength,nic.Name);
                    fallback.Add(entry);
                    if(hasGateway)preferred.Add(entry);
                }
        }
        return (preferred.Count>0?preferred:fallback).DistinctBy(x => x.Item1).ToList();
    }

    internal static IPAddress Broadcast(IPAddress address, int prefixLength)
    {
        var bytes = address.GetAddressBytes();
        var value = BitConverter.ToUInt32(bytes.Reverse().ToArray());
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var broadcast = value | ~mask;
        return new IPAddress(BitConverter.GetBytes(broadcast).Reverse().ToArray());
    }

    internal static async Task<string?> DiscoverTeacher(Pairing pairing,IEnumerable<IPEndPoint>? testTargets=null,TimeSpan? timeout=null)
    {
        byte[] fingerprint;
        try{fingerprint=Convert.FromHexString(pairing.Certificate);}
        catch(FormatException){return null;}
        if(fingerprint.Length!=32)return null;

        var nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var request=Encoding.ASCII.GetBytes($"VPLAB_DISCOVER_V1 {nonce}");
        var targets=testTargets?.ToList() ?? ActiveIPv4()
            .Select(x=>new IPEndPoint(Broadcast(x.Address,x.PrefixLength),DiscoveryPort))
            .Append(new IPEndPoint(IPAddress.Broadcast,DiscoveryPort)).DistinctBy(x=>x.Address).ToList();
        if(testTargets==null)targets.Add(new IPEndPoint(IPAddress.Loopback,DiscoveryPort));

        using var udp=new UdpClient(new IPEndPoint(IPAddress.Any,0)){EnableBroadcast=true};
        foreach(var target in targets)try{await udp.SendAsync(request,target);}catch(SocketException){}
        using var cancellation=new CancellationTokenSource(timeout??TimeSpan.FromSeconds(2.5));
        while(!cancellation.IsCancellationRequested)
        {
            UdpReceiveResult response;
            try{response=await udp.ReceiveAsync(cancellation.Token);}
            catch(OperationCanceledException){break;}
            catch(SocketException){continue;}
            var text=Encoding.ASCII.GetString(response.Buffer);
            var parts=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length!=4 || parts[0]!="VPLAB_TEACHER_V1" || parts[1]!=nonce || !int.TryParse(parts[3],out var port) || port is <1 or >65535)continue;
            try
            {
                var returned=Convert.FromHexString(parts[2]);
                if(returned.Length==32 && CryptographicOperations.FixedTimeEquals(fingerprint,returned))
                    return $"https://{response.RemoteEndPoint.Address}:{port}";
            }
            catch(FormatException){}
        }
        return null;
    }

    internal static int ConfigureFirewall(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) return 2;
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Teacher TCP");
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Teacher Discovery");
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Teacher");
        var tcp = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Teacher TCP", "dir=in", "action=allow", "protocol=TCP", $"localport={ControllerPort}", "profile=any", "remoteip=localsubnet", $"program={executable}", "enable=yes");
        var udp = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Teacher Discovery", "dir=in", "action=allow", "protocol=UDP", $"localport={DiscoveryPort}", "profile=any", "remoteip=localsubnet", $"program={executable}", "enable=yes");
        return tcp == 0 && udp == 0 ? 0 : 1;
    }

    internal static int BlockInternet(string teacherIp)
    {
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Student");
        var r1 = RunNetsh("advfirewall", "set", "allprofiles", "firewallpolicy", "blockinbound,blockoutbound");
        var r2 = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student", "dir=out", "action=allow", "remoteip=127.0.0.1", "enable=yes");
        var r3 = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student", "dir=out", "action=allow", $"remoteip={teacherIp}", "enable=yes");
        var r4 = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student", "dir=out", "action=allow", "remoteip=localsubnet", "enable=yes");
        var r5 = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student", "dir=out", "action=allow", "protocol=UDP", "remoteport=67,68", "enable=yes");
        return r1 == 0 && r2 == 0 && r3 == 0 && r4 == 0 && r5 == 0 ? 0 : 1;
    }

    internal static int AllowInternet()
    {
        var r1 = RunNetsh("advfirewall", "set", "allprofiles", "firewallpolicy", "blockinbound,allowoutbound");
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Student");
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Student Whitelist");
        return r1 == 0 ? 0 : 1;
    }

    internal static void ApplyHostsBlock(List<string> domains)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        var start = lines.FindIndex(x => x.Trim() == "# BEGIN VISHWAPREMI LAB");
        var end = lines.FindIndex(x => x.Trim() == "# END VISHWAPREMI LAB");
        if (start >= 0 && end >= 0 && end > start) lines.RemoveRange(start, end - start + 1);
        lines.Add("# BEGIN VISHWAPREMI LAB");
        foreach (var d in domains)
        {
            lines.Add($"127.0.0.1 {d}");
            if (!d.StartsWith("www.")) lines.Add($"127.0.0.1 www.{d}");
        }
        lines.Add("# END VISHWAPREMI LAB");
        File.WriteAllLines(path, lines);
        Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit();
    }

    internal static void ClearHostsBlock()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path).ToList();
        var start = lines.FindIndex(x => x.Trim() == "# BEGIN VISHWAPREMI LAB");
        var end = lines.FindIndex(x => x.Trim() == "# END VISHWAPREMI LAB");
        if (start >= 0 && end >= 0 && end > start) { lines.RemoveRange(start, end - start + 1); File.WriteAllLines(path, lines); }
        Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit();
    }

    internal static int ApplyWhitelist(string teacherIp, List<string> domains)
    {
        if (BlockInternet(teacherIp) != 0) return 1;
        RunNetsh("advfirewall", "firewall", "delete", "rule", "name=Vishwapremi Lab Student Whitelist");
        var res1 = RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student Whitelist", "dir=out", "action=allow", "protocol=UDP", "remoteport=53", "enable=yes");
        var ips = new HashSet<string>();
        foreach (var d in domains)
        {
            try { foreach (var ip in Dns.GetHostAddresses(d)) ips.Add(ip.ToString()); } catch { }
        }
        var success = res1 == 0;
        foreach (var ip in ips)
        {
            if (RunNetsh("advfirewall", "firewall", "add", "rule", "name=Vishwapremi Lab Student Whitelist", "dir=out", "action=allow", "protocol=TCP", "remoteport=80,443", $"remoteip={ip}", "enable=yes") != 0) success = false;
        }
        return success ? 0 : 1;
    }

    internal static int RegisterElevatedTask(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) return 2;
        var info = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add("/create");
        info.ArgumentList.Add("/tn");
        info.ArgumentList.Add("VishwapremiStudent");
        info.ArgumentList.Add("/tr");
        info.ArgumentList.Add($"\"{executable}\" --startup");
        info.ArgumentList.Add("/sc");
        info.ArgumentList.Add("onlogon");
        info.ArgumentList.Add("/rl");
        info.ArgumentList.Add("HIGHEST");
        info.ArgumentList.Add("/f");
        using var process = Process.Start(info);
        if (process == null) return 1;
        process.WaitForExit();
        try { Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)?.DeleteValue("VishwapremiStudent", false); } catch {}

        var wInfo = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        wInfo.ArgumentList.Add("/create");
        wInfo.ArgumentList.Add("/tn");
        wInfo.ArgumentList.Add("VishwapremiWatchdog");
        wInfo.ArgumentList.Add("/tr");
        wInfo.ArgumentList.Add($"\"{executable}\" --startup");
        wInfo.ArgumentList.Add("/sc");
        wInfo.ArgumentList.Add("minute");
        wInfo.ArgumentList.Add("/mo");
        wInfo.ArgumentList.Add("2");
        wInfo.ArgumentList.Add("/rl");
        wInfo.ArgumentList.Add("HIGHEST");
        wInfo.ArgumentList.Add("/f");
        try { using var wProc = Process.Start(wInfo); wProc?.WaitForExit(); } catch { }

        return process.ExitCode;
    }

    internal static int UnregisterElevatedTask()
    {
        var info = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add("/delete");
        info.ArgumentList.Add("/tn");
        info.ArgumentList.Add("VishwapremiStudent");
        info.ArgumentList.Add("/f");
        using var process = Process.Start(info);
        process?.WaitForExit();

        var wInfo = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        wInfo.ArgumentList.Add("/delete");
        wInfo.ArgumentList.Add("/tn");
        wInfo.ArgumentList.Add("VishwapremiWatchdog");
        wInfo.ArgumentList.Add("/f");
        try { using var wProc = Process.Start(wInfo); wProc?.WaitForExit(); } catch { }

        return process?.ExitCode ?? 0;
    }

    internal static bool SendWakeOnLan(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return false;
        var clean = new string(macAddress.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length != 12) return false;
        var mac = Convert.FromHexString(clean);
        var packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);
        for (int i = 0; i < 16; i++)
            Buffer.BlockCopy(mac, 0, packet, 6 + i * 6, 6);

        using var udp = new UdpClient { EnableBroadcast = true };
        try { udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9)); } catch {}
        try { udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7)); } catch {}

        foreach (var (addr, prefix, _) in ActiveIPv4())
        {
            try
            {
                var subnetBroadcast = Broadcast(addr, prefix);
                udp.Send(packet, packet.Length, new IPEndPoint(subnetBroadcast, 9));
                udp.Send(packet, packet.Length, new IPEndPoint(subnetBroadcast, 7));
            }
            catch {}
        }
        return true;
    }

    internal static string GetMacAddress()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                {
                    var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 6 && !bytes.All(b => b == 0))
                        return string.Join("-", bytes.Select(b => b.ToString("X2")));
                }
            }
        }
        catch {}
        return "";
    }

    private static int RunNetsh(params string[] arguments)
    {
        var info = new ProcessStartInfo("netsh.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info);
        if (process == null) return 1;
        process.WaitForExit();
        return process.ExitCode;
    }
}
