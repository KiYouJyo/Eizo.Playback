# Dependencies

Versions are centrally pinned in `Directory.Packages.props`.

| Package | Initial pinned version | Role |
| --- | ---: | --- |
| LibVLCSharp | 3.10.1 | .NET wrapper around LibVLC |
| LibVLCSharp.WinUI | 3.10.1 | WinUI 3 `VideoView` and D3D11 swap-chain integration |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | Native Windows LibVLC runtime |
| Microsoft.WindowsAppSDK | 1.8.251106002 | WinUI 3 runtime/build dependency |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7175 | Windows SDK build tooling |
| Microsoft.NET.Test.Sdk | 18.9.0 | Test host |
| xunit.v3 | 4.0.0 | Unit test framework |
| xunit.runner.visualstudio | 4.0.0 | Visual Studio / dotnet test integration |

The Windows App SDK and Windows SDK BuildTools versions intentionally match the current LibVLCSharp 3.x WinUI sample baseline used during Stage 2 integration.

## Policy

- Backend dependency versions are pinned centrally.
- Dependency upgrades must pass the full build and compatibility suite.
- The GPL LibVLC Windows package is intentionally not used in the baseline.
- Third-party license notices and redistribution requirements must be reviewed before the first distributable package is published.
- A LibVLCSharp.WinUI upgrade must be checked for changes to swap-chain initialization semantics before adoption.
