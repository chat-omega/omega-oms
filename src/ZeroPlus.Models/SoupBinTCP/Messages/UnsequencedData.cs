using System;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public class UnSequencedData : Message
    {
        static readonly byte _typeByte = Convert.ToByte('U');

        public byte[] Message
        {
            get
            {
                int len = Length - 1;
                byte[] result = new byte[len];
                Buffer.BlockCopy(Bytes, 1, result, 0, len);
                return result;
            }
        }

        public UnSequencedData()
        {
        }

        public UnSequencedData(byte[] message)
        {
            var messageLength = message.Length;
            byte[] payload = new byte[messageLength + 1];
            payload[0] = _typeByte;
            Array.Copy(message, 0, payload, 1, messageLength);
            Bytes = payload;
        }
    }
}