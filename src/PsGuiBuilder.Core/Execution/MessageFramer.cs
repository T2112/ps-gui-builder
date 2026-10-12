// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Buffers.Binary;

namespace PsGuiBuilder.Core.Execution;

public static class MessageFramer
{
    public const int HeaderBytes = 4;
    public const int MaxPayloadBytes = 4 * 1024 * 1024;

    public static byte[] Frame(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayloadBytes)
            throw new InvalidOperationException($"Execution message is larger than {MaxPayloadBytes} bytes.");

        var frame = new byte[HeaderBytes + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderBytes));
        return frame;
    }

    public static async Task<byte[]?> ReadPayloadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        if (!await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false))
            return null;

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 0 || length > MaxPayloadBytes)
            throw new InvalidDataException($"Invalid frame length {length}.");

        if (length == 0)
            return Array.Empty<byte>();

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false))
            throw new EndOfStreamException("The connection closed in the middle of a frame.");

        return payload;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                if (read == 0)
                    return false;

                throw new EndOfStreamException("The connection closed in the middle of a frame.");
            }

            read += count;
        }

        return true;
    }
}
