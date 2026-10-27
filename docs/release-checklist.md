# Release checklist

## Build and packaging

- [ ] Windows CI succeeds from a fresh checkout.
- [ ] Portable executable launches without an installed .NET runtime.
- [ ] Package includes the app license, dependency notices, runtime notices, and setup documentation.
- [ ] Version, artifact name, changelog, and checksum agree.
- [ ] Review signing options and document the actual signing status.

## Acceptance

- [ ] Two physical outputs pass the checklist in testing.md.
- [ ] Source and output format combinations work.
- [ ] Source-volume behavior is verified and documented by driver.
- [ ] Device loss, reconnection, shutdown, and tray behavior pass.
- [ ] One-hour playback completes with recorded resource and buffer observations.
- [ ] Virtual-endpoint installation and licensing are resolved before describing the app as a standalone Voicemeeter replacement.

## Publication

- [ ] Obtain explicit approval before making the repository public.
- [ ] Obtain explicit approval before publishing any public release.
- [ ] Keep driver installation and default-device changes opt-in.
- [ ] Review all documentation for claims not backed by test evidence.

Updates are manual portable-package replacements for the alpha: quit the app, replace the application folder, and keep the separate settings folder. No automatic download, updater service, or background installer is included.
