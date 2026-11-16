# Contributing

Use the SDK in `global.json`. Run `./scripts/build.ps1` before proposing a change. Tests use synthetic audio and a small console runner; failures return a nonzero exit code.

Keep Windows-specific code in the Audio and App projects. Changes to audio callbacks need attention to allocation, bounded memory, stream teardown, and error handling. Add tests for meaningful DSP or state changes, and document hardware checks separately from automated results.

Discuss driver integration before adding a driver dependency. Verify licensing, redistribution, signing, Windows 11 support, volume behavior, and a safe installation/removal path. Disabling security controls is not a supported installation strategy.

The repository is public. Use issues for reproducible bugs and proposed improvements, and submit focused pull requests against `main`. Publishing a release or changing repository visibility requires the owner's approval.
