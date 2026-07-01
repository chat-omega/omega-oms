using K4os.Compression.LZ4;
using System;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class CompressedData : Message
    {
        static readonly byte _typeByte = Convert.ToByte('C');

        public byte[] Message => LZ4Pickler.Unpickle(Bytes, 1, Length - 1);

        public CompressedData()
        {
        }

        public CompressedData(byte[] message)
        {
            message = LZ4Pickler.Pickle(message);
            var messageLength = message.Length;
            byte[] payload = new byte[messageLength + 1];
            payload[0] = _typeByte;
            Array.Copy(message, 0, payload, 1, messageLength);
            Bytes = payload;
        }
    }
}