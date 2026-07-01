using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.SoupBinTCP.Messages;

namespace ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces
{
    public delegate void DebugHandler(Debug message);
    public delegate void LoginAcceptedHandler(LoginAccepted message);
    public delegate void LoginRejectedHandler(LoginRejected message);
    public delegate void ServerHeartbeatHandler(ServerHeartbeat message);
    public delegate void EndOfSessionHandler(EndOfSession message);
    public delegate void LoginRequestHandler(LoginRequest message);
    public delegate void ClientHeartbeatHandler(ClientHeartbeat message);
    public delegate void LogoutRequestHandler(LogoutRequest message);

    public interface ISoupBinTcpDecoder
    {
        event DebugHandler DebugHandler;
        event LoginAcceptedHandler LoginAccepted;
        event LoginRejectedHandler LoginRejected;
        event ServerHeartbeatHandler ServerHeartbeat;
        event EndOfSessionHandler EndOfSession;
        event LoginRequestHandler LoginRequest;
        event ClientHeartbeatHandler ClientHeartbeat;
        event LogoutRequestHandler LogoutRequest;

        public IMessageParser? MessageDecoder { get; set; }

        void Reset();
        void Parse(byte[] buffer, long offset, long size);
        void HandleClientHeartbeat(ClientHeartbeat clientHeartbeat);
        void HandleDebug(Debug debug);
        void HandleEndOfSession(EndOfSession endOfSession);
        void HandleLoginAccepted(LoginAccepted loginAccepted);
        void HandleLoginRejected(LoginRejected loginRejected);
        void HandleLoginRequest(LoginRequest loginRequest);
        void HandleLogoutRequest(LogoutRequest logoutRequest);
        void HandleSequencedData(SequencedData sequencedData);
        void HandleServerHeartbeat(ServerHeartbeat serverHeartbeat);
        void HandleUnSequencedData(UnSequencedData unSequencedData);
        void HandleCompressedData(CompressedData compressedData);
    }
}