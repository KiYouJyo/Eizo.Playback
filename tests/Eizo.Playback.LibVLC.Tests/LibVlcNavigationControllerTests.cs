using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcNavigationControllerTests
{
    private const string ChapteredMatroskaBase64 =
        "GkXfo6NChoEBQveBAULygQRC84EIQoKIbWF0cm9za2FCh4EEQoWBAhhTgGcBAAAAAAAHNRFNm3TPv4RMgmNcTbuLU6uEFUmpZlOsgaFNu4tTq4QWVK5rU6yB7027jFOrhBBDp3BTrIIBY027jFOrhBJUw2dTrIIBvE27jFOrhBxTu2tTrIIHGewBAAAAAAAARAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFUmpZsm/hKEl3RAq17GDD0JATYCMTGF2ZjYxLjcuMTAzV0GMTGF2ZjYxLjcuMTAzc6SQzx1+LSm2b/x+fkm9p2Hkl0SJiECfYAAAAAAAFlSua++/hGBk9XCuAQAAAAAAAGDXgQFzxYiFWQoA6XSo2ZyBACK1nIN1bmSIgQCGhkFfT1BVU1aqg2MuoFa7hATEtACDgQLhkZ+BAbWIQL9AAAAAAABiZIEQVe6BAGOik09wdXNIZWFkAQE4AUAfAAAAAAAQQ6dw1L+EmtCEFEW5AQAAAAAAAERF24EBtp5zxIEBkYEAkoQ7msoAgI+Fh09wZW5pbmdDfIN1bmS2nnPEgQKRhDuaygCShHc1lACAjIWETWFpbkN8g3VuZBJUw2dAgr+ECXW8fHNzn2PAgGfImUWjh0VOQ09ERVJEh4xMYXZmNjEuNy4xMDNzc9djwItjxYiFWQoA6XSo2WfIokWjh0VOQ09ERVJEh5VMYXZjNjEuMTkuMTAxIGxpYm9wdXNnyKFFo4hEVVJBVElPTkSHkzAwOjAwOjAyLjAwODAwMDAwMAAfQ7Z1RM+/hPkglWXngQCji4EAAIAIC+Y7I6tgo4qBABWACAissw7Go4qBACmACAissw7Go4qBAD2ACAissw7Go4qBAFGACAissw7Go4qBAGWACAissw7Go4qBAHmACAissw7Go4qBAI2ACAissw7Go4qBAKGACAissw7Go4qBALWACAissw7Go4qBAMmACAissw7Go4qBAN2ACAissw7Go4qBAPGACAissw7Go4qBAQWACAissw7Go4qBARmACAissw7Go4qBAS2ACAissw7Go4qBAUGACAissw7Go4qBAVWACAissw7Go4qBAWmACAissw7Go4qBAX2ACAissw7Go4qBAZGACAissw7Go4qBAaWACAissw7Go4qBAbmACAissw7Go4qBAc2ACAissw7Go4qBAeGACAissw7Go4qBAfWACAissw7Go4qBAgmACAissw7Go4qBAh2ACAissw7Go4qBAjGACAissw7Go4qBAkWACAissw7Go4qBAlmACAissw7Go4qBAm2ACAissw7Go4qBAoGACAissw7Go4qBApWACAissw7Go4qBAqmACAissw7Go4qBAr2ACAissw7Go4qBAtGACAissw7Go4qBAuWACAissw7Go4qBAvmACAissw7Go4qBAw2ACAissw7Go4qBAyGACAissw7Go4qBAzWACAissw7Go4qBA0mACAissw7Go4qBA12ACAissw7Go4qBA3GACAissw7Go4qBA4WACAissw7Go4qBA5mACAissw7Go4qBA62ACAissw7Go4qBA8GACAissw7Go4qBA9WACAissw7Go4qBA+mACAissw7Go4qBA/2ACAissw7Go4qBBBGACAissw7Go4qBBCWACAissw7Go4qBBDmACAissw7Go4qBBE2ACAissw7Go4qBBGGACAissw7Go4qBBHWACAissw7Go4qBBImACAissw7Go4qBBJ2ACAissw7Go4qBBLGACAissw7Go4qBBMWACAissw7Go4qBBNmACAissw7Go4qBBO2ACAissw7Go4qBBQGACAissw7Go4qBBRWACAissw7Go4qBBSmACAissw7Go4qBBT2ACAissw7Go4qBBVGACAissw7Go4qBBWWACAissw7Go4qBBXmACAissw7Go4qBBY2ACAissw7Go4qBBaGACAissw7Go4qBBbWACAissw7Go4qBBcmACAissw7Go4qBBd2ACAissw7Go4qBBfGACAissw7Go4qBBgWACAissw7Go4qBBhmACAissw7Go4qBBi2ACAissw7Go4qBBkGACAissw7Go4qBBlWACAissw7Go4qBBmmACAissw7Go4qBBn2ACAissw7Go4qBBpGACAissw7Go4qBBqWACAissw7Go4qBBrmACAissw7Go4qBBs2ACAissw7Go4qBBuGACAissw7Go4qBBvWACAissw7Go4qBBwmACAissw7Go4qBBx2ACAissw7Go4qBBzGACAissw7Go4qBB0WACAissw7Go4qBB1mACAissw7Go4qBB22ACAissw7Go4qBB4GACAissw7Go4qBB5WACAissw7Go4qBB6mACAissw7Go4qBB72ACAissw7GoJOhioEH0QAICKyzDsZ1ooQAzf5gHFO7a5e/hEkeRmu7j7OBALeK94EB8YICRPCBCQ==";

    [Fact]
    public async Task NavigationStartsEmpty()
    {
        await using var engine = CreateHeadlessEngine();

        Assert.Empty(engine.Navigation.Titles);
        Assert.Empty(engine.Navigation.Chapters);
        Assert.Null(engine.Navigation.SelectedTitleIndex);
        Assert.Null(engine.Navigation.SelectedChapterIndex);
    }

    [Fact]
    public async Task UnknownChapterIsRejected()
    {
        await using var engine = CreateHeadlessEngine();

        var exception = await Assert.ThrowsAsync<PlaybackException>(
            async () => await engine.Navigation.SelectChapterAsync(
                99,
                TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackErrorCode.ChapterNotFound, exception.Code);
    }

    [Fact]
    public async Task ChapteredMatroskaExposesChapterTimeline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.mka");

        try
        {
            await File.WriteAllBytesAsync(
                path,
                Convert.FromBase64String(ChapteredMatroskaBase64),
                cancellationToken);

            await using var engine = CreateHeadlessEngine();

            var chaptersReady = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            engine.Navigation.NavigationChanged += (_, _) =>
            {
                if (engine.Navigation.Chapters.Count >= 2)
                {
                    chaptersReady.TrySetResult();
                }
            };

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);

            await chaptersReady.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            await engine.Navigation.RefreshAsync(cancellationToken);

            Assert.Equal(2, engine.Navigation.Chapters.Count);

            if (engine.Navigation.Titles.Count > 0)
            {
                var title = Assert.Single(engine.Navigation.Titles);
                Assert.Equal(0, title.Index);
                Assert.Equal(2, title.ChapterCount);
                Assert.True(title.IsSelected);
            }

            var opening = engine.Navigation.Chapters[0];
            var main = engine.Navigation.Chapters[1];

            Assert.Equal("Opening", opening.Name);
            Assert.Equal(TimeSpan.Zero, opening.Start);
            Assert.Equal(TimeSpan.FromSeconds(1), opening.Duration);
            Assert.Equal(TimeSpan.FromSeconds(1), opening.End);

            Assert.Equal("Main", main.Name);
            Assert.Equal(TimeSpan.FromSeconds(1), main.Start);
            Assert.Equal(TimeSpan.FromSeconds(1), main.Duration);
            Assert.Equal(TimeSpan.FromSeconds(2), main.End);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ChapterSelectionAndAdjacentNavigationWork()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.mka");

        try
        {
            await File.WriteAllBytesAsync(
                path,
                Convert.FromBase64String(ChapteredMatroskaBase64),
                cancellationToken);

            await using var engine = CreateHeadlessEngine();

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);

            await WaitForChaptersAsync(engine, cancellationToken);

            await engine.Navigation.SelectChapterAsync(1, cancellationToken);
            await WaitForSelectedChapterAsync(engine, 1, cancellationToken);

            Assert.Equal(1, engine.Navigation.SelectedChapterIndex);

            Assert.True(
                await engine.Navigation.PreviousChapterAsync(cancellationToken));

            await WaitForSelectedChapterAsync(engine, 0, cancellationToken);

            Assert.Equal(0, engine.Navigation.SelectedChapterIndex);

            Assert.False(
                await engine.Navigation.PreviousChapterAsync(cancellationToken));

            Assert.True(
                await engine.Navigation.NextChapterAsync(cancellationToken));

            await WaitForSelectedChapterAsync(engine, 1, cancellationToken);

            Assert.False(
                await engine.Navigation.NextChapterAsync(cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task WaitForChaptersAsync(
        LibVlcPlaybackEngine engine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (engine.Navigation.Chapters.Count < 2)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for chapter discovery.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(50),
                cancellationToken);

            await engine.Navigation.RefreshAsync(cancellationToken);
        }
    }

    private static async Task WaitForSelectedChapterAsync(
        LibVlcPlaybackEngine engine,
        int chapterIndex,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (engine.Navigation.SelectedChapterIndex != chapterIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Timed out waiting for chapter {chapterIndex} selection.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                cancellationToken);

            await engine.Navigation.RefreshAsync(cancellationToken);
        }
    }

    private static LibVlcPlaybackEngine CreateHeadlessEngine() =>
        new(
            new LibVlcPlaybackOptions
            {
                Arguments =
                [
                    "--aout=dummy",
                    "--intf=dummy",
                    "--no-video-title-show"
                ]
            });
}
