# Dependencies

Versions are centrally pinned in `Directory.Packages.props`.

| Package | Initial pinned version | Role |
| --- | ---: | --- |
| LibVLCSharp | 3.10.1 | .NET wrapper around LibVLC |
| LibVLCSharp.WinUI | 3.10.1 | Reserved for later WinUI video-surface integration |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | Native Windows LibVLC runtime |
| Microsoft.NET.Test.Sdk | 18.9.0 | Test host |
| xunit.v3 | 4.0.0 | Unit test framework |
| xunit.runner.visualstudio | 4.0.0 | Visual Studio / dotnet test integration |

## Policy

- Backend dependency versions are pinned centrally.
- Dependency upgrades must pass the full build and compatibility suite.
- The GPL LibVLC Windows package is intentionally not used in the baseline.
- Third-party license notices and redistribution requirements must be reviewed before the first distributable package is published.
