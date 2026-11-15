# Changelog

## 0.1.0-alpha.6 — 2026-11-15

- Put source and speaker controls first, with compact dropdowns for dual EQ presets and full setup profiles below them.
- Added a three-step Setup page, first-run guidance, and a direct link to Windows Sound settings.
- Added original vector artwork and a multi-resolution icon for the executable, windows, and tray.
- Refined the dark theme, A/B accents, sliders, selector placeholders, focus states, and spacing.
- Added reproducible interface previews using sample data without opening audio devices.
- Rewrote the README and prepared release notes, signing guidance, and a focused validation checklist.
- Added a separate SHA-256 checksum to portable packages and included documentation images.

The alpha remains unsigned. Clean-PC installation and physical long-session, reconnect, and sleep/wake tests remain release gates. No public release has been published.

## 0.1.0-alpha.5 — 2026-10-29

- Added named custom EQ presets that can be saved from and loaded onto either speaker.
- Added dual EQ presets containing separate snapshots of Speaker A and Speaker B's bands and enabled states.
- Added save, replace, load, and delete controls inside each EQ panel and a shared dual EQ section on the dashboard.
- Kept EQ libraries independent of full setup profiles; loading EQ leaves devices, levels, mute, routing, and delay intact.
- Added settings migration, six core regression tests, and five Windows app tests for preset workflows and compiled template bindings.

EQ presets load during playback. A dual load sends both sides together in one audio-engine update. Physical playback with the new preset controls still needs an owner test.

## 0.1.0-alpha.4 — 2026-10-28

- Added on-demand VB-CABLE setup with explicit consent and the official administrator installer.
- Added existing-cable detection and reuse, including checks for disabled endpoints.
- Added package hash checks, bounded downloads, safe extraction, and native Windows signature checks.
- Added vendor attribution, license and donation links, cancellation, and restart guidance.
- Added a preparation-only diagnostic command and five tests, bringing the suite to 27 tests.

The real download and signature-verification path passed without launching an installer. Clean-machine driver installation and reboot behavior remain to be tested. Presentation polish is deferred.

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
