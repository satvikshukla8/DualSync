# Auto Sync

Start playback and turn on **Auto Sync** above the speaker cards. After a short settling period, DualSync samples both application queues together about every 50 ms for three seconds. It adds delay to the speaker with the shorter average queue. It then keeps checking in new three-second windows while enabled.

For example, if A averages 12 ms and B averages 20 ms, Auto Sync sets A's added delay to 8 ms and B's to 0 ms. Their average software totals are both 20 ms. A later window calculates a fresh pair of delays rather than adding another 8 ms.

The dashboard shows progress, the measured averages, and the resulting added delays. Manual sliders and ±1 ms buttons are locked while automatic mode is on. Switching Auto Sync off keeps the current delays and unlocks those controls, so you can fine-tune by ear.

## What is measured

Only the bounded source queues owned by DualSync are measured. The averages exclude Windows output buffering, the 5 ms limiter lookahead, and the speakers' Bluetooth/device buffering. Equal application totals do not prove that sound reaches your ears at the same time. Use manual delay if it sounds better for your devices.

This feature does not measure acoustic latency, record from a microphone, change the Bluetooth clock, or resample to correct clock drift.

## When an adjustment is made

- The source must be delivering frames, and both output queues must be available.
- A full three-second measurement needs at least 40 paired samples.
- Startup, pauses, missing/dropped frames, counter resets, and sampling gaps longer than 250 ms discard the partial window. A short settling period precedes a fresh measurement.
- The target average is the larger of A and B. Each added delay is that target minus its own average, rounded to 0.1 ms within the existing 0–500 ms range.
- Differences under 1 ms from the current delay pair are left alone to reduce adjustments caused by small queue fluctuations.

An incomplete or unstable window keeps the previous delays. Stopping playback, changing the full setup, or turning Auto Sync off cancels the current measurement. Playback does not start automatically.

The mode and applied delays are saved with the current settings and full setup profiles. Existing profiles default to manual mode. Individual and dual EQ presets preserve the current sync mode.
