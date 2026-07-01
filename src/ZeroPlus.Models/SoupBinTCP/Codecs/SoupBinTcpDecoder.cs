using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ZeroPlus.Models.Buffers.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;
using ZeroPlus.Models.SoupBinTCP.Data;
using ZeroPlus.Models.SoupBinTCP.Messages;

namespace ZeroPlus.Models.SoupBinTCP.Codecs
{
    public class SoupBinTcpDecoder : ISoupBinTcpDecoder
    {
        public event DebugHandler? DebugHandler;
        public event LoginAcceptedHandler? LoginAccepted;
        public event LoginRejectedHandler? LoginRejected;
        public event ServerHeartbeatHandler? ServerHeartbeat;
        public event EndOfSessionHandler? EndOfSession;
        public event LoginRequestHandler? LoginRequest;
        public event ClientHeartbeatHandler? ClientHeartbeat;
        public event LogoutRequestHandler? LogoutRequest;

        /// <summary>
        /// Messages smaller than this threshold use heap allocation (Gen0 fast-path).
        /// Messages at or above this threshold use ArrayPool to avoid Gen1/Gen2 GC pressure.
        /// </summary>
        private const int PoolingThreshold = 512;

        private readonly ILogger? _logger;
        private readonly IReadBuffer _readBuffer;

        private readonly Channel<PooledBuffer> _inBoundMessagesQueue;
        private Task? _processingTask;
        private readonly byte[] _lengthBytes;
        private CancellationTokenSource? _cts;

        public IMessageParser? MessageDecoder { get; set; }

        public SoupBinTcpDecoder(ILogger<SoupBinTcpDecoder> logger, IReadBuffer readBuffer) : this((ILogger)logger, readBuffer) { }

        public SoupBinTcpDecoder(ILogger? logger, IReadBuffer readBuffer)
        {
            _logger = logger;
            _readBuffer = readBuffer;
            _inBoundMessagesQueue = Channel.CreateUnbounded<PooledBuffer>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
            _lengthBytes = new byte[Message.LENGTH_OF_LENGTH_FIELD];
            StartEngine();
        }

        public void Reset()
        {
            _readBuffer.Clear();
        }

