# Troubleshooting

## No sound

Make sure music is playing through the selected source endpoint, not directly through a different Windows default device. Select two connected outputs, unmute them, and check app and physical volume. WASAPI loopback callbacks can stop while the source is idle; missing-frame counters during silence are expected.

If a source is muted, its captured stream may also be silent. Raising a device's Windows volume is a user action; DualSync will not unmute or reset source volume at startup.

## Source and speaker cannot be the same

Endpoint loopback would recapture DualSync's own playback, producing feedback. Select a separate source. This initial version does not have a driverless way to make two arbitrary physical devices a single logical Windows speaker endpoint.

## Echo, doubling, or an extra speaker

Stop DualSync. Check whether Voicemeeter or another app is also forwarding the source to the outputs. A physical source may itself be audible in addition to both processed speakers. Use a suitable silent source and one forwarding path.

## Windows volume controls do not change both speakers

App volume mode is software-only. Windows source volume mode needs the selected source to match Windows' default playback endpoint for normal volume keys. Its loopback-volume checkbox must match the source driver's behavior. Follow the volume test in [testing.md](testing.md).

## EQ presets sound quiet

Automatic headroom reduces gain by the sum of positive EQ boosts. Many simultaneous boosts can cause a large reduction. Prefer cutting unwanted bands and making modest boosts. The graph shows EQ shape before headroom. Persistent safety-clamp counts mean settings or source material are reaching the output limit.

## Clicks, gaps, or drifting alignment

Stop other audio-routing applications, reduce system load, and inspect buffer drops during active music. The app bounds buffer memory and fills shortages with silence. Bluetooth transport and independent device clocks can still cause gaps or changing alignment. Static delay fixes only a steady timing difference.

## A speaker disconnected

The engine stops and retains its device selections. It retries available selected devices during that playback session. If Windows assigns a different endpoint ID after re-pairing, select the device again. Stop audio to change output selections.

## A device is already in exclusive use

Another application owns the endpoint in exclusive mode, so DualSync cannot open a shared-mode stream. Voicemeeter can hold an output this way. Stop that application's routing yourself before trying DualSync on the same output. DualSync does not take over or terminate other applications.

## Settings did not load

Open the data folder. Unreadable settings are preserved as `settings.json.recovery-*` before defaults are used. Keep the recovery file when reporting an issue. A future schema version is not silently interpreted as the current schema.

## App is already running

Open DualSync from the system tray. Only one instance runs at a time to avoid duplicate streams.

## Reporting a problem

Use **Copy diagnostic report** and include what you were doing, whether the source was actively playing, and the approximate test duration. The report includes endpoint names, IDs, and audio settings. Review it before sharing. Error logs rotate once they reach about 2 MB; the app sends no telemetry.
