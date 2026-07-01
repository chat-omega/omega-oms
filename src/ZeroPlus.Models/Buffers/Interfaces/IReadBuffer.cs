namespace ZeroPlus.Models.Buffers.Interfaces
{
    public interface IReadBuffer
    {
        long Length { get; }

        void Clear();
        void Append(byte[] data, int offset, int length);
        int Read(byte[] bytes, int skip, int start, int end);
        void Remove(int length);
        void SeekOrigin();
    }
}