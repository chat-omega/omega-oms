using System;
using System.Buffers;
using ZeroPlus.Models.Buffers.Interfaces;

namespace ZeroPlus.Models.Buffers
{
    public class RingBuffer : IReadBuffer, IDisposable
    {
        // Default to 128MB, but must be a power of 2 for masking to work
        private const int DEFAULT_CAPACITY = 134_217_728;

        private byte[] _buffer;
        private int _head;
        private int _tail;
        private int _size;
        private int _mask;

        public long Length => _size;

        public RingBuffer(int initialCapacity = DEFAULT_CAPACITY)
        {
            int capacity = EnsurePowerOfTwo(initialCapacity);
            _buffer = ArrayPool<byte>.Shared.Rent(capacity);
            _mask = _buffer.Length - 1;
        }

        public void Append(byte[] buffer, int offset, int size)
        {
            if (size == 0) return;

            if (_size + size > _buffer.Length)
            {
                Grow(_size + size);
            }

            var source = new ReadOnlySpan<byte>(buffer, offset, size);

            // Calculate space until the physical end of the array
            int spaceToEnd = _buffer.Length - _tail;

            if (spaceToEnd >= size)
            {
                source.CopyTo(_buffer.AsSpan(_tail));
            }
            else
            {
                source.Slice(0, spaceToEnd).CopyTo(_buffer.AsSpan(_tail));
                source.Slice(spaceToEnd).CopyTo(_buffer.AsSpan(0));
            }

            _tail = (_tail + size) & _mask; // Bitwise AND instead of Modulo
            _size += size;
        }

        public int Read(byte[] buffer, int skip, int offset, int size)
        {
            if (size > _size - skip) size = Math.Max(0, _size - skip);
            if (size <= 0) return 0;

            // Fix: Correctly wrap the starting position using the mask
            int currentReadPos = (_head + skip) & _mask;
            int spaceToEnd = _buffer.Length - currentReadPos;
            var target = buffer.AsSpan(offset, size);

            if (spaceToEnd >= size)
            {
                _buffer.AsSpan(currentReadPos, size).CopyTo(target);
            }
            else
            {
                _buffer.AsSpan(currentReadPos, spaceToEnd).CopyTo(target);
                _buffer.AsSpan(0, size - spaceToEnd).CopyTo(target.Slice(spaceToEnd));
            }

            return size;
        }

        public void Remove(int size)
        {
            if (size <= 0) return;
            if (size > _size) size = _size;

            _head = (_head + size) & _mask;
            _size -= size;

            if (_size == 0)
            {
                _head = 0;
                _tail = 0;
            }
        }

        private void Grow(int minCapacity)
        {
            int newCapacity = EnsurePowerOfTwo(minCapacity);
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

            // Linearize data into the new buffer
            if (_size > 0)
            {
                Read(newBuffer, 0, 0, _size);
            }

            // Return old buffer to pool
            ArrayPool<byte>.Shared.Return(_buffer);

            _buffer = newBuffer;
            _mask = _buffer.Length - 1;
            _head = 0;
            _tail = _size;
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        public void SeekOrigin() { /* Implementation depends on interface requirements */ }

        private static int EnsurePowerOfTwo(int n)
        {
            if ((n & (n - 1)) == 0) return n;
            int v = n;
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
        }
    }
}