using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ZeroPlus.Models.Buffers;
using ZeroPlus.Models.Buffers.Interfaces;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.SoupBinTCP.Codecs;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;
using ZeroPlus.Models.SoupBinTCP.Data;
using ZeroPlus.Models.SoupBinTCP.Messages;

namespace ZeroPlus.Models.Communication.Tcp;

public abstract class SoupBinTcpSession : TcpSession, IMessageParser, IMessageSender
{
    public const int HEARTBEAT_INTERVAL = 5000;

    private readonly ILogger _logger;
    private readonly ISoupBinTcpDecoder _decoder;
    private readonly ISoupBinTcpEncoder _encoder;
    private Timer? _heartbeatTimer;
    private readonly ServerHeartbeat _serverHeartbeat = new();
    private bool _rejected;

    public DateTime LastHeartbeatUtc { get; private set; }
    public DateTime ConnectTime { get; private set; }
    public DateTime DisconnectTime { get; private set; }
    public int MsgQueueCount => _encoder.MsgQueueCount;
    public EndPoint? RemoteEndpoint => GetRemoteEndpoint();

    public string Received => BytesReceived.FormatBytes();
    public string Sent => BytesSent.FormatBytes();
    public string Pending => BytesPending.FormatBytes();
    public string Sending => BytesSending.FormatBytes();

    protected SoupBinTcpSession(ILogger logger, TcpServer server, IEncodeBufferContext encodeContext, BufferType bufferType = BufferType.Ring) : base(server)
    {
        _logger = logger;
        IReadBuffer readBuffer = bufferType == BufferType.Linear ? new ReadBuffer() : new RingBuffer();
        _decoder = new SoupBinTcpDecoder(_logger, readBuffer)
        {
            MessageDecoder = this,
        };
        _encoder = new SoupBinTcpEncoder(_logger, encodeContext)
        {
            Sender = this,
        };
        RegisterHandlers();
    }

