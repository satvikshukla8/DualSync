using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using DualSync.Audio;
using DualSync.Core;
using DualSync.Setup;
using NAudio.CoreAudioApi;

namespace DualSync.App;

public partial class CableSetupWindow : Window
{
    private CancellationTokenSource? download;
    private bool busy, vendorRunning, attempted, existingDetected;
    public string? SelectedCableId { get; private set; }

    public CableSetupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await CheckAsync();
        Closing += (_, e) =>
        {
            if (vendorRunning)
            {
                e.Cancel = true;
                SetupStatus.Text = "Finish or close the VB-CABLE setup window before closing this window.";
            }
            else download?.Cancel();
        };
    }
    private async Task CheckAsync()
    {
        if (busy) return;
        busy = true; UpdateButtons();
        try
        {
            var devices = await Task.Run(() => DeviceCatalog.Enumerate(DeviceState.All));
            var cables = devices.Where(d => CablePackage.IsCableName(d.Name)).ToList();
            existingDetected = cables.Count != 0;
            ExistingCable.ItemsSource = cables.Where(d => d.Available).ToList();
            ExistingCable.SelectedIndex = ExistingCable.Items.Count > 0 ? 0 : -1;
            SetupStatus.Text = ExistingCable.Items.Count > 0
                ? attempted
                    ? "VB-CABLE is detected. Restart Windows as recommended by VB-Audio before first use."
                    : "VB-CABLE is already installed. Reuse it below; no driver installation is needed."
                : existingDetected
                    ? "An existing cable is disabled or disconnected. Enable it in Windows Sound settings or restart Windows. DualSync will not reinstall it."
                    : attempted
                        ? "The cable is not available yet. If you completed Install Driver, restart Windows and check again. Otherwise the installation could not be confirmed."
                        : "No standard VB-CABLE endpoint was found. You can install it below or continue using a source you already have.";
        }
        catch (Exception ex)
        {
            existingDetected = true; // Fail closed when the existing-device check is unavailable.
            SetupStatus.Text = "Could not check installed audio devices. Installation is disabled: " + ex.Message;
        }
        finally { busy = false; UpdateButtons(); }
    }
    private void UpdateButtons()
    {
        if (InstallButton is null) return;
        InstallButton.IsEnabled = !busy && !existingDetected && !attempted && Consent.IsChecked == true;
        CheckButton.IsEnabled = !busy;
        UseCableButton.IsEnabled = !busy && ExistingCable.Items.Count > 0;
        Consent.IsEnabled = !busy && !existingDetected && !attempted;
        CancelButton.IsEnabled = busy && !vendorRunning && download is not null;
    }
    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (busy || Consent.IsChecked != true || attempted) return;
        await CheckAsync();
        if (existingDetected) return;
        busy = true;
        download = new CancellationTokenSource(); UpdateButtons();
        try
        {
            var progress = new Progress<string>(message => SetupStatus.Text = message);
            var package = await new CableInstaller().PrepareAsync(MainWindow.DataDirectory, progress, download.Token);
            LicenseText.Text = await File.ReadAllTextAsync(package.LicensePath, Encoding.Latin1, download.Token);
            PackageLicense.Visibility = Visibility.Visible;
            // Another process may have installed it while the download was running.
            var now = await Task.Run(() => DeviceCatalog.Enumerate(DeviceState.All));
            if (now.Any(d => CablePackage.IsCableName(d.Name)))
            {
                SetupStatus.Text = "A cable was detected while preparing setup. Reuse it instead of reinstalling.";
                return;
            }
            download.Token.ThrowIfCancellationRequested();
            vendorRunning = true; UpdateButtons();
            SetupStatus.Text = "Approve the Windows administrator prompt, then choose Install Driver in the official VB-CABLE setup. Do not choose Remove Driver.";
            var code = await CableInstaller.RunVendorSetupAsync(package);
            attempted = true;
            SetupStatus.Text = $"VB-CABLE setup closed (exit code {code}). Restart Windows if you installed the driver, then check again. Installation is confirmed only when a cable is detected.";
        }
        catch (OperationCanceledException) { SetupStatus.Text = "Download cancelled. No installer was started."; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { SetupStatus.Text = "Administrator approval was cancelled. No installer was started."; }
        catch (Exception ex) { SetupStatus.Text = "Cable setup could not finish: " + ex.Message; }
        finally
        {
            vendorRunning = false; busy = false;
            download?.Dispose(); download = null; UpdateButtons();
        }
        if (attempted && IsLoaded) await CheckAsync();
    }
    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync();
    private void Consent_Changed(object sender, RoutedEventArgs e) => UpdateButtons();
    private void Cancel_Click(object sender, RoutedEventArgs e) => download?.Cancel();
    private void UseCable_Click(object sender, RoutedEventArgs e)
    {
        if (ExistingCable.SelectedItem is not AudioDevice device || busy) return;
        SelectedCableId = device.Id; DialogResult = true;
    }
    private static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    private void Website_Click(object sender, RoutedEventArgs e) => Open("https://vb-audio.com/Cable/");
    private void Terms_Click(object sender, RoutedEventArgs e) => Open("https://vb-audio.com/Services/licensing.htm");
    private void Donate_Click(object sender, RoutedEventArgs e) => Open("https://shop.vb-audio.com/en/win-apps/11-vb-cable.html");
    private void SoundSettings_Click(object sender, RoutedEventArgs e) => Open("ms-settings:sound");
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
