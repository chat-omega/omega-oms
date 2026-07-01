namespace ZeroPlus.Models.Protocols
{
    public interface IMessageParser
    {
        void Parse(byte[] message);

        /// <summary>
        /// Parse a message from a region of a buffer. Used to avoid intermediate
        /// copies when the caller owns a pooled/rented array.
        /// </summary>
        void Parse(byte[] buffer, int offset, int length)
        {
            byte[] slice = new byte[length];
            System.Buffer.BlockCopy(buffer, offset, slice, 0, length);
            Parse(slice);
        }
    }
}