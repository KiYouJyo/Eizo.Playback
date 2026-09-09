using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

internal sealed class AuthenticatedHttpMediaInput : MediaInput, IDisposable
{
    private const int MaximumReadBuffer = 256 * 1024;

    private readonly object _sync = new();
    private readonly Uri _uri;
    private readonly HttpClient _client;

    private HttpResponseMessage? _activeResponse;
    private Stream? _activeStream;
    private long _position;
    private long? _length;
    private bool _supportsRanges;
    private bool _disposed;

    public AuthenticatedHttpMediaInput(
        Uri uri,
        PlaybackNetworkAccess access)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(access);

        if (!uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Managed authenticated media input requires an absolute HTTP(S) URI.",
                nameof(uri));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException(
                "Credentials must not be embedded in the media URI.",
                nameof(uri));
        }

        _uri = uri;
        _client = CreateClient(uri, access);

        Probe();
        CanSeek = _supportsRanges;
    }

    internal long? Length => _length;

    internal bool SupportsRanges => _supportsRanges;

    internal Exception? LastError { get; private set; }

    public override bool Open(out ulong size)
    {
        lock (_sync)
        {
            try
            {
                ThrowIfDisposed();
                ResetActiveResponse();
                _position = 0;
                LastError = null;

                size = _length is >= 0
                    ? checked((ulong)_length.Value)
                    : ulong.MaxValue;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception;
                size = 0;
                return false;
            }
        }
    }

    public override int Read(IntPtr buffer, uint length)
    {
        if (buffer == IntPtr.Zero || length == 0)
            return 0;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_length is { } totalLength &&
                _position >= totalLength)
            {
                return 0;
            }

            var requested = (int)Math.Min(
                Math.Min((long)length, MaximumReadBuffer),
                int.MaxValue);

            if (_length is { } knownLength)
            {
                requested = (int)Math.Min(
                    requested,
                    Math.Max(0, knownLength - _position));
            }

            if (requested <= 0)
                return 0;

            var rented = ArrayPool<byte>.Shared.Rent(requested);

            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        EnsureActiveResponse();
                        var read = _activeStream!.Read(
                            rented,
                            0,
                            requested);

                        if (read > 0)
                        {
                            Marshal.Copy(
                                rented,
                                0,
                                buffer,
                                read);
                            _position += read;
                            LastError = null;
                            return read;
                        }

                        if (_length is not { } expected ||
                            _position >= expected)
                        {
                            return 0;
                        }

                        ResetActiveResponse();
                    }
                    catch (Exception exception) when (
                        exception is IOException or
                            HttpRequestException or
                            ObjectDisposedException)
                    {
                        LastError = exception;
                        ResetActiveResponse();

                        if (attempt == 1)
                            return -1;
                    }
                }

                return -1;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public override bool Seek(ulong offset)
    {
        lock (_sync)
        {
            try
            {
                ThrowIfDisposed();

                if (!CanSeek ||
                    offset > long.MaxValue)
                {
                    return false;
                }

                var target = checked((long)offset);

                if (_length is { } totalLength &&
                    target > totalLength)
                {
                    return false;
                }

                ResetActiveResponse();
                _position = target;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception;
                return false;
            }
        }
    }

    public override void Close()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            ResetActiveResponse();
            _position = 0;
        }
    }

    public new void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            ResetActiveResponse();
            _client.Dispose();
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Probe()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            _uri);
        request.Headers.Range = new RangeHeaderValue(0, 0);

        using var response = _client.Send(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            _supportsRanges = true;
            _length =
                response.Content.Headers.ContentRange?.Length ??
                response.Content.Headers.ContentLength;
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Authenticated media probe failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                inner: null,
                response.StatusCode);
        }

        _supportsRanges =
            response.Headers.AcceptRanges.Any(value =>
                string.Equals(
                    value,
                    "bytes",
                    StringComparison.OrdinalIgnoreCase));

        _length = response.Content.Headers.ContentLength;
    }

    private void EnsureActiveResponse()
    {
        if (_activeResponse is not null &&
            _activeStream is not null)
        {
            return;
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            _uri);

        if (_supportsRanges)
        {
            request.Headers.Range =
                new RangeHeaderValue(
                    _position,
                    to: null);
        }
        else if (_position != 0)
        {
            request.Dispose();
            throw new IOException(
                "The HTTP media source does not support byte-range seeking.");
        }

        HttpResponseMessage? response = null;

        try
        {
            response = _client.Send(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            if (_supportsRanges &&
                response.StatusCode != HttpStatusCode.PartialContent)
            {
                throw new IOException(
                    $"The HTTP media source stopped honoring byte ranges at offset {_position}; status={(int)response.StatusCode}.");
            }

            if (!_supportsRanges &&
                !response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Authenticated media request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    inner: null,
                    response.StatusCode);
            }

            if (_supportsRanges &&
                response.Content.Headers.ContentRange?.From is long from &&
                from != _position)
            {
                throw new IOException(
                    $"The HTTP media source returned range offset {from} instead of {_position}.");
            }

            _activeStream =
                response.Content.ReadAsStream();
            _activeResponse = response;
            response = null;
        }
        finally
        {
            request.Dispose();
            response?.Dispose();
        }
    }

    private void ResetActiveResponse()
    {
        _activeStream?.Dispose();
        _activeStream = null;

        _activeResponse?.Dispose();
        _activeResponse = null;
    }

    private static HttpClient CreateClient(
        Uri uri,
        PlaybackNetworkAccess access)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression =
                DecompressionMethods.None,
            PreAuthenticate = true,
            UseCookies = false
        };

        if (access.HasCredentials)
        {
            var credential = new NetworkCredential(
                access.UserName ?? string.Empty,
                access.Password ?? string.Empty);
            var cache = new CredentialCache();

            var authority = new UriBuilder(
                uri.Scheme,
                uri.Host,
                uri.IsDefaultPort ? -1 : uri.Port,
                "/").Uri;

            foreach (var scheme in new[]
            {
                "Basic",
                "Digest",
                "NTLM",
                "Negotiate"
            })
            {
                try
                {
                    cache.Add(
                        authority,
                        scheme,
                        credential);
                }
                catch (ArgumentException)
                {
                }
            }

            handler.Credentials = cache;
        }

        var client = new HttpClient(
            handler,
            disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Eizo.Playback/0.2.1");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept-Encoding",
            "identity");

        return client;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
