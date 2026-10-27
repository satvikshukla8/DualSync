# Changelog

## 0.1.0-alpha.1 — 2026-10-27

Initial runnable alpha under hardware validation.

- Added endpoint enumeration and two-output WASAPI routing.
- Added per-speaker 10-band EQ, headroom, gain, mute, delay, and channel matrices.
- Added a WPF interface with response graphs, profiles, tray controls, and diagnostics.
- Added schema-versioned settings and recovery handling.
- Added 17 automated tests, a Windows diagnostics tool, and portable-build automation.

Hardware playback, native source-volume behavior, and long-session reliability still require validation. A dedicated virtual endpoint and adaptive clock correction are not included.
