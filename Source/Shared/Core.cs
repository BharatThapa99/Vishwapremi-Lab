using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vishwapremi;

public static class Files
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".new";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        File.Move(temp, path, true);
    }
    public static T? Read<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default;
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static bool Matches(string text, string hash) => hash.Length == 64 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(text)), Encoding.UTF8.GetBytes(hash));
    public static bool ValidWebsite(string value) => value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
    public static void ValidateDomainList(string value)
    {
        if (value.Length > 4000) throw new ArgumentException("Domain list is too long (max 4000 characters).");
        foreach (var line in value.Split('\n')) { var t = line.Trim(); if (t.Length == 0) continue; if (t.Length > 253 || !t.Contains('.') || t.Contains(' ')) throw new ArgumentException($"Invalid domain format: {t}"); }
    }
    public static List<string> ParseDomainList(string value) => value.Split('\n').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToList();
    public static void ValidateAction(string action, string value)
    {
        if (action is not ("lock" or "website" or "message" or "block-internet" or "allow-internet" or "unblock-internet" or "blacklist" or "whitelist" or "clear-filter" or "file" or "update-app" or "shutdown" or "restart" or "logoff" or "sleep" or "abort-shutdown")) throw new ArgumentException("This action is not supported.");
        if (action == "website" && !ValidWebsite(value)) throw new ArgumentException("Enter a full http:// or https:// website address.");
        if (action == "message" && (string.IsNullOrWhiteSpace(value) || value.Length > 500)) throw new ArgumentException("Write a notice between 1 and 500 characters.");
        if ((action is "block-internet" or "allow-internet" or "unblock-internet" or "clear-filter" or "logoff" or "sleep" or "abort-shutdown") && value.Length > 0) throw new ArgumentException("This action does not accept a value.");
        if (action is "blacklist" or "whitelist") ValidateDomainList(value);
        if ((action is "file" or "update-app") && (string.IsNullOrWhiteSpace(value) || value.Length > 1000 || !value.Contains('|'))) throw new ArgumentException("Invalid file transfer payload.");
        if (action is "shutdown" or "restart" && value.Length > 0 && (!int.TryParse(value, out var sec) || sec < 0 || sec > 3600)) throw new ArgumentException("Countdown delay must be a number between 0 and 3600 seconds.");
    }
}

