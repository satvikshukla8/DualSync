# DualSync

A free, open-source Windows 11 desktop app for playing system audio through two independently tuned speakers.

DualSync is being built for everyday setups such as two Bluetooth speakers with different sound characteristics and playback delays. The interface focuses on independent equalization, simple volume controls, and speaker timing.

**Status: runnable alpha, under hardware validation.** The desktop app and audio engine build successfully, 27 automated tests pass, and a silent two-output WASAPI test passed two start/stop cycles. The portable app launches and its interface has been inspected. The owner reports working Bluetooth playback, EQ, and Windows volume on the test setup. Long-session reliability and acoustic timing still require testing. There is no published GitHub release.

## Included in the alpha

- Optional integrated VB-CABLE setup: automatic download and extraction, signature checks, and the official administrator installer.
- Two independently selected Windows playback devices.
- A separate 10-band equalizer for each speaker, with presets and signal-dependent peak protection.
- Master volume, individual speaker volume, and mute.
- Independent playback delay from 0–500 ms.
- Stereo, left, right, mono, and custom channel mixing.
- One dashboard with profiles, inline left/right routing, and collapsible per-speaker EQ.
- Live queue + added delay = final software buffer, with ±1 ms delay buttons.
- System tray controls and device reconnection handling.
- Optional Windows source-volume control, with a device-specific verification procedure.

Independent Bluetooth devices have their own clocks and buffering. Static delay can reduce a steady timing difference; sample-accurate wireless synchronization is not promised.

## Project structure

```text
DualSync/
├── src/
│   ├── DualSync.Core/          # DSP, bounded buffers, routing, profiles, settings
│   ├── DualSync.Audio/         # WASAPI capture, output streams, device discovery
│   ├── DualSync.Setup/         # Verified cable download and vendor installer launch
│   └── DualSync.App/           # WPF desktop UI, EQ graphs, tray and app lifecycle
├── tests/
│   └── DualSync.Tests/         # Synthetic-audio and configuration tests
├── tools/
│   └── DualSync.Diagnostics/   # Windows endpoint and engine diagnostics
├── docs/
│   ├── architecture.md         # Audio pipeline, module boundaries, open decisions
│   ├── installation.md         # Setup, volume mode, data, removal
│   ├── testing.md              # Automated evidence and hardware procedures
│   └── troubleshooting.md      # Routing, devices, volume, and diagnostics
├── scripts/build.ps1           # Locked restore, build, tests, portable packaging
├── .github/workflows/          # Windows build and private workflow artifacts
├── DualSync.slnx
├── Directory.Build.props
└── global.json
```

## Technology

C# and .NET 10, WPF, and NAudio 3.2 WASAPI APIs. The DSP and settings layer is independent of Windows and the UI. Windows performs shared-mode output format conversion when an endpoint uses a different sample rate or format.

## Audio architecture

```text
Windows playback source
        │
   WASAPI loopback
        │
   48 kHz float stereo
        │
        ├── Buffer A → Routing → EQ → Gain → Delay → Speaker A
        └── Buffer B → Routing → EQ → Gain → Delay → Speaker B
```

The initial backend captures an existing playback endpoint. The source must be different from both output devices to prevent feedback. A physical source may also play its original audio, so a suitable silent source is needed for a two-speaker-only setup.

A dedicated virtual endpoint is under evaluation. Driver licensing, current Windows signing requirements, volume behavior, and redistribution must be verified before choosing a driver. The application will not silently install drivers or change Windows default audio devices.

See [the architecture document](docs/architecture.md) for the decisions and remaining constraints.

## Development

Requirements: Windows 11 and the .NET SDK specified in `global.json`.

```powershell
dotnet restore DualSync.slnx --locked-mode
dotnet build DualSync.slnx -c Release --no-restore
dotnet run --project tests/DualSync.Tests -c Release --no-build
```

Or run `./scripts/build.ps1 -Package` to perform the checks and create a self-contained Windows x64 portable ZIP under `artifacts`. Extract the whole ZIP and run `DualSync.exe`; no separate .NET runtime is required. The CI workflow uses the same script and uploads a workflow artifact without publishing a release.

Read [installation](docs/installation.md) before choosing a source, and use the focused [hardware test checklist](docs/testing.md). Another application holding a Bluetooth endpoint in exclusive mode must release it before DualSync can use it.

## Roadmap

1. Prove capture and simultaneous playback on real devices.
2. Verify independent EQ and audio quality.
3. Verify Windows volume behavior and select a practical virtual-endpoint strategy.
4. Validate delay and channel routing.
5. Finish profiles, tray operation, and reconnection behavior.
6. Improve long-session buffering and drift handling.
7. Package a Windows build and complete release checks.

## Licensing

DualSync's original code is licensed under [MIT](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for the resolved dependencies and bundled runtime licenses. No third-party driver is included in this repository.
