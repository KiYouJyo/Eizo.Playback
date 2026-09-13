using System.Buffers;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

internal sealed class RandomAccessMediaInput : MediaInput, IDisposable
{
    private const int MaximumReadBuffer = 256 * 1024;

    private readonly object _sync = new();
    private readonly IPlaybackRandomAccessSource _source;
    private CancellationTokenSource _ioCancellation = new();

    private long _position;
    private long? _length;
    private bool _disposed;

    public RandomAccessMediaInput(
        IPlaybackRandomAccessSource source,
        CancellationToken cancellationToken = default)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        using var registration = cancellationToken.Register(Interrupt);
        var info = source.GetInfoAsync(cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (info.Length is < 0)
            throw new IOException("Random-access media length cannot be negative.");

        _length = info.Length;
        CanSeek = info.CanSeek;
    }

    public override bool Open(out ulong size)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                size = 0;
                return false;
            }

            if (_ioCancellation.IsCancellationRequested)
            {
                _ioCancellation.Dispose();
                _ioCancellation = new CancellationTokenSource();
            }

            _position = 0;
            size = _length is >= 0
                ? checked((ulong)_length.Value)
                : ulong.MaxValue;
            return true;
        }
    }

    public override int Read(IntPtr buffer, uint length)
    {
        if (buffer == IntPtr.Zero || length == 0)
            return 0;

        lock (_sync)
        {
            if (_disposed || _ioCancellation.IsCancellationRequested)
                return -1;

            if (_length is { } totalLength && _position >= totalLength)
                return 0;

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
                var read = _source.ReadAsync(
                        _position,
                        rented.AsMemory(0, requested),
                        _ioCancellation.Token)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();

                if (read < 0 || read > requested)
                    return -1;

                if (read == 0)
                    return 0;

                Marshal.Copy(rented, 0, buffer, read);
                _position += read;
                return read;
            }
            catch (OperationCanceledException)
            {
                return -1;
            }
            catch (Exception exception)
                when (exception is IOException or
                      HttpRequestException or
                      ObjectDisposedException)
            {
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
            if (_disposed || !CanSeek || offset > long.MaxValue)
                return false;

            var target = checked((long)offset);
            if (_length is { } length && target > length)
                return false;

            _position = target;
            return true;
        }
    }

    public override void Close()
    {
        lock (_sync)
        {
            if (!_disposed)
                _position = 0;
        }
    }

    internal void Interrupt() => _ioCancellation.Cancel();

    public new void Dispose()
    {
        Interrupt();

        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _ioCancellation.Dispose();
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