public class Pairing
{
    public string School { get; set; } = "Shree Vishwapremi Secondary School";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Server { get; set; } = "";
    public string Certificate { get; set; } = "";
    public string EnrollmentToken { get; set; } = "";
    public DateTimeOffset Expires { get; set; }
}
public class Device
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string EnrollmentHash { get; set; } = "";
    public DateTimeOffset EnrollmentExpires { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset Seen { get; set; }
    public string User { get; set; } = "";
    public string Computer { get; set; } = "";
    public string Mac { get; set; } = "";
    public bool Online => Seen > DateTimeOffset.UtcNow.AddSeconds(-20);
}
public class Command
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Device { get; set; } = "";
    public string Action { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Waiting";
    public string Detail { get; set; } = "";
    public bool Expired => Created < DateTimeOffset.UtcNow.AddSeconds(-60);
}
public record Ack(string Id, string Status, string Detail);
public record EnrollmentRequest(string Id, string Token);
public record EnrollmentResponse(string Token);
public record PollRequest(string User, string Computer, List<Ack> Acks, string Mac = "");
public record PollResponse(List<Command> Commands, int ScreenMode = 0, bool RemoteControl = false);
public class InputEvent
{
    public string Type { get; set; } = "";
    public string Button { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Data { get; set; }
    public string Text { get; set; } = "";
}
public class SchoolState
{
    public string PasswordSalt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public List<Device> Devices { get; set; } = [];
    public List<Command> Commands { get; set; } = [];
}

public sealed class LabStore
{
    private readonly object gate = new();
    private readonly string path;
    private readonly SchoolState state;
    public LabStore(string path)
    {
        this.path = path; state = Files.Read<SchoolState>(path) ?? new();
        foreach (var d in state.Devices) d.Seen = default;
    }
    public bool HasPassword { get { lock(gate) return state.PasswordHash.Length > 0; } }
    public void SetPassword(string password)
    {
        if (password.Length < 12) throw new ArgumentException("Use at least 12 characters.");
        lock(gate)
        {
            state.PasswordSalt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            state.PasswordHash = Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(state.PasswordSalt), 210000, HashAlgorithmName.SHA256, 32));
            Save();
        }
    }
    public bool CheckPassword(string password)
    {
        lock(gate)
        {
            if (!HasPassword) return false;
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(state.PasswordHash), Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(state.PasswordSalt), 210000, HashAlgorithmName.SHA256, 32));
        }
    }
    private void Save() => Files.Save(path, state);
    public SchoolState Snapshot()
    {
        lock(gate) return JsonSerializer.Deserialize<SchoolState>(JsonSerializer.Serialize(state))!;
    }
    public Pairing Add(string name, string server, string certificate)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40) throw new ArgumentException("Use a computer name between 1 and 40 characters.");
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host.Length == 0 || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/") throw new ArgumentException("Enter the teacher computer's network address.");
        lock(gate)
        {
            if (state.Devices.Any(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("That computer name is already enrolled. Choose another name or remove the old entry.");
            var token = Files.Token(); var d = new Device { Name = name, EnrollmentHash = Files.Hash(token), EnrollmentExpires = DateTimeOffset.UtcNow.AddHours(24) };
            state.Devices.Add(d); Save();
            return new Pairing { Id=d.Id, Name=name, Server=server.TrimEnd('/'), Certificate=certificate, EnrollmentToken=token, Expires=d.EnrollmentExpires };
        }
    }
    public string? Enroll(string id, string token)
    {
        lock(gate)
        {
            var d = state.Devices.Find(x => x.Id == id);
            if (d == null || d.EnrollmentExpires < DateTimeOffset.UtcNow || !Files.Matches(token,d.EnrollmentHash)) return null;
            var secret=Files.Token(); d.TokenHash=Files.Hash(secret); d.EnrollmentHash=""; Save(); return secret;
        }
    }
    public bool CheckDeviceToken(string id, string token)
    {
        lock(gate)
        {
            var d = state.Devices.Find(x => x.Id == id);
            return d != null && Files.Matches(token, d.TokenHash);
        }
    }
    public PollResponse? Poll(string id, string token, PollRequest request)
    {
        lock(gate)
        {
            var d=state.Devices.Find(x=>x.Id==id);
            if (d==null || !Files.Matches(token,d.TokenHash)) return null;
            d.Seen=DateTimeOffset.UtcNow; d.User=(request.User??"")[..Math.Min(request.User?.Length??0,80)]; d.Computer=(request.Computer??"")[..Math.Min(request.Computer?.Length??0,80)];
            if (!string.IsNullOrWhiteSpace(request.Mac)) d.Mac = request.Mac[..Math.Min(request.Mac.Length, 32)];
            var changed=true;
            foreach(var ack in (request.Acks??[]).Take(100))
            {
                var cmd=state.Commands.Find(c=>c.Id==ack.Id && c.Device==id && c.Status=="Waiting");
                if(cmd!=null && ack.Status is "Executed" or "Failed") {cmd.Status=ack.Status; cmd.Detail=(ack.Detail??"")[..Math.Min(ack.Detail?.Length??0,200)]; changed=true;}
            }
            if(changed) Save();
            return new PollResponse(state.Commands.Where(c=>c.Device==id && c.Status=="Waiting" && !c.Expired).Select(c=>new Command {Id=c.Id, Device=c.Device, Action=c.Action,Value=c.Value,Created=c.Created}).ToList());
        }
    }
    public int Queue(IEnumerable<string> ids, string action, string value)
    {
        Files.ValidateAction(action,value);
        lock(gate)
        {
            var count=0;
            foreach(var id in ids.Distinct())
                if(state.Devices.Any(d=>d.Id==id && d.Online)) {state.Commands.Add(new Command {Device=id,Action=action,Value=value});count++;}
            if(state.Commands.Count>1000) state.Commands.RemoveAll(c=>c.Created<DateTimeOffset.UtcNow.AddDays(-7) && (c.Status!="Waiting" || c.Expired));
            Save();return count;
        }
    }
    public void Remove(string id) {lock(gate) {state.Devices.RemoveAll(d=>d.Id==id); state.Commands.RemoveAll(c=>c.Device==id); Save();}}
}
