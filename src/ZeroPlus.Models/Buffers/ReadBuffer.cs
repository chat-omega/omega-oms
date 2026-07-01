using System.IO;
using ZeroPlus.Models.Buffers.Interfaces;

namespace ZeroPlus.Models.Buffers
{
    public class ReadBuffer : IReadBuffer
    {
        private MemoryStream Buffer { get; set; }
        public long Length => Buffer.Length;

        public ReadBuffer()
        {
            Buffer = new MemoryStream();
        }

        public void Append(byte[] data, int offset, int length)
        {
            Buffer.Position = Buffer.Length;
            Buffer.Write(data, offset, length);
        }

        public void Remove(int length)
        {
            if (Buffer.Length > length)
            {
                byte[] buffer = Buffer.GetBuffer();
                int length1 = (int)Buffer.Length;
                Clear();
                Buffer.Write(buffer, length, length1 - length);
            }
            else
            {
                Clear();
            }
        }

        public void Clear()
        {
            Buffer.SetLength(0L);
        }

        public int Read(byte[] bytes, int skip, int start, int end)
        {
            return Buffer.Read(bytes, 0, end);
        }

        public void SeekOrigin()
        {
            Buffer.Seek(0L, System.IO.SeekOrigin.Begin);
        }
    }
}