    protected EndPoint? GetRemoteEndpoint()
    {
        try
        {
            return Socket?.RemoteEndPoint!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(GetRemoteEndpoint));
            return default;
        }
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            _decoder.Parse(buffer, offset, size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(OnReceived));
        }
    }

    protected override void Dispose(bool disposingManagedResources)
    {
        base.Dispose(disposingManagedResources);
        _decoder.Reset();
        _encoder.StopEngine();
    }

    protected sealed override void OnSessionConnected()
    {
        base.OnSessionConnected();
        StartHeartbeatTimer();
        ConnectTime = DateTime.Now;
        _decoder.Reset();
        _encoder.StartEngine();
        OnConnected();
    }

    protected virtual void OnConnected()
    {
    }

    protected sealed override void OnSessionDisconnected()
    {
        base.OnSessionDisconnected();
        StopHeartbeatTimer();
        DisconnectTime = DateTime.Now;
        _decoder.Reset();
        _encoder.StopEngine();
        OnDisconnected();
    }

    protected virtual void OnDisconnected()
    {
    }

    private void RegisterHandlers()
    {
        _decoder.DebugHandler += OnDebugHandler;
        _decoder.LoginRequest += OnLoginRequest;
        _decoder.ClientHeartbeat += OnClientHeartbeat;
        _decoder.LogoutRequest += OnLogoutRequest;
    }

    protected virtual void OnDebugHandler(Debug message)
    {
        _logger.LogDebug("SoupBin Debug message received. Text: {}", message.Text);
    }

    protected virtual void OnClientHeartbeat(ClientHeartbeat message)
    {
        LastHeartbeatUtc = DateTime.UtcNow;
        _logger.LogTrace("SoupBin {} message received. Time: {}", nameof(ClientHeartbeat), LastHeartbeatUtc);
    }

    protected virtual void OnLoginRequest(LoginRequest message)
    {
        _logger.LogInformation("SoupBin {} message received. Session: {}, Sequence: {}, Username: {}", nameof(LoginRequest), message.RequestedSession, message.RequestedSequenceNumber, message.Username);
    }

    protected virtual void OnLogoutRequest(LogoutRequest message)
    {
        _logger.LogInformation("SoupBin {} message received.", nameof(LogoutRequest));
    }

    public void SendSequenced(ReadOnlySpan<byte> bytes)
    {
        SendFramed(bytes, MessagePacketType.SequencedData);
    }

    public void SendUnSequenced(ReadOnlySpan<byte> bytes)
    {
        SendFramed(bytes, MessagePacketType.UnSequencedData);
    }

    public void SendCompressed(ReadOnlySpan<byte> bytes)
    {
        SendFramed(LZ4Pickler.Pickle(bytes), MessagePacketType.CompressedData);
    }

    /// <summary>
    /// Sends encoded bytes from a <see cref="PooledEncodeBuffer"/> as a sequenced message
    /// and returns the buffer to the pool.
    /// </summary>
    protected void SendSequencedPooled(PooledEncodeBuffer buf, int bytesWritten)
    {
        try
        {
            SendSequenced(buf.Slice(bytesWritten));
        }
        finally
        {
            buf.Return();
        }
    }

    /// <summary>
    /// Sends encoded bytes from a <see cref="PooledEncodeBuffer"/> as an unsequenced message
    /// and returns the buffer to the pool.
    /// </summary>
    protected void SendUnSequencedPooled(PooledEncodeBuffer buf, int bytesWritten)
    {
        try
        {
            SendUnSequenced(buf.Slice(bytesWritten));
        }
        finally
        {
            buf.Return();
        }
    }

    /// <summary>
    /// Sends encoded bytes from a <see cref="PooledEncodeBuffer"/> as a compressed message
    /// and returns the buffer to the pool.
    /// </summary>
    protected void SendCompressedPooled(PooledEncodeBuffer buf, int bytesWritten)
    {
        try
        {
            SendCompressed(buf.Slice(bytesWritten));
        }
        finally
        {
            buf.Return();
        }
    }

    public void SendMessage(Message message)
    {
        SendMessage(message.TotalBytes);
    }

    public void SendMessage(byte[] bytes)
    {
        SendAsync(bytes, 0, bytes.Length);
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("TCP protocol session with Id: {}, Error: {}", Id, error);
        var isBufferOverflow = error == SocketError.NoBufferSpaceAvailable;
        if (isBufferOverflow)
        {
            ForceDisconnect();
        }
    }

    private void ForceDisconnect()
    {
        if (!_rejected)
        {
            StopHeartbeatTimer();
            var rejectMessage = new LoginRejected((char)RejectCode.SessionNotAvailable);
            _rejected = true;
            SendAsync(rejectMessage.TotalBytes.AsSpan(), ignoreBufferCheck: true);
        }
    }

    public void Send(ITopic? topic, bool sendCached)
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            _encoder.Send(topic, sendCached);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(Send));
        }
    }

    public void Reset(ITopic? topic)
    {
        try
        {
            _encoder.Reset(topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(Reset));
        }
    }

    public void RequestDisconnect()
    {
        SendRejectMessage();
    }

    protected void SendRejectMessage()
    {
        var rejectMessage = new LoginRejected((char)RejectCode.SessionNotAvailable);
        SendMessage(rejectMessage);
        StopHeartbeatTimer();
    }

    private void StartHeartbeatTimer()
    {
        StopHeartbeatTimer();
        _heartbeatTimer = new Timer(SendServerHeartbeat, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(HEARTBEAT_INTERVAL));
    }

    private void SendServerHeartbeat(object? state)
    {
        try
        {
            if (!IsConnected)
            {
                return;
            }

            _logger.LogTrace("Session heartbeat timer firing for the client. Session Id: {}", Id);
            SendMessage(_serverHeartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(SendServerHeartbeat));
        }
    }

    private void StopHeartbeatTimer()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    public abstract void Parse(byte[] message);

    public abstract void Parse(byte[] buffer, int offset, int length);
}