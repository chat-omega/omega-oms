using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Org.SbeTool.Sbe.Dll;

namespace ZeroPlus.Models.Buffers
{
    public class DirectBufferPooledObjectPolicy : PooledObjectPolicy<DirectBuffer>
    {
        private readonly ILogger<DirectBufferPooledObjectPolicy> _logger;
        private int _instances;
        public int Id { get; }
        public int InitialCapacity { get; set; } = 1_000_000;
        public int MaxInitialCapacity { get; set; } = 100_000_000;

        public DirectBufferPooledObjectPolicy(ILogger<DirectBufferPooledObjectPolicy> logger)
        {
            _logger = logger;
            Id = _instances;
        }

        public override DirectBuffer Create()
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Creating a new buffer. Count: " + ++_instances);
            }
            if (InitialCapacity > 0)
            {
                byte[] startingBuffer = new byte[InitialCapacity];
                return new DirectBuffer(startingBuffer, ExpandBuffer);
            }
            else
            {
                return new DirectBuffer(ExpandBuffer);
            }
        }

        public override bool Return(DirectBuffer obj)
        {
            obj.Wrap(System.Array.Empty<byte>());
            return true;
        }

        private byte[] ExpandBuffer(int existingBufferSize, int requestedBufferSize)
        {
            int size = requestedBufferSize * 2;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"Buffer expand requested. Id: {Id}, Existing size: {existingBufferSize}, Requested size: {requestedBufferSize}, New size: {size}");
            }

            return new byte[size];
        }
    }
}
