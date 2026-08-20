using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services.Network
{
    /// <summary>
    /// Stream wrapper that fragments the TLS ClientHello and HTTP request headers into separate TCP packets.
    /// This prevents Deep Packet Inspection (DPI) firewalls from inspecting the SNI domain name.
    /// </summary>
    public class AntiDpiStream : Stream
    {
        private readonly NetworkStream _innerStream;
        private bool _isFirstWrite = true;

        public AntiDpiStream(NetworkStream innerStream)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

        public override void Flush() => _innerStream.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _innerStream.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_isFirstWrite && buffer.Length > 40)
            {
                _isFirstWrite = false;
                var span = buffer.Span;

                bool isTlsHandshake = span[0] == 0x16 && span[1] == 0x03;
                bool isHttpRequest = span.Length >= 4 && (
                    span[0] == (byte)'G' ||
                    span[0] == (byte)'P' ||
                    span[0] == (byte)'H' ||
                    span[0] == (byte)'D'
                );

                if (isTlsHandshake)
                {
                    // 1. Write the 5-byte TLS Record Header
                    int chunk1 = Math.Min(5, buffer.Length);
                    await _innerStream.WriteAsync(buffer.Slice(0, chunk1), cancellationToken);
                    await _innerStream.FlushAsync(cancellationToken);

                    // 2. Write next 30 bytes (ClientHello before full SNI)
                    int remaining1 = buffer.Length - chunk1;
                    if (remaining1 > 0)
                    {
                        int chunk2 = Math.Min(30, remaining1);
                        await _innerStream.WriteAsync(buffer.Slice(chunk1, chunk2), cancellationToken);
                        await _innerStream.FlushAsync(cancellationToken);

                        // 3. Write remaining bytes containing SNI and cipher suites
                        int remaining2 = remaining1 - chunk2;
                        if (remaining2 > 0)
                        {
                            await _innerStream.WriteAsync(buffer.Slice(chunk1 + chunk2, remaining2), cancellationToken);
                            await _innerStream.FlushAsync(cancellationToken);
                        }
                    }
                    return;
                }
                else if (isHttpRequest)
                {
                    // Fragment HTTP Request: write first 1 byte (Method initial), flush, then remaining
                    await _innerStream.WriteAsync(buffer.Slice(0, 1), cancellationToken);
                    await _innerStream.FlushAsync(cancellationToken);

                    if (buffer.Length > 1)
                    {
                        await _innerStream.WriteAsync(buffer.Slice(1), cancellationToken);
                        await _innerStream.FlushAsync(cancellationToken);
                    }
                    return;
                }
            }

            await _innerStream.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
