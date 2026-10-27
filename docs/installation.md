# Install and set up the alpha

## Portable build

Unzip the entire portable package to a writable folder and run `DualSync.exe`. Keep its DLLs and runtime files together. The package includes its .NET runtime and does not need an installer, an administrator account, or a separate runtime installation.

The alpha executable is not code signed. Distribution signing and installer design remain release tasks. Do not disable Windows security controls to run the app.

## Choose an audio source

DualSync currently captures an existing Windows playback endpoint. It does not create its own virtual endpoint. Pick a source that receives the audio you want to hear, then select two different physical output devices. The source must be different from both speakers.

A source with audible speakers will continue playing the original stream. Use an existing virtual endpoint or another source whose physical output is silent independently of the Windows endpoint volume. Muting a source endpoint can also silence loopback capture.

On the development PC, existing VB-Audio Virtual Cable endpoints were detected. They may provide a useful source without running Voicemeeter, but this has not yet been validated. The cable is an external component, is not bundled, and is not required to build DualSync. Its presence does not establish its volume behavior or redistribution rights.

Before changing a Windows default playback device, note the current device name. If you choose to route system audio into a different source, make that change yourself in Windows Sound settings. DualSync neither makes that change nor assumes permission to make it. Close or stop any other software that is independently forwarding the same source to the same outputs, to avoid hearing two streams.

## First playback test

1. Start with moderate physical speaker volume.
2. In Dashboard, select the source and the two speakers.
3. Press **Start audio** and play familiar music through the chosen source.
4. Mute each speaker in turn to confirm that its controls act independently.
5. Check the Equalizer and Routing tabs. Add delay to whichever speaker you hear first.

If you hear feedback or repeated audio, stop DualSync and check the source and any external routing software. Stop audio before selecting different devices.

## Windows volume mode

The default **App volume** mode applies a software master gain to both outputs. It preserves individual speaker volume ratios.

For Windows controls, stop playback, open Settings, and select **Windows source volume**. This explicitly opts the master slider into changing the source endpoint's Windows volume while the engine is running. Starting the engine reads the current endpoint volume; it does not restore a saved endpoint volume or unmute the endpoint.

The Windows default playback device must be the selected source for keyboard volume keys and the main Windows volume slider to control that endpoint. Verify behavior using the procedure in [testing.md](testing.md). The checkbox for loopback volume chooses whether the source volume is already present in captured samples or must also be applied by DualSync. Do not assume that every virtual or physical endpoint behaves the same way.

## Background operation

Settings offers close-to-tray and open-in-tray options. Tray menus control both speaker volumes, mute, start/stop, and quit. Audio does not start automatically on application launch. During an active playback session, the app retries the selected devices after a disconnection.

**Launch when I sign in** writes an entry named `DualSync` under the current user's Windows Run registry key only when you click the checkbox. Unchecking it removes that entry. Use a stable location for the portable folder before enabling it.

## Data and removal

Settings, profiles, and bounded error logs are stored in `%LOCALAPPDATA%\DualSync`. Use **Open data folder** in Settings to find them. A diagnostic report copies endpoint names, IDs, and settings only when requested.

To remove the app, disable launch at sign-in if enabled, choose **Quit DualSync** in the tray, and remove the portable folder. Remove the data folder separately if you want to discard settings. DualSync installs no driver.

To restore your previous audio routing, stop DualSync and select the previously noted Windows default playback device. The application itself has no default-device change to undo.

## Build from source

Install the SDK specified in `global.json`, then run:

```powershell
./scripts/build.ps1 -Package
```

The portable folder and ZIP appear under `artifacts`. Local SDK paths can be supplied through `-Dotnet`. Dependencies are pinned by committed lock files. CI runs the same script and uploads a private workflow artifact; it does not publish a GitHub release.
