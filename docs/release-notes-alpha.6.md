# DualSync 0.1.0-alpha.6

Windows 11 x64 portable preview. Extract the whole ZIP and run `DualSync.exe`. Quit an older instance from its tray menu first. Existing profiles and custom EQ presets remain in the separate settings folder.

## Changes

- Speaker controls appear first on the dashboard, with mint and amber accents for A and B.
- Full setup profiles and dual EQ presets use compact dropdowns below the speakers.
- A Setup page explains the source, Windows output, and speaker-selection steps.
- The app, window, and tray use an original two-speaker icon.
- Refined EQ sliders, input controls, focus states, spacing, and empty selectors.
- Portable packages include setup documentation, screenshots, licenses, and a separate SHA-256 checksum.

## Current validation

The automated suite covers audio processing, settings, preset persistence, and EQ bindings. Documentation screenshots are rendered from the actual interface with sample devices and synthetic queue values; they do not show a live Bluetooth test.

The owner previously reported working playback, EQ, and Windows volume. Clean-PC cable installation, reboot/removal, sleep/wake, and an hour of physical playback still need recorded tests on the intended hardware. Use the [release checklist](release-checklist.md) and [testing guide](testing.md).

## Known limits

- Manual delay does not correct changing Bluetooth latency or clock drift.
- The app is unsigned; see [signing status](signing.md).
- VB-CABLE is a separate, proprietary donationware driver. Installation remains optional and requires its own administrator confirmation.
- The user selects the Windows default output in Windows Sound settings. DualSync does not select it automatically.
- Updates use a replacement portable folder after quitting; there is no automatic updater.

Publication is pending. These notes prepare the private build for review and do not announce a public release.
