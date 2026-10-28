using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using DualSync.Core;

namespace DualSync.Setup;

public sealed record PreparedCable(string Directory, string InstallerPath, string LicensePath);

public sealed class CableInstaller
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(90) };

    // Preparation downloads and validates files only. It never elevates or installs a driver.
    public async Task<PreparedCable> PrepareAsync(string dataDirectory, IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("This DualSync package supports Windows x64. Use VB-Audio's official installer for other architectures.");
        var attempt = Path.Combine(Path.GetFullPath(dataDirectory), "driver-setup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(attempt);
        var archive = Path.Combine(attempt, "VBCABLE_Driver_Pack45.zip");
        progress?.Report("Downloading the official VB-CABLE package…");
        using (var response = await Client.GetAsync(CablePackage.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > CablePackage.MaxDownloadBytes)
                throw new InvalidDataException("The driver download is unexpectedly large.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920]; long size = 0; int read;
            while ((read = await input.ReadAsync(buffer, token)) != 0)
            {
                size += read;
                if (size > CablePackage.MaxDownloadBytes) throw new InvalidDataException("The driver download is unexpectedly large.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }
        progress?.Report("Checking package integrity and Windows signatures…");
        var extracted = Path.Combine(attempt, "package");
        await CablePackage.ExtractVerifiedAsync(archive, extracted, CablePackage.Sha256, token);
        var installer = Path.Combine(extracted, CablePackage.InstallerName);
        await Task.Run(() =>
        {
            WindowsSignature.Verify(installer);
            WindowsSignature.Verify(Path.Combine(extracted, "vbaudio_cable64_win10.cat"));
        }, token);
        token.ThrowIfCancellationRequested();
        return new(extracted, installer, Path.Combine(extracted, "readme.txt"));
    }

    // The app calls this only after explicit consent and a fresh existing-device check.
    // The vendor's visible setup retains its Install Driver/license confirmations.
    public static async Task<int> RunVendorSetupAsync(PreparedCable package)
    {
        await Task.Run(() => WindowsSignature.Verify(package.InstallerPath));
        using var process = Process.Start(new ProcessStartInfo(package.InstallerPath)
        {
            UseShellExecute = true, Verb = "runas", WorkingDirectory = package.Directory
        }) ?? throw new InvalidOperationException("Windows did not start the VB-CABLE installer.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