        public void Parse(byte[] buffer, long offset, long size)
        {
            _readBuffer.Append(buffer, (int)offset, (int)size);
            var len = Message.LENGTH_OF_LENGTH_FIELD;
            while (_readBuffer.Length > len)
            {
                _readBuffer.SeekOrigin();
                int bytesRead = _readBuffer.Read(_lengthBytes, 0, 0, len);
                if (bytesRead != len)
                {
                    break;
                }

                int nextMessageLength = BinaryPrimitives.ReadInt32BigEndian(_lengthBytes);
                if (_readBuffer.Length < nextMessageLength + len)
                {
                    break;
                }

                bool usePool = nextMessageLength >= PoolingThreshold;
                byte[] bytes = usePool
                    ? ArrayPool<byte>.Shared.Rent(nextMessageLength)
                    : new byte[nextMessageLength];
                bytesRead = _readBuffer.Read(bytes, bytesRead, 0, nextMessageLength);
                if (bytesRead == nextMessageLength)
                {
                    _inBoundMessagesQueue.Writer.TryWrite(new PooledBuffer(bytes, nextMessageLength, usePool));
                    _readBuffer.Remove(nextMessageLength + len);
                }
                else if (usePool)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
        }

        public void StartEngine()
        {
            StopEngine();
            _cts = new CancellationTokenSource();
            _processingTask = Task.Factory.StartNew(
                () => MessageProcessor(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void StopEngine()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            if (_processingTask != null)
            {
                try { _processingTask.Wait(); } catch { /* ignored */ }
                _processingTask = null;
            }
        }

        private void MessageProcessor(CancellationToken token)
        {
            var reader = _inBoundMessagesQueue.Reader;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    while (reader.TryRead(out var msg))
                    {
                        Decode(msg);
                    }

                    reader.WaitToReadAsync(token).AsTask().GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Message processor crashed!");
            }
        }

        private void Decode(PooledBuffer msg)
        {
            byte[] buffer = msg.Array;
            int length = msg.Length;
            try
            {
                if (length == 0)
                {
                    _logger?.LogError("Invalid buffer! " + nameof(buffer));
                    return;
                }

                byte type = buffer[0];

                switch ((MessagePacketType)type)
                {
                    // Hot path: bypass Message wrappers, pass buffer + offset directly
                    case MessagePacketType.SequencedData:
                        MessageDecoder?.Parse(buffer, 1, length - 1);
                        break;
                    case MessagePacketType.UnSequencedData:
                        MessageDecoder?.Parse(buffer, 1, length - 1);
                        break;
                    case MessagePacketType.CompressedData:
                        byte[] decompressed = LZ4Pickler.Unpickle(buffer, 1, length - 1);
                        MessageDecoder?.Parse(decompressed);
                        break;

                    // Control messages: rare, copy to exact-size array for existing handlers
                    default:
                        DecodeControlMessage(type, buffer, length);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(Decode));
            }
            finally
            {
                if (msg.IsPooled)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void DecodeControlMessage(byte type, byte[] buffer, int length)
        {
            // Control messages are infrequent; allocate exact-size array for Message class compat
            byte[] exact = new byte[length];
            Buffer.BlockCopy(buffer, 0, exact, 0, length);

            switch ((MessagePacketType)type)
            {
                case MessagePacketType.Debug:
                    HandleDebug(Message.LoadFromBytes<Debug>(exact));
                    break;
                case MessagePacketType.LoginAccepted:
                    HandleLoginAccepted(Message.LoadFromBytes<LoginAccepted>(exact));
                    break;
                case MessagePacketType.LoginRejected:
                    HandleLoginRejected(Message.LoadFromBytes<LoginRejected>(exact));
                    break;
                case MessagePacketType.ServerHeartbeat:
                    HandleServerHeartbeat(Message.LoadFromBytes<ServerHeartbeat>(exact));
                    break;
                case MessagePacketType.EndOfSession:
                    HandleEndOfSession(Message.LoadFromBytes<EndOfSession>(exact));
                    break;
                case MessagePacketType.LoginRequest:
                    HandleLoginRequest(Message.LoadFromBytes<LoginRequest>(exact));
                    break;
                case MessagePacketType.ClientHeartbeat:
                    HandleClientHeartbeat(Message.LoadFromBytes<ClientHeartbeat>(exact));
                    break;
                case MessagePacketType.LogoutRequest:
                    HandleLogoutRequest(Message.LoadFromBytes<LogoutRequest>(exact));
                    break;
            }
        }

        public void HandleDebug(Debug debug) => DebugHandler?.Invoke(debug);

        public void HandleSequencedData(SequencedData sequencedData) => MessageDecoder?.Parse(sequencedData.Message);

        public void HandleUnSequencedData(UnSequencedData unSequencedData) => MessageDecoder?.Parse(unSequencedData.Message);

        public void HandleCompressedData(CompressedData compressedData) => MessageDecoder?.Parse(compressedData.Message);

        public void HandleLoginAccepted(LoginAccepted loginAccepted) => LoginAccepted?.Invoke(loginAccepted);

        public void HandleLoginRejected(LoginRejected loginRejected) => LoginRejected?.Invoke(loginRejected);

        public void HandleServerHeartbeat(ServerHeartbeat serverHeartbeat) => ServerHeartbeat?.Invoke(serverHeartbeat);

        public void HandleEndOfSession(EndOfSession endOfSession) => EndOfSession?.Invoke(endOfSession);

        public void HandleLoginRequest(LoginRequest loginRequest) => LoginRequest?.Invoke(loginRequest);

        public void HandleClientHeartbeat(ClientHeartbeat clientHeartbeat) => ClientHeartbeat?.Invoke(clientHeartbeat);

        public void HandleLogoutRequest(LogoutRequest logoutRequest) => LogoutRequest?.Invoke(logoutRequest);

        /// <summary>
        /// Carries a message buffer through the channel with its actual data length
        /// and whether it was rented from ArrayPool (and must be returned).
        /// </summary>
        private readonly struct PooledBuffer
        {
            public readonly byte[] Array;
            public readonly int Length;
            public readonly bool IsPooled;

            public PooledBuffer(byte[] array, int length, bool isPooled)
            {
                Array = array;
                Length = length;
                IsPooled = isPooled;
            }
        }
    }
}
