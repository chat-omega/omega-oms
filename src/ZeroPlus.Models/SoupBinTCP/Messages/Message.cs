using System;

namespace ZeroPlus.Models.SoupBinTCP.Messages
{
    public abstract class Message
    {
        public const int LENGTH_OF_LENGTH_FIELD = 4;

        public int Length => Bytes.Length;

        public char Type => Convert.ToChar(Bytes[0]);

        public byte[] Bytes { get; protected set; } = [];

        public byte[] TotalBytes
        {
            get
            {
                byte[] result = new byte[Bytes.Length + LENGTH_OF_LENGTH_FIELD];
                byte[] lengthBytes = BitConverter.GetBytes(Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                }

                Array.Copy(lengthBytes, 0, result, 0, lengthBytes.Length);
                Array.Copy(Bytes, 0, result, LENGTH_OF_LENGTH_FIELD, Bytes.Length);
                return result;
            }
        }

        public static T LoadFromBytes<T>(byte[] bytes) where T : Message, new()
        {
            return new T
            {
                Bytes = bytes
            };
        }
    }
}