namespace ZeroPlus.Models.Protocols
{
    /// <summary>
    /// Protocol-agnostic context for encoding messages into a reusable buffer.
    /// Concrete implementations (e.g. SbeEncodeBufferContext) carry the encoder
    /// and buffer-wrapper types specific to a particular protocol.
    /// </summary>
    public interface IEncodeBufferContext
    {
        /// <summary>
        /// The current encode buffer. Always returns the latest byte[]
        /// even after buffer expansion (property is evaluated at access time).
        /// </summary>
        byte[] Buffer { get; }
    }
}
