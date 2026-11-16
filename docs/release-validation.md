# Public alpha 7 validation record

Prepared 2026-11-15. This record distinguishes software checks from physical-device acceptance.

| Check | Evidence or next action |
| --- | --- |
| Build and tests | Local Release build passed with zero warnings/errors; 40/40 core checks and 9/9 Windows app checks passed on 2026-11-15. The Auto Sync commit also passed the fresh GitHub Windows build. The public release uses the Windows workflow artifact built from its tagged commit. |
| Packaging | Self-contained Windows x64 ZIP, README, documentation/images, app/dependency/runtime licenses, and a matching separate SHA-256 file. Product version and required ZIP contents were checked locally. |
| Interface | Rendered from the app's workspace XAML at 1180 px and 860 px widths; Setup and expanded EQ screens inspected. Sample names and synthetic queue values are labelled. |
| Signing | DualSync is unsigned. Code-signing provider enrollment and publisher validation are not configured. |
| Launch guard | Launching the packaged alpha 6 while alpha 5 was open displayed the expected already-running notice. The older instance was left running. A main-window launch after quitting it remains for the owner to check. |
| Earlier playback | Owner reported working Bluetooth playback, EQ, and Windows volume. This is one setup's evidence. |
| Presets and Auto Sync | Automated preset persistence, paired three-second queue averaging, cancellation/recovery, and manual-control binding checks pass. Owner playback acceptance of Auto Sync is still pending. |
| Fresh Windows machine | Pending: first launch, no cable, cable installation, administrator cancellation, reboot, existing/disabled cable, and driver removal. |
| Long playback | Pending: at least one hour with two physical outputs, buffer observations, CPU/memory, and audible timing notes. |
| Device lifecycle | Pending: disconnect/reconnect each output, sleep/wake, tray operation, and clean shutdown on intended devices. |
| Public distribution | Owner explicitly approved public source and the first public alpha on 2026-11-15. Hardware gaps remain disclosed in the release notes. |

Record hardware results in the table below. The development PC already has a virtual cable, so it cannot establish the no-driver first-install path without a separate clean environment.

| Date / tester | Windows build | Source and speaker models | Duration / action | Result and issues |
| --- | --- | --- | --- | --- |
| Pending | | | | |

Keep existing audio routing intact during development checks. Physical playback and Windows output changes require deliberate tester action. Never replace a pending hardware result with an offline DSP result or a rendered screenshot.

## Published release verification — 2026-11-15

- [Public release](https://github.com/satvikshukla8/DualSync/releases/tag/v0.1.0-alpha.7), marked as a prerelease.
- Tagged source commit: `109e18c03717ac3ddf7ad53f0c6fe2bcc30d7af1`.
- [Windows workflow](https://github.com/satvikshukla8/DualSync/actions/runs/43799180849): build, all 49 checks, packaging, and artifact upload passed.
- Release assets were taken from that exact workflow artifact. Its digest and the executable's source version were verified before publication.
- Repository metadata, release metadata, and the source tag were readable without authentication. The Windows ZIP and checksum were downloaded without authentication and matched the verified artifact.
- Windows ZIP SHA-256: `0bde7cd001a8cee8964f7a3fb67659e3c0462ca68d25523575e94c46ea135dfa`.

The hardware acceptance gaps above remain open. No Reddit post was submitted as part of this launch preparation.
