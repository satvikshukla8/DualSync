# Integrated cable setup

DualSync can download, verify, and open the official **standard VB-CABLE** installer. Driver binaries are fetched on demand from VB-Audio; they are not committed to this repository or bundled in the portable ZIP. DualSync remains MIT-licensed. VB-CABLE is a separate proprietary donationware product by VB-Audio Software under its own terms.

## User flow

1. Stop DualSync playback and choose **Set up audio cable** beside Audio source.
2. If an installed standard cable is detected, choose **Use installed cable**. No installer is launched. Disabled or disconnected cables are reported instead of automatically reinstalled.
3. Otherwise, review the attribution and license links, opt into driver installation, and choose **Install VB-CABLE**.
4. DualSync downloads and extracts the package, checks its reviewed SHA-256, and verifies the Windows signatures on the x64 setup executable and Windows 10/11 driver catalog.
5. Approve Windows' administrator prompt. In VB-Audio's visible setup, choose **Install Driver**. If it offers **Remove Driver**, close it; an existing installation needs review.
6. Restart Windows as recommended by VB-Audio, reopen DualSync, and use the installed cable as the source.
7. For system-wide audio, choose the same cable as Windows' playback output. The setup window has an **Open Windows Sound settings** button. DualSync does not programmatically change the default device; review the output selection after driver installation.

The app needs Internet access for preparation. A failed download, changed package, invalid signature, cancelled download, or denied administrator prompt does not start installation. An installer exit code alone is not proof of installation: the cable must subsequently be detected. The helper does not force a reboot, import publisher certificates, alter driver-signing policy, or automate removal.

## Existing installations and removal

Endpoint detection includes disabled and disconnected render endpoints and recognizes the standard VB-Audio Virtual Cable name. Completely renamed or partially installed devices may not be recognized, so the vendor's visible setup confirmation remains important. Paid extra cables A+B/C+D and Voicemeeter are not offered as install packages.

The driver is shared system software. Removing DualSync does not remove VB-CABLE. If you no longer need the cable for any app, follow VB-Audio's official removal instructions and restart Windows. Review your Windows output selection afterward.

Downloaded archives, extracted files, and the original package license remain in the app's `driver-setup` data subfolder. They can be removed after the vendor installer has closed. They are not part of the audio processing path.

## Reviewed package

- Source: <https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip>
- SHA-256: `B950E39F01AF1D04EA623C8F6D8EB9B6EA5C477C637295FABF20631C85116BFB`
- Executable: `VBCABLE_Setup_x64.exe`
- The reviewed setup executable has a valid VB-Audio publisher signature; the Windows 10/11 catalog has a valid Microsoft Windows Hardware Compatibility signature on the development PC.
- Verification date: 2026-10-28.

The checksum pins reviewed bytes, not an upstream-published checksum. If the vendor changes the archive at the same URL, preparation stops until the manifest is reviewed and updated. Windows independently evaluates driver trust during installation; passing a preparation check is not a promise that every Windows configuration will accept the driver.

## Licensing basis

The publisher's current [distribution terms](https://vb-audio.com/Services/licensing.htm), section “VB-CABLE Distribution with other product,” allow the standard package to accompany an application when its donationware model remains applicable. Required attribution, origin, and the ability to support VB-Audio are exposed in the setup window. The original package readme is preserved and shown after preparation; it remains the vendor's license, not DualSync's MIT license. Professional or organizational deployments must review the vendor's additional licensing conditions. Extra cables A+B/C+D are excluded.

References: [VB-CABLE download and installation](https://vb-audio.com/Cable/), [VB-CABLE manual](https://vb-audio.com/Cable/VBCABLE_ReferenceManual.pdf), and the unmodified `readme.txt` inside the vendor archive.

## Validation

The preparation-only diagnostic command was run against the real official archive and completed download, hash validation, extraction, and native Windows signature verification. It did not launch setup or modify a driver.

```powershell
dotnet run --project tools/DualSync.Diagnostics -- --prepare-cable-only C:\path\to\test-data
```

Automated tests cover standard-cable identification, modified packages, extraction without overwriting existing files, path traversal, original license preservation, and cancellation. Installation, denied UAC prompts, reboot, partially installed devices, and removal still require a clean Windows test machine or VM. The working development PC was not used for a reinstall.
