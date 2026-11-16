# Working on DualSync

- Target Windows 11, C#, .NET 10, WPF, and WASAPI.
- Keep DSP and settings logic in Core, Windows audio integration in Audio, and desktop concerns in App.
- Keep audio callbacks free of UI calls, file I/O, blocking waits, and unbounded allocations.
- Validate signal processing with meaningful synthetic-audio tests. Never claim physical Bluetooth testing without actual evidence.
- Never install drivers, alter Windows default audio devices, register startup, or change persistent system settings without explicit user action or approval.
- The owner approved the first public alpha launch on 2026-11-15. The repository is public. Future releases and visibility changes still require an explicit owner request.
- Use permissively licensed dependencies where practical; preserve their notices. Do not bundle a driver before verifying redistribution and signing.
- Reject capture/output feedback paths and duplicate outputs. Preserve per-speaker settings when master volume changes.
- Document limitations and distinguish static delay from clock drift and Bluetooth jitter.
- Commit meaningful changes. Update the README when build status changes.
