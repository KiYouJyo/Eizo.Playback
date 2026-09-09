using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class AuthenticatedHttpMediaInputTests
{
    [Fact]
    public async Task AuthenticatedRangeInputReadsAndSeeksWithoutCredentialUri()
    {
        var payload = Enumerable.Range(0, 128 * 1024)
            .Select(index => (byte)(index % 251))
            .ToArray();

        await using var server = new BasicRangeServer(
            "alice",
            "secret",
            payload);

        using var input = new AuthenticatedHttpMediaInput(
            server.MediaUri,
            new PlaybackNetworkAccess(
                "alice",
                "secret"));

        Assert.True(input.SupportsRanges);
        Assert.Equal(payload.LongLength, input.Length);
        Assert.DoesNotContain(
            "alice",
            server.MediaUri.AbsoluteUri,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "secret",
            server.MediaUri.AbsoluteUri,
            StringComparison.Ordinal);

        Assert.True(input.Open(out var size));
        Assert.Equal((ulong)payload.LongLength, size);

        var pointer = Marshal.AllocHGlobal(64);
        try
        {
            var firstRead = input.Read(pointer, 64);
            Assert.Equal(64, firstRead);

            var first = new byte[firstRead];
            Marshal.Copy(pointer, first, 0, firstRead);
            Assert.Equal(payload[..64], first);

            Assert.True(input.Seek(8192));

            var seekRead = input.Read(pointer, 64);
            Assert.Equal(64, seekRead);

            var afterSeek = new byte[seekRead];
            Marshal.Copy(pointer, afterSeek, 0, seekRead);
            Assert.Equal(payload[8192..8256], afterSeek);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        Assert.True(server.AuthorizedRequestCount >= 3);
        Assert.True(server.RangeRequestCount >= 3);
    }

    [Fact]
    public async Task WrongCredentialFailsDuringManagedProbe()
    {
        await using var server = new BasicRangeServer(
            "alice",
            "secret",
            new byte[1024]);

        var exception = Assert.Throws<HttpRequestException>(() =>
            new AuthenticatedHttpMediaInput(
                server.MediaUri,
                new PlaybackNetworkAccess(
                    "alice",
                    "wrong")));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            exception.StatusCode);
        Assert.True(server.UnauthorizedRequestCount > 0);
    }

    private sealed class BasicRangeServer
        : IAsyncDisposable
    {
        private readonly TcpListener _listener =
            new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _shutdown =
            new();
        private readonly string _expectedAuthorization;
        private readonly byte[] _payload;
        private readonly Task _acceptLoop;

        private int _authorizedRequestCount;
        private int _unauthorizedRequestCount;
        private int _rangeRequestCount;

        public BasicRangeServer(
            string userName,
            string password,
            byte[] payload)
        {
            _payload = payload;
            _expectedAuthorization =
                "Basic " +
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        userName + ":" + password));

            _listener.Start();

            var endpoint =
                (IPEndPoint)_listener.LocalEndpoint;

            MediaUri = new Uri(
                $"http://127.0.0.1:{endpoint.Port}/media.bin");

            _acceptLoop =
                AcceptLoopAsync(_shutdown.Token);
        }

        public Uri MediaUri { get; }

        public int AuthorizedRequestCount =>
            Volatile.Read(
                ref _authorizedRequestCount);

        public int UnauthorizedRequestCount =>
            Volatile.Read(
                ref _unauthorizedRequestCount);

        public int RangeRequestCount =>
            Volatile.Read(
                ref _rangeRequestCount);

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _listener.Stop();

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
                    client =
                        await _listener.AcceptTcpClientAsync(
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
                var request =
                    await ReadRequestAsync(
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
                    Interlocked.Increment(
                        ref _unauthorizedRequestCount);

                    await WriteResponseAsync(
                        stream,
                        "401 Unauthorized",
                        [
                            "WWW-Authenticate: Basic realm=\"EizoPlaybackTest\"",
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
                        "/media.bin",
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
                        out var range) &&
                    TryParseRange(
                        range,
                        _payload.Length,
                        out var requestedStart,
                        out var requestedEnd))
                {
                    start = requestedStart;
                    end = requestedEnd;
                    partial = true;

                    Interlocked.Increment(
                        ref _rangeRequestCount);
                }

                var length = end - start + 1;
                var headers = new List<string>
                {
                    "Content-Type: application/octet-stream",
                    "Accept-Ranges: bytes",
                    $"Content-Length: {length}"
                };

                if (partial)
                {
                    headers.Add(
                        $"Content-Range: bytes {start}-{end}/{_payload.Length}");
                }

                var body = _payload
                    .AsSpan(start, length)
                    .ToArray();

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
                    out var parsedEnd))
            {
                end = Math.Min(
                    parsedEnd,
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

            var headers =
                new Dictionary<string, string>(
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

            var headerBytes =
                Encoding.ASCII.GetBytes(
                    headerText);

            await stream.WriteAsync(
                headerBytes,
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

        private sealed record Request(
            string Method,
            string Path,
            IReadOnlyDictionary<string, string> Headers);
    }
}
