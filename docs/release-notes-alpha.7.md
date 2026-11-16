# DualSync 0.1.0-alpha.7

Adds optional Auto Sync using three-second averages of the two application queues.

- Samples both queues together at about 20 Hz while audio is running.
- Adds delay to the shorter average queue and rechecks every three-second window.
- Shows sampling progress, measured averages, and the applied A/B delays.
- Replaces the delay pair from scratch, ignores changes under 1 ms, and waits through startup, pauses, and buffer dropouts.
- Locks manual delay controls while enabled. Switching off keeps the last delays and unlocks fine adjustment.
- Saves the mode in full setup profiles and preserves it when loading EQ presets.

See [Auto Sync](auto-sync.md) for the calculation and limits. This aligns application buffers; Bluetooth/device latency and acoustic timing are not measured. Physical playback acceptance is still pending.

The suite has 40 core checks and nine Windows app checks. Existing fresh-PC, long-session, reconnection, and signing checks remain in the [release checklist](release-checklist.md). This is the first public alpha, not a stable production release.
