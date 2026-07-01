using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using ZeroPlus.Models.Buffers;
using ZeroPlus.Models.Buffers.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.SoupBinTCP.Codecs;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;
using ZeroPlus.Models.SoupBinTCP.Data;
using ZeroPlus.Models.SoupBinTCP.Messages;

namespace ZeroPlus.Models.Communication.Tcp;

public abstract class SoupBinTcpClient : TcpClient, IMessageParser
{
    private readonly int HEARTBEAT_INTERVAL = 5000;

    private readonly ILogger _logger;
    private readonly ISoupBinTcpDecoder _decoder;

    private Timer? _heartbeatTimer;
    private Timer? _reconnectTimer;
    private readonly ClientHeartbeat _clientHeartbeat = new();

    protected ISoupBinTcpClientConfig _config;

    public string? Session { get; private set; }
    public DateTime LastHeartbeatUtc { get; private set; }

    public string Received => BytesReceived.FormatBytes();
    public string Sent => BytesSent.FormatBytes();
    public string Pending => BytesPending.FormatBytes();
    public string Sending => BytesSending.FormatBytes();

    protected SoupBinTcpClient(ILogger logger, ISoupBinTcpClientConfig config, BufferType bufferType = BufferType.Ring) : base(config.ServerAddress, config.ServerPort)
    {
        _logger = logger;
        _config = config;
        IReadBuffer readBuffer = bufferType == BufferType.Linear ? new ReadBuffer() : new RingBuffer();
        _decoder = new SoupBinTcpDecoder(_logger, readBuffer)
        {
            MessageDecoder = this,
        };
        OptionReceiveBufferSize = config.ReceiveBufferSize;
        OptionSendBufferSize = config.SendBufferSize;
        RegisterHandlers();
    }

    protected void UpdateConfig(ISoupBinTcpClientConfig config)
    {
        _config = config;
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
        StopReconnectTimer();
        StopHeartbeatTimer();
        _decoder.Reset();
    }

    protected sealed override void OnClientConnected()
    {
        base.OnClientConnected();
        _decoder.Reset();
        _logger.LogInformation("Client connected. Session Id: {}", Id);
        OnConnected();
    }

    protected virtual void OnConnected()
    {
    }

    protected sealed override void OnClientDisconnected()
    {
        base.OnClientDisconnected();
        _decoder.Reset();
        _reconnectTimer?.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        StopHeartbeatTimer();
        _logger.LogInformation("Client disconnected. Session Id: {}", Id);
        OnDisconnected();
    }

    protected virtual void OnDisconnected()
    {
    }

    protected void Login(Version? version)
    {
        version ??= Assembly.GetExecutingAssembly().GetName().Version;
        LoginRequest loginRequest = new(_config.SessionUsername, version!.ToString(), _config.SessionId);
        SendMessage(loginRequest);
    }

    private void RegisterHandlers()
    {
        _decoder.DebugHandler += OnDebugHandler;
        _decoder.LoginAccepted += OnLoginAccepted;
        _decoder.LoginRejected += OnLoginRejected;
        _decoder.ServerHeartbeat += OnServerHeartbeat;
        _decoder.EndOfSession += OnEndOfSession;
    }

    protected virtual void OnDebugHandler(Debug message)
    {
        _logger.LogDebug("SoupBin Debug message received. Text: {}", message.Text);
    }

    protected virtual void OnServerHeartbeat(ServerHeartbeat message)
    {
        LastHeartbeatUtc = DateTime.UtcNow;
        _logger.LogTrace("SoupBin {} message received. Time: {}", nameof(ServerHeartbeat), LastHeartbeatUtc);
    }

    protected virtual void OnLoginAccepted(LoginAccepted message)
    {
        Session = message.Session;
        StartHeartbeatTimer();
        _logger.LogInformation("SoupBin {} message received. Session: {}, Sequence: {}", nameof(LoginAccepted), message.Session, message.SequenceNumber);
    }

    protected virtual void OnLoginRejected(LoginRejected message)
    {
        DisconnectAndStop();
        _logger.LogWarning("Login Rejected. Reason: {}", message.RejectReasonCode);
    }

    protected virtual void OnEndOfSession(EndOfSession message)
    {
        _logger.LogInformation("End of Session. Session: {}", Session);
        Disconnect();
    }

    public bool ConnectAndStart()
    {
        try
        {
            _logger.LogInformation("Client starting a new session. Session Id: {}", Id);
            UpdateAddress(_config.ServerAddress, _config.ServerPort);
            StopHeartbeatTimer();
            StartReconnectTimer();
            return ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(ConnectAndStart));
            return false;
        }
    }

    public bool DisconnectAndStop()
    {
        try
        {
            _logger.LogInformation("Client stopping the session. Session Id: {}", Id);
            StopHeartbeatTimer();
            StopReconnectTimer();
            DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(DisconnectAndStop));
            return false;
        }
    }

    public override bool Reconnect()
    {
        try
        {
            _logger.LogInformation("Client restarting session. Session Id: {}", Id);
            UpdateAddress(_config.ServerAddress, _config.ServerPort);
            StopHeartbeatTimer();
            return ReconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(Reconnect));
            return false;
        }
    }

    private void StartReconnectTimer()
    {
        _reconnectTimer = new Timer(_ =>
        {
            _logger.LogTrace($"Client reconnect timer firing for the client. Session Id: " + Id);
            TryConnectAsync();
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private bool TryConnectAsync()
    {
        try
        {
            return ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(TryConnectAsync));
            return false;
        }
    }

    private void StopReconnectTimer()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }

    private void StartHeartbeatTimer()
    {
        try
        {
            StopHeartbeatTimer();
            _heartbeatTimer = new Timer(SendClientHeartbeat, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(HEARTBEAT_INTERVAL));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(StartHeartbeatTimer));
        }
    }

    private void SendClientHeartbeat(object? state)
    {
        try
        {
            if (!IsConnected)
            {
                return;
            }
            _logger.LogTrace("Client heartbeat timer firing for the client. Session: {}, Id: {}", Session, Id);
            SendMessage(_clientHeartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(SendClientHeartbeat));
        }
    }

    private void StopHeartbeatTimer()
    {
        try
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(StopHeartbeatTimer));
        }
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("Client caught a socket error. Session Id: {}, Error: {}", Id, error);
    }

    public void SendDebug(string debugMessage)
    {
        SendMessage(new Debug(debugMessage));
        _logger.LogDebug("Sending debug message. Session Id: {}, Message: {}", Id, debugMessage);
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
        var bytes = message.TotalBytes;
        SendAsync(bytes, 0, bytes.Length);
    }

    public abstract void Parse(byte[] message);

    public abstract void Parse(byte[] buffer, int offset, int length);
}