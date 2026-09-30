using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Vishwapremi;

public class LabBackup
{
    public int Version { get; set; } = 1;
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");
    public string School { get; set; } = "Shree Vishwapremi Secondary School";
    public string CertificatePfxBase64 { get; set; } = "";
    public SchoolState State { get; set; } = new();
}

internal static class ProfileMigration
{
    private const string Magic = "VPLAB_BAK_V1";

    public static void Export(string targetPath, string password, LabStore store)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Teacher password is required to encrypt the backup.");

        var identityPath = Path.Combine(Desktop.Folder("Teacher"), "identity.bin");
        if (!File.Exists(identityPath))
            throw new FileNotFoundException("Teacher security identity (identity.bin) not found.");

        var pfxBytes = Vault.Read(identityPath);
        var snapshot = store.Snapshot();

        var backup = new LabBackup
        {
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            CertificatePfxBase64 = Convert.ToBase64String(pfxBytes),
            State = snapshot
        };

        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);

        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(backup, Files.Json));
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(fs);
        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(salt);
        writer.Write(iv);
        writer.Write(tag);
        writer.Write(ciphertext.Length);
        writer.Write(ciphertext);
    }

    public static (int DeviceCount, string School) Import(string sourcePath, string password, string? customFolder = null)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required to decrypt the backup file.");

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Backup file not found.");

        using var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        var magicBytes = reader.ReadBytes(12);
        if (magicBytes.Length != 12 || Encoding.ASCII.GetString(magicBytes) != Magic)
            throw new InvalidDataException("This is not a valid Vishwapremi Lab profile backup file (.vpbak).");

        var salt = reader.ReadBytes(16);
        var iv = reader.ReadBytes(12);
        var tag = reader.ReadBytes(16);
        var len = reader.ReadInt32();
        if (len <= 0 || len > 20_000_000)
            throw new InvalidDataException("Invalid or corrupted backup payload.");

        var ciphertext = reader.ReadBytes(len);

        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, 16);
        try
        {
            aes.Decrypt(iv, ciphertext, tag, plaintext);
        }
        catch (CryptographicException)
        {
            throw new UnauthorizedAccessException("Incorrect password for this backup file, or the file is corrupted.");
        }

        var json = Encoding.UTF8.GetString(plaintext);
        var backup = JsonSerializer.Deserialize<LabBackup>(json, Files.Json)
            ?? throw new InvalidDataException("Failed to decode backup data.");

        if (string.IsNullOrEmpty(backup.CertificatePfxBase64))
            throw new InvalidDataException("Backup is missing the teacher certificate identity.");

        var pfxBytes = Convert.FromBase64String(backup.CertificatePfxBase64);
        using var testCert = X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.UserKeySet);

        var targetFolder = customFolder ?? Desktop.Folder("Teacher");
        Directory.CreateDirectory(targetFolder);

        // Save certificate using local machine's DPAPI
        var identityPath = Path.Combine(targetFolder, "identity.bin");
        Vault.Save(identityPath, pfxBytes);

        // Save school.json
        var schoolPath = Path.Combine(targetFolder, "school.json");
        Files.Save(schoolPath, backup.State);

        return (backup.State.Devices.Count, backup.School);
    }
}
