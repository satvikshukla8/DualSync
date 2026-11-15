# Alpha 6 validation record

Prepared 2026-11-15. This record distinguishes software checks from physical-device acceptance.

| Check | Evidence or next action |
| --- | --- |
| Build and tests | Local Release build passed with zero warnings/errors; 33/33 core checks and 5/5 Windows app checks passed on 2026-11-15. Packaging uses scripts/build.ps1 -Package. |
| Packaging | Self-contained Windows x64 ZIP, README, documentation/images, app/dependency/runtime licenses, and a matching separate SHA-256 file. Product version and required ZIP contents were checked locally. |
| Interface | Rendered from the app's workspace XAML at 1180 px and 860 px widths; Setup and expanded EQ screens inspected. Sample names and synthetic queue values are labelled. |
| Signing | DualSync is unsigned. Code-signing provider enrollment and publisher validation are not configured. |
| Launch guard | Launching the packaged alpha 6 while alpha 5 was open displayed the expected already-running notice. The older instance was left running. A main-window launch after quitting it remains for the owner to check. |
| Earlier playback | Owner reported working Bluetooth playback, EQ, and Windows volume. This is one setup's evidence. |
| Current preset controls | Automated save/load/replace/delete and restart persistence pass. Owner playback acceptance is still pending. |
| Fresh Windows machine | Pending: first launch, no cable, cable installation, administrator cancellation, reboot, existing/disabled cable, and driver removal. |
| Long playback | Pending: at least one hour with two physical outputs, buffer observations, CPU/memory, and audible timing notes. |
| Device lifecycle | Pending: disconnect/reconnect each output, sleep/wake, tray operation, and clean shutdown on intended devices. |
| Public distribution | Pending explicit owner approval after reviewing the build and the remaining acceptance results. |

Record hardware results in the table below. The development PC already has a virtual cable, so it cannot establish the no-driver first-install path without a separate clean environment.

| Date / tester | Windows build | Source and speaker models | Duration / action | Result and issues |
| --- | --- | --- | --- | --- |
| Pending | | | | |

Keep existing audio routing intact during development checks. Physical playback and Windows output changes require deliberate tester action. Never replace a pending hardware result with an offline DSP result or a rendered screenshot.
