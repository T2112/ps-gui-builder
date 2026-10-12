// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;

namespace PsGuiBuilder.Core.Execution;

// Decodes captured process output. Windows PowerShell writes UTF-16 LE when its
// output encoding is Unicode, and UTF-8 when the console code page is UTF-8.
// pwsh writes UTF-8. The first bytes choose the decoder.
public static class ProcessOutputText
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return "";

        var encoding = SelectEncoding(bytes);
        var skip = BomLength(bytes);
        return encoding.GetString(bytes[skip..]).TrimStart('\uFEFF');
    }

    public static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        await CopyAsync(stream, text => { builder.Append(text); }, cancellationToken).ConfigureAwait(false);
        return builder.ToString();
    }

    public static async Task CopyAsync(Stream stream, Action<string> write, CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (write is null)
            throw new ArgumentNullException(nameof(write));

        var buffer = new byte[4096];
        var pending = new List<byte>(64);
        Decoder? decoder = null;
        Encoding? encoding = null;
        var first = true;

        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (decoder is null)
            {
                if (count > 0)
                {
                    for (var i = 0; i < count; i++)
                        pending.Add(buffer[i]);
                }

                if (!ReadyToDetect(pending, endOfStream: count == 0))
                {
                    if (count == 0)
                        return;
                    continue;
                }

                var sample = pending.ToArray();
                encoding = SelectEncoding(sample);
                decoder = encoding.GetDecoder();
                var skip = BomLength(sample);
                Emit(encoding, decoder, sample, skip, sample.Length - skip, flush: count == 0, write, ref first);
                pending.Clear();
                if (count == 0)
                    return;
                continue;
            }

            if (count == 0)
            {
                Emit(encoding!, decoder, buffer, 0, 0, flush: true, write, ref first);
                return;
            }

            Emit(encoding!, decoder, buffer, 0, count, flush: false, write, ref first);
        }
    }

    public static Encoding SelectEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Utf8;
        if (LooksLikeUtf16Le(bytes))
            return Encoding.Unicode;
        return Utf8;
    }

    public static bool LooksLikeUtf16Le(ReadOnlySpan<byte> bytes)
    {
        var pairs = bytes.Length / 2;
        if (pairs == 0)
            return false;

        var zeros = 0;
        for (var i = 1; i < pairs * 2; i += 2)
        {
            if (bytes[i] == 0)
                zeros++;
        }

        return zeros > 0 && zeros * 4 >= pairs * 3;
    }

    private static int BomLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return 2;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return 3;
        return 0;
    }

    private static bool ReadyToDetect(List<byte> pending, bool endOfStream)
    {
        var count = pending.Count;
        if (count == 0)
            return false;
        if (endOfStream)
            return true;
        if (count >= 3)
            return true;
        return count == 2 && !(pending[0] == 0xEF && pending[1] == 0xBB);
    }

    private static void Emit(
        Encoding encoding,
        Decoder decoder,
        byte[] buffer,
        int offset,
        int count,
        bool flush,
        Action<string> write,
        ref bool first)
    {
        if (count == 0 && !flush)
            return;

        var maxChars = encoding.GetMaxCharCount(Math.Max(count, 1));
        var chars = new char[maxChars];
        decoder.Convert(buffer, offset, count, chars, 0, chars.Length, flush, out _, out var used, out _);
        if (used == 0)
            return;

        var text = new string(chars, 0, used);
        if (first)
        {
            text = text.TrimStart('\uFEFF');
            first = false;
        }

        if (text.Length > 0)
            write(text);
    }
}
