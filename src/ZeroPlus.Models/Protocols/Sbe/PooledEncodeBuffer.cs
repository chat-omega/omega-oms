using System;
using System.Buffers;
using Microsoft.Extensions.ObjectPool;
using Org.SbeTool.Sbe.Dll;

namespace ZeroPlus.Models.Protocols.Sbe;

/// <summary>
/// Thread-safe pooled encode buffer. Each <see cref="Rent"/> call returns an independent
/// <see cref="DirectBuffer"/> + <c>byte[]</c> pair backed by <see cref="ArrayPool{T}"/>.
/// No locks needed — concurrent callers get separate buffer instances.
/// </summary>
public sealed class PooledEncodeBuffer
{
    private static readonly ObjectPool<PooledEncodeBuffer> Pool =
        new DefaultObjectPool<PooledEncodeBuffer>(
            new DefaultPooledObjectPolicy<PooledEncodeBuffer>(),
            maximumRetained: Environment.ProcessorCount * 2);

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(1_048_576);
    private byte[]? _previousBuffer;

    public DirectBuffer DirectBuffer { get; }

    public PooledEncodeBuffer()
    {
        DirectBuffer = new DirectBuffer(_buffer, ExpandBuffer);
    }

    private byte[] ExpandBuffer(int existingSize, int requestedSize)
    {
        // Do NOT return the old buffer to ArrayPool here.
        // DirectBuffer.TryResizeBuffer will Marshal.Copy from the old pinned
        // buffer into the new one AFTER this callback returns. Returning it
        // now would let another thread rent and overwrite it before the copy.
        _previousBuffer = _buffer;
        _buffer = ArrayPool<byte>.Shared.Rent(requestedSize * 2);
        return _buffer;
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> over the first <paramref name="bytesWritten"/> bytes of the buffer.
    /// </summary>
    public ReadOnlySpan<byte> Slice(int bytesWritten)
        => new ReadOnlySpan<byte>(_buffer, 0, bytesWritten);

    /// <summary>
    /// Rent an encode buffer from the pool. Call <see cref="Return"/> when done.
    /// </summary>
    public static PooledEncodeBuffer Rent() => Pool.Get();

    /// <summary>
    /// Return this buffer to the pool for reuse.
    /// </summary>
    public void Return()
    {
        if (_previousBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_previousBuffer);
            _previousBuffer = null;
        }
        Pool.Return(this);
    }
}
