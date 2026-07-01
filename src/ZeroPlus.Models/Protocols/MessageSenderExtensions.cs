using System;

namespace ZeroPlus.Models.Protocols;

public static class MessageSenderExtensions
{
    /// <summary>
    /// Sends encoded bytes using the appropriate method based on the compressed flag.
    /// Matches the original SoupBinTcpEncoder routing behavior.
    /// </summary>
    public static void SendEncoded(this IMessageSender sender, byte[] encodeBuffer, int bytesWritten, bool compressed)
    {
        var span = new ReadOnlySpan<byte>(encodeBuffer, 0, bytesWritten);
        if (compressed)
            sender.SendCompressed(span);
        else
            sender.SendSequenced(span);
    }

    /// <summary>
    /// Resolves the active encode buffer from the context at send time.
    /// This avoids stale array references when the underlying buffer expands.
    /// </summary>
    public static void SendEncoded(this IMessageSender sender, IEncodeBufferContext encodeContext, int bytesWritten, bool compressed)
    {
        sender.SendEncoded(encodeContext.Buffer, bytesWritten, compressed);
    }
}