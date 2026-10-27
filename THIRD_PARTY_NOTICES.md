# Third-party notices

DualSync's original code is licensed under MIT. No audio driver is bundled.

## NuGet dependencies

The resolved package metadata and lock files were checked for this build.

| Component | Version | Role | License |
| --- | --- | --- | --- |
| NAudio.Wasapi | 3.2.0 | WASAPI and Windows Core Audio | MIT |
| NAudio.Core | 3.2.0 | Audio formats and provider abstractions | MIT |
| System.Numerics.Tensors | 9.0.0 | Transitive dependency of NAudio.Core | MIT |

NAudio copyright 2020 Mark Heath. NAudio source and license: <https://github.com/naudio/NAudio>. System.Numerics.Tensors copyright (c) .NET Foundation and Contributors. Source and license: <https://github.com/dotnet/runtime>.

The MIT permission and warranty terms printed in `LICENSE` also apply to these components with their respective copyright notices above. Keep this notice with redistributed binaries.

## Self-contained .NET runtime

Portable packages include Microsoft's .NET and Windows Desktop runtime files, licensed under MIT and other licenses described in their notices. The build script copies the resolved runtime packs' license and third-party notice files into `licenses/`, along with System.Numerics.Tensors notices. Preserve that directory when distributing the portable folder. Runtime reference: <https://github.com/dotnet/runtime>; Windows Desktop reference: <https://github.com/dotnet/wpf>.

## DSP reference

The peaking-EQ implementation uses mathematical filter equations described by Robert Bristow-Johnson in the [W3C Audio EQ Cookbook](https://www.w3.org/TR/audio-eq-cookbook/). The implementation is original code; no text or source code from that document is bundled.

## Development tooling

The Microsoft .NET SDK and GitHub Actions are development tools, not application service dependencies. DualSync performs audio processing locally and requires no paid SDK, cloud service, or API subscription.
