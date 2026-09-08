# Compatibility matrix

This matrix records what **Eizo.Playback itself has verified in CI**.

It is intentionally narrower than the full list of formats/codecs supported by upstream LibVLC. A capability being supported by LibVLC does not automatically mean Eizo.Playback has a regression fixture for it yet.

## CI environment

Current automated compatibility checks run on:

- GitHub Actions `windows-latest`
- .NET 10
- LibVLCSharp 3.10.1
- VideoLAN.LibVLC.Windows 3.0.23.1
- WinUI / Windows App SDK project build and pack validation

## Verified media/runtime cases

| Area | Fixture / path | CI status | What is verified |
| --- | --- | --- | --- |
| Local audio | Generated PCM WAV | Verified | Open, play, audio-track discovery, seek/pause capabilities, runtime statistics |
| Matroska chapters | Embedded Matroska + Opus fixture | Verified | Chapter discovery, names, timing, selection, previous/next chapter |
| Local URI validation | Missing local file | Verified | Missing files fail before LibVLC invocation |
| Network source contract | HTTPS URI | Verified at contract level | Source accepted/classified as network without leaking full URI into diagnostics |
| External subtitles | Missing local subtitle | Verified at validation level | Missing subtitle rejected before backend invocation |
| WinUI surface | LibVLCSharp.WinUI project | Build/pack verified | D3D11 swap-chain bootstrap code compiles and packages on Windows |
| Native runtime loading | LibVLC Windows package | Verified | LibVLC native runtime initializes on clean Windows CI |
| NuGet packaging | All four packages | Verified | Release configuration packages successfully |

## Not yet claimed as CI-verified

The following may be supported by LibVLC or by current adapter code, but do not yet have a dedicated Eizo.Playback regression fixture:

- H.264 / H.265 / AV1 video decode
- AAC / FLAC / DTS / TrueHD multi-codec coverage
- multi-audio-track switching with a real multi-track fixture
- ASS / SSA / SRT subtitle rendering with a real subtitle fixture
- remote WebDAV streaming against a live server
- HTTP Range behavior
- Blu-ray / DVD title navigation
- HDR output
- active hardware-decoder confirmation
- visual WinUI frame presentation in headless CI

These should not be presented in release notes as independently verified Eizo.Playback compatibility until a fixture/test is added.

## Timestamp tolerance

Container and codec timing can differ slightly from authored chapter boundaries.

For example, the Matroska/Opus fixture authored around 1.000 seconds may be reported by LibVLC near 1.008 seconds.

Tests therefore validate media timeline values with a small tolerance instead of rewriting backend timestamps.

## Adding a compatibility claim

A new compatibility claim should normally include:

1. a small deterministic fixture or generated media source;
2. an automated test that exercises the real LibVLC runtime where practical;
3. a row in this matrix;
4. no credentials, personal media, or copyrighted third-party media committed to the repository.
