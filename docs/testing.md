# Testing DualSync

## Automated checks

Run `./scripts/build.ps1` to restore locked dependencies, build all projects, and execute the test program. The test runner exits with a nonzero code on failure; `dotnet test` is not the entry point for this dependency-free test harness.

The 33 core tests cover:

- Biquad stability across supported band centers, sample rates, and gain limits.
- Analytical and measured sine-wave frequency response, without fixed attenuation.
- Flat EQ, bypass, master gain, mute ramps, and speaker gain ratios.
- Boosted-EQ bypass and reset transitions within the limiter ceiling.
- All channel-routing matrices.
- Impulse placement at zero, integer, fractional, and maximum delay.
- Rapid parameter changes and finite, bounded output.
- Buffer wraparound, overflow, underflow, and concurrent frame publication.
- Feedback, duplicate-output, and unavailable-device validation.
- Numeric settings validation, serialization, atomic writes, and recovery.
- Managed allocations in steady-state DSP processing.
- Cable identification, download integrity, safe extraction, license preservation, and cancellation.
- Single and dual EQ snapshot isolation, settings preservation, disk reload, replacement, deletion, limits, malformed entries, and migration.

Five Windows app tests exercise cross-speaker EQ loading, batched dual loading, shared-list updates, full setup independence, and compiled WPF save/load bindings at a compact panel width. They also verify that styled EQ sliders retain their numeric range and live band binding. These tests do not open audio devices or change Windows settings.

Passing `--screenshots docs/assets` to the built app test runner renders the actual workspace XAML with sample device names and synthetic queue values. Every preview is labelled as such. The renderer excludes event handlers and app startup, so it cannot start playback, install a cable, or modify user settings. Packaging runs this renderer and includes the resulting images. This checks presentation without claiming hardware playback.

WASAPI format conversion is delegated to Windows rather than reimplemented. Resampling between endpoint formats therefore requires Windows integration testing. No adaptive resampler or clock-synchronization algorithm is claimed by this version.

## Diagnostic tools

```powershell
dotnet run --project tools/DualSync.Diagnostics -- --list
dotnet run --project tools/DualSync.Diagnostics -- --benchmark
dotnet run --project tools/DualSync.Diagnostics -- --route SOURCE_ID OUTPUT_A_ID OUTPUT_B_ID
dotnet run --project tools/DualSync.Diagnostics -- --smoke SOURCE_ID OUTPUT_A_ID OUTPUT_B_ID
dotnet run --project tools/DualSync.Diagnostics -- --prepare-cable-only C:\path\to\test-data
```

`--list` reads endpoint metadata. `--benchmark` measures 60 seconds of offline two-speaker DSP, excluding capture, playback, UI, and transport. `--route` opens actual devices and forwards captured system audio for ten seconds at moderate software gain. It does not change defaults or endpoint volume. Use it only after checking the route and avoiding external duplicate playback.

`--smoke` opens the selected source and outputs for two three-second start/stop cycles with zero software master gain. It verifies that reported output peaks remain zero and that streams are released between cycles. This checks stream initialization and lifecycle without playing the captured signal aloud.

`--prepare-cable-only` downloads and verifies the real official cable package, including native Windows signature checks. It cannot launch the installer and does not change drivers. The real preparation path passed on 2026-10-28. Clean-machine installation, UAC cancellation, restart, and removal still need manual validation; see [cable setup](cable-setup.md).

## Recorded development checks — 2026-10-27

