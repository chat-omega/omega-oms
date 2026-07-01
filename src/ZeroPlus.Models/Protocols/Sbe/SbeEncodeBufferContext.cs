using System;
using Org.SbeTool.Sbe.Dll;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;

namespace ZeroPlus.Models.Protocols.Sbe
{
    /// <summary>
    /// SBE-specific encode context that bundles the ISbeMessageEncoder,
    /// a reusable DirectBuffer, and the underlying byte[] lifecycle.
    /// Topics cast IEncodeBufferContext to this type to access SBE encoding.
    /// </summary>
    public class SbeEncodeBufferContext : IEncodeBufferContext
    {
        private byte[] _buffer;

        /// <summary>
        /// The SBE message encoder instance.
        /// </summary>
        public ISbeMessageEncoder Encoder { get; }

        /// <summary>
        /// The reusable DirectBuffer wrapping the encode buffer.
        /// </summary>
        public DirectBuffer DirectBuffer { get; }

        /// <inheritdoc />
        public byte[] Buffer => _buffer;

        public SbeEncodeBufferContext(ISbeMessageEncoder encoder, int initialCapacity = 1_048_576)
        {
            Encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _buffer = new byte[initialCapacity];
            DirectBuffer = new DirectBuffer(_buffer, ExpandBuffer);
        }

        private byte[] ExpandBuffer(int existingSize, int requestedSize)
        {
            int newSize = requestedSize * 2;
            _buffer = new byte[newSize];
            return _buffer;
        }
    }
}
