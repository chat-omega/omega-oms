using System;

namespace ZeroPlus.Models.Protocols
{
    public interface IMessageSender
    {
        public void SendSequenced(ReadOnlySpan<byte> bytes);
        public void SendCompressed(ReadOnlySpan<byte> bytes);
        public void SendUnSequenced(ReadOnlySpan<byte> bytes);
    }
}