- Release build: all five projects compiled with zero warnings and errors.
- Core tests: 22/22 passed for alpha 2.
- Fresh GitHub Windows build: compilation, tests, portable packaging, and private artifact upload passed.
- Device discovery: Windows render endpoints enumerated successfully, including two Bluetooth endpoints and existing virtual endpoints.
- Silent WASAPI smoke test: two start/stop cycles passed using a monitor endpoint and an existing virtual output. Each cycle captured approximately three seconds of source frames, with zero output peaks, overruns, and safety clamps. Startup underruns were recorded.
- Bluetooth stream initialization: an attempted silent test was blocked because an output was already held in exclusive mode by another application. No application was stopped or reconfigured to bypass that condition.
- Offline two-speaker DSP: 60 seconds processed in approximately 1.12 seconds wall time and 1.14 CPU seconds on the development PC. This is not a whole-app CPU or latency measurement.
- Desktop: the portable executable launched. Dashboard and EQ layouts were visually inspected; background, text-inheritance, and close-event issues were repaired. Moving Speaker A's EQ updated its graph and persisted its gain while Speaker B stayed unchanged; Reset EQ restored a flat response. The packaged app closed successfully after the shutdown fix.
- Owner feedback on alpha 2: playback, EQ, and Windows-volume behavior were reported as working. This is owner-reported hardware evidence, not independent acoustic measurement.
- Alpha 3 UI preview: profiles, routing, and collapsible EQ were displayed together. With synthetic queue data, Speaker A showed 12 + 23 = 35 ms; clicking +1 ms changed this to 12 + 24 = 36 ms. At 500 ms added delay, the total was 512 ms and the increment button was disabled. The EQ dropdown expanded inline. Computer-use verification stopped when the owner pressed Escape; later interaction checks were not performed.

Acoustic timing and one-hour reliability remain unverified. Synthetic queue values used for UI checks are not physical latency measurements.

## Hardware acceptance checklist

Record the source and speaker names, Windows build, profile, and test duration. The presence of endpoints in enumeration does not establish audible playback or synchronization.

| Test | Procedure | Expected result |
| --- | --- | --- |
| Simultaneous playback | Start familiar music, mute A and then B separately | Both receive the source; only the selected speaker mutes |
| Independent EQ | Lower A's low bands, then bypass A; repeat for B | Audible change is limited to the chosen output |
| Parameter changes | Move EQ, volume, and delay controls during music | No severe click, instability, or crash |
| Gain/headroom | Boost bands gradually and inspect clamp counters | Level remains bounded; reduce gain if clamping persists |
| Channel routing | Play a known left/right test file; select Left, Right, Mono, Custom | Correct signals appear at each output |
| Static delay | Use percussive audio and add 1–10 ms to the earlier speaker | A useful steady alignment can be found |
| Profiles | Save, rename, load, close, and restart | Device IDs and individual parameters persist |
| Custom EQ presets | Save different curves from A and B, load each on either side, replace one, then restart | The chosen EQ loads on that side; other audio settings and saved curves remain intact |
| Dual EQ presets | Save two distinct curves as a pair, change both, then load the pair during playback | Both original curves and enabled states return to their original sides; volume, routing, and delay stay the same |
| Tray | Hide the window, use tray controls, then quit | Audio continues while hidden and stops on quit |
| Device loss | Disconnect one device during playback, then reconnect | Engine stops safely and retries the chosen devices |
| Startup option | Explicitly enable and disable launch at sign-in | Only the DualSync Run entry changes |
| Long session | Play for at least one hour and inspect diagnostics | No crash or growing memory; document drops and changing alignment |
| Mixed formats | Use devices with different Windows mix rates | Shared-mode conversion plays without incorrect pitch |

## Windows volume test

Use a flat EQ and start at moderate volume. Windows source volume is the alpha 2 default. Confirm both speaker volume ratios remain constant while changing the app master.

For Windows source mode:

1. Record the source's current Windows volume and mute state.
2. Stop audio and select Windows source volume. Leave “Source already applies Windows volume” unchecked for your VB-Cable.
3. Start audio and play a steady signal. Change Windows source volume between 25% and 75%.
4. Confirm the app master mirrors Windows and both speakers follow the level change.
5. If the slider moves but speaker level does not, uncheck the source-volume checkbox so DualSync applies the source scalar.
6. If the volume change becomes disproportionately strong, the capture may already be scaled; recheck the checkbox and test again.
7. Test mute, keyboard volume keys, and app-master changes. Verify that keyboard controls target the chosen default source.
8. Return the endpoint to its previously recorded volume and mute state when done.

A volume scalar does not imply a fixed acoustic dB change. Compare behavior and independent speaker ratios, and record the source driver. If neither setting behaves consistently, keep App volume mode and report the endpoint details. Do not mark native volume integration complete for that source.

## What remains unverified

The initial automated tests validate software behavior. Physical speaker fidelity, Bluetooth latency, audible artifacts, long-session drift, source-volume semantics, and reconnection on the owner's hardware require the manual checks above. Static delay cannot eliminate clock drift or random Bluetooth buffering changes.
