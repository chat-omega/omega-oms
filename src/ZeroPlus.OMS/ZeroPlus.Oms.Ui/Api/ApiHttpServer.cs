using NetCoreServer;
using NLog;
using System.Net.Sockets;

namespace ZeroPlus.Oms.Ui.Api
{
    public class ApiHttpServer : HttpServer
    {
        public ConnectionStatusChangedEventHandler ConnectionStatusChanged;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ApiHttpServer(string address, int port) : base(address, port) { }

        protected override void OnStarted()
        {
            ConnectionStatusChanged?.Invoke(IsStarted);
        }

        protected override void OnStopped()
        {
            ConnectionStatusChanged?.Invoke(IsStarted);
        }

        protected override HttpSession CreateSession()
        {
            return new ApiHttpSession(this);
        }

        protected override void OnError(SocketError error)
        {
            _log.Error($"HTTP session caught an error: {error}");
        }
    }
}
