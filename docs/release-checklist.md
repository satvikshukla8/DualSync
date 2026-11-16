# Release checklist

## Build and packaging

- [x] Application code passes Windows CI from a fresh checkout, including 49 automated checks and portable packaging.
- [ ] Current alpha opens its main window after quitting the older instance (earlier alphas launched; alpha 6's duplicate-instance notice was verified).
- [x] Package includes the app license, dependency notices, runtime notices, and setup documentation.
- [x] App, window, and tray have an original icon; screenshots and setup guidance are prepared.
- [x] Version, artifact name, changelog, and checksum agree for alpha 7.
- [x] Review signing options and document the actual signing status: unsigned, see signing.md.
- [ ] Configure a trusted publisher identity and sign the build before claiming signed distribution.

## Hardware validation still open for the public alpha

- [ ] Two physical outputs pass the checklist in testing.md.
- [ ] Source and output format combinations work.
- [ ] Source-volume behavior is verified and documented by driver.
- [ ] Device loss, reconnection, shutdown, and tray behavior pass.
- [ ] One-hour playback completes with recorded resource and buffer observations.
- [ ] Auto Sync measures stable queues during playback, recovers after pause/reconnect, and returns to manual control correctly on the owner's devices.
- [x] Offer opt-in setup of standard VB-CABLE, with attribution, license links, package integrity checks, and visible vendor confirmation.
- [ ] Test the opt-in cable installer on a clean Windows machine, including UAC denial, existing/disabled cable, restart, and removal.

## Publication

- [x] Owner explicitly approved a public repository and the first public alpha release on 2026-11-15.
- [x] Public README, download links, release notes, and issue templates are prepared.
- [x] Keep driver installation and default-device changes opt-in.
- [x] Review documentation claims; rendered previews and pending hardware tests are identified.

Updates are manual portable-package replacements for the alpha: quit the app, replace the application folder, and keep the separate settings folder. No automatic download, updater service, or background installer is included.

The first public alpha is offered for testing with the hardware gaps above disclosed. It is not a stable-production validation claim. The release process verifies the exact tagged Windows build, attaches its ZIP and checksum, and checks public access before sharing the download.
