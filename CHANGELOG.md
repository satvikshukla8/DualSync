# Changelog

## 0.1.0-alpha.3 — 2026-10-27

- Combined profiles, routing, EQ, and speaker controls into one dashboard.
- Added a collapsible equalizer inside each speaker card.
- Added live per-speaker queue, added delay, and their combined software buffer value.
- Added ±1 ms delay buttons with 0–500 ms limits.
- Added readable left/right channel choices and inline custom mixing controls.

The displayed final buffer is queue plus added delay. It excludes limiter lookahead, Windows output buffering, and Bluetooth/device latency.

## 0.1.0-alpha.2 — 2026-10-27

- Removed the sum-of-boosts attenuation that could make EQ almost silent.
- Added a stereo-linked limiter with 5 ms lookahead; quiet audio stays at unity gain.
- Made Windows source volume the default, with software gain for sources such as VB-Cable that bypass native volume.
- Made source mute and zero volume apply in every mode, with notification polling as a fallback.
- Migrated alpha 1 app-only profiles while preserving speaker EQ, volumes, delay, and routing.
- Added four regression tests, bringing the suite to 22 tests.

Actual Windows-volume behavior on the owner's source still needs a retest. No driver, default playback device, or driver-control-panel setting is changed by this update.

## 0.1.0-alpha.1 — 2026-10-27

Initial runnable alpha under hardware validation.

- Added endpoint enumeration and two-output WASAPI routing.
- Added per-speaker 10-band EQ, headroom, gain, mute, delay, and channel matrices.
- Added a WPF interface with response graphs, profiles, tray controls, and diagnostics.
- Added schema-versioned settings and recovery handling.
- Added 18 automated tests, a Windows diagnostics tool, and portable-build automation.

Hardware playback, native source-volume behavior, and long-session reliability still require validation. A dedicated virtual endpoint and adaptive clock correction are not included.
