# DualSync architecture

This document describes the initial implementation and the decisions still requiring device testing. It is not a claim of hardware validation.

## Module boundaries

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| `DualSync.Core` | Peaking-EQ coefficients, signal processing, bounded stereo buffers, routing validation, profile models, settings persistence | .NET libraries |
| `DualSync.Audio` | Enumerate endpoints, capture the source, create independent output streams, report device failures and buffer statistics | Core, NAudio.Wasapi |
| `DualSync.Setup` | Prepare the official cable package, validate Windows signatures, and launch the vendor installer after consent | Core, Windows trust APIs |
| `DualSync.App` | WPF controls, speaker view models, EQ response graph, profiles, tray, user-facing setup | Audio, Core |
| `DualSync.Tests` | Deterministic synthetic signals and configuration tests | Core |
| `DualSync.App.Tests` | EQ preset view-model workflows and compiled WPF template bindings without opening audio devices | App |
| `DualSync.Diagnostics` | Inspect Windows endpoints and report engine behavior | Audio |

The audio engine must operate without a window. The core must remain testable without Windows audio hardware. UI work and disk I/O stay outside audio callbacks.

## Capture and playback

The first backend uses WASAPI loopback on an explicitly selected render endpoint, requesting 48 kHz, two-channel IEEE float samples. A capture callback copies complete stereo frames into one bounded single-producer/single-consumer buffer per output. Each output has its own WASAPI shared-mode player and processing state. Windows converts the stream to the endpoint's shared-mode format.

The source cannot also be an output, and an output cannot be selected twice. This prevents the application from recapturing its own output or sending duplicate processed streams to a single endpoint. Original playback through a physical source is a separate issue: the user needs a silent source, or will hear that source in addition to the selected speakers. DualSync does not silently mute it.

## Per-speaker processing

1. Read stereo frames from the output's bounded buffer; fill missing frames with silence.
2. Apply a smoothed channel matrix.
3. Apply ten peaking biquads, with independent left/right filter state.
4. Smooth gain and EQ bypass, preserving normal-level audio without a fixed EQ volume cut.
5. Apply a stereo-linked peak limiter with 5 ms lookahead, plus a final numerical safety clamp.
6. Read the delay line at a configurable fractional-frame offset, crossfading delay changes.
7. Return samples to the shared-mode player.

The EQ uses the peaking-filter equations described in the [W3C Audio EQ Cookbook](https://www.w3.org/TR/audio-eq-cookbook/). Band centers run from 31.25 Hz to 16 kHz, with gain from −12 to +12 dB. Alpha 1's sum-of-positive-boosts compensation was removed after hardware testing showed excessive whole-signal attenuation. Alpha 2 leaves normal-level audio unchanged and limits actual peaks above 0.98 full scale. The graph displays the filter response before signal-dependent limiting.

Bypass crossfades between the original and EQ signals. A monotonic peak queue tracks the limiter's lookahead window; a short linear attack and 100 ms release avoid unnecessary gain jumps. Stereo channels share one limiter gain. Both outputs receive the same fixed 5 ms processing delay in addition to their configured speaker delays.

## Windows master volume

The application distinguishes its own software master gain from the selected source endpoint's Windows volume. Endpoint mode is now the default and mirrors changes in both directions while retaining individual speaker gains. Windows mute and zero endpoint volume silence both outputs in every mode, including App volume mode. Endpoint notifications are backed by a read-only poll on the monitoring timer.

Loopback's relationship to endpoint volume needs verification on the actual source. Applying software master gain to samples that are already scaled would apply volume twice. A source-volume mode therefore needs an explicit setting for whether captured samples already include that volume. Keyboard volume keys control Windows' default playback endpoint; that endpoint must be the chosen source for the expected behavior.

Reliable integration through a named DualSync logical endpoint remains an open delivery requirement. Alpha 4 adds [opt-in setup of the standard VB-CABLE driver](cable-setup.md) as a separately licensed prerequisite, rather than creating a DualSync kernel driver. It does not programmatically change Windows default devices.

## Architecture alternatives

| Approach | Advantages | Constraints | Decision |
| --- | --- | --- | --- |
| Existing endpoint loopback | No driver installation; existing Windows APIs; straightforward engine validation | Separate silent source needed; original source playback remains; endpoint-volume behavior must be checked | Initial backend |
| Dedicated virtual playback endpoint | Natural routing destination; clean source/output separation; potential native master-volume integration | Driver signing, licensing, supported Windows versions, installation and rollback | Continue evaluation |

The [VirtualDrivers virtual audio driver](https://github.com/VirtualDrivers/Virtual-Audio-Driver) currently describes a beta that requires test signing, which is unsuitable for the normal installation path. [Scream](https://github.com/duncanthrax/scream) is an MS-PL virtual network sound card with published signed Windows builds; its network transport, Windows 11 compatibility under current signing policy, loopback behavior, and endpoint volume still require verification. Neither driver is bundled or selected as a dependency.

Microsoft's [driver signing requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-reqs) include an EV certificate for the dashboard account. A new custom driver would introduce signing and maintenance costs, so it is not part of this initial implementation. A published signed driver must still be checked against current Windows policy before being recommended.

## Timing and buffering

Each output owns a buffer capped at 500 ms of source frames. Overflow drops incoming frames and increments a counter; underflow returns silence and increments a separate counter. Memory cannot grow with playback duration. These policies are recovery bounds, not clock synchronization.

Static speaker delay is independent of the buffer. Device clocks can drift, while Bluetooth transport introduces changing latency and scheduling jitter. Long-session testing will determine whether a shared source timeline and adaptive resampling are needed. The initial backend relies on Windows for format conversion and does not implement adaptive clock correction.

## Settings and safety boundaries

Profiles identify endpoints by stable Windows IDs and store processing parameters separately from the engine. Settings use a schema-versioned JSON file, atomic replacement, numeric validation, and recovery copies for unreadable configurations.

Schema 3 adds two independent EQ libraries alongside full setup profiles. Single presets store ten bands and an enabled flag and can be applied to either output; dual presets store separate A/B snapshots. Capture, load, and library export copy band arrays. A dual load updates both view models before sending one complete profile to the audio engine. Neither EQ operation changes endpoint IDs, master settings, speaker levels, mute, routing, or delay. Loading a full setup does not replace either EQ library. Schema 1 and 2 settings upgrade while retaining their existing profiles and speaker settings.

Startup registration is optional. Driver installation, default-device changes, persistent system settings, repository visibility changes, and public releases require explicit user approval. Closing or stopping the engine must release its capture stream, output streams, volume subscription, and endpoint objects.

## Validation still required

- Continue testing desktop lifecycle and device recovery on physical hardware.
- Confirm both Bluetooth speakers receive audio.
- Listen for artifacts during parameter changes.
- Verify source volume, mute, keyboard volume, and per-speaker gain behavior.
- Test mixed sample rates and device loss/reconnection.
- Run long playback sessions and measure buffer trends, CPU use, and dropouts.

Primary references: [Microsoft loopback recording](https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording), [endpoint volume controls](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-volume-controls), [NAudio capture](https://naudio.github.io/NAudio/docs/WasapiRecorder.html), and [NAudio playback](https://naudio.github.io/NAudio/docs/WasapiPlayer.html).
