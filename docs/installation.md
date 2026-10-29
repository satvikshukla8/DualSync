# Install and set up the alpha

## Portable build

Unzip the entire portable package to a writable folder and run `DualSync.exe`. Keep its DLLs and runtime files together. The package includes its .NET runtime and does not need an installer, an administrator account, or a separate runtime installation.

If you need a virtual source, choose **Set up audio cable** on the dashboard. DualSync can download and open the official VB-CABLE installer after you opt in. That separate driver installation requires Windows administrator approval and may require a restart. Existing cables are reused. See [integrated cable setup](cable-setup.md) for licensing, verification, and removal details.

The alpha executable is not code signed. Distribution signing and installer design remain release tasks. Do not disable Windows security controls to run the app.

## Choose an audio source

DualSync currently captures an existing Windows playback endpoint. It does not create its own virtual endpoint. Pick a source that receives the audio you want to hear, then select two different physical output devices. The source must be different from both speakers.

A source with audible speakers will continue playing the original stream. Use an existing virtual endpoint or another source whose physical output is silent independently of the Windows endpoint volume. Muting a source endpoint can also silence loopback capture.

The owner has reported successful playback and Windows-volume control using an existing VB-Audio Virtual Cable. The cable is a separately licensed external component and is not required to build DualSync. The setup helper fetches it on demand; the driver is not bundled in the portable app.

Before changing a Windows default playback device, note the current device name. If you choose to route system audio into a different source, make that change yourself in Windows Sound settings. DualSync neither makes that change nor assumes permission to make it. Close or stop any other software that is independently forwarding the same source to the same outputs, to avoid hearing two streams.

## First playback test

1. Start with moderate physical speaker volume.
2. In Dashboard, select the source and the two speakers.
3. Press **Start audio** and play familiar music through the chosen source.
4. Mute each speaker in turn to confirm that its controls act independently.
5. Choose left/right routing on each speaker card. Open its Equalizer dropdown to tune it. Add delay to whichever speaker you hear first, using the slider or ±1 ms buttons.

The profile selector at the top saves and loads the complete two-speaker setup. Open **Manage profiles** for rename, delete, and reset actions. Settings and detailed diagnostics remain on the Settings page.

## Save custom EQ presets

Open a speaker's **Equalizer** dropdown, adjust its bands, and enter a name under **Custom EQ presets**. Press **Save EQ**. The saved curve is available on either speaker: choose it from that speaker's saved EQ list and press **Load EQ**. For example, load a custom Rock curve on A and a custom Pop curve on B.

To save that combination, enter **Indie** in **Dual EQ presets** and press **Save EQ**. Loading Indie later restores the saved A curve to A and the saved B curve to B. The pair contains copies of both curves, so editing or deleting an individual preset does not change it.

EQ presets store all ten bands and each EQ's enabled state. Loading them works during playback and leaves devices, volume, mute, routing, and delay alone. Selecting a saved name does not apply it until you press **Load EQ**. To update a saved preset, select or type its name and press **Replace EQ**; use a new name to keep another version. **Delete** removes the selected saved preset and keeps the current sound. Both libraries persist across restarts and full setup profile changes.

## Read the buffer display

Each speaker shows **queue + added delay = final buffer**. Queue comes from the same live frame count used by diagnostics. The result is a software subtotal, not a measured end-to-end latency: limiter lookahead adds 5 ms, and Windows output buffers and Bluetooth/device latency add further delay. When audio is stopped, the live queue and total show a dash rather than a stale value. Delay-button changes update the subtotal immediately.

If you hear feedback or repeated audio, stop DualSync and check the source and any external routing software. Stop audio before selecting different devices.

## Windows volume mode

The default **Windows source volume** mode follows the source endpoint's Windows volume and mute, preserving individual speaker volume ratios. Legacy alpha 1 App volume profiles migrate to this mode while keeping speaker settings. **App volume** remains available for an independent software master and still respects source mute and zero volume.

The app master changes the source endpoint's Windows volume only when you move the slider in Windows source volume mode while audio is running. Starting the engine reads the current endpoint volume; it does not restore a saved endpoint volume or unmute the endpoint.

The Windows default playback device must be the selected source for keyboard volume keys and the main Windows volume slider to control that endpoint. Leave **Source already applies Windows volume** unchecked for a VB-Cable with native volume bypassed. Check it only when capture already includes source attenuation, to avoid applying volume twice. Do not assume every driver behaves the same way. Verify with [testing.md](testing.md).

## Background operation

Settings offers close-to-tray and open-in-tray options. Tray menus control both speaker volumes, mute, start/stop, and quit. Audio does not start automatically on application launch. During an active playback session, the app retries the selected devices after a disconnection.

**Launch when I sign in** writes an entry named `DualSync` under the current user's Windows Run registry key only when you click the checkbox. Unchecking it removes that entry. Use a stable location for the portable folder before enabling it.

## Data and removal

Settings, profiles, and bounded error logs are stored in `%LOCALAPPDATA%\DualSync`. Use **Open data folder** in Settings to find them. A diagnostic report copies endpoint names, IDs, and settings only when requested.

To remove the app, disable launch at sign-in if enabled, choose **Quit DualSync** in the tray, and remove the portable folder. Remove the data folder separately if you want to discard settings. An optionally installed VB-CABLE driver is shared system software and is not removed with DualSync; follow VB-Audio's removal instructions if you no longer need it.

To restore your previous audio routing, stop DualSync and select the previously noted Windows default playback device. The application itself has no default-device change to undo.

## Build from source

Install the SDK specified in `global.json`, then run:

```powershell
./scripts/build.ps1 -Package
```

The portable folder and ZIP appear under `artifacts`. Local SDK paths can be supplied through `-Dotnet`. Dependencies are pinned by committed lock files. CI runs the same script and uploads a private workflow artifact; it does not publish a GitHub release.
