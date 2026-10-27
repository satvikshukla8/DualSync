# DualSync

A free, open-source Windows 11 desktop app for playing system audio through two independently tuned speakers.

DualSync is being built for everyday setups such as two Bluetooth speakers with different sound characteristics and playback delays. The interface focuses on independent equalization, simple volume controls, and speaker timing.

**Status: early development.** This first repository checkpoint contains the project structure, the core DSP and settings implementation, a compiling WASAPI audio library, and the desktop interface under construction. The full desktop application has not yet been built or tested on physical Bluetooth speakers. There is no release download yet.

## Planned capabilities

- Two independently selected Windows playback devices.
- A separate 10-band equalizer for each speaker, with presets and automatic headroom.
- Master volume, individual speaker volume, and mute.
- Independent playback delay from 0–500 ms.
- Stereo, left, right, mono, and custom channel mixing.
- Saved profiles, system tray controls, and device reconnection handling.
- Windows volume integration, with endpoint behavior verified before claiming support.

Independent Bluetooth devices have their own clocks and buffering. Static delay can reduce a steady timing difference; sample-accurate wireless synchronization is not promised.

## Project structure

```text
DualSync/
├── src/
│   ├── DualSync.Core/          # DSP, bounded buffers, routing, profiles, settings
│   ├── DualSync.Audio/         # WASAPI capture, output streams, device discovery
│   └── DualSync.App/           # WPF desktop UI, EQ graphs, tray and app lifecycle
├── tests/
│   └── DualSync.Tests/         # Synthetic-audio and configuration tests
├── tools/
│   └── DualSync.Diagnostics/   # Windows endpoint and engine diagnostics
├── docs/
│   └── architecture.md         # Audio pipeline, module boundaries, open decisions
├── scripts/                   # Build and packaging scripts (being added)
├── .github/workflows/          # Windows CI (being added)
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
dotnet build src/DualSync.Audio -c Release --no-restore
```

The audio library compiles at this checkpoint. Desktop startup, automated tests, packaging, and CI are still being connected; building the entire solution is not yet a supported verification step.

## Roadmap

1. Prove capture and simultaneous playback on real devices.
2. Verify independent EQ and audio quality.
3. Verify Windows volume behavior and select a practical virtual-endpoint strategy.
4. Validate delay and channel routing.
5. Finish profiles, tray operation, and reconnection behavior.
6. Improve long-session buffering and drift handling.
7. Package a Windows build and complete release checks.

## Licensing

MIT is the intended license for DualSync's original code. The license file and dependency notices will be added after checking the resolved dependency licenses. No third-party driver is included in this repository.
