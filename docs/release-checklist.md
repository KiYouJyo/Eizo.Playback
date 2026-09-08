# Release checklist

Use this checklist before publishing an Eizo.Playback release or consuming a new package baseline from the Eizo application.

## Source state

- `main` is clean and contains the intended release commit.
- CI is green on the release commit.
- dependency versions in `Directory.Packages.props` are reviewed.
- `docs/compatibility-matrix.md` matches the tests actually present.
- no full media URLs, credentials, tokens or private paths are stored in diagnostics or test assets.

## Build validation

Run:

```powershell
dotnet restore Eizo.Playback.slnx
dotnet build Eizo.Playback.slnx -c Release --no-restore
dotnet test Eizo.Playback.slnx -c Release --no-build
dotnet pack Eizo.Playback.slnx -c Release --no-build -o artifacts/packages
```

Expected packages:

- `Eizo.Playback.Abstractions`
- `Eizo.Playback.Core`
- `Eizo.Playback.LibVLC`
- `Eizo.Playback.LibVLC.WinUI`

## API boundary

Contract tests must remain green.

In particular:

- `Eizo.Playback.Abstractions` must not reference LibVLCSharp or WinUI.
- public LibVLC backend signatures must not expose LibVLCSharp types.
- Eizo application code should control playback through Eizo contracts rather than native LibVLC types.

## Package metadata

Each package should contain:

- repository metadata;
- author metadata;
- description;
- README.

A project license is deliberately **not** selected by this checklist. License choice must be made explicitly by the repository owner before a public package release if required.

## Compatibility claims

Only claim a format/feature as Eizo.Playback CI-verified if it is backed by the compatibility matrix and an automated regression path.

Upstream LibVLC support can be described separately as upstream capability.

## Versioning

The repository currently uses a shared `VersionPrefix` for all packages.

Before the first external/stable release:

- decide whether all packages continue lockstep versioning;
- bump `VersionPrefix`;
- update release notes;
- tag the exact release commit.

## Eizo application integration

Before updating the Eizo UI repository:

- consume one coherent package set from the same Eizo.Playback version;
- run a WinUI smoke test with real video output;
- verify player surface recreation after navigation/unload;
- verify audio/subtitle menus against a representative anime/drama file;
- verify diagnostics do not display sensitive source URLs.
