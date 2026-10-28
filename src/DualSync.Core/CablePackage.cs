using System.IO.Compression;
using System.Security.Cryptography;

namespace DualSync.Core;

public static class CablePackage
{
    public const string DownloadUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
    public const string Sha256 = "B950E39F01AF1D04EA623C8F6D8EB9B6EA5C477C637295FABF20631C85116BFB";
    public const string InstallerName = "VBCABLE_Setup_x64.exe";
    public const long MaxDownloadBytes = 4 * 1024 * 1024;
    public const long MaxExtractedBytes = 32 * 1024 * 1024;
    public static bool IsCableName(string name) => name.Contains("VB-Audio Virtual Cable", StringComparison.OrdinalIgnoreCase);

    public static async Task VerifyHashAsync(string path, string expectedHash, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        var actual = await SHA256.HashDataAsync(stream, token);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHash)))
            throw new InvalidDataException("The VB-CABLE download did not match the reviewed package. Nothing was installed. Use the official website or update DualSync.");
    }

    public static async Task ExtractVerifiedAsync(string archivePath, string destination, string expectedHash, CancellationToken token = default)
    {
        await VerifyHashAsync(archivePath, expectedHash, token);
        using var archive = ZipFile.OpenRead(archivePath);
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (archive.Entries.Count > 128) throw new InvalidDataException("Unexpected driver package contents.");
        long total = 0;
        // Validate the whole archive before writing any entries.
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (entry.FullName.Contains(':') || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The driver package contains an unsafe path.");
            total = checked(total + entry.Length);
            if (total > MaxExtractedBytes) throw new InvalidDataException("The driver package is unexpectedly large.");
        }
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var input = entry.Open();
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, token);
        }
        if (!File.Exists(Path.Combine(root, InstallerName)) || !File.Exists(Path.Combine(root, "readme.txt")))
            throw new InvalidDataException("The VB-CABLE installer or its license is missing.");
    }
}
