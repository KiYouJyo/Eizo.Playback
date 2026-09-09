using System.Net;
using System.Net.Sockets;
using System.Text;
using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class NetworkPlaybackIntegrationTests
{
    [Fact]
    public async Task AuthenticatedHttpSourceUsesRangeAndSupportsSeek()
    {
        await using var server = new AuthenticatedRangeServer(
            "alice",
            "secret");

        await using var engine = new LibVlcPlaybackEngine();

        var source = PlaybackSource.FromUri(
            server.MediaUri,
            "network-test.wav",
            new PlaybackNetworkAccess(
                "alice",
                "secret"));

        var cancellationToken =
            TestContext.Current.CancellationToken;

        await engine.OpenAsync(
            source,
            cancellationToken);
        await engine.PlayAsync(
            cancellationToken);

        await WaitUntilAsync(
            () =>
                engine.State is PlaybackState.Playing
                    or PlaybackState.Buffering
                    or PlaybackState.Paused,
            TimeSpan.FromSeconds(8),
            cancellationToken);

        await WaitUntilAsync(
            () => engine.Duration > TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(8),
            cancellationToken);

        await engine.SeekAsync(
            TimeSpan.FromSeconds(2),
            cancellationToken);

        await WaitUntilAsync(
            () => server.RangeRequestCount > 0,
            TimeSpan.FromSeconds(5),
            cancellationToken);

        Assert.True(server.AuthorizedRequestCount > 0);
        Assert.True(server.RangeRequestCount > 0);
        Assert.NotEqual(PlaybackState.Failed, engine.State);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    "Network playback condition was not reached.");

            await Task.Delay(
                50,
                cancellationToken);
        }
    }

    private sealed class AuthenticatedRangeServer
        : IAsyncDisposable
    {
        private readonly TcpListener _listener =
            new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _shutdown =
            new();
        private readonly string _expectedAuthorization;
        private readonly byte[] _payload =
            CreateWavePayload();
        private Task? _acceptLoop;
        private int _authorizedRequestCount;
        private int _rangeRequestCount;

        public AuthenticatedRangeServer(
            string userName,
            string password)
        {
            _expectedAuthorization =
                "Basic " +
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        userName + ":" + password));

            _listener.Start();
            var endpoint =
                (IPEndPoint)_listener.LocalEndpoint;

            MediaUri = new Uri(
                $"http://127.0.0.1:{endpoint.Port}/media.wav");

            _acceptLoop = AcceptLoopAsync(
                _shutdown.Token);
        }

        public Uri MediaUri { get; }

        public int AuthorizedRequestCount =>
            Volatile.Read(ref _authorizedRequestCount);

        public int RangeRequestCount =>
            Volatile.Read(ref _rangeRequestCount);

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _listener.Stop();

            if (_acceptLoop is not null)
            {
                try
                {
                    await _acceptLoop;
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SocketException)
                {
                }
            }

            _shutdown.Dispose();
        }

        private async Task AcceptLoopAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener
                        .AcceptTcpClientAsync(
                            cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(
                    () => HandleClientAsync(
                        client,
                        cancellationToken),
                    cancellationToken);
            }
        }

        private async Task HandleClientAsync(
            TcpClient client,
            CancellationToken cancellationToken)
        {
            using (client)
            await using (var stream =
                client.GetStream())
            {
                var request = await ReadRequestAsync(
                    stream,
                    cancellationToken);

                if (request is null)
                    return;

                if (!request.Headers.TryGetValue(
                        "Authorization",
                        out var authorization) ||
                    !string.Equals(
                        authorization,
                        _expectedAuthorization,
                        StringComparison.Ordinal))
                {
                    await WriteResponseAsync(
                        stream,
                        "401 Unauthorized",
                        [
                            "WWW-Authenticate: Basic realm=\"EizoTest\"",
                            "Content-Length: 0"
                        ],
                        null,
                        cancellationToken);
                    return;
                }

                Interlocked.Increment(
                    ref _authorizedRequestCount);

                if (!string.Equals(
                        request.Path,
                        "/media.wav",
                        StringComparison.Ordinal))
                {
                    await WriteResponseAsync(
                        stream,
                        "404 Not Found",
                        ["Content-Length: 0"],
                        null,
                        cancellationToken);
                    return;
                }

                var start = 0;
                var end = _payload.Length - 1;
                var partial = false;

                if (request.Headers.TryGetValue(
                        "Range",
                        out var rangeHeader) &&
                    TryParseRange(
                        rangeHeader,
                        _payload.Length,
                        out var rangeStart,
                        out var rangeEnd))
                {
                    start = rangeStart;
                    end = rangeEnd;
                    partial = true;
                    Interlocked.Increment(
                        ref _rangeRequestCount);
                }

                var length = end - start + 1;
                var headers = new List<string>
                {
                    "Content-Type: audio/wav",
                    "Accept-Ranges: bytes",
                    $"Content-Length: {length}"
                };

                if (partial)
                {
                    headers.Add(
                        $"Content-Range: bytes {start}-{end}/{_payload.Length}");
                }

                byte[]? body = null;

                if (!string.Equals(
                        request.Method,
                        "HEAD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    body = _payload
                        .AsSpan(start, length)
                        .ToArray();
                }

                await WriteResponseAsync(
                    stream,
                    partial
                        ? "206 Partial Content"
                        : "200 OK",
                    headers,
                    body,
                    cancellationToken);
            }
        }

        private static async Task<Request?> ReadRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var requestLine =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    requestLine))
            {
                return null;
            }

            var parts = requestLine.Split(
                ' ',
                3,
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return null;

            var headers = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var line =
                    await reader.ReadLineAsync(
                        cancellationToken);

                if (string.IsNullOrEmpty(line))
                    break;

                var separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                headers[line[..separator].Trim()] =
                    line[(separator + 1)..].Trim();
            }

            return new Request(
                parts[0],
                parts[1],
                headers);
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            string status,
            IEnumerable<string> headers,
            byte[]? body,
            CancellationToken cancellationToken)
        {
            var headerText =
                "HTTP/1.1 " + status + "\r\n" +
                string.Join(
                    "\r\n",
                    headers) +
                "\r\nConnection: close\r\n\r\n";

            var bytes =
                Encoding.ASCII.GetBytes(
                    headerText);

            await stream.WriteAsync(
                bytes,
                cancellationToken);

            if (body is { Length: > 0 })
            {
                await stream.WriteAsync(
                    body,
                    cancellationToken);
            }

            await stream.FlushAsync(
                cancellationToken);
        }

        private static bool TryParseRange(
            string value,
            int totalLength,
            out int start,
            out int end)
        {
            start = 0;
            end = totalLength - 1;

            if (!value.StartsWith(
                    "bytes=",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var firstRange = value[6..]
                .Split(',', 2)[0]
                .Trim();

            var dash = firstRange.IndexOf('-');
            if (dash < 0 ||
                !int.TryParse(
                    firstRange[..dash],
                    out start))
            {
                return false;
            }

            if (dash < firstRange.Length - 1 &&
                int.TryParse(
                    firstRange[(dash + 1)..],
                    out var requestedEnd))
            {
                end = Math.Min(
                    requestedEnd,
                    totalLength - 1);
            }

            start = Math.Clamp(
                start,
                0,
                totalLength - 1);
            end = Math.Clamp(
                end,
                start,
                totalLength - 1);

            return true;
        }

        private static byte[] CreateWavePayload()
        {
            const int sampleRate = 44_100;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int seconds = 4;
            var sampleCount =
                sampleRate * seconds;
            var dataLength =
                sampleCount *
                channels *
                (bitsPerSample / 8);

            using var memory =
                new MemoryStream(
                    44 + dataLength);
            using var writer =
                new BinaryWriter(
                    memory,
                    Encoding.ASCII,
                    leaveOpen: true);

            writer.Write(
                Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(
                Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(
                Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(
                sampleRate *
                channels *
                (bitsPerSample / 8));
            writer.Write(
                (short)(
                    channels *
                    (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(
                Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            for (var i = 0; i < sampleCount; i++)
            {
                var sample =
                    (short)(
                        Math.Sin(
                            2d *
                            Math.PI *
                            440d *
                            i /
                            sampleRate) *
                        short.MaxValue *
                        0.08d);
                writer.Write(sample);
            }

            writer.Flush();
            return memory.ToArray();
        }

        private sealed record Request(
            string Method,
            string Path,
            IReadOnlyDictionary<string, string> Headers);
    }
}
