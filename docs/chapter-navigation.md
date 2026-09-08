# Chapter navigation

## Entry point

```csharp
IPlaybackNavigationController navigation = engine.Navigation;
```

## Chapter timeline

`PlaybackChapterInfo` exposes:

- `Index`
- `TitleIndex`
- `Name`
- `Start`
- `Duration`
- `End`
- `IsSelected`

The LibVLC backend prefers full chapter descriptions so start/duration can be preserved.

If a backend only exposes chapter names, timing values may be null.

## Matroska episodes

Anime and drama MKV/MKA files commonly contain chapters without a meaningful title hierarchy.

For that reason:

```text
Titles.Count == 0
```

does not imply:

```text
Chapters.Count == 0
```

The UI should display chapters whenever the chapter collection is non-empty.

## Selection

```csharp
await engine.Navigation.SelectChapterAsync(chapterIndex);
```

For media with multiple titles:

```csharp
await engine.Navigation.SelectTitleAsync(titleIndex);
```

Selecting a title refreshes the chapter context for that title.

## Adjacent movement

```csharp
bool moved = await engine.Navigation.NextChapterAsync();
bool movedBack = await engine.Navigation.PreviousChapterAsync();
```

The methods return `false` when there is no valid adjacent chapter.

## OP/ED skip policy

The playback adapter deliberately does not label chapters as OP, ED, preview or recap.

A future Eizo policy layer can use:

- normalized chapter name
- start time
- duration
- episode duration
- user preference

to classify chapter semantics without coupling those rules to LibVLC.
