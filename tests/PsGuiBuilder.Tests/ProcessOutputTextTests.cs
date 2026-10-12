// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Tests;

public class ProcessOutputTextTests
{
    [Fact]
    public void Decodes_utf8_bytes_from_a_utf8_console_code_page()
    {
        // The bytes Windows PowerShell wrote on the GitHub Actions runner for "hello".
        var utf8 = new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x0d, 0x0a };

        Assert.False(ProcessOutputText.LooksLikeUtf16Le(utf8));
        Assert.Equal("hello\r\n", ProcessOutputText.Decode(utf8));
    }

    [Fact]
    public void Decodes_utf16_le_with_and_without_a_bom()
    {
        var utf16 = Encoding.Unicode.GetBytes("hello\r\n");
        var bom = new byte[utf16.Length + 2];
        bom[0] = 0xFF;
        bom[1] = 0xFE;
        utf16.CopyTo(bom, 2);

        Assert.Equal("hello\r\n", ProcessOutputText.Decode(utf16));
        Assert.Equal("hello\r\n", ProcessOutputText.Decode(bom));
        Assert.Equal(Encoding.Unicode.WebName, ProcessOutputText.SelectEncoding(utf16).WebName);
        Assert.Equal(Encoding.Unicode.WebName, ProcessOutputText.SelectEncoding(bom).WebName);
    }

    [Fact]
    public void Decodes_mixed_utf16_when_most_odd_bytes_are_nul()
    {
        var mixed = Encoding.Unicode.GetBytes("hello 世界\r\n");

        Assert.True(ProcessOutputText.LooksLikeUtf16Le(mixed));
        Assert.Equal("hello 世界\r\n", ProcessOutputText.Decode(mixed));
    }

    [Fact]
    public async Task Streams_short_reads_for_utf8_and_utf16()
    {
        var utf8 = new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x0d, 0x0a };
        var utf16 = Encoding.Unicode.GetBytes("hello\r\n");
        var bom = new byte[utf16.Length + 2];
        bom[0] = 0xFF;
        bom[1] = 0xFE;
        utf16.CopyTo(bom, 2);

        Assert.Equal("hello\r\n", await ReadInChunks(utf8, 3));
        Assert.Equal("hello\r\n", await ReadInChunks(utf16, 3));
        Assert.Equal("hello\r\n", await ReadInChunks(bom, 2));
        Assert.Equal("", await ReadInChunks(Array.Empty<byte>(), 4));
    }

    private static async Task<string> ReadInChunks(byte[] data, int chunkSize)
    {
        await using var stream = new ChunkStream(data, chunkSize);
        return await ProcessOutputText.ReadAllAsync(stream, CancellationToken.None);
    }

    private sealed class ChunkStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        private int _position;

        public ChunkStream(byte[] data, int chunkSize)
        {
            _data = data;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _data.Length - _position;
            if (remaining <= 0)
                return 0;

            var n = Math.Min(count, Math.Min(_chunkSize, remaining));
            Buffer.BlockCopy(_data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
