namespace Eizo.Playback.Backends.LibVLC;

internal static class LibVlcCodecFormatter
{
    public static string? FromFourCc(uint value)
    {
        if (value == 0)
        {
            return null;
        }

        Span<char> characters = stackalloc char[4];
        var length = 0;

        for (var index = 0; index < 4; index++)
        {
            var current = (byte)((value >> (index * 8)) & 0xff);

            if (current == 0)
            {
                continue;
            }

            if (current is < 0x20 or > 0x7e)
            {
                return $"0x{value:X8}";
            }

            characters[length++] = (char)current;
        }

        if (length == 0)
        {
            return null;
        }

        return new string(characters[..length]).ToUpperInvariant();
    }
}
